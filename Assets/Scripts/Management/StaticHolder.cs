using UnityEngine;

namespace RacingProject.Management
{
    // Настройки звука: хранятся в PlayerPrefs и переживают и перезагрузку сцены, и перезапуск игры
    public static class StaticHolder
    {
        private const string MusikVolumeKey = "MusikVolume";
        private const string EngineVolumeKey = "EngineVolume";
        // Положение слайдера по умолчанию (0..1)
        private const float DefaultVolume = 0.75f;

        public static float MusikVolume
        {
            get => PlayerPrefs.GetFloat(MusikVolumeKey, DefaultVolume);
            set => PlayerPrefs.SetFloat(MusikVolumeKey, value);
        }

        public static float EngineVolume
        {
            get => PlayerPrefs.GetFloat(EngineVolumeKey, DefaultVolume);
            set => PlayerPrefs.SetFloat(EngineVolumeKey, value);
        }
    }
}
