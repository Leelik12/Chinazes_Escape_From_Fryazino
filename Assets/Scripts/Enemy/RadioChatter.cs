using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Props;

namespace RacingProject.Enemy
{
    // Рация в машине: короткие сообщения штаба и перехваченные переговоры врагов. Сервер решает, что сказать
    // (начало волны и откуда идут враги, особые враги и босс, артналёт, передышка с подсказкой, где ремкомплект,
    // натиск, громкие взрывы, сбитые опоры, зачистка), и рассылает текст обоим игрокам. У каждого — щелчок
    // тангенты, неразборчивый голос в эфире и строка на экране (WaveHud). Сообщения не идут чаще minGap,
    // важные (налёт, передышка, босс) пропускают очередь
    public class RadioChatter : NetworkBehaviour
    {
        public enum Speaker { Hq, Intercept }

        // У всех: новое сообщение (кто говорит, текст) — показывает WaveHud
        public static event System.Action<Speaker, string> Message;

        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PlayerHealth carHealth;
        [SerializeField] private AudioSource voice;
        [SerializeField] private AudioClip squelchIn;
        [SerializeField] private AudioClip squelchOut;
        [SerializeField] private AudioClip[] babble = new AudioClip[0];
        [SerializeField] private AudioClip[] interceptBabble = new AudioClip[0];
        [SerializeField] private AudioMixerGroup output;
        [Tooltip("Не чаще одного обычного сообщения за столько секунд")]
        [SerializeField] private float minGap = 7f;
        [SerializeField] private float explosionGap = 25f;
        [SerializeField] private float poleGap = 60f;
        [SerializeField] private float ramGap = 30f;

        private float nextFree;
        private float nextExplosionLine;
        private float nextPoleLine;
        private float nextRamLine;
        private int lastCombo;

        private bool IsServerSide => !IsSpawned || IsServer;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            if (enemyManager != null)
            {
                enemyManager.WaveStarted += OnWaveStarted;
                enemyManager.WaveCleared += OnWaveCleared;
                enemyManager.ServerEnemySpawned += OnEnemySpawned;
            }
            ArtilleryStrikes.ServerStrikeCalled += OnStrike;
            PropNetwork.ServerExploded += OnExploded;
            PowerLines.ServerToppled += OnToppled;
        }

