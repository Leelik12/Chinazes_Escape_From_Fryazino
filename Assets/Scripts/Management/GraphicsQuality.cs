using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RacingProject.Management
{
    // Настройки графики из меню: пресет качества и отдельные параметры (масштаб рендера, сглаживание, тени, SSAO,
    // постобработка, текстуры, анизотропия, трава, деревья, детализация), а для игры на мониторе — режим окна,
    // разрешение, вертикальная синхронизация и ограничение кадров. Каждый параметр — номер варианта в PlayerPrefs.
    // Применяются при старте, после каждой загрузки сцены и сразу при изменении. Ассет URP меняется на лету,
    // поэтому в редакторе его исходные значения возвращаются при выходе из Play Mode
    public static class GraphicsQuality
    {
        public enum Option
        {
            RenderScale, AntiAliasing, Shadows, Ssao, PostProcessing, Textures, Anisotropic, Grass, Trees, Detail,
            WindowMode, Resolution, VSync, FrameLimit
        }

        // Параметры, которые задаёт пресет (по порядку перечисления до WindowMode)
        private const int PresetOptions = (int)Option.WindowMode;

        public static readonly string[] PresetNames = { "Низкое", "Среднее", "Высокое", "Ультра" };
        public const string CustomPresetName = "Своё";
        // Варианты параметров пресетов: масштаб, сглаживание, тени, SSAO, постобработка, текстуры, анизотропия, трава, деревья, детализация.
        // «Высокое» повторяет прежние настройки проекта
        private static readonly int[][] Presets =
        {
            new[] { 5, 0, 0, 0, 1, 1, 1, 1, 0, 0 },
            new[] { 5, 1, 1, 0, 1, 2, 1, 2, 1, 1 },
            new[] { 5, 1, 2, 1, 1, 2, 2, 3, 2, 2 },
            new[] { 5, 2, 4, 1, 1, 2, 2, 4, 3, 3 },
        };
        private const int DefaultPreset = 2;

        private static readonly Dictionary<Option, string[]> Labels = new Dictionary<Option, string[]>
        {
            { Option.RenderScale, new[] { "50%", "60%", "70%", "80%", "90%", "100%" } },
            { Option.AntiAliasing, new[] { "Выкл", "MSAA 2x", "MSAA 4x", "MSAA 8x" } },
            { Option.Shadows, new[] { "Выкл", "Низкие", "Средние", "Высокие", "Ультра" } },
            { Option.Ssao, new[] { "Выкл", "Вкл" } },
            { Option.PostProcessing, new[] { "Выкл", "Вкл" } },
            { Option.Textures, new[] { "Низкие", "Средние", "Высокие" } },
            { Option.Anisotropic, new[] { "Выкл", "Вкл", "Макс" } },
            { Option.Grass, new[] { "Выкл", "Близко", "Средне", "Далеко", "Макс" } },
            { Option.Trees, new[] { "Близко", "Средне", "Далеко", "Макс" } },
            { Option.Detail, new[] { "Низкая", "Средняя", "Высокая", "Макс" } },
            { Option.WindowMode, new[] { "Полный экран", "Окно", "Монопольно" } },
            { Option.VSync, new[] { "Выкл", "Вкл" } },
            { Option.FrameLimit, new[] { "30", "60", "90", "120", "144", "Нет" } },
        };

        private static readonly float[] RenderScales = { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f };
        private static readonly int[] MsaaSamples = { 1, 2, 4, 8 };
        private static readonly int[] ShadowResolution = { 512, 512, 1024, 2048, 4096 };
        private static readonly int[] AdditionalShadowResolution = { 256, 256, 512, 1024, 2048 };
        private static readonly float[] ShadowDistance = { 0f, 25f, 50f, 70f, 100f };
        private static readonly int[] ShadowCascades = { 1, 1, 2, 2, 4 };
        private static readonly int[] TextureMipLimit = { 2, 1, 0 };
        private static readonly AnisotropicFiltering[] Aniso = { AnisotropicFiltering.Disable, AnisotropicFiltering.Enable, AnisotropicFiltering.ForceEnable };
        private static readonly float[] GrassDistance = { 0f, 40f, 60f, 80f, 120f };
        private static readonly float[] GrassDensity = { 0f, 0.35f, 0.5f, 0.6f, 0.8f };
        private static readonly float[] TreeBillboard = { 25f, 40f, 50f, 90f };
        private static readonly float[] LodBias = { 1f, 1.5f, 2f, 3f };
        private static readonly FullScreenMode[] WindowModes = { FullScreenMode.FullScreenWindow, FullScreenMode.Windowed, FullScreenMode.ExclusiveFullScreen };
        private static readonly int[] FrameLimits = { 30, 60, 90, 120, 144, -1 };

        private const string KeyPrefix = "Gfx.";
        // Старая настройка «Низкая графика»: при первом запуске переносится в пресет «Низкое»
        private const string LegacyLowKey = "LowGraphics";

        public static event Action Changed;

        // Камеры, у которых постобработка была выключена изначально (карта, служебные): их не трогаем
        private static readonly HashSet<int> noPostCameras = new HashSet<int>();
        private static readonly HashSet<int> seenCameras = new HashSet<int>();
        private static AssetSnapshot snapshot;
        private static List<Resolution> resolutions;

        public static int Get(Option option)
        {
            if (option == Option.Resolution) return ResolutionIndex();
            int def = (int)option < PresetOptions ? Presets[DefaultPreset][(int)option] : DefaultFor(option);
            return Mathf.Clamp(PlayerPrefs.GetInt(KeyPrefix + option, def), 0, Count(option) - 1);
        }

        public static void Set(Option option, int value)
        {
            if (option == Option.Resolution)
            {
                var list = Resolutions();
                if (list.Count == 0) return;
                Resolution r = list[Mathf.Clamp(value, 0, list.Count - 1)];
                PlayerPrefs.SetString(KeyPrefix + option, r.width + "x" + r.height);
            }
            else
                PlayerPrefs.SetInt(KeyPrefix + option, Mathf.Clamp(value, 0, Count(option) - 1));
            PlayerPrefs.Save();
            Apply();
        }

        public static string[] OptionLabels(Option option)
        {
            if (option != Option.Resolution) return Labels[option];
            var list = Resolutions();
            var names = new string[list.Count];
            for (int i = 0; i < list.Count; i++) names[i] = list[i].width + "×" + list[i].height;
            return names;
        }

        public static int Count(Option option) => OptionLabels(option).Length;

        // Номер пресета, которому совпадают все параметры, или −1 («Своё»)
        public static int Preset
        {
            get
            {
                for (int p = 0; p < Presets.Length; p++)
                {
                    bool match = true;
                    for (int i = 0; i < PresetOptions && match; i++)
                        match = Get((Option)i) == Presets[p][i];
                    if (match) return p;
                }
                return -1;
            }
        }

        public static string PresetName => Preset >= 0 ? PresetNames[Preset] : CustomPresetName;

        public static void SetPreset(int preset)
        {
            preset = Mathf.Clamp(preset, 0, Presets.Length - 1);
            for (int i = 0; i < PresetOptions; i++)
                PlayerPrefs.SetInt(KeyPrefix + (Option)i, Presets[preset][i]);
            PlayerPrefs.Save();
            Apply();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (PlayerPrefs.GetInt(LegacyLowKey, 0) == 1)
            {
                PlayerPrefs.DeleteKey(LegacyLowKey);
                SetPreset(0);
            }
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting -= RestoreAsset;
            Application.quitting += RestoreAsset;
            ViewModeService.Changed += mode => Apply();
            Apply();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        public static void Apply()
        {
            ApplyPipeline();
            ApplyQuality();
            ApplyTerrains();
            ApplyCameras();
            ApplyDisplay();
            Changed?.Invoke();
        }

        private static void ApplyPipeline()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) return;
            if (snapshot == null) snapshot = new AssetSnapshot(asset);

            int shadows = Get(Option.Shadows);
            asset.renderScale = RenderScales[Get(Option.RenderScale)];
            asset.msaaSampleCount = MsaaSamples[Get(Option.AntiAliasing)];
            asset.shadowDistance = Mathf.Max(ShadowDistance[shadows], 1f);
            asset.shadowCascadeCount = ShadowCascades[shadows];
            asset.mainLightShadowmapResolution = ShadowResolution[shadows];
            asset.additionalLightsShadowmapResolution = AdditionalShadowResolution[shadows];
            bool ssao = Get(Option.Ssao) == 1;
            foreach (ScriptableRendererData data in asset.rendererDataList)
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                    if (feature is ScreenSpaceAmbientOcclusion)
                        feature.SetActive(ssao);
            }
        }

        private static void ApplyQuality()
        {
            QualitySettings.globalTextureMipmapLimit = TextureMipLimit[Get(Option.Textures)];
            QualitySettings.anisotropicFiltering = Aniso[Get(Option.Anisotropic)];
            QualitySettings.lodBias = LodBias[Get(Option.Detail)];
        }

        private static void ApplyTerrains()
        {
            int grass = Get(Option.Grass);
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                terrain.detailObjectDistance = GrassDistance[grass];
                terrain.detailObjectDensity = GrassDensity[grass];
                terrain.treeBillboardDistance = TreeBillboard[Get(Option.Trees)];
            }
        }

        // Камеры ригов включаются по ходу игры, поэтому настраиваются и выключенные
        private static void ApplyCameras()
        {
            bool msaa = Get(Option.AntiAliasing) > 0;
            bool shadows = Get(Option.Shadows) > 0;
            bool post = Get(Option.PostProcessing) == 1;
            foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                cam.allowMSAA = msaa;
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data == null) continue;
                int id = cam.GetInstanceID();
                if (seenCameras.Add(id) && !data.renderPostProcessing)
                    noPostCameras.Add(id);
                data.renderShadows = shadows;
                if (!noPostCameras.Contains(id))
                    data.renderPostProcessing = post;
            }
        }

        // Окно, разрешение, синхронизация и лимит кадров действуют только на мониторе: в VR кадр задаёт шлем
        private static void ApplyDisplay()
        {
            if (ViewModeService.IsVR)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                return;
            }
            QualitySettings.vSyncCount = Get(Option.VSync);
            Application.targetFrameRate = Get(Option.VSync) == 1 ? -1 : FrameLimits[Get(Option.FrameLimit)];
