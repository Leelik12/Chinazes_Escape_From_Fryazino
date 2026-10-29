using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using RacingProject.Pickups;

namespace RacingProject.Enemy
{
    // Режиссёр забега (как в Left 4 Dead): следит за напряжением экипажа и дозирует давление поверх волн.
    // Напряжение — от потерянной прочности и урона за последние секунды. Когда экипажу тяжело — передышка:
    // подкрепления ждут, налётов нет, штаб по рации подсказывает, где ремкомплект. Когда экипаж давно не получал
    // урона — натиск: в волну приходит ещё один враг. Налёты артиллерии — время от времени, сразу — если машина
    // стоит на месте. Между волнами размер следующей волны подстраивается под то, как прошла прошлая.
    // Работает только на сервере
    public class GameDirector : MonoBehaviour
    {
        public enum Phase { Build, Relax }

        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PlayerHealth carHealth;
        [SerializeField] private ArtilleryStrikes artillery;
        [SerializeField] private RadioChatter radio;
        [SerializeField] private RepairKits repairKits;

        [Header("Напряжение")]
        [Tooltip("Окно, в котором считается недавний урон, с")]
        [SerializeField] private float damageWindow = 15f;
        [Tooltip("Урон за окно (доля прочности), который даёт полное напряжение")]
        [SerializeField] private float damageForFullStress = 0.35f;
        [Tooltip("Выше — передышка")]
        [SerializeField] private float relaxStress = 0.72f;
        [SerializeField] private float relaxTime = 18f;

        [Header("Натиск")]
        [Tooltip("Без урона столько секунд в бою — натиск")]
        [SerializeField] private float calmTime = 30f;
        [SerializeField] private float pushCooldown = 40f;
        [SerializeField] private int maxFlankersPerWave = 2;

        [Header("Артналёты")]
        [SerializeField] private int artilleryFromWave = 2;
        [Tooltip("Пауза между налётами, с (случайно в диапазоне)")]
        [SerializeField] private Vector2 artilleryInterval = new Vector2(55f, 110f);
        [Tooltip("Машина медленнее этого, м/с, столько секунд — стоит на месте, налёт сразу")]
        [SerializeField] private float campSpeed = 4f;
        [SerializeField] private float campTime = 12f;
        [SerializeField] private int minShells = 5;
        [SerializeField] private int maxShells = 10;

        [Header("Размер волны")]
        [SerializeField] private int minBias = -2;
        [SerializeField] private int maxBias = 3;
        [Tooltip("Быстрая зачистка: меньше этого секунд на врага")]
        [SerializeField] private float fastSecondsPerEnemy = 9f;

        public Phase Current { get; private set; }
        public float Stress { get; private set; }

        private readonly Queue<KeyValuePair<float, int>> recentDamage = new Queue<KeyValuePair<float, int>>();
        private int recentSum;
        private float lastDamageTime;
        private float phaseUntil;
        private float nextPush;
        private float nextArtillery;
        private float slowSince;
        private int flankersThisWave;
        private int waveEnemies;
        private Rigidbody carBody;

        private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void OnEnable()
        {
            if (carHealth != null) carHealth.OnDamageTaken += OnDamage;
            if (enemyManager != null)
            {
                enemyManager.WaveStarted += OnWaveStarted;
                enemyManager.WaveCleared += OnWaveCleared;
            }
        }

        private void OnDisable()
        {
            if (carHealth != null) carHealth.OnDamageTaken -= OnDamage;
            if (enemyManager != null)
            {
                enemyManager.WaveStarted -= OnWaveStarted;
                enemyManager.WaveCleared -= OnWaveCleared;
            }
        }

        private void Start()
        {
            if (carHealth != null) carBody = carHealth.GetComponent<Rigidbody>();
            lastDamageTime = Time.time;
            nextPush = Time.time + pushCooldown;
            nextArtillery = Time.time + artilleryInterval.y;
            slowSince = Time.time;
        }

        private void OnDamage(int damage)
        {
            if (damage <= 0) return;
            lastDamageTime = Time.time;
            recentDamage.Enqueue(new KeyValuePair<float, int>(Time.time, damage));
            recentSum += damage;
        }

