using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RacingProject.Car
{
    public class Magnitola : MonoBehaviour
    {
        public static bool MusicAlreadyStarted = false;   //  добавлено

        public AudioSource audioSource;
        public AudioClip[] fireTracks;
        public bool shuffleMode = true;

        private List<AudioClip> playlist;
        private int currentTrackIndex = 0;
        // Эта магнитола запустила музыку и отвечает за сброс флага
        private bool ownsMusic;

        void Start()
        {
            // Если музыка уже играет или треков нет — блокируем повторный запуск
            if (MusicAlreadyStarted || fireTracks == null || fireTracks.Length == 0)
            {
                audioSource.enabled = false;
                return;
            }

            // Если здесь — значит запускаем музыку первый раз
            MusicAlreadyStarted = true;
            ownsMusic = true;
            audioSource.enabled = true;

            playlist = fireTracks.ToList();
            if (shuffleMode)
                ShufflePlaylist();

            PlayNextTrack();
        }

        // Статический флаг переживает перезагрузку сцены: без сброса после рестарта музыки не было бы
        void OnDestroy()
        {
            if (ownsMusic)
                MusicAlreadyStarted = false;
        }

        void Update()
        {
            if (!audioSource.enabled) return;

            if (!audioSource.isPlaying)
                PlayNextTrack();
        }

        void ShufflePlaylist()
        {
            for (int i = 0; i < playlist.Count; i++)
            {
                int r = Random.Range(i, playlist.Count);
                (playlist[i], playlist[r]) = (playlist[r], playlist[i]);
            }
        }

        void PlayNextTrack()
        {
            if (!audioSource.enabled) return;

            audioSource.Stop();
            audioSource.clip = playlist[currentTrackIndex];
            audioSource.Play();

            currentTrackIndex++;
            if (currentTrackIndex >= playlist.Count)
            {
                currentTrackIndex = 0;
                if (shuffleMode) ShufflePlaylist();
            }
        }
    }
}
