using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace RacingProject.Management
{
    public class MenuManager : MonoBehaviour
    {
        public Slider volumeSliderMusik;
        public Slider volumeSliderEngine;
        public AudioMixer audioMixer;
        public AudioMixer MusikMixer;

        // Нижняя граница микшера: такой уровень уже не слышен
        private const float MinDb = -80f;

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
            MusikMixer.SetFloat("MusikVolume", ToDecibels(volume));
            StaticHolder.MusikVolume = volume;
        }

        public void SetVolumeEngine(float volume)
        {
            audioMixer.SetFloat("EngineVolume", ToDecibels(volume));
            StaticHolder.EngineVolume = volume;
        }

        // Слух воспринимает громкость логарифмически: линейный слайдер 0..1 переводится в 20·log10.
        // 1 соответствует 0 дБ, 0 — тишине
        private static float ToDecibels(float volume)
        {
            return volume > 0.0001f ? Mathf.Max(MinDb, Mathf.Log10(volume) * 20f) : MinDb;
        }

        public void EndGame()
        {
            Application.Quit();
        }
    }
}
