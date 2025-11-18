using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Magnitola : MonoBehaviour
{
    public static bool MusicAlreadyStarted = false;   //  добавлено

    public AudioSource audioSource;
    public AudioClip[] fireTracks;
    public bool shuffleMode = true;

    private List<AudioClip> playlist;
    private int currentTrackIndex = 0;

    void Start()
    {
        // Если музыка уже играет — блокируем повторный запуск
        if (MusicAlreadyStarted)
        {
            audioSource.enabled = false;
            return;
        }

        // Если здесь — значит запускаем музыку первый раз
        MusicAlreadyStarted = true;
        audioSource.enabled = true;

        playlist = fireTracks.ToList();
        if (shuffleMode)
            ShufflePlaylist();

        PlayNextTrack();
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
