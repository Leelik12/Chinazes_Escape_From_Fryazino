using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Magnitola : MonoBehaviour
{
    [Header("Настройки магнитолы")]
    public AudioSource audioSource;
    public AudioClip[] fireTracks; // Закинь сюда все треки через инспектор
    public bool shuffleMode = true; // Хардкорный рандом или по порядку

    private List<AudioClip> playlist;
    private int currentTrackIndex = 0;

    void Start()
    {
        // Инициализируем плейлист
        playlist = fireTracks.ToList();

        // Запускаем крутой микс
        if (shuffleMode)
            ShufflePlaylist();

        PlayNextTrack();
    }

    void Update()
    {
        // Если трек закончился - врубаем следующий
        if (!audioSource.isPlaying && playlist.Count > 0)
        {
            PlayNextTrack();
        }
    }

    void ShufflePlaylist()
    {
        // Твоя любимая функция - рандом по-пацански
        for (int i = 0; i < playlist.Count; i++)
        {
            AudioClip temp = playlist[i];
            int randomIndex = Random.Range(i, playlist.Count);
            playlist[i] = playlist[randomIndex];
            playlist[randomIndex] = temp;
        }
    }

    void PlayNextTrack()
    {
        if (playlist.Count == 0) return;

        // Врубаем текущий трек
        audioSource.clip = playlist[currentTrackIndex];
        audioSource.Play();

        // Подготавливаем следующий трек
        currentTrackIndex++;
        if (currentTrackIndex >= playlist.Count)
        {
            currentTrackIndex = 0;
            if (shuffleMode)
                ShufflePlaylist();
        }
    }

    // Кнопка для переключения трека (можно привязать к UI или клавише)
    public void SkipTrack()
    {
        audioSource.Stop();
        PlayNextTrack();
    }

    // Метод для добавления новых треков на ходу
    public void AddFireTrack(AudioClip newTrack)
    {
        playlist.Add(newTrack);
    }
}