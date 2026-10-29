using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using RacingProject.Management;
using RacingProject.Network;
using RacingProject.Pickups;

namespace RacingProject.Enemy
{
    // Волны врагов. Сервер спавнит врагов, считает очки и решает, когда волна кончилась.
    // Между волнами — передышка: бонус за быструю зачистку, ремонт машины и выбор одной из трёх карточек улучшений
    // (RunUpgrades). Пока идёт бой, менеджер раздаёт стрелкам места вокруг машины игроков (впереди, по бокам, сзади),
    // а застрявших далеко и провалившихся под карту переносит на точки спавна, чтобы волна не повисла
    public class EnemyManager : NetworkBehaviour
    {
        [Header("Спавнпоинты")]
        [Tooltip("Все точки спавна врагов")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("Начальные точки спавна (2 врага)")]
        [SerializeField] private Transform[] initialSpawnPoints;

        [Tooltip("Точка спавна волны не ближе этого к машине игроков, м")]
        [SerializeField] private float spawnMinDistance = 120f;
        [Tooltip("И не дальше этого, иначе враг долго едет до боя, м")]
        [SerializeField] private float spawnMaxDistance = 650f;
        [Tooltip("Дальше этого игрок появление врага не разглядит, даже если его ничто не закрывает, м")]
        [SerializeField] private float spawnUnseenDistance = 200f;
        [SerializeField] private LayerMask sightMask = ~0;

        [Header("Префаб врага")]
        [Tooltip("Префаб с NetworkObject, зарегистрированный в списке сетевых префабов NetworkManager")]
        [SerializeField] private GameObject enemyPrefab;

        [Header("Босс")]
        [Tooltip("Усиленный враг (тоже зарегистрирован в списке сетевых префабов)")]
        [SerializeField] private GameObject bossPrefab;
        [Tooltip("Босс выезжает в каждой такой волне (0 — без босса)")]
        [SerializeField] private int bossEveryWaves = 5;

        [Header("Особые враги")]
        [Tooltip("Таран, гранатомётчик и т. п.: в волнах после стартовой могут выехать вместо обычного врага")]
        [SerializeField] private SpecialEnemy[] specialEnemies = new SpecialEnemy[0];

        [Header("Засады")]
        [Tooltip("Точки во дворах, частном секторе и промзоне, где враг может поджидать машину игроков")]
        [SerializeField] private Transform[] ambushPoints = new Transform[0];
        [Tooltip("С этой волны часть врагов ждёт в засаде")]
        [SerializeField] private int ambushFromWave = 2;
        [Tooltip("Шанс, что очередной враг волны будет ждать в засаде, а не выедет с точки спавна")]
        [Range(0f, 1f)] [SerializeField] private float ambushChance = 0.35f;
        [Tooltip("Засада не ближе этого к машине игроков, чтобы её появление не было видно, м")]
        [SerializeField] private float ambushMinDistance = 140f;
        [Tooltip("И не дальше этого, иначе игрок до неё не доедет, м")]
        [SerializeField] private float ambushMaxDistance = 450f;

        [Header("Очки")]
        [Tooltip("Машина игроков: очки за выживание идут, пока она цела")]
        [SerializeField] private PlayerHealth carHealth;
        [Tooltip("Убийства с паузой не больше этой складываются в комбо, с")]
        [SerializeField] private float comboWindow = 5f;
        [Tooltip("Наибольший множитель комбо")]
        [SerializeField] private int maxCombo = 5;
        [Tooltip("Очки за выживание каждые survivalInterval секунд — только в бою, когда враг ближе combatDistance")]
        [SerializeField] private int survivalPoints = 10;
        [SerializeField] private float survivalInterval = 5f;
        [SerializeField] private float combatDistance = 200f;

        [Header("UI")]
        [SerializeField] private TMP_Text killedTextUI;

        [Header("Настройки")]
        [Tooltip("Врагов в первой волне после стартовой")]
        [SerializeField] private int enemiesPerWave = 4;
        [Tooltip("Сколько врагов стартовой волны подъезжает после врагов на начальных точках")]
        [SerializeField] private int initialReinforcements = 2;

        [Header("Сложность волн")]
        [Tooltip("Через сколько волн в волне становится на одного врага больше")]
        [SerializeField] private int extraEnemyEveryWaves = 2;
        [Tooltip("Больше врагов в одной волне не бывает")]
        [SerializeField] private int maxEnemiesPerWave = 10;
        [Tooltip("На сколько растёт здоровье врага за каждую волну (0.1 = +10%)")]
        [SerializeField] private float healthGrowthPerWave = 0.1f;
        [Tooltip("На сколько растёт урон пулемёта врага за каждую волну (0.1 = +10%)")]
        [SerializeField] private float damageGrowthPerWave = 0.1f;
        [Tooltip("Предел роста здоровья и урона относительно первой волны")]
        [SerializeField] private float maxStatMultiplier = 2.5f;
        [Tooltip("К этой волне меткость, скорострельность и скорость врагов выходят на предел")]
        [SerializeField] private int difficultyFullWave = 10;
        [Tooltip("Множитель разброса врагов: на первой волне и на difficultyFullWave (меньше — метче)")]
        [SerializeField] private Vector2 spreadByWave = new Vector2(1.3f, 0.7f);
        [Tooltip("Множитель паузы между выстрелами и перезарядки гранатомёта (меньше — чаще)")]
        [SerializeField] private Vector2 fireIntervalByWave = new Vector2(1.25f, 0.8f);
        [Tooltip("Множитель предельной скорости машин врагов")]
        [SerializeField] private Vector2 speedByWave = new Vector2(0.92f, 1.1f);
        [Tooltip("Когда врагов в волне больше, чем точек спавна, остальные подъезжают с такой паузой, с")]
        [SerializeField] private float reinforcementDelay = 6f;

