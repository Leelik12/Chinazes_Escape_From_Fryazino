using System;
using UnityEngine;

namespace RacingProject.Management
{
    // Настройки комфорта в VR: поворот стиком плавный или рывками и затемнение краёв обзора, когда машину
    // резко разгоняет, тормозит или разворачивает. Хранятся в PlayerPrefs, применяет их VRComfortRig на ригах
    public static class VRComfort
    {
        public enum TurnMode
        {
            Smooth,
            Snap,
        }

        private const string TurnKey = "VRTurnMode";
        private const string VignetteKey = "VRVignette";

        public static readonly string[] TurnNames = { "Плавный", "Рывками" };

        public static event Action Changed;

        public static TurnMode Turn
        {
            get => (TurnMode)Mathf.Clamp(PlayerPrefs.GetInt(TurnKey, (int)TurnMode.Smooth), 0, 1);
            set
            {
                PlayerPrefs.SetInt(TurnKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }

        public static bool Vignette
        {
            get => PlayerPrefs.GetInt(VignetteKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(VignetteKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Changed?.Invoke();
            }
        }
    }
}
