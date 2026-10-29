using RacingProject.Audio;
using UnityEngine;

namespace RacingProject.Effects
{
    // Ткань на ветру: флаг (закреплён левым краем у древка) или бельё на верёвке (закреплено верхним краем).
    // Сетка двусторонняя (обратная сторона — свои вершины и нормали, иначе Lit с изнанки тёмный), вершины колышет
    // бегущая волна с порывами. Дальше animateDistance от слушателя ткань замирает. Цвет — _BaseColor блока
    public class WindCloth : MonoBehaviour
    {
        public enum Edge { Left, Top }

        [SerializeField] private Edge pinned = Edge.Left;
        [SerializeField] private Vector2 size = new Vector2(1.6f, 1f);
        [SerializeField] private Vector2Int cells = new Vector2Int(10, 6);
        [SerializeField] private Material material;
        [SerializeField] private Color color = Color.white;
        [Tooltip("Размах волны у свободного края, м")]
        [SerializeField] private float amplitude = 0.18f;
        [SerializeField] private float waveSpeed = 4f;
        [SerializeField] private float animateDistance = 120f;

        private Mesh mesh;
        private Vector3[] rest;
        private Vector3[] vertices;
        private int frontCount;
        private float seed;

        private void Start()
        {
            seed = Random.value * 50f;
            Build();
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        // Плоскость XY: флаг уходит от древка по +X и вниз по −Y, бельё висит вниз от верёвки (−Y)
        private void Build()
        {
            int cx = Mathf.Max(1, cells.x), cy = Mathf.Max(1, cells.y);
            frontCount = (cx + 1) * (cy + 1);
            rest = new Vector3[frontCount * 2];
            var uv = new Vector2[frontCount * 2];
            for (int y = 0; y <= cy; y++)
                for (int x = 0; x <= cx; x++)
                {
                    int i = y * (cx + 1) + x;
                    float u = x / (float)cx, v = y / (float)cy;
                    Vector3 p = pinned == Edge.Left
                        ? new Vector3(u * size.x, -v * size.y, 0f)
                        : new Vector3((u - 0.5f) * size.x, -v * size.y, 0f);
                    rest[i] = rest[i + frontCount] = p;
                    uv[i] = uv[i + frontCount] = new Vector2(u, 1f - v);
                }
            var triangles = new int[cx * cy * 12];
            int t = 0;
            for (int y = 0; y < cy; y++)
                for (int x = 0; x < cx; x++)
                {
                    int a = y * (cx + 1) + x, b = a + 1, c = a + cx + 1, d = c + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                    // Изнанка — те же точки в обратном порядке
                    triangles[t++] = a + frontCount; triangles[t++] = c + frontCount; triangles[t++] = b + frontCount;
                    triangles[t++] = b + frontCount; triangles[t++] = c + frontCount; triangles[t++] = d + frontCount;
                }
            vertices = (Vector3[])rest.Clone();
            mesh = new Mesh { name = "WindCloth" };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(pinned == Edge.Left ? size.x * 0.5f : 0f, -size.y * 0.5f, 0f), new Vector3(size.x + 1f, size.y + 1f, 1.5f));

            var filter = gameObject.GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = gameObject.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
            Wave(0f);
        }

        private void Update()
        {
            if (mesh == null) return;
            if ((transform.position - ActiveListener.Position).sqrMagnitude > animateDistance * animateDistance) return;
            Wave(Time.time);
        }

        private void Wave(float time)
        {
            // Порывы: медленный шум силы ветра
            float gust = 0.55f + 0.45f * Mathf.PerlinNoise(seed, time * 0.25f);
            for (int i = 0; i < frontCount; i++)
            {
                Vector3 p = rest[i];
                // Свободнее всего дальний от крепления край
                float free = pinned == Edge.Left ? p.x / Mathf.Max(0.01f, size.x) : -p.y / Mathf.Max(0.01f, size.y);
                float along = pinned == Edge.Left ? p.x : p.y;
                float phase = along * 3.2f - time * waveSpeed + seed;
                float offset = (Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2.3f + p.y * 2f)) * amplitude * gust * free;
                p.z += offset;
                if (pinned == Edge.Left)
                    p.y -= free * free * 0.08f * (1.2f - gust);
                else
                    p.x += Mathf.Sin(time * 1.3f + seed) * 0.04f * free;
                vertices[i] = vertices[i + frontCount] = p;
            }
            mesh.vertices = vertices;
            mesh.RecalculateNormals();
        }
    }
}
