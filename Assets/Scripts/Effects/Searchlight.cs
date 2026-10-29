using UnityEngine;
using UnityEngine.Rendering;

namespace RacingProject.Effects
{
    // Прожектор на вышке КПП: медленно водит лучом по кругу и чуть качает им вверх-вниз. Луч виден в воздухе —
    // конус с шейдером RacingProject/LightBeam (свечение гаснет к концу по цвету вершин и мягко к краям),
    // плюс настоящий прожекторный свет
    public class Searchlight : MonoBehaviour
    {
        [SerializeField] private Light spot;
        [SerializeField] private Material beamMaterial;
        [Tooltip("Скорость поворота, градусов в секунду")]
        [SerializeField] private float turnSpeed = 18f;
        [Tooltip("Размах по горизонтали, градусов (360 — полный круг)")]
        [SerializeField] private float sweep = 140f;
        [SerializeField] private float pitch = 18f;
        [SerializeField] private float pitchSwing = 6f;
        [SerializeField] private float beamLength = 70f;
        [SerializeField] private float beamRadius = 5f;
        [SerializeField, Range(0f, 1f)] private float beamAlpha = 0.25f;
        [Tooltip("Сдвиг фазы, чтобы соседние прожекторы не ходили синхронно")]
        [SerializeField] private float phase;

        private Quaternion baseRotation;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            if (beamMaterial != null) CreateBeam();
        }

        private void CreateBeam()
        {
            var go = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = BuildCone(beamLength, beamRadius, beamAlpha);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = beamMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        // Конус вдоль +Z без донышка: у вершины луч яркий, к концу прозрачный. Обе стороны рисует шейдер (Cull Off)
        public static Mesh BuildCone(float length, float radius, float alpha)
        {
            const int sides = 20;
            const int rings = 6;
            var vertices = new Vector3[(sides + 1) * (rings + 1)];
            var colors = new Color[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                for (int s = 0; s <= sides; s++)
                {
                    float a = (float)s / sides * Mathf.PI * 2f;
                    int i = r * (sides + 1) + s;
                    float rad = Mathf.Lerp(0.25f, radius, t);
                    vertices[i] = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, t * length);
                    colors[i] = new Color(1f, 0.97f, 0.88f, alpha * (1f - t) * (1f - t));
                    uv[i] = new Vector2(0.5f, 0.5f);
                }
            }
            var triangles = new int[sides * rings * 6];
            int k = 0;
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int a = r * (sides + 1) + s, b = a + 1, c = a + sides + 1, d = c + 1;
                    triangles[k++] = a; triangles[k++] = c; triangles[k++] = b;
                    triangles[k++] = b; triangles[k++] = c; triangles[k++] = d;
                }
            var mesh = new Mesh { name = "SearchlightBeam", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
            mesh.RecalculateBounds();
            return mesh;
        }

        private void Update()
        {
            float t = Time.time + phase;
            float yaw = sweep >= 360f ? t * turnSpeed : Mathf.Sin(t * turnSpeed / Mathf.Max(1f, sweep) * Mathf.PI) * sweep * 0.5f;
            float tilt = pitch + Mathf.Sin(t * 0.37f) * pitchSwing;
            transform.localRotation = baseRotation * Quaternion.Euler(tilt, yaw, 0f);
            if (spot != null && spot.transform != transform)
                spot.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }
    }
}
