using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RacingProject.Management
{
    // Настройка «Низкое качество графики» для слабых ПК: выключает на камерах тени и сглаживание MSAA.
    // Меняются только свойства камер, ассеты рендера не трогаются. Выбор хранится в PlayerPrefs
    // и применяется к камерам после каждой загрузки сцены
    public static class GraphicsQuality
    {
        private const string LowQualityKey = "LowGraphics";

        public static event Action<bool> Changed;

        public static bool Low => PlayerPrefs.GetInt(LowQualityKey, 0) == 1;

        public static void SetLow(bool low)
        {
            PlayerPrefs.SetInt(LowQualityKey, low ? 1 : 0);
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke(low);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded += (scene, mode) => Apply();
            Apply();
        }

        // Камеры ригов включаются по ходу игры, поэтому настраиваются и выключенные
        public static void Apply()
        {
            bool low = Low;
            foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                cam.allowMSAA = !low;
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                    data.renderShadows = !low;
            }
        }
    }
}