        [Header("Передышка между волнами")]
        [Tooltip("Самая короткая передышка, с")]
        [SerializeField] private float breakTime = 4f;
        [Tooltip("Дольше этого карточку не ждут — выбирается случайная, с")]
        [SerializeField] private float cardTimeout = 15f;
        [Tooltip("Продолжение сохранённого забега: столько секунд до первой волны, с")]
        [SerializeField] private float resumeDelay = 5f;
        [Tooltip("Ремонт машины во время передышки, прочности в секунду")]
        [SerializeField] private float breakRepairPerSecond = 40f;
        [Tooltip("Волна, зачищенная быстрее parTimeBase + parTimePerEnemy × врагов, приносит бонус за каждую сэкономленную секунду")]
        [SerializeField] private float parTimeBase = 25f;
        [SerializeField] private float parTimePerEnemy = 8f;
        [SerializeField] private int bonusPerSecond = 4;

        [Header("Выпадение ремкомплектов")]
        [SerializeField] private RepairKits repairKits;
        [Tooltip("Шанс, что из подбитого врага выпадет ремкомплект (из босса — всегда)")]
        [Range(0f, 1f)] [SerializeField] private float kitDropChance = 0.2f;
        [Tooltip("Если прочность машины ниже этой доли, ремкомплект выпадает вдвое чаще")]
        [Range(0f, 1f)] [SerializeField] private float kitDropLowHealth = 0.4f;

        [Header("Роли и страховка")]
        [Tooltip("Ближе этого к машине игроков стрелкам раздаются места вокруг неё, м")]
        [SerializeField] private float slotDistance = 160f;
        [Tooltip("Враг дальше этого от машины игроков дольше stragglerTime секунд переносится на точку спавна, м")]
        [SerializeField] private float stragglerDistance = 700f;
        [SerializeField] private float stragglerTime = 30f;
        [Tooltip("Враг ниже машины игроков на столько метров провалился под карту, м")]
        [SerializeField] private float fallenDepth = 60f;

        // Места вокруг игрока по порядку важности: угол от его хода по часовой стрелке. Сначала фланги сзади,
        // потом бока, затем один перекрывает дорогу впереди, ещё один держится сзади
        private static readonly float[] SlotAngles = { 140f, -140f, 95f, -95f, 0f, 180f, 45f, -45f, 120f, -120f };
        private const float WatchInterval = 1f;

        // Враги живут и умирают на сервере, счёт убийств и номер волны видят оба игрока
        private readonly NetworkVariable<int> killedEnemies = new NetworkVariable<int>();
        private readonly NetworkVariable<int> wave = new NetworkVariable<int>();
        private readonly NetworkVariable<int> score = new NetworkVariable<int>();
        private readonly NetworkVariable<int> combo = new NetworkVariable<int>(1);
        // Прочность босса для UI, -1 — босса нет
        private readonly NetworkVariable<float> bossHealth = new NetworkVariable<float>(-1f);
        // Идёт передышка между волнами
        private readonly NetworkVariable<bool> breakActive = new NetworkVariable<bool>();

        // Итог раунда для экрана гибели
        public int Wave => wave.Value;
        public int Kills => killedEnemies.Value;
        public int Score => score.Value;
        // Для приборов: множитель комбо и прочность босса 0–1 (меньше нуля — босса нет)
        public int Combo => combo.Value;
        public int MaxCombo => maxCombo;
        public float BossHealth01 => bossHealth.Value;
        public bool BreakActive => breakActive.Value;
        // Сервер: в этой волне жив босс (на такие волны задания не выдаются, WaveObjectives)
        public bool BossAlive => boss != null;

        // События для HUD волн (у обоих игроков)
        public event System.Action<int> WaveStarted;
        // Номер волны, время зачистки, бонус
        public event System.Action<int, float, int> WaveCleared;
        // Предложены карточки; пустой список — выбор закрыт
        public event System.Action<List<RunUpgrades.Card>> CardsOffered;
        public event System.Action<RunUpgrades.Card> CardChosen;

        // Текущее предложение карточек (у обоих игроков), пусто — выбора нет
        public IReadOnlyList<RunUpgrades.Card> OfferedCards => offeredCards;
        public float CardDeadline { get; private set; }