        public override void OnNetworkDespawn()
        {
            if (enemyManager != null)
            {
                enemyManager.WaveStarted -= OnWaveStarted;
                enemyManager.WaveCleared -= OnWaveCleared;
                enemyManager.ServerEnemySpawned -= OnEnemySpawned;
            }
            ArtilleryStrikes.ServerStrikeCalled -= OnStrike;
            PropNetwork.ServerExploded -= OnExploded;
            PowerLines.ServerToppled -= OnToppled;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || enemyManager == null) return;
            // Серия убийств
            int combo = enemyManager.Combo;
            if (combo >= 4 && lastCombo < 4)
                Say(Speaker.Hq, Pick("Красиво работаете, экипаж!", "Вот это серия! Так держать.", "Минус четыре. Штаб доволен."), false);
            lastCombo = combo;
        }

        // ---------- поводы ----------

        private void OnWaveStarted(int wave)
        {
            if (wave <= 1)
            {
                Say(Speaker.Hq, "«Тахо», я Штаб. Город под противником. Держитесь сколько сможете.", true);
                return;
            }
            StartCoroutine(AnnounceWave(wave));
        }

        // Через секунду после старта волны враги уже на точках: откуда они идут
        private IEnumerator AnnounceWave(int wave)
        {
            yield return new WaitForSeconds(1.2f);
            if (enemyManager == null || carHealth == null) yield break;
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (GameObject enemy in enemyManager.ActiveEnemies)
                if (enemy != null) { sum += enemy.transform.position; count++; }
            if (count == 0) yield break;
            string from = Compass(sum / count - carHealth.transform.position);
            Say(Speaker.Hq, $"Волна {wave}. {Cars(count)} — идут {from}.", true);
        }

        private void OnWaveCleared(int wave, float seconds, int bonus)
        {
            Say(Speaker.Hq, Pick("Чисто. Передышка — чините машину.", "Квадрат чист. Выбирайте, чем усилиться.",
                "Отбились. Готовьтесь, это не последние."), true);
        }

        private void OnEnemySpawned(GameObject enemy)
        {
            if (carHealth == null) return;
            string from = Compass(enemy.transform.position - carHealth.transform.position);
            string name = enemy.name;
            if (name.StartsWith("EnemyBoss"))
                Say(Speaker.Hq, $"Внимание! Тяжёлый броневик «Хозяин» выдвинулся {from}. Бейте наверняка.", true);
            else if (name.StartsWith("EnemyUral"))
                Say(Speaker.Intercept, Pick($"«Урал» на связи. Захожу {from}, иду на таран!", $"Я «Урал», вижу их. Сейчас размажу!"), false);
            else if (name.StartsWith("EnemyUaz"))
                Say(Speaker.Intercept, Pick("«Буханка»: гранатомёт заряжен, ищу цель.", $"«Буханка» выдвигается {from}. Держите их на дистанции."), false);
        }

        private void OnStrike(Vector3 center)
        {
            if (carHealth == null) return;
            Vector3 toCenter = center - carHealth.transform.position;
            toCenter.y = 0f;
            string text = toCenter.magnitude < 25f
                ? "Артналёт по вашему квадрату! Гоните оттуда!"
                : $"Артналёт! Квадрат {CompassTo(toCenter)} от вас — не заезжайте в красный круг!";
            Say(Speaker.Hq, text, true);
        }

        private void OnExploded(ExplosiveBarrel barrel)
        {
            if (!barrel.IsLarge || Time.time < nextExplosionLine) return;
            nextExplosionLine = Time.time + explosionGap;
            Say(Speaker.Hq, Pick("Ну вы дали! Отсюда видно зарево.", "Это что сейчас рвануло? Хорошо рвануло.",
                "Цепочкой пошло! Держитесь подальше от огня."), false);
        }

        private void OnToppled(Vector3 position)
        {
            if (Time.time < nextPoleLine) return;
            nextPoleLine = Time.time + poleGap;
            Say(Speaker.Hq, Pick("Связь трещит… Вы там опору снесли? Не лезьте к проводам!",
                "Провода под током на земле — объезжайте искры."), false);
        }

        // Режиссёр: экипажу тяжело — передышка и подсказка
        public void SayRelief(Vector3? kit)
        {
            if (carHealth == null) return;
            string text = "Держитесь! Отвлекаем огонь на себя, у вас пара минут.";
            if (kit.HasValue)
            {
                Vector3 to = kit.Value - carHealth.transform.position;
                to.y = 0f;
                text = $"Держитесь! Ремкомплект в {Mathf.RoundToInt(to.magnitude / 10f) * 10} м {CompassTo(to)}. Подкрепления противника задержим.";
            }
            Say(Speaker.Hq, text, true);
        }

        // Режиссёр: натиск — к врагам пришла подмога
        public void SayFlank()
        {
            Say(Speaker.Intercept, Pick("Обходи их, зажимай с фланга!", "Подтягиваюсь, сейчас прижмём.",
                "Вижу джип! Все ко мне!"), false);
        }

        // Задания волн (WaveObjectives): всегда в эфир
        public void SayObjective(string text)
        {
            Say(Speaker.Hq, text, true);
        }

        // Поломки машины (CarDamageState), гранатомёт стрелка (RocketLauncher) и таран врага (EnemyRamDamage)
        public enum CarEvent { Flat, Radiator, Overheat, Launcher, RocketsEmpty, Ram }

        public void SayCarEvent(CarEvent kind)
        {
            switch (kind)
            {
                case CarEvent.Flat:
                    Say(Speaker.Hq, Pick("Колесо пробито! Машину тянет — держите руль, ремкомплект залатает.",
                        "Слышали хлопок? Это ваша шина. Ищите ремкомплект."), true);
                    break;
                case CarEvent.Radiator:
                    Say(Speaker.Hq, Pick("Радиатор пробит, из-под капота пар! Не гоните на полном газу — мотор перегреется.",
                        "У вас течёт радиатор. Берегите мотор, ремкомплект поможет."), true);
                    break;
                case CarEvent.Overheat:
                    Say(Speaker.Hq, Pick("Мотор перегрет! Сбросьте газ, дайте ему остыть.", "Стрелка температуры в красном — отпустите газ!"), false);
                    break;
                case CarEvent.Launcher:
                    Say(Speaker.Hq, Pick("Стрелок, у вас РПГ на турели! Выстрел — правая кнопка мыши, в шлеме — левый курок.",
                        "Нашли гранатомёт? Отлично. Бейте по Уралу и боссу, ящики с выстрелами — на дорогах и у подбитых УАЗов."), true);
                    break;
                case CarEvent.RocketsEmpty:
                    Say(Speaker.Hq, "Выстрелы к РПГ кончились. Ящики — на дорогах и у подбитых «буханок».", false);
                    break;
                case CarEvent.Ram:
                    if (Time.time < nextRamLine) break;
                    nextRamLine = Time.time + ramGap;
                    Say(Speaker.Intercept, Pick("Он нас таранит! Уходи с дороги!", "Меня снесли! Машину крутит!",
                        "Это не водитель, это бульдозер!"), false);
                    break;
            }
        }

        // ---------- отправка ----------

        private void Say(Speaker speaker, string text, bool important)
        {
            if (!IsServerSide) return;
            if (!important && Time.time < nextFree) return;
            nextFree = Time.time + minGap;
            if (IsSpawned)
                SayRpc((int)speaker, text);
            else
                Show(speaker, text);
        }

        [Rpc(SendTo.Everyone)]
        private void SayRpc(int speaker, string text)
        {
            Show((Speaker)speaker, text);
        }

        private void Show(Speaker speaker, string text)
        {
            Message?.Invoke(speaker, text);
            if (voice == null) return;
            StopAllCoroutines();
            StartCoroutine(Transmit(speaker, text));
        }

        // Щелчок тангенты, голос в эфире на время чтения строки, щелчок отпускания
        private IEnumerator Transmit(Speaker speaker, string text)
        {
            voice.outputAudioMixerGroup = output;
            if (squelchIn != null) voice.PlayOneShot(squelchIn, 0.7f);
            yield return new WaitForSeconds(0.12f);
            AudioClip[] pool = speaker == Speaker.Intercept && interceptBabble.Length > 0 ? interceptBabble : babble;
            float talk = Mathf.Clamp(text.Length / 22f, 1.2f, 4f);
            float end = Time.time + talk;
            while (pool.Length > 0 && Time.time < end)
            {
                AudioClip clip = pool[Random.Range(0, pool.Length)];
                voice.PlayOneShot(clip, 0.55f);
                yield return new WaitForSeconds(Mathf.Min(clip.length, end - Time.time + 0.05f));
            }
            if (squelchOut != null) voice.PlayOneShot(squelchOut, 0.7f);
        }

        // ---------- слова ----------

        private static string Pick(params string[] variants) => variants[Random.Range(0, variants.Length)];

        private static string Cars(int count)
        {
            int last = count % 10, lastTwo = count % 100;
            string word = last == 1 && lastTwo != 11 ? "машина" : last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? "машины" : "машин";
            return $"{count} {word}";
        }

        // Направление по сторонам света: «с севера», «с юго-запада»… (мир: +Z — север, +X — восток)
        private static string Compass(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1f) return "рядом";
            float angle = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
            string[] names = { "с севера", "с северо-востока", "с востока", "с юго-востока", "с юга", "с юго-запада", "с запада", "с северо-запада" };
            return names[Mathf.RoundToInt(angle / 45f) % 8];
        }

        // Куда: «к северу», «к юго-западу»…
        public static string CompassTo(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1f) return "рядом";
            float angle = Mathf.Repeat(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, 360f);
            string[] names = { "к северу", "к северо-востоку", "к востоку", "к юго-востоку", "к югу", "к юго-западу", "к западу", "к северо-западу" };
            return names[Mathf.RoundToInt(angle / 45f) % 8];
        }
    }
}
