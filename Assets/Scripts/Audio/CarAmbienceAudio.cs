using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Enemy;
using RacingProject.Network;

namespace RacingProject.Audio
{
    // Звуки состояния машины и боя: свист ветра по скорости (стрелку в люке громче, водителю в салоне тише),
    // сердцебиение при малом здоровье и сирена, когда появляется босс. Всё считается у каждого игрока по своей
    // копии машины, по сети ничего не передаётся
    public class CarAmbienceAudio : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private AudioMixerGroup output;

        [Header("Ветер")]
        [SerializeField] private AudioClip windClip;
        [Tooltip("Скорость, на которой ветер звучит в полную силу, км/ч")]
        [SerializeField] private float windFullSpeed = 150f;
        [Tooltip("Медленнее этого ветра не слышно, км/ч")]
        [SerializeField] private float windStartSpeed = 25f;
        [SerializeField, Range(0f, 1f)] private float windVolume = 0.5f;
        [Tooltip("Громкость ветра у стрелка в люке и у водителя в салоне (одиночная игра — 1)")]
        [SerializeField] private float gunnerWind = 1.5f;
        [SerializeField] private float driverWind = 0.55f;

        [Header("Сердцебиение")]
        [SerializeField] private AudioClip heartbeatClip;
        [Tooltip("Ниже этой доли здоровья слышно сердце")]
        [SerializeField, Range(0f, 1f)] private float heartbeatHealth = 0.3f;
        [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.8f;

        private AudioSource wind;
        private AudioSource heartbeat;
        private Vector3 lastPosition;
        private float speed;
        private EnemyManager enemies;
        private float nextEnemySearch;
        private bool bossShown;

        private void Awake()
        {
            wind = CreateLoop("Wind", windClip);
            heartbeat = CreateLoop("Heartbeat", heartbeatClip);
            lastPosition = transform.position;
        }

        private AudioSource CreateLoop(string name, AudioClip clip)
        {
            if (clip == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = output;
            return source;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            // Скорость по перемещению: одинаково у хоста и у напарника, у которого машина кинематическая
            if (dt > 0f)
            {
                float measured = (transform.position - lastPosition).magnitude / dt * 3.6f;
                speed = Mathf.Lerp(speed, measured, 1f - Mathf.Exp(-4f * dt));
            }
            lastPosition = transform.position;

            if (wind != null)
            {
                float k = Mathf.Clamp01(Mathf.InverseLerp(windStartSpeed, windFullSpeed, speed));
                float role = LocalPlayerRole.Current == PlayerRole.Gunner ? gunnerWind
                    : LocalPlayerRole.Current == PlayerRole.Driver ? driverWind : 1f;
                SetLoop(wind, windVolume * role * k * k, Mathf.Lerp(0.85f, 1.3f, k));
            }

            if (heartbeat != null && health != null)
            {
                float hp = health.MaxHealth > 0 ? (float)health.CurrentHealth / health.MaxHealth : 1f;
                float danger = health.IsDead || LocalPlayerRole.Current == PlayerRole.None ? 0f
                    : Mathf.Clamp01(1f - hp / heartbeatHealth);
                // Чем меньше здоровья, тем громче и чаще
                SetLoop(heartbeat, heartbeatVolume * Mathf.Sqrt(danger), Mathf.Lerp(0.95f, 1.25f, danger));
            }

            UpdateBoss();
        }

        // Сирена при появлении босса: прочность босса из «нет» (меньше нуля) стала числом
        private void UpdateBoss()
        {
            if (enemies == null)
            {
                if (Time.unscaledTime < nextEnemySearch) return;
                nextEnemySearch = Time.unscaledTime + 1f;
                enemies = FindFirstObjectByType<EnemyManager>();
                if (enemies == null || !enemies.IsSpawned) { enemies = null; return; }
                bossShown = enemies.BossHealth01 >= 0f;
            }
            if (!enemies.IsSpawned) { enemies = null; return; }
            bool boss = enemies.BossHealth01 >= 0f;
            if (boss && !bossShown)
                SoundLibrary.Play2D(SoundLibrary.Sfx.BossHorn, 0.9f);
            bossShown = boss;
        }

        private static void SetLoop(AudioSource source, float volume, float pitch)
        {
            source.volume = Mathf.MoveTowards(source.volume, volume, Time.deltaTime * 2f);
            source.pitch = pitch;
            if (source.volume > 0.001f && !source.isPlaying) source.Play();
            else if (source.volume <= 0.001f && source.isPlaying) source.Stop();
        }
    }
}