        // Режиссёр (GameDirector, сервер): поправка числа врагов в волне и пауза подкреплений, пока игрокам тяжело
        public int WaveSizeBias { get; set; }
        public bool HoldReinforcements { get; set; }
        // Сервер: враги на карте и событие появления врага (для режиссёра и рации)
        public IReadOnlyList<GameObject> ActiveEnemies => activeEnemies;
        public event System.Action<GameObject> ServerEnemySpawned;

        [System.Serializable]
        private class SpecialEnemy
        {
            [Tooltip("Префаб с NetworkObject, зарегистрированный в списке сетевых префабов")]
            public GameObject prefab;
            [Tooltip("Начиная с этой волны")]
            public int fromWave = 3;
            [Tooltip("Шанс, что очередной враг волны окажется этим")]
            [Range(0f, 1f)] public float chance = 0.3f;
            [Tooltip("Больше стольких в одной волне не бывает")]
            public int maxPerWave = 1;
        }

        private List<GameObject> activeEnemies = new List<GameObject>();
        private readonly Dictionary<GameObject, int> specialsThisWave = new Dictionary<GameObject, int>();
        private readonly Dictionary<GameObject, float> farSince = new Dictionary<GameObject, float>();
        private readonly List<RunUpgrades.Card> offeredCards = new List<RunUpgrades.Card>();
        private readonly System.Random cardRandom = new System.Random();
        private int pendingReinforcements;
        private bool gameStarted = false;
        private EnemyHealth boss;
        private float lastKillTime = float.NegativeInfinity;
        private float nextSurvivalTime;
        private float nextWatchTime;
        private float waveStartTime;
        private int waveEnemyCount;
        private bool cardPicked;
        private float regenCarry;

        void Start()
        {
            UpdateUI();
        }

        public override void OnNetworkSpawn()
        {
            // Новый раунд — улучшения прошлого забега не действуют
            RunUpgrades.Reset();
            killedEnemies.OnValueChanged += OnKillsChanged;
            wave.OnValueChanged += OnKillsChanged;
            score.OnValueChanged += OnKillsChanged;
            combo.OnValueChanged += OnKillsChanged;
            bossHealth.OnValueChanged += OnBossHealthChanged;
            if (carHealth != null) carHealth.OnDeath += OnCarDied;
            UpdateUI();
        }

        public override void OnNetworkDespawn()
        {
            killedEnemies.OnValueChanged -= OnKillsChanged;
            wave.OnValueChanged -= OnKillsChanged;
            score.OnValueChanged -= OnKillsChanged;
            combo.OnValueChanged -= OnKillsChanged;
            bossHealth.OnValueChanged -= OnBossHealthChanged;
            if (carHealth != null) carHealth.OnDeath -= OnCarDied;
        }

        // Забег окончен: продолжать нечего
        private void OnCarDied()
        {
            if (IsServer && LocalPlayerRole.IsSolo) RunSave.Clear();
        }

        // Сервер: очки за выживание, сброс комбо, прочность босса для UI, роли и страховка врагов, ремонт на ходу
        private void Update()
        {
            if (!IsServer || !IsSpawned || !gameStarted) return;

            bool carAlive = carHealth == null || !carHealth.IsDead;
            if (Time.time >= nextSurvivalTime)
            {
                nextSurvivalTime = Time.time + survivalInterval;
                // Очки за выживание — только в бою: на передышке и вдали от врагов их не набить
                if (carAlive && !breakActive.Value && EnemyNearCar())
                    score.Value += survivalPoints;
            }

            if (combo.Value > 1 && Time.time - lastKillTime > comboWindow)
                combo.Value = 1;

            float bossFraction = boss != null ? boss.HealthFraction : -1f;
            if (!Mathf.Approximately(bossHealth.Value, bossFraction))
                bossHealth.Value = bossFraction;

            if (carAlive)
                RepairCar(RunUpgrades.RegenPerSecond + (breakActive.Value ? breakRepairPerSecond : 0f));

            if (Time.time >= nextWatchTime)
            {
                nextWatchTime = Time.time + WatchInterval;
                WatchEnemies();
                AssignSlots();
            }
        }

        // Вызывается при старте игры из RoomController
        public void StartGame()
        {
            if (gameStarted) return;
            gameStarted = true;
            // Гараж бункера: броня — у обоих игроков (как карточка «Бронелисты»), гранатомёт выдаёт сервер
            if (carHealth != null) carHealth.IncreaseMaxHealth(GarageLoadout.ArmorBonus);
            if (IsServer && GarageLoadout.StartRockets > 0)
            {
                Turret.RocketLauncher launcher = FindFirstObjectByType<Turret.RocketLauncher>();
                if (launcher != null) launcher.AddAmmo(GarageLoadout.StartRockets);
            }

            // Только сервер отвечает за спавн врагов и счёт
            if (IsServer)
            {
                killedEnemies.Value = 0;
                score.Value = 0;
                combo.Value = 1;
                wave.Value = 1;
                nextSurvivalTime = Time.time + survivalInterval;
                // «Продолжить» в меню: забег с сохранённой волны (RunSave), иначе новый забег стирает сохранение
                RunSave.Data saved = RunSave.ContinueRequested && LocalPlayerRole.IsSolo ? RunSave.Load() : null;
                RunSave.ContinueRequested = false;
                if (saved != null)
                    StartCoroutine(ResumeRun(saved));
                else
                {
                    if (LocalPlayerRole.IsSolo) RunSave.Clear();
                    SpawnInitialEnemies();
                }
            }
        }

