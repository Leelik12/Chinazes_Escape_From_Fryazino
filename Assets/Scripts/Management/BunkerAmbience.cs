using UnityEngine;

namespace RacingProject.Management
{
    // Освещение бункера-меню: пока бункер включён, гасит солнце и яркий общий свет сцены,
    // чтобы комнату освещали только лампа и экраны. При выключении (старт игры прячет меню) всё возвращает
    public class BunkerAmbience : MonoBehaviour
    {
        [Tooltip("Солнце сцены: в бункере выключается")]
        [SerializeField] private Light sun;
        [Tooltip("Общий свет внутри бункера")]
        [SerializeField] private Color ambient = new Color(0.1f, 0.105f, 0.1f);
        [Tooltip("Сила отражений неба внутри бункера")]
        [SerializeField, Range(0f, 1f)] private float reflections = 0.15f;

        private Color savedAmbient;
        private UnityEngine.Rendering.AmbientMode savedMode;
        private float savedReflections;
        private bool savedSun;
        private bool applied;

        private void OnEnable()
        {
            savedAmbient = RenderSettings.ambientLight;
            savedMode = RenderSettings.ambientMode;
            savedReflections = RenderSettings.reflectionIntensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.reflectionIntensity = reflections;
            if (sun != null)
            {
                savedSun = sun.enabled;
                sun.enabled = false;
            }
            applied = true;
        }

        private void OnDisable()
        {
            if (!applied) return;
            applied = false;
            RenderSettings.ambientMode = savedMode;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.reflectionIntensity = savedReflections;
            if (sun != null)
                sun.enabled = savedSun;
        }
    }
}
