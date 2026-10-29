using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Car;
using RacingProject.Network;

namespace RacingProject.Audio
{
    // Мотор машины игроков из четырёх зацикленных слоёв, записанных на своих оборотах (холостые, низкие, средние,
    // высокие): соседние слои плавно сменяют друг друга, высота тона следует оборотам. Под нагрузкой мотор громче
    // и ярче, на сбросе газа глуше. Переключение передачи — стук и короткий провал, сброс газа на высоких
    // оборотах — хлопки в выхлоп. Обороты, газ и передачу считает водитель и передаёт напарнику
    public class CarEngineAudio : RoleSyncedBehaviour
    {
        [SerializeField] private CarControllerSample car;
        [Tooltip("Слои по возрастанию оборотов")]
        [SerializeField] private AudioClip[] layers = new AudioClip[0];
        [Tooltip("Обороты, на которых записан каждый слой (Tools/Audio/generate_vehicle_sounds.py)")]
        [SerializeField] private float[] layerRpm = { 820f, 1900f, 3400f, 5400f };
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float volume = 0.75f;
        [Tooltip("0 — звук в голове (как у водителя в салоне), 1 — от машины")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0.35f;

        [Header("Обороты")]
        [SerializeField] private float idleRpm = 820f;
        [SerializeField] private float redlineRpm = 5600f;
        [Tooltip("Скорость, с которой стрелка оборотов догоняет нужное значение, доля в секунду")]
        [SerializeField] private float rpmResponse = 6f;

        [Header("Нагрузка")]
        [Tooltip("Срез высоких частот на сбросе газа и под полной нагрузкой, Гц")]
        [SerializeField] private float offLoadCutoff = 1400f;
        [SerializeField] private float onLoadCutoff = 16000f;
        [SerializeField, Range(0f, 1f)] private float offLoadVolume = 0.55f;

        [Header("Хлопки и переключения")]
        [Tooltip("Точка выхлопа (хлопки звучат оттуда)")]
        [SerializeField] private Transform exhaust;
        [SerializeField, Range(0f, 1f)] private float backfireChance = 0.6f;
        [Tooltip("Хлопки — только со сброса газа выше этой доли оборотов")]
        [SerializeField, Range(0f, 1f)] private float backfireRpm = 0.55f;

        private AudioSource[] sources;
        private AudioLowPassFilter[] filters;
        private float rpm01;
        private float throttle;
        private int gear;
        private bool misfire;
        private float smoothRpm;
        private float smoothLoad;
        private float lastThrottle;
        private int lastGear = -1;
        private float shiftDip;
        private float misfireDip;
        private float nextPop;

        private void Start()
        {
            sources = new AudioSource[layers.Length];
            filters = new AudioLowPassFilter[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var go = new GameObject("EngineLayer " + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.clip = layers[i];
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;
                source.spatialBlend = spatialBlend;
                source.minDistance = 6f;
                source.maxDistance = 140f;
                source.dopplerLevel = 0f;
                source.outputAudioMixerGroup = output;
                // Разные слои начинаются с разных мест, чтобы их узор не совпадал
                source.time = layers[i] != null ? Random.Range(0f, layers[i].length) : 0f;
                source.Play();
                filters[i] = go.AddComponent<AudioLowPassFilter>();
                sources[i] = source;
            }
            smoothRpm = idleRpm;
        }

        private void Update()
        {
            if (sources == null) return;
            // Водитель (и одиночная игра) считает сам, у напарника значения приходят по сети
            if (IsLocallyControlled && car != null)
            {
                rpm01 = car.EngineRpm01;
                throttle = car.Throttle;
                gear = car.CurrentGear;
                misfire = car.Misfiring;
            }

            float dt = Time.deltaTime;
            float targetRpm = Mathf.Lerp(idleRpm, redlineRpm, Mathf.Clamp01(rpm01));
            smoothRpm = Mathf.Lerp(smoothRpm, targetRpm, 1f - Mathf.Exp(-rpmResponse * dt));
            smoothLoad = Mathf.Lerp(smoothLoad, throttle, 1f - Mathf.Exp(-8f * dt));

            UpdateEvents(dt);

            float load = Mathf.Clamp01(smoothLoad);
            float master = volume * Mathf.Lerp(offLoadVolume, 1f, load) * (1f - shiftDip * 0.6f) * (1f - misfireDip * 0.7f);
            float cutoff = Mathf.Lerp(offLoadCutoff, onLoadCutoff, load * load) * (1f - shiftDip * 0.5f);
            // Перебои разбитого мотора — тон проседает
            float pitchDrop = 1f - misfireDip * 0.15f;
            for (int i = 0; i < sources.Length; i++)
            {
                float weight = LayerWeight(i, smoothRpm);
                sources[i].volume = master * weight;
                sources[i].pitch = Mathf.Clamp(smoothRpm / layerRpm[Mathf.Min(i, layerRpm.Length - 1)], 0.5f, 2.2f) * pitchDrop;
                filters[i].cutoffFrequency = cutoff;
            }
        }

        // Вес слоя: треугольник по логарифму оборотов между соседними слоями, сумма весов — около единицы по мощности
        private float LayerWeight(int i, float rpm)
        {
            int count = Mathf.Min(sources.Length, layerRpm.Length);
            if (i >= count) return 0f;
            float x = Mathf.Log(rpm);
            float center = Mathf.Log(layerRpm[i]);
            float w;
            if (x <= center)
                w = i == 0 ? 1f : Mathf.InverseLerp(Mathf.Log(layerRpm[i - 1]), center, x);
            else
                w = i == count - 1 ? 1f : 1f - Mathf.InverseLerp(center, Mathf.Log(layerRpm[i + 1]), x);
            // Равная мощность при смешивании
            return Mathf.Sin(w * Mathf.PI * 0.5f);
        }

        private void UpdateEvents(float dt)
        {
            shiftDip = Mathf.MoveTowards(shiftDip, 0f, dt * 5f);
            misfireDip = Mathf.MoveTowards(misfireDip, misfire ? 1f : 0f, dt * (misfire ? 30f : 8f));

            if (lastGear >= 0 && gear != lastGear && gear != 0)
            {
                shiftDip = 1f;
                SoundLibrary.Play(SoundLibrary.Sfx.GearShift, transform.position, 0.8f);
            }
            lastGear = gear;

            // Сброс газа на высоких оборотах — хлопок, иногда очередь хлопков
            bool lifted = lastThrottle > 0.6f && throttle < 0.15f;
            if (lifted && rpm01 > backfireRpm && Random.value < backfireChance)
                nextPop = Time.time + Random.Range(0.05f, 0.18f);
            if (nextPop > 0f && Time.time >= nextPop)
            {
                nextPop = throttle < 0.15f && rpm01 > backfireRpm * 0.8f && Random.value < 0.35f ? Time.time + Random.Range(0.12f, 0.3f) : 0f;
                Vector3 at = exhaust != null ? exhaust.position : transform.position;
                SoundLibrary.Play(SoundLibrary.Sfx.Backfire, at, Random.Range(0.6f, 1f));
            }
            lastThrottle = throttle;
        }

        public override void Serialize(SyncStream stream)
        {
            byte rpmByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(rpm01 / 1.05f) * 255f);
            byte throttleByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(throttle) * 255f);
            byte gearByte = (byte)Mathf.Clamp(gear, 0, 255);
            bool misfireFlag = misfire;
            stream.Serialize(ref rpmByte);
            stream.Serialize(ref throttleByte);
            stream.Serialize(ref gearByte);
            stream.Serialize(ref misfireFlag);
            if (!stream.IsWriting)
            {
                rpm01 = rpmByte / 255f * 1.05f;
                throttle = throttleByte / 255f;
                gear = gearByte;
                misfire = misfireFlag;
            }
        }
    }
}
