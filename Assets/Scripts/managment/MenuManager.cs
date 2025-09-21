using System.Collections.Generic;
using TMPro;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;


public class MenuManager : MonoBehaviour
{
    public TMP_Text messedge;
    public TMP_Text Best;
    public TMP_Text Last;
    public Slider volumeSliderMusik;
    public Slider volumeSliderEngine;
    public AudioMixer audioMixer;
    public AudioMixer MusikMixer;

    private void Awake()
    {
        if (StaticHolder.PlayerWin)
        {
            messedge.text = "Вы выиграли";
            Best.text = "Лучшее время: " + StaticHolder.BestTime;
            Last.text = "Последнее время: " + StaticHolder.LastTime;
        }
        else if (StaticHolder.PlayerLose)
        {
            messedge.text = "Вы проиграли";
            Best.text = "Лучшее время: " + StaticHolder.BestTime;
            Last.text = "Последнее время: " + StaticHolder.LastTime;
        }
        if (StaticHolder.Record)
        {
            messedge.text = messedge.text + " и поставили рекорд!";
        }
    }
    void Start()
    {

        volumeSliderMusik.value = StaticHolder.MusikVolume;
        SetVolumeMusik(StaticHolder.MusikVolume);
        volumeSliderMusik.onValueChanged.AddListener(OnVolumeChangedMusik);

        volumeSliderEngine.value = StaticHolder.EngineVolume;
        SetVolumeEngine(StaticHolder.EngineVolume);
        volumeSliderEngine.onValueChanged.AddListener(OnVolumeChangedEngine);
    }
    void OnVolumeChangedMusik(float volume)
    {
        SetVolumeMusik(volume);
        StaticHolder.MusikVolume = volume; // Сохраняем значение
    }
    void OnVolumeChangedEngine(float volume)
    {
        SetVolumeEngine(volume);
        StaticHolder.EngineVolume = volume; // Сохраняем значение
    }
    public void SetVolumeMusik(float volume)
    {
        float dB = Mathf.Lerp(-20f, 20f, volume); // volume от 0 до 1
        MusikMixer.SetFloat("MusikVolume", dB);
        StaticHolder.MusikVolume = volume;
        Debug.Log("qqqqqqqqqqqqqqqq");
    }

    public void SetVolumeEngine(float volume)
    {
        float dB = Mathf.Lerp(-20f, 20f, volume); // volume от 0 до 1
        audioMixer.SetFloat("EngineVolume", dB);
        StaticHolder.EngineVolume = volume;
        Debug.Log("wwwwwwwwwwwwww");
    }

    public void StartGame()
    {
        SceneManager.LoadScene(1);
    }
    public void EndGame()
    {
        Application.Quit();
    }
}
