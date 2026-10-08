using System;
using System.Collections;
using RacingProject.Network;
using UnityEngine;
using UnityEngine.Audio;

namespace RacingProject.Management
{
    // Выход из бункера при старте раунда: на экранах «Запуск», воет ревун и мигает красная лампа,
    // штурвал гермодвери проворачивается, кремальеры откидываются, створка открывается в комнату,
    // и из коридора льётся дневной свет. Затем экран гаснет, игроки оказываются в машине, и затемнение снимается.
    // Оси деталей и знаки поворотов задаёт BuildBunker.cs в системе родителя каждой детали
    public class MenuExitSequence : MonoBehaviour
    {
        [Serializable]
        public class Part
        {
            public Transform transform;
            [Tooltip("Ось вращения в системе родителя детали")]
            public Vector3 axis;
            [Tooltip("Угол открытого положения, градусы (знак задаёт направление)")]
            public float angle;

            [NonSerialized] public Quaternion rest;
        }

        [SerializeField] private LanLobby lobby;
        [SerializeField] private AudioMixerGroup output;

        [Header("Гермодверь")]
        [SerializeField] private Part leaf = new Part();
        [SerializeField] private Part wheel = new Part();
        [SerializeField] private Part[] dogs = new Part[0];

        [Header("Свет")]
        [SerializeField] private Light alarm;
        [SerializeField] private float alarmIntensity = 3f;
        [SerializeField] private Light daylight;
        [SerializeField] private float daylightIntensity = 2.5f;

        [Header("Время, с")]
        [SerializeField] private float wheelStart = 0.7f;
        [SerializeField] private float wheelTime = 1.3f;
        [SerializeField] private float dogsStart = 2.05f;
        [SerializeField] private float dogInterval = 0.18f;
        [SerializeField] private float doorStart = 2.7f;
        [SerializeField] private float doorTime = 2.2f;
        [SerializeField] private float fadeStart = 4.4f;
        [SerializeField] private float fadeOut = 0.8f;
        [SerializeField] private float fadeIn = 1.2f;

        private AudioSource alarmSource;
        private AudioSource effects;
        private bool playing;

        public bool Playing => playing;

        private void Awake()
        {
            foreach (Part p in AllParts())
                if (p.transform != null)
                    p.rest = p.transform.localRotation;
            if (alarm != null) alarm.enabled = false;
            if (daylight != null) daylight.enabled = false;

            alarmSource = MakeSource("Alarm", alarm != null ? alarm.transform : transform);
            alarmSource.clip = BunkerSounds.Klaxon;
            alarmSource.loop = true;
            alarmSource.volume = 0.5f;
            effects = MakeSource("DoorSounds", leaf.transform != null ? leaf.transform : transform);
        }

        // Запустить выход; done вызывается, когда экран полностью погас (тогда включаются риги игроков)
        public void Play(Action done)
        {
            if (playing) return;
            playing = true;
            StartCoroutine(Run(done));
        }

        private IEnumerator Run(Action done)
        {
            if (lobby != null) lobby.ShowLaunch("Запуск. Экипаж — на выход!");
            alarmSource.Play();
            if (alarm != null) alarm.enabled = true;

            float t0 = Time.unscaledTime;
            bool ratchet = false, creak = false;
            bool[] clanked = new bool[dogs.Length];
            bool fading = false;
            while (true)
            {
                float t = Time.unscaledTime - t0;

                // Ревун мигает лампой в такт гудкам и замолкает, когда дверь пошла
                if (alarm != null)
                    alarm.intensity = alarmIntensity * (Mathf.Repeat(t, 1f) < 0.6f ? 1f : 0.15f);
                if (t > doorStart + 0.6f && alarmSource.isPlaying)
                {
                    alarmSource.Stop();
                    if (alarm != null) alarm.enabled = false;
                }

                // Штурвал: два оборота, туго в начале
                float w = Mathf.Clamp01((t - wheelStart) / wheelTime);
                Pose(wheel, EaseInOut(w));
                if (w > 0f && !ratchet) { ratchet = true; effects.PlayOneShot(BunkerSounds.Ratchet, 0.8f); }

                // Кремальеры откидываются по очереди, каждая с лязгом
                for (int i = 0; i < dogs.Length; i++)
                {
                    float d = Mathf.Clamp01((t - dogsStart - i * dogInterval) / 0.15f);
                    Pose(dogs[i], d * d);
                    if (d >= 1f && !clanked[i])
                    {
                        clanked[i] = true;
                        effects.PlayOneShot(BunkerSounds.Clank, 0.9f);
                    }
                }

                // Тяжёлая створка трогается медленно и плавно останавливается
                float o = Mathf.Clamp01((t - doorStart) / doorTime);
                Pose(leaf, EaseInOut(o));
                if (o > 0f && !creak) { creak = true; effects.PlayOneShot(BunkerSounds.Creak, 0.9f); }
                if (daylight != null)
                {
                    daylight.enabled = o > 0f;
                    daylight.intensity = daylightIntensity * Mathf.SmoothStep(0f, 1f, o * 1.4f);
                }

                if (t >= fadeStart && !fading)
                {
                    fading = true;
                    ScreenFader.FadeTo(1f, fadeOut);
                }
                if (t >= fadeStart + fadeOut)
                    break;
                yield return null;
            }

            // Риги включаются в полной темноте; меню и бункер прячутся, затемнение снимается уже в машине
            ScreenFader.FadeTo(1f, 0.01f);
            done?.Invoke();
            ScreenFader.FadeTo(0f, fadeIn);
        }

        private static void Pose(Part part, float k)
        {
            if (part.transform == null) return;
            part.transform.localRotation = Quaternion.AngleAxis(part.angle * k, part.axis) * part.rest;
        }

        private static float EaseInOut(float k) => k * k * (3f - 2f * k);

        private Part[] AllParts()
        {
            var all = new Part[dogs.Length + 2];
            all[0] = leaf;
            all[1] = wheel;
            dogs.CopyTo(all, 2);
            return all;
        }

        private AudioSource MakeSource(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = output;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 12f;
            source.dopplerLevel = 0f;
            return source;
        }
    }
}
