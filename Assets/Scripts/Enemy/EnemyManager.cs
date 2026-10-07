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

        private List<GameObject> activeEnemies = new List<GameObject>();
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

            while (spawned < count && availablePoints.Count > 0)
            {
                int index = Random.Range(0, availablePoints.Count);
                Transform chosen = availablePoints[index];
                availablePoints.RemoveAt(index);

                SpawnEnemyAt(chosen.position, chosen.rotation);
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
                SpawnEnemyAt(chosen.position, chosen.rotation);
            }
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
