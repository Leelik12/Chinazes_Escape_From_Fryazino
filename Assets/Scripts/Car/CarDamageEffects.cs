using UnityEngine;
using RacingProject.Enemy;

namespace RacingProject.Car
{
    // Видимые повреждения машины: дым из-под капота густеет и темнеет по мере потери прочности,
    // перед гибелью появляется огонь, при гибели — взрыв. Работает и для машины игроков (PlayerHealth),
    // и для врагов (EnemyHealth, взрыв врага создаёт он сам). Прочность синхронизирована,
    // поэтому эффекты одинаковы у обоих игроков
    public class CarDamageEffects : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [Tooltip("Для машины врага вместо PlayerHealth")]
        [SerializeField] private EnemyHealth enemyHealth;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem fire;
        [Tooltip("Создаётся при гибели машины")]
        [SerializeField] private GameObject deathExplosionPrefab;

        [Header("Пороги прочности (доля от максимума)")]
        [SerializeField, Range(0f, 1f)] private float smokeBelow = 0.6f;
        [SerializeField, Range(0f, 1f)] private float fireBelow = 0.25f;

        [Header("Дым")]
        [SerializeField] private float minSmokeRate = 4f;
        [SerializeField] private float maxSmokeRate = 30f;
        [SerializeField] private Color lightSmokeColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
        [SerializeField] private Color heavySmokeColor = new Color(0.12f, 0.12f, 0.12f, 0.9f);

        [Header("Огонь")]
        [SerializeField] private float fireRate = 25f;

        private float lastFraction = -1f;

        private void Awake()
        {
            if (health == null && enemyHealth == null)
                health = GetComponentInParent<PlayerHealth>();
        }

        private void OnEnable()
        {
            if (health != null)
                health.OnDeath += OnCarDeath;
        }

        private void OnDisable()
        {
            if (health != null)
                health.OnDeath -= OnCarDeath;
        }

        private void Update()
        {
            float fraction;
            if (enemyHealth != null)
                fraction = enemyHealth.HealthFraction;
            else if (health != null && health.MaxHealth > 0)
                fraction = Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth);
            else
                return;

            if (Mathf.Approximately(fraction, lastFraction)) return;
            lastFraction = fraction;

            // 0 у порога дыма, 1 при нулевой прочности
            float damage = fraction < smokeBelow ? Mathf.InverseLerp(smokeBelow, 0f, fraction) : -1f;
            SetRate(smoke, damage >= 0f ? Mathf.Lerp(minSmokeRate, maxSmokeRate, damage) : 0f);
            if (smoke != null && damage >= 0f)
            {
                ParticleSystem.MainModule main = smoke.main;
                main.startColor = Color.Lerp(lightSmokeColor, heavySmokeColor, damage);
            }

            SetRate(fire, fraction < fireBelow && fraction > 0f ? fireRate : 0f);
        }

        private void OnCarDeath()
        {
            SetRate(fire, 0f);
            if (deathExplosionPrefab != null)
                Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
        }

        private static void SetRate(ParticleSystem system, float rate)
        {
            if (system == null) return;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            if (rate > 0f && !system.isPlaying)
                system.Play();
        }
    }
}
