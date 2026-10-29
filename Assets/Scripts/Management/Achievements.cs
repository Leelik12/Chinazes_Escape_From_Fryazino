using System;
using UnityEngine;
using RacingProject.Enemy;
using RacingProject.Pickups;
using RacingProject.Props;

namespace RacingProject.Management
{
    // Достижения: у каждого игрока свои, хранятся в PlayerPrefs на его компьютере. Условия проверяются по событиям,
    // которые видят оба игрока (волны, счёт, комбо, взрывы, урон машине), поэтому в кооперативе получают оба.
    // Состояние забега сбрасывается с первой волной. Список показывает вкладка «Награды» терминала (AchievementsPage),
    // о новом достижении сообщает HUD волн (WaveHud)
    public class Achievements : MonoBehaviour
    {
        public enum Id
        {
            Wave5,
            Wave10,
            Wave20,
            Boss,
            TankerChain,
            NoKitNight,
            MaxCombo,
            Score10k,
            Kills100,
            Flawless,
        }

        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PlayerHealth carHealth;
        [Tooltip("Сколько крупных взрывов (бензовозы, резервуары, машины) подряд нужно для «бензиновой цепочки»")]
        [SerializeField] private int chainCount = 3;
        [Tooltip("За сколько секунд должны случиться взрывы цепочки")]
        [SerializeField] private float chainWindow = 4f;

        public const int KillsGoal = 100;
        public const int ScoreGoal = 10000;
        private const string KillsKey = "AchKills";

        private static readonly string[] Titles =
        {
            "Пятая волна", "До ночи", "Двадцатая", "Крупная рыба", "Бензиновая цепочка",
            "Ночь без аптечки", "Комбо", "Десять тысяч", "Сотня", "Без царапины",
        };

        private static readonly string[] Descriptions =
        {
            "Дожить до 5-й волны", "Дожить до 10-й волны — до полной ночи", "Дожить до 20-й волны",
            "Подбить босса", "Взорвать три бензовоза, резервуара или брошенные машины подряд",
            "Дожить до 10-й волны, не подобрав ни одного ремкомплекта", "Набрать комбо наибольшего множителя",
            "Набрать " + ScoreGoal + " очков за один забег", "Подбить " + KillsGoal + " врагов за все забеги",
            "Зачистить волну, не получив ни одного урона",
        };

        public static int Count => Titles.Length;
        public static string Title(Id id) => Titles[(int)id];
        public static string Description(Id id) => Descriptions[(int)id];
        public static bool Has(Id id) => PlayerPrefs.GetInt(Key(id), 0) == 1;
        public static int TotalKills => PlayerPrefs.GetInt(KillsKey, 0);

        // Получено новое достижение (у этого игрока)
        public static event Action<Id> Unlocked;

        private static string Key(Id id) => "Ach" + id;

        // Забег: подобран ли ремкомплект, был ли урон в этой волне, время последних крупных взрывов
        private bool kitTaken;
        private bool hitThisWave;
        private float lastBoss = -1f;
        private readonly float[] blasts = new float[8];
        private int blastIndex;

        private void OnEnable()
        {
            if (enemyManager != null)
            {
                enemyManager.WaveStarted += OnWaveStarted;
                enemyManager.WaveCleared += OnWaveCleared;
            }
            if (carHealth != null) carHealth.OnDamageTaken += OnCarDamaged;
            EnemyHealth.Killed += OnEnemyKilled;
            RepairKits.PickedUp += OnKitPicked;
            ExplosiveBarrel.BarrelExploded += OnExploded;
        }

        private void OnDisable()
        {
            if (enemyManager != null)
            {
                enemyManager.WaveStarted -= OnWaveStarted;
                enemyManager.WaveCleared -= OnWaveCleared;
            }
            if (carHealth != null) carHealth.OnDamageTaken -= OnCarDamaged;
            EnemyHealth.Killed -= OnEnemyKilled;
            RepairKits.PickedUp -= OnKitPicked;
            ExplosiveBarrel.BarrelExploded -= OnExploded;
        }

        private void Update()
        {
            if (enemyManager == null || !enemyManager.IsSpawned || Dead) return;
            if (enemyManager.Score >= ScoreGoal) Unlock(Id.Score10k);
            if (enemyManager.Combo >= enemyManager.MaxCombo) Unlock(Id.MaxCombo);
            // Босс подбит: его полоска прочности пропала, а машина цела
            float boss = enemyManager.BossHealth01;
            if (lastBoss >= 0f && boss < 0f) Unlock(Id.Boss);
            lastBoss = boss;
        }

        private bool Dead => carHealth != null && carHealth.IsDead;

        private void OnWaveStarted(int wave)
        {
            if (wave <= 1)
            {
                kitTaken = false;
                Array.Clear(blasts, 0, blasts.Length);
            }
            hitThisWave = false;
            if (wave >= 5) Unlock(Id.Wave5);
            if (wave >= 10) Unlock(Id.Wave10);
            if (wave >= 20) Unlock(Id.Wave20);
            if (wave >= 10 && !kitTaken) Unlock(Id.NoKitNight);
        }

        private void OnWaveCleared(int wave, float seconds, int bonus)
        {
            if (!hitThisWave && !Dead) Unlock(Id.Flawless);
        }

        private void OnCarDamaged(int damage)
        {
            hitThisWave = true;
        }

        private void OnKitPicked()
        {
            kitTaken = true;
        }

        private void OnEnemyKilled(Vector3 point, ulong attacker)
        {
            int kills = TotalKills + 1;
            PlayerPrefs.SetInt(KillsKey, kills);
            if (kills >= KillsGoal) Unlock(Id.Kills100);
        }

        // Крупные взрывы: chainCount штук за chainWindow секунд
        private void OnExploded(ExplosiveBarrel barrel)
        {
            if (!barrel.IsLarge) return;
            blasts[blastIndex] = Time.time;
            blastIndex = (blastIndex + 1) % blasts.Length;
            int recent = 0;
            foreach (float t in blasts)
                if (t > 0f && Time.time - t <= chainWindow) recent++;
            if (recent >= chainCount) Unlock(Id.TankerChain);
        }

        private static void Unlock(Id id)
        {
            if (Has(id)) return;
            PlayerPrefs.SetInt(Key(id), 1);
            PlayerPrefs.Save();
            Unlocked?.Invoke(id);
        }
    }
}
