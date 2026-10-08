using System;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace RacingProject.Ambience
{
    // Далёкий фон войны: очереди, взрывы, сирена звучат время от времени со случайных сторон вокруг слушателя.
    // Звуки локальные: у каждого игрока свой фон, по сети не передаётся
    public class AmbientSoundscape : MonoBehaviour
    {
        [Serializable]
        private class DistantSound
        {
            public string name;
            public AudioClip[] clips = new AudioClip[0];
            [Tooltip("Пауза между звуками этого вида, с")]
            public float minInterval = 10f;
            public float maxInterval = 30f;
            [Tooltip("Расстояние от слушателя, м")]
            public float minDistance = 150f;
            public float maxDistance = 450f;
            [Range(0f, 1f)] public float minVolume = 0.4f;
            [Range(0f, 1f)] public float maxVolume = 0.8f;
        }

        [SerializeField] private DistantSound[] sounds = new DistantSound[0];
        [Tooltip("Сколько далёких звуков может звучать одновременно")]
        [SerializeField] private int voices = 3;
        [Tooltip("Громкость падает линейно до нуля на этом расстоянии: далёкие звуки должны оставаться слышны")]
        [SerializeField] private float rolloffDistance = 1200f;
        [SerializeField] private AudioMixerGroup output;

        private AudioSource[] pool;
        private float[] nextTime;
        private AudioListener listener;
        private float nextListenerSearch;

        private void Awake()
        {
            pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var go = new GameObject("DistantVoice" + i);
                go.transform.SetParent(transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.85f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1f;
                source.maxDistance = rolloffDistance;
                source.dopplerLevel = 0f;
                source.spread = 60f;
                source.outputAudioMixerGroup = output;
                pool[i] = source;
            }

            // Первые звуки — не сразу и не все вместе
            nextTime = new float[sounds.Length];
            for (int i = 0; i < sounds.Length; i++)
                nextTime[i] = Time.time + Random.Range(sounds[i].minInterval * 0.3f, sounds[i].maxInterval);
        }

        private void Update()
        {
            // Слушатель меняется с режимом (VR-риг или камера ПК), ищем его заново, если прежний пропал
            if ((listener == null || !listener.isActiveAndEnabled) && Time.time >= nextListenerSearch)
            {
                nextListenerSearch = Time.time + 1f;
                listener = FindFirstObjectByType<AudioListener>();
            }
            if (listener == null) return;

            for (int i = 0; i < sounds.Length; i++)
            {
                if (Time.time < nextTime[i]) continue;
                DistantSound sound = sounds[i];
                nextTime[i] = Time.time + Random.Range(sound.minInterval, sound.maxInterval);
                Play(sound, listener.transform.position);
            }
        }

        private void Play(DistantSound sound, Vector3 around)
        {
            if (sound.clips.Length == 0) return;
            AudioSource source = Array.Find(pool, s => !s.isPlaying);
            if (source == null) return;

            Vector2 direction = Random.insideUnitCircle.normalized;
            float distance = Random.Range(sound.minDistance, sound.maxDistance);
            source.transform.position = around + new Vector3(direction.x, 0f, direction.y) * distance + Vector3.up * Random.Range(5f, 30f);
            source.clip = sound.clips[Random.Range(0, sound.clips.Length)];
            source.volume = Random.Range(sound.minVolume, sound.maxVolume);
            source.pitch = Random.Range(0.92f, 1.05f);
            source.Play();
        }
    }
}
