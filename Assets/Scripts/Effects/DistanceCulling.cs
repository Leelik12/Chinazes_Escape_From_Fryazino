using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingProject.Effects
{
    // Отсечение по дистанции для всех игровых камер (монитор и VR): ночной туман скрывает всё дальше ~150 м,
    // а рисовать город целиком дорого. Объекты слоёв из cullLayers дальше cullDistance не рисуются
    // (layerCullDistances; сферическое отсечение в URP не работает). Ориентиры и зарева пожаров лежат на слое Landmark
    // и видны всегда; террейн и вода не отсекаются. Мелкие предметы наполнения карты (слой SmallProps: лужи, знаки,
    // хлам, дворовые мелочи) пропадают раньше — дальше smallDistance их не разглядеть, а вызовов отрисовки они дают много
    public class DistanceCulling : MonoBehaviour
    {
        [SerializeField] private float cullDistance = 300f;
        [Tooltip("Слои, которые отсекаются по дистанции")]
        [SerializeField] private LayerMask cullLayers = 1;
        [SerializeField] private float smallDistance = 150f;
        [Tooltip("Слои мелких предметов, которые отсекаются на smallDistance")]
        [SerializeField] private LayerMask smallLayers = 0;

        private readonly float[] distances = new float[32];
        private readonly HashSet<Camera> applied = new HashSet<Camera>();

        private void Awake()
        {
            for (int i = 0; i < 32; i++)
                distances[i] = (smallLayers.value & (1 << i)) != 0 ? smallDistance
                    : (cullLayers.value & (1 << i)) != 0 ? cullDistance : 0f;
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += Apply;
        private void OnDisable() => RenderPipelineManager.beginCameraRendering -= Apply;

        private void Apply(ScriptableRenderContext context, Camera camera)
        {
            if (camera.cameraType != CameraType.Game || camera.targetTexture != null) return;
            if (!applied.Add(camera)) return;
            camera.layerCullDistances = distances;
        }
    }
}
