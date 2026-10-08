using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

namespace RacingProject.Enemy
{
    public class EnemyManager : NetworkBehaviour
    {
        [Header("Спавнпоинты")]
        [Tooltip("Все точки спавна врагов")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("Начальные точки спавна (2 врага)")]
        [SerializeField] private Transform[] initialSpawnPoints;

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
        [Tooltip("Очки за выживание каждые survivalInterval секунд")]
        [SerializeField] private int survivalPoints = 10;
        [SerializeField] private float survivalInterval = 5f;

        [Header("UI")]
        [SerializeField] private TMP_Text killedTextUI;

        [Header("Настройки")]
        [Tooltip("Врагов в первой волне после стартовой")]
        [SerializeField] private int enemiesPerWave = 2;

        [Header("Сложность волн")]
        [Tooltip("Через сколько волн в волне становится на одного врага больше")]
        [SerializeField] private int extraEnemyEveryWaves = 2;
        [Tooltip("Больше врагов в одной волне не бывает")]
        [SerializeField] private int maxEnemiesPerWave = 5;
        [Tooltip("На сколько растёт здоровье врага за каждую волну (0.1 = +10%)")]
        [SerializeField] private float healthGrowthPerWave = 0.1f;
        [Tooltip("На сколько растёт урон пулемёта врага за каждую волну (0.1 = +10%)")]
        [SerializeField] private float damageGrowthPerWave = 0.1f;
        [Tooltip("Предел роста здоровья и урона относительно первой волны")]
        [SerializeField] private float maxStatMultiplier = 2.5f;
        [Tooltip("Когда врагов в волне больше, чем точек спавна, остальные подъезжают с такой паузой, с")]
        [SerializeField] private float reinforcementDelay = 6f;

        // Враги живут и умирают на сервере, счёт убийств и номер волны видят оба игрока
        private readonly NetworkVariable<int> killedEnemies = new NetworkVariable<int>();
        private readonly NetworkVariable<int> wave = new NetworkVariable<int>();
        private readonly NetworkVariable<int> score = new NetworkVariable<int>();
        private readonly NetworkVariable<int> combo = new NetworkVariable<int>(1);
        // Прочность босса для UI, -1 — босса нет
        private readonly NetworkVariable<float> bossHealth = new NetworkVariable<float>(-1f);

        // Итог раунда для экрана гибели
        public int Wave => wave.Value;
        public int Kills => killedEnemies.Value;
        public int Score => score.Value;
        // Для приборов: множитель комбо и прочность босса 0–1 (меньше нуля — босса нет)
        public int Combo => combo.Value;
        public float BossHealth01 => bossHealth.Value;

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
        private int pendingReinforcements;
        private bool gameStarted = false;
        private EnemyHealth boss;
        private float lastKillTime = float.NegativeInfinity;
        private float nextSurvivalTime;

        void Start()
        {
            UpdateUI();
        }

        public override void OnNetworkSpawn()
        {
            killedEnemies.OnValueChanged += OnKillsChanged;
            wave.OnValueChanged += OnKillsChanged;
            score.OnValueChanged += OnKillsChanged;
            combo.OnValueChanged += OnKillsChanged;
            bossHealth.OnValueChanged += OnBossHealthChanged;
            UpdateUI();
        }

        public override void OnNetworkDespawn()
        {
            killedEnemies.OnValueChanged -= OnKillsChanged;
            wave.OnValueChanged -= OnKillsChanged;
            score.OnValueChanged -= OnKillsChanged;
            combo.OnValueChanged -= OnKillsChanged;
            bossHealth.OnValueChanged -= OnBossHealthChanged;
        }

        // Сервер: очки за выживание, сброс комбо, прочность босса для UI
        private void Update()
        {
            if (!IsServer || !IsSpawned || !gameStarted) return;

            bool carAlive = carHealth == null || !carHealth.IsDead;
            if (carAlive && Time.time >= nextSurvivalTime)
            {
                nextSurvivalTime = Time.time + survivalInterval;
                score.Value += survivalPoints;
            }

            if (combo.Value > 1 && Time.time - lastKillTime > comboWindow)
                combo.Value = 1;

            float bossFraction = boss != null ? boss.HealthFraction : -1f;
            if (!Mathf.Approximately(bossHealth.Value, bossFraction))
                bossHealth.Value = bossFraction;
        }

