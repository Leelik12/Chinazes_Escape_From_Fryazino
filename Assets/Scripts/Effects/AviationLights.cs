using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RacingProject.Audio;

namespace RacingProject.Effects
{
    // Заградительные огни высотных ориентиров (телевышка, трубы, водонапорная башня, кран): красные точки
    // мигают сквозь туман и помогают держать направление ночью. Огонь — квадрат свечения (не глушится туманом),
    // повёрнутый к слушателю; мигание одинаково у всех огней одной группы, у групп — свой сдвиг по фазе
    public class AviationLights : MonoBehaviour
    {
        [Tooltip("Точки огней (дочерние объекты ориентиров)")]
        [SerializeField] private Transform[] points = new Transform[0];
        [SerializeField] private Material glowMaterial;
        [SerializeField] private Color color = new Color(1f, 0.08f, 0.04f);
        [SerializeField] private float size = 3f;
        [Tooltip("Период мигания, с")]
        [SerializeField] private float period = 1.6f;
        [Tooltip("Доля периода, когда огонь горит")]
        [SerializeField, Range(0.05f, 1f)] private float onShare = 0.45f;
        [SerializeField] private float brightness = 6f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private readonly List<Renderer> glows = new List<Renderer>();
        private readonly List<float> phases = new List<float>();
        private MaterialPropertyBlock block;

        private void Start()
        {
            if (glowMaterial == null) return;
            block = new MaterialPropertyBlock();
            Mesh quad = BuildQuad();
            foreach (Transform point in points)
            {
                if (point == null) continue;
                var go = new GameObject("AviationGlow", typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = point.gameObject.layer;
                go.transform.SetParent(point, false);
                go.transform.localScale = Vector3.one * size / Mathf.Max(0.01f, point.lossyScale.x);
                go.GetComponent<MeshFilter>().sharedMesh = quad;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = glowMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                glows.Add(renderer);
                // Сдвиг фазы по ориентиру: огни одной вышки мигают вместе
                Vector3 p = point.root.position;
                phases.Add(Mathf.Repeat((p.x * 0.013f + p.z * 0.029f), 1f));
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "AviationGlowQuad" };
            mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void LateUpdate()
        {
            if (glows.Count == 0) return;
            Vector3 eye = ActiveListener.Position;
            float t = Time.time / period;
            for (int i = 0; i < glows.Count; i++)
            {
                Renderer glow = glows[i];
                if (glow == null) continue;
                bool on = Mathf.Repeat(t + phases[i], 1f) < onShare;
                glow.enabled = on;
                if (!on) continue;
                Transform tr = glow.transform;
                Vector3 toEye = eye - tr.position;
                if (toEye.sqrMagnitude > 0.01f)
                    tr.rotation = Quaternion.LookRotation(-toEye);
                block.Clear();
                block.SetColor(ColorId, color * brightness);
                glow.SetPropertyBlock(block);
            }
        }
    }
}
