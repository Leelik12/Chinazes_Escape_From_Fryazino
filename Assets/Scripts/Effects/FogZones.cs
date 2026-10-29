using System;
using UnityEngine;

namespace RacingProject.Effects
{
    // Туман по районам: у каждой зоны свой множитель плотности и цвет (дым над промзоной, сырость у озера,
    // чистое поле). Влияние зоны плавно спадает к её краю; где зон нет — туман сцены как есть.
    // Плотность умножает WeatherController (он же добавляет дождь), цвет ставит этот компонент.
    // Под водой (WaterVolume) туман густой и зелёный
    public class FogZones : MonoBehaviour
    {
        [Serializable]
        public class Zone
        {
            public string name;
            public Vector3 center;
            [Tooltip("Радиус полного влияния, м")]
            public float radius = 150f;
            [Tooltip("Ширина спада к краю, м")]
            public float falloff = 120f;
            public float density = 1f;
            public Color color = Color.gray;
        }

        [SerializeField] private Zone[] zones = new Zone[0];
        [Tooltip("Как быстро туман подстраивается под зону, 1/с")]
        [SerializeField] private float blendSpeed = 0.5f;
        [SerializeField] private float underwaterDensity = 0.35f;
        [SerializeField] private Color underwaterColor = new Color(0.05f, 0.11f, 0.1f);

        // Камера под водой — выставляет WaterVolume
        public static bool Underwater { get; set; }
        // Множитель плотности тумана; WeatherController умножает на него свою плотность
        public static float DensityMultiplier { get; private set; } = 1f;
        // Плотность сцены без погоды и зон — запоминает WeatherController
        public static float BaseDensity { get; set; } = 0.05f;
        // Множитель цвета тумана по времени суток (TimeOfDay): тёплый на закате, тёмный ночью
        public static Color Tint { get; set; } = Color.white;

        private Color baseColor;
        private Color color;
        private float density = 1f;

        private void Awake()
        {
            baseColor = RenderSettings.fogColor;
            color = baseColor;
        }

        private void Update()
        {
            Vector3 eye = Audio.ActiveListener.Position;
            float weight = 0f;
            float targetDensity = 0f;
            Color targetColor = Color.clear;
            foreach (Zone zone in zones)
            {
                Vector3 offset = eye - zone.center;
                offset.y = 0f;
                float w = 1f - Mathf.Clamp01((offset.magnitude - zone.radius) / Mathf.Max(1f, zone.falloff));
                if (w <= 0f) continue;
                weight += w;
                targetDensity += zone.density * w;
                targetColor += zone.color * w;
            }
            // Вне зон — туман сцены; внутри нескольких — среднее по весам
            float outside = Mathf.Clamp01(1f - weight);
            float total = weight + outside;
            targetDensity = (targetDensity + outside) / total;
            targetColor = (targetColor + baseColor * outside) / total;

            float k = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            density = Mathf.Lerp(density, targetDensity, k);
            color = Color.Lerp(color, targetColor, k);
            if (Underwater)
            {
                DensityMultiplier = underwaterDensity / Mathf.Max(0.0001f, BaseDensity);
                RenderSettings.fogColor = underwaterColor;
            }
            else
            {
                DensityMultiplier = density;
                RenderSettings.fogColor = color * Tint;
            }
        }

        private void OnDestroy()
        {
            DensityMultiplier = 1f;
            Underwater = false;
            Tint = Color.white;
            RenderSettings.fogColor = baseColor;
        }
    }
}
