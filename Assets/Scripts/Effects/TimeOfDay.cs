using RacingProject.Enemy;
using UnityEngine;

namespace RacingProject.Effects
{
    // Время суток за забег: первая волна — закат (низкое рыжее солнце, тёплый туман), к середине — сумерки
    // (свет сцены как есть), к nightWave — ночь: слабая холодная луна, тёмные небо, туман и окружение,
    // и главным светом становятся фары, фонари и пожары. Идёт по номеру волны (он синхронен у обоих игроков),
    // переход плавный. Свет солнца и окружения отдаёт WeatherController (молнии вспыхивают поверх),
    // цвет тумана — FogZones.Tint. Небо — копия материала skybox на время раунда, ассет не меняется
    public class TimeOfDay : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private WeatherController weather;
        [Tooltip("К этой волне наступает полная ночь")]
        [SerializeField] private int nightWave = 10;
        [Tooltip("За сколько секунд свет доходит до новой волны")]
        [SerializeField] private float blendTime = 25f;

        [Header("Закат")]
        [SerializeField] private Vector2 sunsetSunAngles = new Vector2(9f, 285f);
        [SerializeField] private Color sunsetSunColor = new Color(1f, 0.56f, 0.3f);
        [SerializeField] private float sunsetSunIntensity = 1.25f;
        [SerializeField] private Color sunsetAmbient = new Color(0.78f, 0.62f, 0.55f);
        [SerializeField] private Color sunsetFogTint = new Color(2.1f, 1.45f, 1.1f);
        [SerializeField] private Color sunsetSkyTint = new Color(0.75f, 0.52f, 0.42f);
        [SerializeField] private float sunsetSkyExposure = 1.35f;

        [Header("Ночь")]
        [SerializeField] private Vector2 nightSunAngles = new Vector2(55f, 350f);
        [SerializeField] private Color nightSunColor = new Color(0.45f, 0.55f, 0.78f);
        [SerializeField] private float nightSunIntensity = 0.16f;
        [SerializeField] private Color nightAmbient = new Color(0.2f, 0.23f, 0.31f);
        [SerializeField] private Color nightFogTint = new Color(0.3f, 0.32f, 0.38f);
        [SerializeField] private Color nightSkyTint = new Color(0.35f, 0.38f, 0.45f);
        [SerializeField] private float nightSkyExposure = 0.45f;

        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        // Свет сцены — середина пути (сумерки)
        private Quaternion duskRotation;
        private Color duskSunColor;
        private float duskSunIntensity;
        private Color duskAmbient;
        private Color duskSkyTint = Color.white;
        private float duskSkyExposure = 1f;
        private Material sceneSky;
        private Material sky;
        // 0 — закат, 1 — ночь
        private float night;

        public static float Night { get; private set; }

        private void Awake()
        {
            if (sun == null) sun = RenderSettings.sun;
            if (enemyManager == null) enemyManager = FindFirstObjectByType<EnemyManager>();
            if (weather == null) weather = FindFirstObjectByType<WeatherController>();
            if (sun != null)
            {
                duskRotation = sun.transform.rotation;
                duskSunColor = sun.color;
                duskSunIntensity = sun.intensity;
            }
            duskAmbient = RenderSettings.ambientLight;
            sceneSky = RenderSettings.skybox;
            if (sceneSky != null)
            {
                sky = new Material(sceneSky);
                if (sky.HasProperty(TintId)) duskSkyTint = sky.GetColor(TintId);
                if (sky.HasProperty(ExposureId)) duskSkyExposure = sky.GetFloat(ExposureId);
                RenderSettings.skybox = sky;
            }
            Apply(0f);
        }

        private float Target
        {
            get
            {
                int wave = enemyManager != null ? enemyManager.Wave : 0;
                return Mathf.Clamp01((wave - 1f) / Mathf.Max(1, nightWave - 1));
            }
        }

        private void Update()
        {
            float target = Target;
            // Новый забег — сразу снова закат, а не медленный рассвет
            if (target < night - 0.2f) night = target;
            else night = Mathf.MoveTowards(night, target, Time.deltaTime / Mathf.Max(0.1f, blendTime) / Mathf.Max(1, nightWave - 1));
            Apply(night);
        }

        // Закат → сумерки (свет сцены) → ночь
        private void Apply(float t)
        {
            Night = t;
            float a = Mathf.Clamp01(t * 2f);
            float b = Mathf.Clamp01(t * 2f - 1f);
            bool first = t < 0.5f;
            Quaternion sunset = Quaternion.Euler(sunsetSunAngles.x, sunsetSunAngles.y, 0f);
            Quaternion moon = Quaternion.Euler(nightSunAngles.x, nightSunAngles.y, 0f);

            Color sunColor = first ? Color.Lerp(sunsetSunColor, duskSunColor, a) : Color.Lerp(duskSunColor, nightSunColor, b);
            float intensity = first ? Mathf.Lerp(sunsetSunIntensity, duskSunIntensity, a) : Mathf.Lerp(duskSunIntensity, nightSunIntensity, b);
            Color ambient = first ? Color.Lerp(sunsetAmbient, duskAmbient, a) : Color.Lerp(duskAmbient, nightAmbient, b);
            if (sun != null)
                sun.transform.rotation = first ? Quaternion.Slerp(sunset, duskRotation, a) : Quaternion.Slerp(duskRotation, moon, b);
            if (weather != null) weather.SetBaseLighting(intensity, sunColor, ambient);
            else
            {
                if (sun != null)
                {
                    sun.color = sunColor;
                    sun.intensity = intensity;
                }
                RenderSettings.ambientLight = ambient;
            }
            FogZones.Tint = first ? Color.Lerp(sunsetFogTint, Color.white, a) : Color.Lerp(Color.white, nightFogTint, b);
            if (sky != null)
            {
                if (sky.HasProperty(TintId))
                    sky.SetColor(TintId, first ? Color.Lerp(sunsetSkyTint, duskSkyTint, a) : Color.Lerp(duskSkyTint, nightSkyTint, b));
                if (sky.HasProperty(ExposureId))
                    sky.SetFloat(ExposureId, first ? Mathf.Lerp(sunsetSkyExposure, duskSkyExposure, a) : Mathf.Lerp(duskSkyExposure, nightSkyExposure, b));
            }
        }

        private void OnDestroy()
        {
            Night = 0f;
            FogZones.Tint = Color.white;
            if (sun != null)
            {
                sun.transform.rotation = duskRotation;
                sun.color = duskSunColor;
                sun.intensity = duskSunIntensity;
            }
            if (sceneSky != null) RenderSettings.skybox = sceneSky;
            if (sky != null) Destroy(sky);
        }
    }
}