        // Одиночная игра: улучшения, счёт и прочность из сохранения, короткая передышка — и следующая волна
        private IEnumerator ResumeRun(RunSave.Data saved)
        {
            killedEnemies.Value = saved.kills;
            score.Value = saved.score;
            wave.Value = Mathf.Max(1, saved.wave);
            for (int card = 0; card < saved.cards.Length && card < RunUpgrades.CardCount; card++)
                for (int level = 0; level < saved.cards[card]; level++)
                {
                    RunUpgrades.Apply((RunUpgrades.Card)card);
                    if ((RunUpgrades.Card)card == RunUpgrades.Card.Armor && carHealth != null)
                        carHealth.IncreaseMaxHealth(RunUpgrades.ArmorPerLevel);
                }
            if (carHealth != null && saved.health > 0) carHealth.RestoreHealth(saved.health);
            Turret.RocketLauncher launcher = FindFirstObjectByType<Turret.RocketLauncher>();
            if (launcher != null && saved.rockets > launcher.Ammo) launcher.AddAmmo(saved.rockets - launcher.Ammo);

            breakActive.Value = true;
            yield return new WaitForSeconds(resumeDelay);
            breakActive.Value = false;
            SpawnRandomWave();
        }

        // Сервер, одиночная игра: снимок забега после зачищенной волны и выбранной карточки
        private void SaveRun()
        {
            if (!LocalPlayerRole.IsSolo || carHealth == null || carHealth.IsDead) return;
            var cards = new int[RunUpgrades.CardCount];
            for (int i = 0; i < cards.Length; i++) cards[i] = RunUpgrades.Level((RunUpgrades.Card)i);
            Turret.RocketLauncher launcher = FindFirstObjectByType<Turret.RocketLauncher>();
            RunSave.Save(new RunSave.Data
            {
                wave = wave.Value,
                score = score.Value,
                kills = killedEnemies.Value,
                cards = cards,
                health = carHealth.CurrentHealth,
                rockets = launcher != null ? launcher.Ammo : 0,
            });
        }

        private void SpawnInitialEnemies()
        {
            foreach (var spawn in initialSpawnPoints)
            {
                Debug.Log($"[EnemyManager] Сервер спавнит врага в {spawn.name}");
                SpawnEnemyAt(spawn.position, spawn.rotation);
            }

            // Ещё несколько врагов стартовой волны подъезжают с обычных точек спавна
            List<Transform> wavePoints = GetWaveSpawnPoints();
            if (initialReinforcements > 0 && wavePoints.Count > 0)
            {
                pendingReinforcements = initialReinforcements;
                StartCoroutine(SpawnReinforcements(wavePoints));
            }
            BeginWave(initialSpawnPoints.Length + pendingReinforcements);
        }

        private GameObject SpawnEnemyAt(Vector3 position, Quaternion rotation)
        {
            return SpawnEnemyAt(enemyPrefab, position, rotation);
        }

        private GameObject SpawnEnemyAt(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject enemy = Instantiate(prefab, position, rotation);
            ApplyWaveDifficulty(enemy);
            // Враги уничтожаются вместе со сценой при перезапуске раунда
            enemy.GetComponent<NetworkObject>().Spawn(true);
            activeEnemies.Add(enemy);
            ServerEnemySpawned?.Invoke(enemy);

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.OnDeath += OnEnemyKilled;
            }
            return enemy;
        }

        // Здоровье, урон, меткость, скорострельность и скорость врага считает только сервер,
        // поэтому усиливать достаточно его копию до спавна
        private void ApplyWaveDifficulty(GameObject enemy)
        {
            int wavesPassed = Mathf.Max(0, wave.Value - 1);

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.ScaleMaxHealth(GetMultiplier(healthGrowthPerWave, wavesPassed));

            // Кривая сложности: от первой волны к difficultyFullWave
            float t = difficultyFullWave > 1 ? Mathf.Clamp01((float)wavesPassed / (difficultyFullWave - 1)) : 1f;
            float spread = Mathf.Lerp(spreadByWave.x, spreadByWave.y, t);
            float interval = Mathf.Lerp(fireIntervalByWave.x, fireIntervalByWave.y, t);

            float damageMultiplier = GetMultiplier(damageGrowthPerWave, wavesPassed);
            foreach (EnemyGun gun in enemy.GetComponentsInChildren<EnemyGun>(true))
            {
                gun.damage *= damageMultiplier;
                gun.spreadAngle *= spread;
                gun.fireRate *= interval;
            }
            foreach (EnemyRocketLauncher launcher in enemy.GetComponentsInChildren<EnemyRocketLauncher>(true))
            {
                launcher.damage *= damageMultiplier;
                launcher.ScaleDifficulty(spread, interval);
            }
            EnemyCarController controller = enemy.GetComponent<EnemyCarController>();
            if (controller != null)
                controller.ScaleSpeed(Mathf.Lerp(speedByWave.x, speedByWave.y, t));
        }

