using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Audio;
using RacingProject.Car;
using RacingProject.Network;

namespace RacingProject.Effects
{
    // Погода раунда (её выбирает сервер в RoomController, у обоих игроков одна): дождь идёт вокруг слушателя,
    // асфальт, бордюры и разметка становятся тёмными и глянцевыми (в них отражаются фары), из-под колёс летят
    // брызги, по крыше стучит. В грозу — молнии (вспышка солнца и неба) и гром с задержкой по «расстоянию».
    // Сами молнии у каждого игрока свои: синхронна погода, а не каждый разряд
    public class WeatherController : MonoBehaviour
    {
        [SerializeField] private RoomController room;
        [Tooltip("Частицы дождя: летят вокруг слушателя (мировые координаты)")]
        [SerializeField] private ParticleSystem rain;
        [SerializeField] private float rainRate = 5000f;
        [SerializeField] private float stormRate = 9000f;
        [Tooltip("Материалы, которые мокнут (клоны на время раунда, ассеты не меняются)")]
        [SerializeField] private Material[] wetMaterials = new Material[0];
        [SerializeField, Range(0f, 1f)] private float wetSmoothness = 0.82f;
        [SerializeField, Range(0f, 1f)] private float wetDarken = 0.62f;

        [Header("Звук")]
        [SerializeField] private AudioClip rainClip;
        [SerializeField] private AudioClip roofClip;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float rainVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float roofVolume = 0.5f;

        [Header("Гроза")]
        [SerializeField] private Light sun;
        [SerializeField] private Vector2 lightningInterval = new Vector2(10f, 30f);
        [SerializeField] private float lightningIntensity = 3.5f;
        [SerializeField] private Color lightningColor = new Color(0.75f, 0.82f, 1f);
        [Tooltip("Насколько гуще туман в дождь")]
        [SerializeField] private float rainFogMultiplier = 1.25f;

        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BumpScaleId = Shader.PropertyToID("_BumpScale");
        private static readonly int ChannelId = Shader.PropertyToID("_SmoothnessTextureChannel");

        private class WetMaterial
        {
            public Material clone;
            public float smoothness;
            public Color color;
            public float bump;
            public bool albedoSmoothness;
        }

        private readonly List<WetMaterial> wet = new List<WetMaterial>();
        // Погода сейчас: 0 — сухо, 1 — дождь, 2 — гроза (капли на стекле и дворники, WindshieldRain)
        public static int Current { get; private set; }
        private int weather = -1;
        private float wetness;
        private AudioSource rainSource;
        private AudioSource roofSource;
        private float nextLightning;
        private float flash;
        private float sunIntensity;
        private Color sunColor;
        private Color ambient;
        private Color sceneAmbient;
        private float fogDensity;
        private readonly List<float> pendingThunder = new List<float>();

        private void Awake()
        {
            if (room == null) room = FindFirstObjectByType<RoomController>();
            if (sun != null)
            {
                sunIntensity = sun.intensity;
                sunColor = sun.color;
            }
            ambient = RenderSettings.ambientLight;
            sceneAmbient = ambient;
            fogDensity = RenderSettings.fogDensity;
            FogZones.BaseDensity = fogDensity;
            rainSource = CreateLoop("Rain", rainClip);
            roofSource = CreateLoop("RainRoof", roofClip);
            if (rain != null)
            {
                var emission = rain.emission;
                emission.rateOverTime = 0f;
            }
        }

