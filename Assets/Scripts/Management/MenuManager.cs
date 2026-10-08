using UnityEngine;
using UnityEngine.Audio;

namespace RacingProject.Management
{
    public class MenuManager : MonoBehaviour
    {
        public AudioMixer audioMixer;
        public AudioMixer MusikMixer;
        public AudioMixer ambientMixer;

        // Нижняя граница микшера: такой уровень уже не слышен
        private const float MinDb = -80f;

        // Громкость задаётся на экране настроек (SettingsMenu); здесь применяется сохранённая
        void Start()
        {
            SetVolumeMusik(StaticHolder.MusikVolume);
            SetVolumeEngine(StaticHolder.EngineVolume);
            SetVolumeAmbient(StaticHolder.AmbientVolume);
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

        public void SetVolumeAmbient(float volume)
        {
            ambientMixer.SetFloat("AmbientVolume", ToDecibels(volume));
            StaticHolder.AmbientVolume = volume;
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