        private float GetMultiplier(float growthPerWave, int wavesPassed)
        {
            return Mathf.Min(1f + growthPerWave * wavesPassed, maxStatMultiplier);
        }

        // Только на сервере
        private void OnEnemyKilled(GameObject enemy)
        {
            killedEnemies.Value++;

            // Комбо: убийства подряд с короткой паузой умножают очки
            combo.Value = Time.time - lastKillTime <= comboWindow ? Mathf.Min(combo.Value + 1, maxCombo) : 1;
            lastKillTime = Time.time;
            EnemyHealth killed = enemy.GetComponent<EnemyHealth>();
            score.Value += (killed != null ? killed.ScoreValue : 0) * combo.Value;
            bool wasBoss = killed != null && killed == boss;
            if (wasBoss)
                boss = null;

            // Ремкомплект из подбитого врага: из босса всегда, а если машина сильно побита — чаще
            if (repairKits != null)
            {
                float chance = kitDropChance;
                if (carHealth != null && carHealth.MaxHealth > 0 && (float)carHealth.CurrentHealth / carHealth.MaxHealth < kitDropLowHealth)
                    chance *= 2f;
                if (wasBoss || Random.value < chance)
                    repairKits.Drop(enemy.transform.position);
            }

            activeEnemies.Remove(enemy);
            farSince.Remove(enemy);

            TryEndWave();
        }

        // Сервер решает, когда начинать новую волну; волна кончается, когда подъехали и убиты все
        // и закончено задание волны (WaveObjectives — выполнено или провалено)
        public void TryEndWave()
        {
            if (!IsServer) return;
            bool objective = WaveObjectives.Instance != null && WaveObjectives.Instance.Active;
            if (activeEnemies.Count == 0 && pendingReinforcements == 0 && !breakActive.Value && !objective)
                StartCoroutine(WaveBreak());
        }

        // Сервер: награда за задание волны
        public void AddScore(int points)
        {
            if (IsServer && points > 0) score.Value += points;
        }

        // Задание «колонна»: count врагов друг за другом с лучшей точки спавна
        public List<GameObject> SpawnConvoy(int count)
        {
            var convoy = new List<GameObject>();
            if (!IsServer || !gameStarted) return convoy;
            List<Transform> points = GetWaveSpawnPoints();
            if (points.Count == 0) return convoy;
            Transform chosen = RankSpawnPoints(points)[0];
            for (int i = 0; i < count; i++)
            {
                Vector3 position = chosen.position - chosen.forward * 12f * i;
                if (Physics.Raycast(position + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 40f, sightMask, QueryTriggerInteraction.Ignore))
                    position = hit.point + Vector3.up * 0.5f;
                convoy.Add(SpawnEnemyAt(PickWavePrefab(), position, chosen.rotation));
                waveEnemyCount++;
            }
            return convoy;
        }

