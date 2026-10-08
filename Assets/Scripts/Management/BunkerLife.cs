using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RacingProject.Management
{
    // Жизнь бункера меню: гудят вентиляция и лампы, шипит радиостанция, кнопки терминала щёлкают,
    // а на каждое нажатие оператор перекидывает тумблер на панели под этим экраном.
    // Время от времени наверху рвётся снаряд: глухой раскат, лампы качаются и проседают, со свода сыплется пыль.
    // Всё работает только пока показано меню (бункер прячется вместе с ним); звуки идут в группу окружения
    public class BunkerLife : MonoBehaviour
    {
        [System.Serializable]
        public class ToggleRow
        {
            [Tooltip("Холст, кнопки которого щёлкают этим рядом тумблеров")]
            public Canvas screen;
            public Transform[] toggles = new Transform[0];
            [Tooltip("Ось наклона рычага — «вправо» вдоль панели (в системе родителя тумблеров)")]
            public Vector3 axis;
            [Tooltip("«Вверх по скату» панели (в системе родителя тумблеров)")]
            public Vector3 up;
            [Tooltip("Угол между положениями «вверх» и «вниз», градусы")]
            public float throwAngle = 43.6f;
        }

        [SerializeField] private AudioMixerGroup output;

        [Header("Фон")]
        [SerializeField] private Transform vent;
        [SerializeField, Range(0f, 1f)] private float ventVolume = 0.35f;
        [SerializeField] private Transform radio;
        [SerializeField, Range(0f, 1f)] private float radioVolume = 0.12f;
        [Tooltip("Подвесные лампы: гудят и качаются от разрывов (точка подвеса — начало объекта)")]
        [SerializeField] private Transform[] hangingLamps = new Transform[0];
        [SerializeField, Range(0f, 1f)] private float buzzVolume = 0.06f;

        [Header("Терминал")]
        [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.25f;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.5f;
        [SerializeField] private ToggleRow[] toggleRows = new ToggleRow[0];

        [Header("Обстрел")]
        [SerializeField] private Vector2 shellingInterval = new Vector2(25f, 60f);
        [SerializeField, Range(0f, 1f)] private float rumbleVolume = 0.8f;
        [Tooltip("Свет, который проседает при разрыве")]
        [SerializeField] private Light[] dipLights = new Light[0];
        [Tooltip("Пыль со свода: крошка и мягкие облачка выпускаются при разрыве")]
        [SerializeField] private ParticleSystem dust;
        [SerializeField] private int dustCount = 90;
        [SerializeField] private ParticleSystem dustCloud;
        [SerializeField] private int cloudCount = 10;
        [Tooltip("Наибольший размах качания ламп, градусы")]
        [SerializeField] private float swayAngle = 6f;

        private const int VoiceCount = 6;
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private int nextVoice;

        private struct Throw
        {
            public Transform part;
            public Quaternion from, to;
            public float start;
        }
        private readonly List<Throw> throws = new List<Throw>();
        private const float ThrowTime = 0.06f;

        private float nextShell;
        private float shellTime = -100f;
        private float[] baseIntensity;
        private bool[] flickers;
        private Quaternion[] lampRest;
        private Vector2[] swayPhase;

        private void Awake()
        {
            Loop("Vent", vent, BunkerSounds.Vent, ventVolume, 1f, 6f);
            Loop("Radio", radio, BunkerSounds.Radio, radioVolume, 1f, 4f);
            foreach (Transform lamp in hangingLamps)
                Loop("Buzz", lamp, BunkerSounds.LampBuzz, buzzVolume, 1f, 3f);
            for (int i = 0; i < VoiceCount; i++)
                voices.Add(Source("Voice", transform, 1f, 8f));

            baseIntensity = new float[dipLights.Length];
            flickers = new bool[dipLights.Length];
            for (int i = 0; i < dipLights.Length; i++)
            {
                baseIntensity[i] = dipLights[i].intensity;
                flickers[i] = dipLights[i].GetComponent<FlickerLight>() != null;
            }
            lampRest = new Quaternion[hangingLamps.Length];
            swayPhase = new Vector2[hangingLamps.Length];
            for (int i = 0; i < hangingLamps.Length; i++)
                lampRest[i] = hangingLamps[i].localRotation;
        }

        private void OnEnable()
        {
            TerminalButtonFx.Hovered += OnHovered;
            TerminalButtonFx.Pressed += OnPressed;
            // Первый разрыв — не сразу, чтобы игрок успел осмотреться
            nextShell = Time.unscaledTime + Random.Range(8f, 15f);
        }

        private void OnDisable()
        {
            TerminalButtonFx.Hovered -= OnHovered;
            TerminalButtonFx.Pressed -= OnPressed;
        }

        private void OnHovered(TerminalButtonFx button)
        {
            Play(BunkerSounds.Hover, button.transform.position, hoverVolume, Random.Range(0.95f, 1.05f));
        }

        private void OnPressed(TerminalButtonFx button)
        {
            Play(BunkerSounds.Click, button.transform.position, clickVolume, Random.Range(0.95f, 1.05f));
            foreach (ToggleRow row in toggleRows)
                if (row.screen == button.Screen && row.toggles.Length > 0)
                {
                    FlipToggle(row, row.toggles[Random.Range(0, row.toggles.Length)]);
                    break;
                }
        }

        // Рычаг уходит в противоположное положение: из двух поворотов выбирается тот, что переводит его через середину
        private void FlipToggle(ToggleRow row, Transform part)
        {
            if (throws.Exists(t => t.part == part)) return;
            var filter = part.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            // Рычаг смотрит туда же, куда центр его сетки от оси
            Vector3 tip = filter.sharedMesh.bounds.center;
            bool isUp = Vector3.Dot(part.localRotation * tip, row.up) > 0f;
            Quaternion a = Quaternion.AngleAxis(row.throwAngle, row.axis) * part.localRotation;
            Quaternion b = Quaternion.AngleAxis(-row.throwAngle, row.axis) * part.localRotation;
            bool aUp = Vector3.Dot(a * tip, row.up) > 0f;
            throws.Add(new Throw { part = part, from = part.localRotation, to = aUp != isUp ? a : b, start = Time.unscaledTime });
            Play(BunkerSounds.Toggle, part.position, clickVolume, Random.Range(0.9f, 1.1f));
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = throws.Count - 1; i >= 0; i--)
            {
                Throw t = throws[i];
                float k = Mathf.Clamp01((now - t.start) / ThrowTime);
                t.part.localRotation = Quaternion.Slerp(t.from, t.to, k * k);
                if (k >= 1f) throws.RemoveAt(i);
            }

            if (now >= nextShell)
                Shell();
            SwayLamps(now);
        }

        // Просадка света — после FlickerLight, который сам задаёт яркость в Update
        private void LateUpdate()
        {
            float since = Time.unscaledTime - shellTime;
            float dip = 1f;
            if (since < 1.2f)
            {
                // Резкий провал, пара вспышек нити и возврат
                float flicker = Mathf.PerlinNoise(since * 25f, 3.1f);
                dip = Mathf.Lerp(0.25f + 0.5f * flicker, 1f, Mathf.SmoothStep(0f, 1f, since / 1.2f));
            }
            for (int i = 0; i < dipLights.Length; i++)
            {
                if (dipLights[i] == null) continue;
                if (flickers[i]) dipLights[i].intensity *= dip;
                else dipLights[i].intensity = baseIntensity[i] * dip;
            }
        }

        private void Shell()
        {
            shellTime = Time.unscaledTime;
            nextShell = shellTime + Random.Range(shellingInterval.x, shellingInterval.y);
            // Раскат приходит сверху и чуть сбоку; звучит почти без направления, как сквозь грунт
            Vector3 from = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * new Vector3(0f, 3f, 4f);
            AudioSource voice = Play(BunkerSounds.Rumble, from, rumbleVolume, Random.Range(0.8f, 1.1f));
            voice.spatialBlend = 0.3f;
            for (int i = 0; i < swayPhase.Length; i++)
                swayPhase[i] = Random.insideUnitCircle.normalized * swayAngle * Random.Range(0.6f, 1f);
            if (dust != null) dust.Emit(dustCount);
            if (dustCloud != null) dustCloud.Emit(cloudCount);
        }

        // Затухающее качание подвесных ламп после разрыва
        private void SwayLamps(float now)
        {
            float since = now - shellTime;
            for (int i = 0; i < hangingLamps.Length; i++)
            {
                if (since > 12f)
                {
                    hangingLamps[i].localRotation = lampRest[i];
                    continue;
                }
                float decay = Mathf.Exp(-since * 0.45f) * Mathf.Clamp01(since / 0.15f);
                float swing = Mathf.Sin(since * 2f * Mathf.PI * 0.55f) * decay;
                float side = Mathf.Sin(since * 2f * Mathf.PI * 0.55f + 1.3f) * decay * 0.4f;
                hangingLamps[i].localRotation = lampRest[i] * Quaternion.Euler(swayPhase[i].x * swing, 0f, swayPhase[i].y * swing + swayPhase[i].x * side);
            }
        }

        private void Loop(string name, Transform at, AudioClip clip, float volume, float blend, float range)
        {
            if (at == null) return;
            AudioSource source = Source(name, at, blend, range);
            source.clip = clip;
            source.loop = true;
            source.volume = volume;
            // Разные точки старта, чтобы одинаковые циклы не звучали в унисон
            source.timeSamples = Random.Range(0, clip.samples);
            source.Play();
        }

        private AudioSource Source(string name, Transform parent, float blend, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = output;
            source.spatialBlend = blend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = range;
            source.dopplerLevel = 0f;
            return source;
        }

        private AudioSource Play(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            AudioSource voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Count;
            voice.transform.position = position;
            voice.spatialBlend = 1f;
            voice.pitch = pitch;
            voice.Stop();
            voice.clip = clip;
            voice.volume = volume;
            voice.Play();
            return voice;
        }
    }
}
