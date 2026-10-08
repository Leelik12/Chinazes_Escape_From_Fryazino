using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Car
{
    // Вмятины на кузове от ударов. Столкновения ловит сервер (физику машин считает хост) и рассылает удар
    // обоим игрокам в осях машины, каждый мнёт свою копию сетки одинаково. Вершины рядом с точкой удара
    // вдавливаются куполом, а нормали наклоняются к центру вмятины, поэтому её видно и без пересчёта нормалей
    // всей сетки. Для поиска ближних вершин сетка заранее раскладывается по ячейкам (в фоновом потоке)
    public class CarDeformation : NetworkBehaviour
    {
        [Tooltip("Видимые сетки кузова, которые мнутся (нужен Read/Write в импорте модели)")]
        [SerializeField] private MeshFilter[] bodies = new MeshFilter[0];

        [Header("Удар")]
        [Tooltip("Удары со скоростью сближения меньше этой не оставляют вмятин, м/с")]
        [SerializeField] private float minImpactSpeed = 4f;
        // Машины в проекте примерно вдвое больше настоящих, поэтому вмятины крупные
        [Tooltip("Глубина вмятины на каждый м/с сверх порога, м")]
        [SerializeField] private float depthPerSpeed = 0.025f;
        [SerializeField] private float maxDepth = 0.32f;
        [Tooltip("Радиус вмятины: от минимального при слабом ударе до максимального при сильном, м")]
        [SerializeField] private float minRadius = 0.6f;
        [SerializeField] private float maxRadius = 1.4f;
        [Tooltip("Дальше этого вершина от исходного места не уходит, сколько ни бей, м")]
        [SerializeField] private float maxTotalDent = 0.5f;
        [Tooltip("Лёгкие предметы мнут слабее: полная вмятина от тел тяжелее этого, кг")]
        [SerializeField] private float fullDentMass = 800f;
        [Tooltip("Пауза между вмятинами, с: машины после удара ещё трутся друг о друга")]
        [SerializeField] private float hitCooldown = 0.3f;

        private const float CellSize = 0.4f;

        private class Body
        {
            public MeshFilter filter;
            public Mesh mesh;
            public Vector3[] original;
            public Vector3[] vertices;
            public Vector3[] normals;
            // Ячейка сетки (в осях меша) → индексы вершин; null, пока раскладка не готова
            public Dictionary<Vector3Int, List<int>> grid;
            public float cell;
            public bool dirty;
        }

        private readonly List<Body> parts = new List<Body>();
        private float nextHitTime;

        private void Start()
        {
            foreach (MeshFilter filter in bodies)
            {
                if (filter == null || filter.sharedMesh == null) continue;
                if (!filter.sharedMesh.isReadable)
                {
                    Debug.LogWarning($"CarDeformation: сетка {filter.sharedMesh.name} без Read/Write, вмятин на ней не будет", filter);
                    continue;
                }
                // Своя копия сетки: общий ассет мял бы все такие машины сразу
                Mesh mesh = Instantiate(filter.sharedMesh);
                mesh.MarkDynamic();
                filter.sharedMesh = mesh;
                var body = new Body
                {
                    filter = filter,
                    mesh = mesh,
                    vertices = mesh.vertices,
                    normals = mesh.normals,
                };
                body.original = (Vector3[])body.vertices.Clone();
                if (body.normals.Length != body.vertices.Length)
                    body.normals = null;
                float scale = Mathf.Max(1e-4f, MaxAbs(filter.transform.lossyScale));
                body.cell = CellSize / scale;
                parts.Add(body);

                Vector3[] verts = body.original;
                float cell = body.cell;
                Task.Run(() => BuildGrid(verts, cell)).ContinueWith(t => body.grid = t.Result, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        public override void OnDestroy()
        {
            foreach (Body body in parts)
                if (body.mesh != null)
                    Destroy(body.mesh);
            base.OnDestroy();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsSpawned && !IsServer) return;
            if (Time.time < nextHitTime || collision.contactCount == 0) return;

            // Средняя точка касаний и нормаль, развёрнутая внутрь машины
            Vector3 point = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++)
                point += collision.GetContact(i).point;
            point /= collision.contactCount;
            Vector3 normal = collision.GetContact(0).normal;
            Rigidbody own = GetComponent<Rigidbody>();
            Vector3 center = own != null ? own.worldCenterOfMass : transform.position;
            if (Vector3.Dot(normal, center - point) < 0f)
                normal = -normal;
            // Касание днищем о землю (приземление, бордюр) не мнёт
            if (Vector3.Dot(normal, transform.up) > 0.75f) return;

            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, normal));
            if (speed < minImpactSpeed) return;

            float strength = 1f;
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic)
                strength = Mathf.Clamp01(other.mass / fullDentMass);
            float depth = Mathf.Min(maxDepth, (speed - minImpactSpeed) * depthPerSpeed * strength + 0.04f * strength);
            if (depth < 0.02f) return;

            nextHitTime = Time.time + hitCooldown;
            float radius = Mathf.Lerp(minRadius, maxRadius, depth / maxDepth);
            Vector3 localPoint = transform.InverseTransformPoint(point);
            Vector3 localDir = transform.InverseTransformDirection(normal);
            if (IsSpawned)
                DentRpc(localPoint, localDir, depth, radius);
            else
                Dent(localPoint, localDir, depth, radius);
        }

        [Rpc(SendTo.Everyone)]
        private void DentRpc(Vector3 localPoint, Vector3 localDir, float depth, float radius)
        {
            Dent(localPoint, localDir, depth, radius);
        }

        // Точка и направление (внутрь машины) — в осях корня машины, глубина и радиус — в метрах
        public void Dent(Vector3 localPoint, Vector3 localDir, float depth, float radius)
        {
            Vector3 worldPoint = transform.TransformPoint(localPoint);
            Vector3 worldDir = transform.TransformDirection(localDir).normalized;
            foreach (Body body in parts)
                DentBody(body, worldPoint, worldDir, depth, radius);
        }

        private void LateUpdate()
        {
            // Вмятины кадра выгружаются в сетку один раз
            foreach (Body body in parts)
            {
                if (!body.dirty) continue;
                body.dirty = false;
                body.mesh.SetVertices(body.vertices);
                if (body.normals != null)
                    body.mesh.SetNormals(body.normals);
                body.mesh.RecalculateBounds();
            }
        }

        private void DentBody(Body body, Vector3 worldPoint, Vector3 worldDir, float depth, float radius)
        {
            Transform t = body.filter.transform;
            float scale = Mathf.Max(1e-4f, MaxAbs(t.lossyScale));
            Vector3 p = t.InverseTransformPoint(worldPoint);
            Vector3 dir = t.InverseTransformDirection(worldDir).normalized;
            float r = radius / scale;
            float d = depth / scale;
            float maxOffset = maxTotalDent / scale;
            float r2 = r * r;

            if (body.grid != null)
            {
                Vector3Int lo = CellOf(p - Vector3.one * r, body.cell);
                Vector3Int hi = CellOf(p + Vector3.one * r, body.cell);
                for (int x = lo.x; x <= hi.x; x++)
                for (int y = lo.y; y <= hi.y; y++)
                for (int z = lo.z; z <= hi.z; z++)
                {
                    if (!body.grid.TryGetValue(new Vector3Int(x, y, z), out List<int> cell)) continue;
                    foreach (int i in cell)
                        DentVertex(body, i, p, dir, d, r, r2, maxOffset);
                }
            }
            else
            {
                // Раскладка ещё строится — перебор всех вершин
                for (int i = 0; i < body.vertices.Length; i++)
                    DentVertex(body, i, p, dir, d, r, r2, maxOffset);
            }
            body.dirty = true;
        }

        private static void DentVertex(Body body, int i, Vector3 p, Vector3 dir, float depth, float r, float r2, float maxOffset)
        {
            // Близость меряем по исходному месту вершины: так соседние вершины кузова не разъезжаются
            Vector3 rest = body.original[i];
            Vector3 delta = rest - p;
            float dist2 = delta.sqrMagnitude;
            if (dist2 >= r2) return;

            // Купол (1 − x²)²: глубже всего в центре, у края плавно сходит на нет
            float x = Mathf.Sqrt(dist2) / r;
            float k = 1f - x * x;
            Vector3 moved = body.vertices[i] + dir * (depth * k * k);
            Vector3 offset = moved - rest;
            if (offset.sqrMagnitude > maxOffset * maxOffset)
                moved = rest + offset.normalized * maxOffset;
            body.vertices[i] = moved;

            if (body.normals == null) return;
            // Стенка чаши смотрит к центру вмятины: наклон по производной купола
            Vector3 radial = delta - dir * Vector3.Dot(delta, dir);
            if (radial.sqrMagnitude < 1e-8f) return;
            float slope = depth * 4f * x * k / r;
            Vector3 n = body.normals[i];
            float facing = Vector3.Dot(n, -dir) >= 0f ? 1f : -1f;
            body.normals[i] = (n - radial.normalized * (slope * facing)).normalized;
        }

        private static Dictionary<Vector3Int, List<int>> BuildGrid(Vector3[] vertices, float cell)
        {
            var grid = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = CellOf(vertices[i], cell);
                if (!grid.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>();
                    grid.Add(key, list);
                }
                list.Add(i);
            }
            return grid;
        }

        private static Vector3Int CellOf(Vector3 v, float cell)
        {
            return new Vector3Int(Mathf.FloorToInt(v.x / cell), Mathf.FloorToInt(v.y / cell), Mathf.FloorToInt(v.z / cell));
        }

        private static float MaxAbs(Vector3 v)
        {
            return Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
        }
    }
}