        private AudioSource CreateLoop(string name, AudioClip clip)
        {
            if (clip == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = output;
            return source;
        }

        // Клоны мокнущих материалов подставляются во все рендереры сцены один раз, при первом дожде
        private void MakeWetClones()
        {
            var map = new Dictionary<Material, Material>();
            foreach (Material source in wetMaterials)
            {
                if (source == null || map.ContainsKey(source)) continue;
                var clone = new Material(source) { name = source.name + " (Wet)" };
                map[source] = clone;
                wet.Add(new WetMaterial
                {
                    clone = clone,
                    smoothness = source.GetFloat(SmoothnessId),
                    color = source.GetColor(BaseColorId),
                    bump = source.HasProperty(BumpScaleId) ? source.GetFloat(BumpScaleId) : 1f,
                    albedoSmoothness = source.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"),
                });
                // Глянец воды задаётся числом, а не альфой текстуры (в ней — сухая шероховатость асфальта)
                clone.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                clone.SetFloat(ChannelId, 0f);
            }
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                    if (materials[i] != null && map.TryGetValue(materials[i], out Material clone))
                    {
                        materials[i] = clone;
                        changed = true;
                    }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        private void Update()
        {
            int current = room != null ? room.Weather : 0;
            if (current != weather)
            {
                weather = current;
                Current = weather;
                if (weather > 0 && wet.Count == 0) MakeWetClones();
                nextLightning = Time.time + Random.Range(lightningInterval.x, lightningInterval.y) * 0.5f;
            }

            float dt = Time.deltaTime;
            // Дорога мокнет за полминуты, а в начале раунда сразу мокрая (дождь шёл и до него)
            float target = weather > 0 ? 1f : 0f;
            wetness = Time.timeSinceLevelLoad < 3f ? target : Mathf.MoveTowards(wetness, target, dt / 30f);
            WheelEffects.Wetness = wetness;
            ApplyWet();

            if (rain != null)
            {
                Vector3 eye = ActiveListener.Position;
                rain.transform.position = eye + Vector3.up * 12f;
                var emission = rain.emission;
                emission.rateOverTime = weather == 2 ? stormRate : weather == 1 ? rainRate : 0f;
            }

            float hatch = LocalPlayerRole.Current == PlayerRole.Gunner ? 1f : 0.45f;
            SetLoop(rainSource, weather > 0 ? rainVolume * (weather == 2 ? 1f : 0.75f) * Mathf.Lerp(0.7f, 1f, hatch) : 0f);
            // По крыше стучит громче всего в салоне у водителя
            SetLoop(roofSource, weather > 0 && LocalPlayerRole.Current != PlayerRole.None ? roofVolume * Mathf.Lerp(1f, 0.55f, hatch) : 0f);

            RenderSettings.fogDensity = fogDensity * Mathf.Lerp(1f, rainFogMultiplier, wetness) * FogZones.DensityMultiplier;
            UpdateStorm(dt);
        }

        private void ApplyWet()
        {
            foreach (WetMaterial material in wet)
            {
                float dry = material.albedoSmoothness ? 0.25f : material.smoothness;
                material.clone.SetFloat(SmoothnessId, Mathf.Lerp(dry, Mathf.Max(dry, wetSmoothness), wetness));
                material.clone.SetColor(BaseColorId, Color.Lerp(material.color, material.color * wetDarken, wetness));
                material.clone.SetFloat(BumpScaleId, material.bump * Mathf.Lerp(1f, 0.55f, wetness));
                // Без дождя глянец снова из текстуры, как у ассета
                if (wetness <= 0f && material.albedoSmoothness)
                {
                    material.clone.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    material.clone.SetFloat(ChannelId, 1f);
                    material.clone.SetFloat(SmoothnessId, material.smoothness);
                }
                else if (material.albedoSmoothness)
                {
                    material.clone.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    material.clone.SetFloat(ChannelId, 0f);
                }
            }
        }

        private void UpdateStorm(float dt)
        {
            if (weather == 2 && Time.time >= nextLightning)
            {
                nextLightning = Time.time + Random.Range(lightningInterval.x, lightningInterval.y);
                flash = 1f;
                // Гром — через 0,4–3 с, как от разряда в 150–1000 м
                pendingThunder.Add(Time.time + Random.Range(0.4f, 3f));
            }
            for (int i = pendingThunder.Count - 1; i >= 0; i--)
            {
                if (Time.time < pendingThunder[i]) continue;
                pendingThunder.RemoveAt(i);
                SoundLibrary.Play2D(SoundLibrary.Sfx.Thunder, Random.Range(0.6f, 1f), Random.Range(0.9f, 1.05f));
            }

            // Разряд мерцает: два-три всплеска за четверть секунды
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            float flicker = flash > 0f ? flash * (0.55f + 0.45f * Mathf.Sin(Time.time * 70f)) : 0f;
            if (sun != null)
            {
                sun.intensity = Mathf.Lerp(sunIntensity, lightningIntensity, flicker);
                sun.color = Color.Lerp(sunColor, lightningColor, flicker);
            }
            RenderSettings.ambientLight = Color.Lerp(ambient, ambient * 2.2f, flicker);
        }

        // Свет без молний — его меняет время суток (TimeOfDay); вспышки грозы идут поверх него
        public void SetBaseLighting(float intensity, Color color, Color ambientColor)
        {
            sunIntensity = intensity;
            sunColor = color;
            ambient = ambientColor;
        }

        private static void SetLoop(AudioSource source, float volume)
        {
            if (source == null) return;
            source.volume = Mathf.MoveTowards(source.volume, volume, Time.deltaTime * 0.5f);
            if (source.volume > 0.001f && !source.isPlaying)
            {
                source.time = Random.Range(0f, source.clip.length);
                source.Play();
            }
            else if (source.volume <= 0.001f && source.isPlaying)
                source.Stop();
        }

        private void OnDestroy()
        {
            WheelEffects.Wetness = 0f;
            Current = 0;
            foreach (WetMaterial material in wet)
                if (material.clone != null) Destroy(material.clone);
            // Настройки освещения — свойства сцены, но в редакторе их лучше вернуть как были
            RenderSettings.ambientLight = sceneAmbient;
            RenderSettings.fogDensity = fogDensity;
        }
    }
}