        // Вызывается при старте игры из RoomController
        public void StartGame()
        {
            if (gameStarted) return;
            gameStarted = true;

            // Только сервер отвечает за спавн врагов и счёт
            if (IsServer)
            {
                killedEnemies.Value = 0;
                score.Value = 0;
                combo.Value = 1;
                wave.Value = 1;
                nextSurvivalTime = Time.time + survivalInterval;
                SpawnInitialEnemies();
            }
        }

        private void SpawnInitialEnemies()
        {
            foreach (var spawn in initialSpawnPoints)
            {
                Debug.Log($"[EnemyManager] Сервер спавнит врага в {spawn.name}");
                SpawnEnemyAt(spawn.position, spawn.rotation);
            }
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

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.OnDeath += OnEnemyKilled;
            }
            return enemy;
        }

        // Здоровье и урон врага считает только сервер, поэтому усиливать достаточно его копию до спавна
        private void ApplyWaveDifficulty(GameObject enemy)
        {
            int wavesPassed = Mathf.Max(0, wave.Value - 1);

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.ScaleMaxHealth(GetMultiplier(healthGrowthPerWave, wavesPassed));

            float damageMultiplier = GetMultiplier(damageGrowthPerWave, wavesPassed);
            foreach (EnemyGun gun in enemy.GetComponentsInChildren<EnemyGun>(true))
                gun.damage *= damageMultiplier;
            foreach (EnemyRocketLauncher launcher in enemy.GetComponentsInChildren<EnemyRocketLauncher>(true))
                launcher.damage *= damageMultiplier;
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
            if (killed == boss)
                boss = null;

            activeEnemies.Remove(enemy);

            // Сервер решает, когда начинать новую волну; волна кончается, когда подъехали и убиты все
            if (activeEnemies.Count == 0 && pendingReinforcements == 0)
            {
                SpawnRandomWave();
            }
        }

        private void SpawnRandomWave()
        {
            wave.Value++;
            specialsThisWave.Clear();

            List<Transform> wavePoints = GetWaveSpawnPoints();
            if (wavePoints.Count == 0) return;

            // Каждые extraEnemyEveryWaves волн — на одного врага больше
            int extra = extraEnemyEveryWaves > 0 ? (wave.Value - 2) / extraEnemyEveryWaves : 0;
            int count = Mathf.Clamp(enemiesPerWave + extra, 1, Mathf.Max(1, maxEnemiesPerWave));

            List<Transform> availablePoints = new List<Transform>(wavePoints);
            int spawned = 0;

            // Волна с боссом: он занимает место одного из врагов
            if (bossPrefab != null && bossEveryWaves > 0 && wave.Value % bossEveryWaves == 0)
            {
                int bossIndex = Random.Range(0, availablePoints.Count);
                Transform bossPoint = availablePoints[bossIndex];
                availablePoints.RemoveAt(bossIndex);

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

                int index = Random.Range(0, availablePoints.Count);
                Transform chosen = availablePoints[index];
                availablePoints.RemoveAt(index);

                SpawnEnemyAt(PickWavePrefab(), chosen.position, chosen.rotation);
                spawned++;
            }

            // Точек меньше, чем врагов: остальные подъезжают позже, когда первые отъедут от точек
            pendingReinforcements = count - spawned;
            if (pendingReinforcements > 0)
                StartCoroutine(SpawnReinforcements(wavePoints));
        }

        private IEnumerator SpawnReinforcements(List<Transform> wavePoints)
        {
            while (pendingReinforcements > 0)
            {
                yield return new WaitForSeconds(reinforcementDelay);

                Transform chosen = wavePoints[Random.Range(0, wavePoints.Count)];
                pendingReinforcements--;
                SpawnEnemyAt(PickWavePrefab(), chosen.position, chosen.rotation);
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
