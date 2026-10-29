using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace RacingProject.Audio
{
    // Разовые звуки игры (удары, стекло, обломки, пули, гильзы, сигналы): клипы по видам и общий пул источников.
    // Каждый игрок проигрывает их сам: то, что видят оба (удары, взрывы, выстрелы), приходит в RPC владельцев событий.
    // У вида свой предел одновременных голосов, лишний звук забирает самый старый голос того же вида
    public class SoundLibrary : MonoBehaviour
    {
        public enum Sfx
        {
            ImpactHeavy,
            ImpactLight,
            Glass,
            DebrisClatter,
            Backfire,
            GearShift,
            BulletWhizz,
            HitMetal,
            HitConcrete,
            HitDirt,
            Casing,
            GunOverheat,
            GunCooled,
            BossHorn,
            DeafRing,
            Thunder,
            Splash,
            TireBurst,
        }

        [Serializable]
        public class Entry
        {
            public Sfx kind;
            public AudioClip[] clips = new AudioClip[0];
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("До этого расстояния громкость полная, м")]
            public float minDistance = 4f;
            [Tooltip("Дальше этого звук не слышен, м")]
            public float maxDistance = 120f;
            [Tooltip("Случайный разброс высоты тона, доля")]
            public float pitchJitter = 0.06f;
            [Tooltip("Сколько таких звуков может звучать одновременно")]
            public int maxVoices = 4;
            [Tooltip("Чаще этого один и тот же вид не запускается, с")]
            public float minInterval = 0.02f;
            public AudioMixerGroup output;
        }

        [SerializeField] private Entry[] entries = new Entry[0];
        [SerializeField] private int poolSize = 32;

        private static SoundLibrary instance;

        private class Voice
        {
            public AudioSource source;
            public Sfx kind;
            public float startTime;
        }

        private readonly List<Voice> voices = new List<Voice>();
        private readonly Dictionary<Sfx, Entry> byKind = new Dictionary<Sfx, Entry>();
        private readonly Dictionary<Sfx, float> lastPlay = new Dictionary<Sfx, float>();
        private readonly Dictionary<Sfx, int> lastClip = new Dictionary<Sfx, int>();

        public static bool Ready => instance != null;

        private void Awake()
        {
            instance = this;
            foreach (Entry entry in entries)
                byKind[entry.kind] = entry;
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject("Sfx " + i);
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.dopplerLevel = 0f;
                voices.Add(new Voice { source = source, startTime = float.NegativeInfinity });
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // Звук в точке мира (объёмный)
        public static void Play(Sfx kind, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (instance != null) instance.PlayInternal(kind, position, volume, pitch, true);
        }

        // Звук «в голове» (двумерный): сигналы, звон в ушах
        public static void Play2D(Sfx kind, float volume = 1f, float pitch = 1f)
        {
            if (instance != null) instance.PlayInternal(kind, Vector3.zero, volume, pitch, false);
        }

        public static float Duration(Sfx kind)
        {
            if (instance == null || !instance.byKind.TryGetValue(kind, out Entry entry) || entry.clips.Length == 0 || entry.clips[0] == null)
                return 0f;
            return entry.clips[0].length;
        }

        private void PlayInternal(Sfx kind, Vector3 position, float volume, float pitch, bool spatial)
        {
            if (!byKind.TryGetValue(kind, out Entry entry) || entry.clips.Length == 0 || volume <= 0.001f) return;
            float now = Time.unscaledTime;
            if (lastPlay.TryGetValue(kind, out float last) && now - last < entry.minInterval) return;

            // Слишком далеко от слушателя — не тратим голос
            if (spatial)
            {
                AudioListener listener = ActiveListener.Current;
                if (listener != null && (listener.transform.position - position).sqrMagnitude > entry.maxDistance * entry.maxDistance)
                    return;
            }

            Voice voice = TakeVoice(kind, entry.maxVoices);
            if (voice == null) return;
            lastPlay[kind] = now;

            // Без повтора одного и того же варианта подряд
            int index = UnityEngine.Random.Range(0, entry.clips.Length);
            if (entry.clips.Length > 1 && lastClip.TryGetValue(kind, out int previous) && index == previous)
                index = (index + 1) % entry.clips.Length;
            lastClip[kind] = index;

            AudioSource source = voice.source;
            source.Stop();
            source.clip = entry.clips[index];
            source.outputAudioMixerGroup = entry.output;
            source.volume = entry.volume * Mathf.Clamp01(volume);
            source.pitch = pitch * (1f + UnityEngine.Random.Range(-entry.pitchJitter, entry.pitchJitter));
            source.spatialBlend = spatial ? 1f : 0f;
            // Звуки «в голове» не глушатся фильтром слушателя (звон в ушах звучит поверх оглушения)
            source.bypassListenerEffects = !spatial;
            source.minDistance = entry.minDistance;
            source.maxDistance = entry.maxDistance;
            source.transform.position = position;
            source.Play();
            voice.kind = kind;
            voice.startTime = now;
        }

        // Свободный голос, иначе самый старый голос того же вида (если их уже предел) или вообще самый старый
        private Voice TakeVoice(Sfx kind, int maxVoices)
        {
            int sameKind = 0;
            Voice oldestSame = null, oldestAny = null, free = null;
            foreach (Voice voice in voices)
            {
                bool busy = voice.source.isPlaying;
                if (!busy)
                {
                    if (free == null) free = voice;
                    continue;
                }
                if (voice.kind == kind)
                {
                    sameKind++;
                    if (oldestSame == null || voice.startTime < oldestSame.startTime) oldestSame = voice;
                }
                if (oldestAny == null || voice.startTime < oldestAny.startTime) oldestAny = voice;
            }
            if (sameKind >= Mathf.Max(1, maxVoices)) return oldestSame;
            return free ?? oldestAny;
        }
    }
}