        private void OnWaveStarted(int wave)
        {
            flankersThisWave = 0;
            waveEnemies = 0;
            lastDamageTime = Time.time;
            if (enemyManager != null && IsServer)
                waveEnemies = enemyManager.ActiveEnemies.Count;
        }

        // Как прошла волна: быстро и почти без потерь — следующая крупнее, тяжело — меньше
        private void OnWaveCleared(int wave, float seconds, int bonus)
        {
            if (!IsServer || enemyManager == null || carHealth == null) return;
            float health = carHealth.MaxHealth > 0 ? (float)carHealth.CurrentHealth / carHealth.MaxHealth : 1f;
            int enemies = Mathf.Max(1, waveEnemies);
            int bias = enemyManager.WaveSizeBias;
            if (health > 0.7f && seconds < enemies * fastSecondsPerEnemy) bias++;
            else if (health < 0.35f) bias--;
            enemyManager.WaveSizeBias = Mathf.Clamp(bias, minBias, maxBias);
            // На передышке давление сбрасывается
            SetPhase(Phase.Build, 0f);
        }

        private void Update()
        {
            if (!IsServer || enemyManager == null || carHealth == null || carHealth.IsDead) return;
            float now = Time.time;

            while (recentDamage.Count > 0 && now - recentDamage.Peek().Key > damageWindow)
                recentSum -= recentDamage.Dequeue().Value;
            float health = carHealth.MaxHealth > 0 ? (float)carHealth.CurrentHealth / carHealth.MaxHealth : 1f;
            float burst = Mathf.Clamp01(recentSum / Mathf.Max(1f, carHealth.MaxHealth * damageForFullStress));
            float target = Mathf.Clamp01(0.45f * (1f - health) + 0.55f * burst);
            // Напряжение растёт быстро, спадает медленно
            Stress = Mathf.MoveTowards(Stress, target, (target > Stress ? 0.6f : 0.05f) * Time.deltaTime);

            bool fighting = !enemyManager.BreakActive && enemyManager.ActiveEnemies.Count > 0;
            float speed = carBody != null ? carBody.linearVelocity.magnitude : 0f;
            if (speed > campSpeed) slowSince = now;

            switch (Current)
            {
                case Phase.Relax:
                    if (now >= phaseUntil) SetPhase(Phase.Build, 0f);
                    break;
                default:
                    if (fighting && Stress >= relaxStress)
                    {
                        SetPhase(Phase.Relax, relaxTime);
                        if (radio != null) radio.SayRelief(NearestKit());
                    }
                    else if (fighting && now - lastDamageTime > calmTime && now >= nextPush && flankersThisWave < maxFlankersPerWave)
                    {
                        nextPush = now + pushCooldown;
                        if (enemyManager.SpawnFlanker())
                        {
                            flankersThisWave++;
                            waveEnemies++;
                            if (radio != null) radio.SayFlank();
                        }
                    }
                    break;
            }

            UpdateArtillery(now, fighting);
        }

        private void UpdateArtillery(float now, bool fighting)
        {
            if (artillery == null || !fighting || Current == Phase.Relax || enemyManager.Wave < artilleryFromWave) return;
            bool camping = now - slowSince > campTime;
            if (!camping && now < nextArtillery) return;

            Vector3 car = carHealth.transform.position;
            Vector3 velocity = carBody != null ? carBody.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            // Квадрат — там, где машина будет через несколько секунд: чтобы уйти, надо свернуть или ускориться
            Vector2 jitter = Random.insideUnitCircle * (camping ? 4f : 12f);
            Vector3 center = car + velocity * (camping ? 0f : 4.5f) + new Vector3(jitter.x, 0f, jitter.y);
            int shells = Mathf.Clamp(minShells + enemyManager.Wave / 3, minShells, maxShells);
            artillery.CallStrike(center, shells);
            nextArtillery = now + Random.Range(artilleryInterval.x, artilleryInterval.y);
            slowSince = now;
        }

        private void SetPhase(Phase phase, float duration)
        {
            Current = phase;
            phaseUntil = Time.time + duration;
            if (enemyManager != null)
                enemyManager.HoldReinforcements = phase == Phase.Relax;
        }

        private Vector3? NearestKit()
        {
            if (repairKits == null || carHealth == null) return null;
            return repairKits.NearestAvailable(carHealth.transform.position);
        }
    }
}
