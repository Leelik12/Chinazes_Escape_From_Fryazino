using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Car;
using RacingProject.Enemy;
using RacingProject.Management;
using RacingProject.Network;

namespace RacingProject.Audio
{
    // Динамическая музыка боя: пять слоёв одной длины (Tools/Audio/generate_music.py) играют синхронно,
    // а их громкость следует за накалом боя у этого игрока: враги рядом с машиной, недавний урон, артналёт.
    // Затишье — пэд и колокольчики, дальше добавляются пульс, барабаны и напряжение; пока жив босс — его слой.
    // На передышке накал спадает, на зачистку волны звучит развязка. Клавиша «Магнитола» (GameKeys) переключает
    // на треки магнитолы машины и обратно, выбор хранится в PlayerPrefs. Слои выведены в группу музыки,
    // поэтому громкость музыки в настройках действует и на них
    public class MusicDirector : MonoBehaviour
    {
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PlayerHealth carHealth;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField] private AudioClip calm;
        [SerializeField] private AudioClip pulse;
        [SerializeField] private AudioClip drums;
        [SerializeField] private AudioClip tension;
        [SerializeField] private AudioClip boss;
        [SerializeField] private AudioClip clearStinger;
        [Tooltip("Общая громкость музыки боя")]
        [SerializeField] private float volume = 0.8f;

        [Header("Накал")]
        [Tooltip("Враги ближе этого считаются целиком, дальше — слабее до нуля на fightRange, м")]
        [SerializeField] private float closeRange = 50f;
        [SerializeField] private float fightRange = 240f;
        [Tooltip("Столько врагов рядом — полный накал от врагов")]
        [SerializeField] private float enemiesForFull = 3f;
        [Tooltip("Прибавка накала за каждые 100 урона, гаснет за damageFade с")]
        [SerializeField] private float damageHeat = 0.25f;
        [SerializeField] private float damageFade = 8f;
        [SerializeField] private float riseSpeed = 0.5f;
        [SerializeField] private float fallSpeed = 0.07f;

        private const string MagnitolaPref = "MagnitolaOn";

        private AudioSource[] layers;
        private float[] levels;
        private AudioSource stinger;
        private bool started;
        private bool magnitolaOn;
        private float master;
        private float intensity;
        private float heat;
        private float nextScan;
        private float bossLevel;
        private readonly List<EnemyHealth> enemies = new List<EnemyHealth>();

        // Накал 0–1 у этого игрока (для отладки)
        public float Intensity => intensity;

        private void Awake()
        {
            var clips = new[] { calm, pulse, drums, tension, boss };
            layers = new AudioSource[clips.Length];
            levels = new float[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                layers[i] = MakeSource("MusicLayer_" + i);
                layers[i].clip = clips[i];
                layers[i].loop = true;
            }
            stinger = MakeSource("MusicStinger");
            magnitolaOn = PlayerPrefs.GetInt(MagnitolaPref, 0) == 1;
        }

        private void OnEnable()
        {
            if (enemyManager != null) enemyManager.WaveCleared += OnWaveCleared;
            if (carHealth != null) carHealth.OnDamageTaken += OnDamage;
        }

        private void OnDisable()
        {
            if (enemyManager != null) enemyManager.WaveCleared -= OnWaveCleared;
            if (carHealth != null) carHealth.OnDamageTaken -= OnDamage;
        }

        private void OnDamage(int damage)
        {
            heat = Mathf.Min(1f, heat + damageHeat * damage / 100f);
        }

        private void OnWaveCleared(int wave, float seconds, int bonus)
        {
            if (clearStinger != null && !magnitolaOn && started)
                stinger.PlayOneShot(clearStinger, volume);
        }