#if !UNITY_EDITOR
            var list = Resolutions();
            if (list.Count == 0) return;
            Resolution r = list[ResolutionIndex()];
            FullScreenMode mode = WindowModes[Get(Option.WindowMode)];
            if (Screen.width != r.width || Screen.height != r.height || Screen.fullScreenMode != mode)
                Screen.SetResolution(r.width, r.height, mode);
#endif
        }

        private static int DefaultFor(Option option)
        {
            switch (option)
            {
                case Option.VSync: return 1;
                case Option.FrameLimit: return FrameLimits.Length - 1;
                default: return 0;
            }
        }

        // Разрешения монитора без повторов по частоте, от меньшего к большему
        private static List<Resolution> Resolutions()
        {
            if (resolutions != null) return resolutions;
            resolutions = new List<Resolution>();
            foreach (Resolution r in Screen.resolutions)
                if (!resolutions.Exists(x => x.width == r.width && x.height == r.height))
                    resolutions.Add(r);
            if (resolutions.Count == 0)
                resolutions.Add(Screen.currentResolution);
            return resolutions;
        }

        private static int ResolutionIndex()
        {
            var list = Resolutions();
            string saved = PlayerPrefs.GetString(KeyPrefix + Option.Resolution, "");
            for (int i = 0; i < list.Count; i++)
                if (list[i].width + "x" + list[i].height == saved)
                    return i;
            // По умолчанию — разрешение рабочего стола (или самое большое)
            Resolution desktop = Screen.currentResolution;
            int best = list.Count - 1;
            for (int i = 0; i < list.Count; i++)
                if (list[i].width == desktop.width && list[i].height == desktop.height)
                    best = i;
            return best;
        }

        private static void RestoreAsset()
        {
#if UNITY_EDITOR
            snapshot?.Restore();
#endif
        }

        // Исходные значения ассета URP и SSAO: в редакторе изменения ассета пережили бы выход из Play Mode
        private class AssetSnapshot
        {
            private readonly UniversalRenderPipelineAsset asset;
            private readonly float renderScale, shadowDistance;
            private readonly int msaa, cascades, mainResolution, additionalResolution;
            private readonly List<KeyValuePair<ScriptableRendererFeature, bool>> features = new List<KeyValuePair<ScriptableRendererFeature, bool>>();

            public AssetSnapshot(UniversalRenderPipelineAsset asset)
            {
                this.asset = asset;
                renderScale = asset.renderScale;
                shadowDistance = asset.shadowDistance;
                msaa = asset.msaaSampleCount;
                cascades = asset.shadowCascadeCount;
                mainResolution = asset.mainLightShadowmapResolution;
                additionalResolution = asset.additionalLightsShadowmapResolution;
                foreach (ScriptableRendererData data in asset.rendererDataList)
                    if (data != null)
                        foreach (ScriptableRendererFeature f in data.rendererFeatures)
                            features.Add(new KeyValuePair<ScriptableRendererFeature, bool>(f, f.isActive));
            }

            public void Restore()
            {
                if (asset == null) return;
                asset.renderScale = renderScale;
                asset.shadowDistance = shadowDistance;
                asset.msaaSampleCount = msaa;
                asset.shadowCascadeCount = cascades;
                asset.mainLightShadowmapResolution = mainResolution;
                asset.additionalLightsShadowmapResolution = additionalResolution;
                foreach (var f in features)
                    if (f.Key != null) f.Key.SetActive(f.Value);
            }
        }
    }
}