        // Колонна ушла: оставшиеся машины пропадают без очков и трофеев
        public void RemoveEscaped(GameObject enemy)
        {
            if (!IsServer || enemy == null) return;
            activeEnemies.Remove(enemy);
            farSince.Remove(enemy);
            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned) networkObject.Despawn(true);
            else Destroy(enemy);
        }

        // Передышка: бонус за скорость, ремонт, выбор карточки; потом следующая волна
        private IEnumerator WaveBreak()
        {
            breakActive.Value = true;
            float clearTime = Time.time - waveStartTime;
            float par = parTimeBase + parTimePerEnemy * waveEnemyCount;
            int bonus = Mathf.Max(0, Mathf.RoundToInt((par - clearTime) * bonusPerSecond * wave.Value));
            score.Value += bonus;
            WaveClearedRpc(wave.Value, clearTime, bonus);

            List<RunUpgrades.Card> offer = RunUpgrades.Roll(cardRandom);
            float breakEnd = Time.time + breakTime;
            if (offer.Count > 0)
            {
                cardPicked = false;
                OfferCardsRpc(CardCode(offer, 0), CardCode(offer, 1), CardCode(offer, 2), cardTimeout);
                float deadline = Time.time + cardTimeout;
                while (!cardPicked && Time.time < deadline)
                    yield return null;
                if (!cardPicked)
                    PickCard(offer[cardRandom.Next(offer.Count)]);
                // После выбора — секунда на то, чтобы прочитать карточку
                breakEnd = Mathf.Max(breakEnd, Time.time + 1.5f);
            }
            while (Time.time < breakEnd)
                yield return null;

            // Карточка к этому моменту применена у всех (CardChosenRpc)
            SaveRun();
            breakActive.Value = false;
            SpawnRandomWave();
        }

        // Выбор карточки игроком (на любом из компьютеров): одна из предложенных
        public void ChooseCard(RunUpgrades.Card card)
        {
            if (!IsSpawned || !offeredCards.Contains(card)) return;
            ChooseCardRpc((int)card);
        }

        [Rpc(SendTo.Server)]
        private void ChooseCardRpc(int card)
        {
            if (cardPicked || !breakActive.Value || !offeredCards.Contains((RunUpgrades.Card)card)) return;
            PickCard((RunUpgrades.Card)card);
        }

        private void PickCard(RunUpgrades.Card card)
        {
            cardPicked = true;
            CardChosenRpc((int)card);
        }

        // Карточки передаются кодами: -1 — нет карточки
        private static int CardCode(List<RunUpgrades.Card> offer, int index) => index < offer.Count ? (int)offer[index] : -1;

        [Rpc(SendTo.Everyone)]
        private void OfferCardsRpc(int first, int second, int third, float timeout)
        {
            offeredCards.Clear();
            foreach (int code in new[] { first, second, third })
                if (code >= 0) offeredCards.Add((RunUpgrades.Card)code);
            CardDeadline = Time.time + timeout;
            CardsOffered?.Invoke(offeredCards);
        }

        [Rpc(SendTo.Everyone)]
        private void CardChosenRpc(int code)
        {
            RunUpgrades.Card card = (RunUpgrades.Card)code;
            offeredCards.Clear();
            RunUpgrades.Apply(card);
            // Прочность есть у машины на обоих компьютерах: прибавку делают оба, чинит сервер
            if (card == RunUpgrades.Card.Armor && carHealth != null)
                carHealth.IncreaseMaxHealth(RunUpgrades.ArmorPerLevel);
            CardChosen?.Invoke(card);
            CardsOffered?.Invoke(offeredCards);
        }

        [Rpc(SendTo.Everyone)]
        private void WaveClearedRpc(int number, float seconds, int bonus)
        {
            WaveCleared?.Invoke(number, seconds, bonus);
        }

        [Rpc(SendTo.Everyone)]
        private void WaveStartedRpc(int number)
        {
            WaveStarted?.Invoke(number);
        }

        private void BeginWave(int count)
        {
            waveStartTime = Time.time;
            waveEnemyCount = count;
            WaveStartedRpc(wave.Value);
        }

        private void SpawnRandomWave()
        {
            wave.Value++;
            specialsThisWave.Clear();

            List<Transform> wavePoints = GetWaveSpawnPoints();
            if (wavePoints.Count == 0) return;

            // Каждые extraEnemyEveryWaves волн — на одного врага больше
            int extra = extraEnemyEveryWaves > 0 ? (wave.Value - 2) / extraEnemyEveryWaves : 0;
            int count = Mathf.Clamp(enemiesPerWave + extra + WaveSizeBias, 1, Mathf.Max(1, maxEnemiesPerWave));

            // Точки по пригодности: на нужном расстоянии от машины и вне её поля зрения — в начале
            List<Transform> availablePoints = RankSpawnPoints(wavePoints);
            int spawned = 0;

            // Волна с боссом: он занимает место одного из врагов
            if (bossPrefab != null && bossEveryWaves > 0 && wave.Value % bossEveryWaves == 0)
            {
                Transform bossPoint = TakeSpawnPoint(availablePoints);
                boss = SpawnEnemyAt(bossPrefab, bossPoint.position, bossPoint.rotation).GetComponent<EnemyHealth>();
                spawned++;
            }

            List<Transform> freeAmbushes = new List<Transform>(ambushPoints);
            while (spawned < count)
            {
                // Засада: враг ждёт впереди по ходу машины игроков и выезжает, когда она подъедет
                if (wave.Value >= ambushFromWave && Random.value < ambushChance && TryPickAmbushPoint(freeAmbushes, out Transform ambush))
                {
                    GameObject ambusher = SpawnEnemyAt(PickWavePrefab(), ambush.position, ambush.rotation);
                    EnemyCarController controller = ambusher.GetComponent<EnemyCarController>();
                    if (controller != null)
                        controller.waitInAmbush = true;
                    spawned++;
                    continue;
                }
                if (availablePoints.Count == 0) break;

                Transform chosen = TakeSpawnPoint(availablePoints);
                SpawnEnemyAt(PickWavePrefab(), chosen.position, chosen.rotation);
                spawned++;
            }

            // Точек меньше, чем врагов: остальные подъезжают позже, когда первые отъедут от точек
            pendingReinforcements = count - spawned;
            if (pendingReinforcements > 0)
                StartCoroutine(SpawnReinforcements(wavePoints));
            BeginWave(count);
        }

        // Режиссёр: ещё один враг в текущую волну — подъезжает с лучшей точки спавна
        public bool SpawnFlanker()
        {
            if (!IsServer || !gameStarted || breakActive.Value) return false;
            List<Transform> points = GetWaveSpawnPoints();
            if (points.Count == 0) return false;
            Transform chosen = RankSpawnPoints(points)[0];
            SpawnEnemyAt(PickWavePrefab(), chosen.position, chosen.rotation);
            waveEnemyCount++;
            return true;
        }

        private IEnumerator SpawnReinforcements(List<Transform> wavePoints)
        {
            while (pendingReinforcements > 0)
            {
                yield return new WaitForSeconds(reinforcementDelay);
                while (HoldReinforcements)
                    yield return null;

                // Машина уже уехала: подходящую точку выбираем заново
                Transform chosen = RankSpawnPoints(wavePoints)[0];
                pendingReinforcements--;
                SpawnEnemyAt(PickWavePrefab(), chosen.position, chosen.rotation);
            }
        }

        // Точки спавна по пригодности. Лучшие — на расстоянии spawnMinDistance…spawnMaxDistance от машины игроков
        // и вне её поля зрения (случайно между собой), затем видимые на этом расстоянии, затем остальные — ближние к нужному кольцу
        private List<Transform> RankSpawnPoints(List<Transform> points)
        {
            var ranked = new List<Transform>(points);
            Transform car = carHealth != null ? carHealth.transform : null;
            if (car == null)
            {
                Shuffle(ranked);
                return ranked;
            }
            var score = new Dictionary<Transform, float>();
            foreach (Transform point in ranked)
            {
                if (point == null) { score[point] = float.MaxValue; continue; }
                float distance = Vector3.Distance(point.position, car.position);
                bool inRing = distance >= spawnMinDistance && distance <= spawnMaxDistance;
                float outside = distance < spawnMinDistance ? spawnMinDistance - distance : Mathf.Max(0f, distance - spawnMaxDistance);
                float rank = inRing ? (HiddenFromCar(point.position, car, distance) ? 0f : 1000f) : 2000f + outside;
                score[point] = rank + Random.value * 100f;
            }
            ranked.Sort((a, b) => score[a].CompareTo(score[b]));
            return ranked;
        }

        private static Transform TakeSpawnPoint(List<Transform> ranked)
        {
            Transform point = ranked[0];
            ranked.RemoveAt(0);
            return point;
        }

        private static void Shuffle(List<Transform> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // Точку не видно с машины: далеко или её закрывают дома и рельеф
        private bool HiddenFromCar(Vector3 point, Transform car, float distance)
        {
            if (distance > spawnUnseenDistance) return true;
            Vector3 eye = car.position + Vector3.up * 2.5f;
            Vector3 body = point + Vector3.up * 1.5f;
            if (!Physics.Linecast(eye, body, out RaycastHit hit, sightMask, QueryTriggerInteraction.Ignore))
                return false;
            return !hit.collider.transform.IsChildOf(car.root);
        }

        // Есть ли враг ближе combatDistance к машине игроков
        private bool EnemyNearCar()
        {
            if (carHealth == null) return activeEnemies.Count > 0;
            Vector3 car = carHealth.transform.position;
            foreach (GameObject enemy in activeEnemies)
                if (enemy != null && (enemy.transform.position - car).sqrMagnitude < combatDistance * combatDistance)
                    return true;
            return false;
        }

        // Ремонт: передышка и улучшение «ремонт на ходу». Доли единицы копятся, чтобы медленный ремонт не терялся
        private void RepairCar(float perSecond)
        {
            if (carHealth == null || perSecond <= 0f) return;
            regenCarry += perSecond * Time.deltaTime;
            int whole = Mathf.FloorToInt(regenCarry);
            if (whole <= 0) return;
            regenCarry -= whole;
            carHealth.Heal(whole);
        }

        // Страховка: провалившийся под карту или давно отставший враг переносится на точку спавна вне поля зрения
        private void WatchEnemies()
        {
            if (carHealth == null) return;
            Vector3 car = carHealth.transform.position;
            List<Transform> wavePoints = null;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                GameObject enemy = activeEnemies[i];
                if (enemy == null)
                {
                    activeEnemies.RemoveAt(i);
                    continue;
                }
                Vector3 position = enemy.transform.position;
                bool fallen = position.y < car.y - fallenDepth;
                bool far = (position - car).sqrMagnitude > stragglerDistance * stragglerDistance;
                if (!far) farSince.Remove(enemy);
                else if (!farSince.ContainsKey(enemy)) farSince[enemy] = Time.time;
                bool straggler = far && Time.time - farSince[enemy] > stragglerTime;
                if (!fallen && !straggler) continue;

                EnemyCarController controller = enemy.GetComponent<EnemyCarController>();
                if (wavePoints == null) wavePoints = RankSpawnPoints(GetWaveSpawnPoints());
                if (controller == null || wavePoints.Count == 0) continue;
                Transform point = TakeSpawnPoint(wavePoints);
                Debug.Log($"[EnemyManager] {enemy.name} {(fallen ? "провалился под карту" : "давно отстал")} — переносим на {point.name}");
                controller.Teleport(point.position + Vector3.up * 1.5f, point.rotation);
                farSince.Remove(enemy);
                if (wavePoints.Count == 0) wavePoints = null;
            }
        }

        // Роли: стрелкам рядом с машиной игроков — разные места вокруг неё. Каждому — ближайшее к нему из нужных мест,
        // поэтому машины не меняются местами через игрока и не толкаются за одно место
        private readonly List<EnemyCarController> slotCandidates = new List<EnemyCarController>();
        private readonly List<float> slotBearings = new List<float>();

        private void AssignSlots()
        {
            if (carHealth == null) return;
            Transform car = carHealth.transform;
            Rigidbody carBody = car.GetComponentInParent<Rigidbody>();
            Vector3 heading = carBody != null && carBody.linearVelocity.sqrMagnitude > 4f ? carBody.linearVelocity : car.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.001f) return;

            slotCandidates.Clear();
            slotBearings.Clear();
            foreach (GameObject enemy in activeEnemies)
            {
                if (enemy == null) continue;
                EnemyCarController controller = enemy.GetComponent<EnemyCarController>();
                if (controller == null || !controller.HoldsSlot || controller.waitInAmbush) continue;
                Vector3 offset = enemy.transform.position - car.position;
                offset.y = 0f;
                if (offset.sqrMagnitude > slotDistance * slotDistance) continue;
                slotCandidates.Add(controller);
                slotBearings.Add(Vector3.SignedAngle(heading, offset, Vector3.up));
            }

            int count = Mathf.Min(slotCandidates.Count, SlotAngles.Length);
            var takenSlot = new bool[count];
            var done = new bool[slotCandidates.Count];
            // Жадно: пара «враг — место» с наименьшей разницей углов, пока места не кончатся
            for (int n = 0; n < count; n++)
            {
                int bestEnemy = -1, bestSlot = -1;
                float bestDelta = float.MaxValue;
                for (int e = 0; e < slotCandidates.Count; e++)
                {
                    if (done[e]) continue;
                    for (int s = 0; s < count; s++)
                    {
                        if (takenSlot[s]) continue;
                        float delta = Mathf.Abs(Mathf.DeltaAngle(slotBearings[e], SlotAngles[s]));
                        if (delta < bestDelta) { bestDelta = delta; bestEnemy = e; bestSlot = s; }
                    }
                }
                if (bestEnemy < 0) break;
                done[bestEnemy] = true;
                takenSlot[bestSlot] = true;
                slotCandidates[bestEnemy].AssignSlot(SlotAngles[bestSlot]);
            }
        }

        // Точка засады на нужном расстоянии от машины игроков; если машина едет — впереди по ходу
        private bool TryPickAmbushPoint(List<Transform> free, out Transform point)
        {
            point = null;
            Transform car = carHealth != null ? carHealth.transform : null;
            if (car == null || free.Count == 0) return false;

            Rigidbody carBody = car.GetComponentInParent<Rigidbody>();
            Vector3 heading = carBody != null ? carBody.linearVelocity : Vector3.zero;
            heading.y = 0f;
            bool moving = heading.sqrMagnitude > 25f;

            List<Transform> fits = new List<Transform>();
            foreach (Transform candidate in free)
            {
                if (candidate == null) continue;
                Vector3 offset = candidate.position - car.position;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < ambushMinDistance || distance > ambushMaxDistance) continue;
                if (moving && Vector3.Dot(heading.normalized, offset / distance) < 0.3f) continue;
                fits.Add(candidate);
            }
            if (fits.Count == 0) return false;

            point = fits[Random.Range(0, fits.Count)];
            free.Remove(point);
            return true;
        }

        // Обычный враг или, с заданным шансом, один из особых, которым уже пора и которых в волне ещё мало
        private GameObject PickWavePrefab()
        {
            int start = Random.Range(0, Mathf.Max(1, specialEnemies.Length));
            for (int i = 0; i < specialEnemies.Length; i++)
            {
                SpecialEnemy special = specialEnemies[(start + i) % specialEnemies.Length];
                if (special.prefab == null || wave.Value < special.fromWave) continue;

                specialsThisWave.TryGetValue(special.prefab, out int already);
                if (already >= special.maxPerWave || Random.value >= special.chance) continue;

                specialsThisWave[special.prefab] = already + 1;
                return special.prefab;
            }
            return enemyPrefab;
        }

        // Стартовые точки рядом с машиной игроков, в волнах после первой их не используем
        private List<Transform> GetWaveSpawnPoints()
        {
            List<Transform> points = new List<Transform>(spawnPoints);
            foreach (var p in initialSpawnPoints) points.Remove(p);
            return points;
        }

        private void OnKillsChanged(int previous, int current)
        {
            UpdateUI();
        }

        private void OnBossHealthChanged(float previous, float current)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (killedTextUI == null) return;

            if (wave.Value <= 0)
            {
                killedTextUI.text = $"Убито: {killedEnemies.Value}";
                return;
            }

            string text = $"Волна {wave.Value}  Убито: {killedEnemies.Value}\nОчки: {score.Value}";
            if (combo.Value > 1)
                text += $"  x{combo.Value}";
            if (bossHealth.Value >= 0f)
                text += $"\nБосс: {Mathf.CeilToInt(bossHealth.Value * 100f)}%";
            killedTextUI.text = text;
        }
    }
}