        private void Update()
        {
            bool inRound = LocalPlayerRole.Current != PlayerRole.None;
            if (inRound && !started) StartLayers();
            if (!started)
            {
                ApplyMagnitola(false);
                return;
            }

            if (inRound && GameKeys.WasPressedThisFrame(GameAction.Radio))
            {
                magnitolaOn = !magnitolaOn;
                PlayerPrefs.SetInt(MagnitolaPref, magnitolaOn ? 1 : 0);
            }
            ApplyMagnitola(inRound);

            bool dead = carHealth != null && carHealth.IsDead;
            float target = inRound && !dead ? TargetIntensity() : 0f;
            intensity = Mathf.MoveTowards(intensity, target, (target > intensity ? riseSpeed : fallSpeed) * Time.deltaTime);
            heat = Mathf.MoveTowards(heat, 0f, Time.deltaTime / damageFade);

            bool bossAlive = enemyManager != null && enemyManager.BossHealth01 >= 0f;
            bossLevel = Mathf.MoveTowards(bossLevel, bossAlive ? 1f : 0f, Time.deltaTime / 3f);
            float wantMaster = inRound && !magnitolaOn && !dead ? 1f : 0f;
            master = Mathf.MoveTowards(master, wantMaster, Time.deltaTime / (dead ? 3f : 1.5f));

            float i = intensity;
            levels[0] = Mathf.Lerp(0.9f, 0.35f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.7f, i)));
            levels[1] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.3f, i));
            levels[2] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.6f, i));
            levels[3] = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.85f, i));
            levels[4] = bossLevel;
            for (int k = 0; k < layers.Length; k++)
                layers[k].volume = levels[k] * master * volume;
        }

        // Накал: враги рядом с машиной, урон, налёт; на передышке — ноль; босс держит накал высоким
        private float TargetIntensity()
        {
            if (enemyManager == null || carHealth == null || enemyManager.BreakActive) return 0f;
            if (Time.time >= nextScan)
            {
                nextScan = Time.time + 1f;
                enemies.Clear();
                enemies.AddRange(FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None));
            }
            Vector3 car = carHealth.transform.position;
            float near = 0f;
            foreach (EnemyHealth enemy in enemies)
            {
                if (enemy == null || enemy.HealthFraction <= 0f) continue;
                float d = Vector3.Distance(enemy.transform.position, car);
                near += 1f - Mathf.Clamp01((d - closeRange) / (fightRange - closeRange));
            }
            float target = 0.7f * Mathf.Clamp01(near / enemiesForFull) + heat;
            if (ArtilleryStrikes.Instance != null && ArtilleryStrikes.Instance.Active) target += 0.25f;
            target += Mathf.Min(0.15f, enemyManager.Wave * 0.015f);
            if (enemyManager.BossHealth01 >= 0f) target = Mathf.Max(target, 0.8f);
            return Mathf.Clamp01(target);
        }

        // Все слои с одного и того же отсчёта звуковой карты — такты совпадают
        private void StartLayers()
        {
            started = true;
            double at = AudioSettings.dspTime + 0.2;
            foreach (AudioSource layer in layers)
            {
                layer.volume = 0f;
                if (layer.clip != null) layer.PlayScheduled(at);
            }
        }

        // Магнитола машины играет всегда: в меню слышна, как и раньше, а в раунде — только когда выбрана
        // вместо музыки боя
        private void ApplyMagnitola(bool inRound)
        {
            foreach (Magnitola magnitola in FindMagnitolas())
                if (magnitola.audioSource != null)
                    magnitola.audioSource.mute = inRound && !magnitolaOn;
        }

        private Magnitola[] magnitolas;
        private float nextMagnitolaScan;

        private Magnitola[] FindMagnitolas()
        {
            if (magnitolas == null || Time.time >= nextMagnitolaScan)
            {
                nextMagnitolaScan = Time.time + 2f;
                magnitolas = FindObjectsByType<Magnitola>(FindObjectsSortMode.None);
            }
            return magnitolas;
        }

        private AudioSource MakeSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = output;
            source.volume = 0f;
            source.ignoreListenerPause = false;
            return source;
        }
    }
}
