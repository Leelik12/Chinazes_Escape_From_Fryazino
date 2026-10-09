using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Car
{
    // Вмятины на кузове от ударов и отрыв деталей. Столкновения ловит сервер (физику машин считает хост) и рассылает удар
    // обоим игрокам в осях машины, каждый мнёт свою копию сетки одинаково. Вершины рядом с точкой удара
    // вдавливаются куполом, а нормали наклоняются к центру вмятины, поэтому её видно и без пересчёта нормалей
    // всей сетки. Для поиска ближних вершин сетка заранее раскладывается по ячейкам (в фоновом потоке).
    // Детали: сетка кузова делится на «острова» — несвязанные куски геометрии (бампер, зеркало, капот, фара).
    // Удары рядом копят урон острова, и когда он набран, остров вырезается из кузова и улетает обломком (CarDebris).
    // Урон считается из тех же ударов, что и вмятины, поэтому у обоих игроков отрываются одни и те же детали
    public class CarDeformation : NetworkBehaviour
    {
        [Tooltip("Видимые сетки кузова, которые мнутся (нужен Read/Write в импорте модели)")]
        [SerializeField] private MeshFilter[] bodies = new MeshFilter[0];

        [Header("Удар")]
        [Tooltip("Удары со скоростью сближения меньше этой не оставляют вмятин, м/с")]
        [SerializeField] private float minImpactSpeed = 3f;
        // Машины в проекте примерно вдвое больше настоящих, поэтому вмятины крупные
        [Tooltip("Глубина вмятины на каждый м/с сверх порога, м")]
        [SerializeField] private float depthPerSpeed = 0.045f;
        [SerializeField] private float maxDepth = 0.55f;
        [Tooltip("Радиус вмятины: от минимального при слабом ударе до максимального при сильном, м")]
        [SerializeField] private float minRadius = 0.7f;
        [SerializeField] private float maxRadius = 1.8f;
        [Tooltip("Дальше этого вершина от исходного места не уходит, сколько ни бей, м")]
        [SerializeField] private float maxTotalDent = 0.9f;
        [Tooltip("Лёгкие предметы мнут слабее: полная вмятина от тел тяжелее этого, кг")]
        [SerializeField] private float fullDentMass = 800f;
        [Tooltip("Пауза между вмятинами, с: машины после удара ещё трутся друг о друга")]
        [SerializeField] private float hitCooldown = 0.3f;

        [Header("Отрыв деталей")]
        [Tooltip("Сколько урона выдерживает деталь: сумма глубин вмятин рядом с ней, м. Сильный удар отрывает сразу, слабые — за несколько раз")]
        [SerializeField] private float partStrength = 0.4f;
        [Tooltip("Деталь получает урон, если её ближайшая вершина не дальше этого от точки удара, м")]
        [SerializeField] private float partReach = 0.8f;
        [Tooltip("Размер отрываемой детали по наибольшей стороне, м: мельче — остаются (или летят вместе с соседом), крупнее — это сам кузов")]
        [SerializeField] private float minPartSize = 0.2f;
        [SerializeField] private float maxPartSize = 4.2f;
        [Tooltip("Больше этого за один удар не отрывается")]
        [SerializeField] private int maxPartsPerHit = 3;
        [Tooltip("Сколько обломок лежит на дороге, прежде чем исчезнуть, с")]
        [SerializeField] private float debrisLifetime = 25f;

        private const float CellSize = 0.4f;
        // Вершины ближе этого считаются одной точкой (швы UV и нормалей режут сетку, но деталь от этого не делится)
        private const float WeldPrecision = 1000f;

        private class Island
        {
            public Vector3 min = Vector3.positiveInfinity;
            public Vector3 max = Vector3.negativeInfinity;
            public int vertexCount;
            public bool detachable;
            public float damage;
            public bool detached;
        }

        private class Layout
        {
            public Dictionary<Vector3Int, List<int>> grid;
            public int[] vertexIsland;
            public Island[] islands;
        }

        private class Body
        {
            public MeshFilter filter;
            public Mesh mesh;
            public Vector3[] original;
            public Vector3[] vertices;
            public Vector3[] normals;
            public int[][] triangles;
            // UV, касательные и цвета для обломков; читаются при первом отрыве
            public CarDebris.SourceChannels channels;
            // Ячейки сетки и острова; null, пока раскладка не готова
            public Layout layout;
            public float cell;
            public bool dirty;
            public bool trianglesDirty;
            // Удары, пришедшие до готовности раскладки: урон деталям добавится, когда она будет готова
            public List<Vector4> pendingHits = new List<Vector4>();
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
                    triangles = new int[mesh.subMeshCount][],
                };
                body.original = (Vector3[])body.vertices.Clone();
                if (body.normals.Length != body.vertices.Length)
                    body.normals = null;
                for (int s = 0; s < mesh.subMeshCount; s++)
                    body.triangles[s] = mesh.GetTriangles(s);
                float scale = Mathf.Max(1e-4f, MaxAbs(filter.transform.lossyScale));
                body.cell = CellSize / scale;
                parts.Add(body);

                Vector3[] verts = body.original;
                int[][] tris = body.triangles;
                float cell = body.cell;
                Vector3 lossy = filter.transform.lossyScale;
                float minSize = minPartSize, maxSize = maxPartSize;
                Task.Run(() => BuildLayout(verts, tris, cell, lossy, minSize, maxSize))
                    .ContinueWith(t => OnLayoutReady(body, t.Result), TaskScheduler.FromCurrentSynchronizationContext());
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
            // Свои же обломки не мнут
            if (collision.collider.GetComponentInParent<CarDebris>() != null) return;

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
            {
                DentBody(body, worldPoint, worldDir, depth, radius);
                Vector3 meshPoint = body.filter.transform.InverseTransformPoint(worldPoint);
                if (body.layout != null)
                    DamageParts(body, meshPoint, depth, radius);
                else
                    body.pendingHits.Add(new Vector4(meshPoint.x, meshPoint.y, meshPoint.z, depth));
            }
        }

        private void LateUpdate()
        {
            // Вмятины кадра выгружаются в сетку один раз
            foreach (Body body in parts)
            {
                if (body.trianglesDirty)
                {
                    body.trianglesDirty = false;
                    for (int s = 0; s < body.triangles.Length; s++)
                        body.mesh.SetTriangles(body.triangles[s], s, false);
                }
                if (!body.dirty) continue;
                body.dirty = false;
                body.mesh.SetVertices(body.vertices);
                if (body.normals != null)
                    body.mesh.SetNormals(body.normals);
                body.mesh.RecalculateBounds();
            }
        }

        private void OnLayoutReady(Body body, Layout layout)
        {
            if (this == null) return;
            body.layout = layout;
            // Удары до готовности раскладки: радиус для урона берётся средний, этого достаточно
            foreach (Vector4 hit in body.pendingHits)
                DamageParts(body, new Vector3(hit.x, hit.y, hit.z), hit.w, Mathf.Lerp(minRadius, maxRadius, hit.w / maxDepth));
            body.pendingHits.Clear();
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

            if (body.layout != null)
            {
                foreach (int i in Nearby(body, p, r))
                    DentVertex(body, i, p, dir, d, r, r2, maxOffset);
            }
            else
            {
                // Раскладка ещё строится — перебор всех вершин
                for (int i = 0; i < body.vertices.Length; i++)
                    DentVertex(body, i, p, dir, d, r, r2, maxOffset);
            }
            body.dirty = true;
        }

        // Вершины из ячеек, задевающих куб со стороной 2r вокруг точки (в осях меша)
        private static IEnumerable<int> Nearby(Body body, Vector3 p, float r)
        {
            Vector3Int lo = CellOf(p - Vector3.one * r, body.cell);
            Vector3Int hi = CellOf(p + Vector3.one * r, body.cell);
            for (int x = lo.x; x <= hi.x; x++)
            for (int y = lo.y; y <= hi.y; y++)
            for (int z = lo.z; z <= hi.z; z++)
            {
                if (!body.layout.grid.TryGetValue(new Vector3Int(x, y, z), out List<int> cell)) continue;
                foreach (int i in cell)
                    yield return i;
            }
        }

        // Урон деталям рядом с ударом: чем ближе деталь, тем больше. Точка — в осях меша, глубина и радиус — в метрах
        private void DamageParts(Body body, Vector3 p, float depth, float radius)
        {
            Layout layout = body.layout;
            float scale = Mathf.Max(1e-4f, MaxAbs(body.filter.transform.lossyScale));
            float reach = partReach / scale;
            float reach2 = reach * reach;

            // Ближайшая вершина каждой задетой детали
            var closest = new Dictionary<int, float>();
            foreach (int i in Nearby(body, p, reach))
            {
                int island = layout.vertexIsland[i];
                Island info = layout.islands[island];
                if (!info.detachable || info.detached) continue;
                float dist2 = (body.original[i] - p).sqrMagnitude;
                if (dist2 >= reach2) continue;
                if (!closest.TryGetValue(island, out float best) || dist2 < best)
                    closest[island] = dist2;
            }

            var broken = new List<int>();
            foreach (KeyValuePair<int, float> pair in closest)
            {
                Island info = layout.islands[pair.Key];
                float distance = Mathf.Sqrt(pair.Value) * scale;
                info.damage += depth * (1f - 0.5f * distance / partReach);
                if (info.damage >= partStrength)
                    broken.Add(pair.Key);
            }
            if (broken.Count == 0) return;

            // Сильнее всех пострадавшие отрываются первыми, остальные ждут следующего удара
            broken.Sort((a, b) => layout.islands[b].damage.CompareTo(layout.islands[a].damage));
            if (broken.Count > maxPartsPerHit)
                broken.RemoveRange(maxPartsPerHit, broken.Count - maxPartsPerHit);
            Detach(body, broken);
        }

        // Вырезает детали (и мелочь внутри их габарита: болты, накладки) из кузова и бросает каждую обломком.
        // Треугольники кузова перебираются один раз на все детали удара: у крупных сеток их сотни тысяч
        private void Detach(Body body, List<int> broken)
        {
            Layout layout = body.layout;
            float pad = 0.05f / Mathf.Max(1e-4f, MaxAbs(body.filter.transform.lossyScale));
            // Остров → номер обломка
            var group = new Dictionary<int, int>();
            for (int g = 0; g < broken.Count; g++)
            {
                Island main = layout.islands[broken[g]];
                group[broken[g]] = g;
                var bounds = new Bounds();
                bounds.SetMinMax(main.min - Vector3.one * pad, main.max + Vector3.one * pad);
                for (int i = 0; i < layout.islands.Length; i++)
                {
                    Island other = layout.islands[i];
                    if (other.detached || other.detachable || group.ContainsKey(i)) continue;
                    if (bounds.Contains(other.min) && bounds.Contains(other.max))
                        group[i] = g;
                }
            }
            foreach (int i in group.Keys)
                layout.islands[i].detached = true;

            // Треугольники деталей уходят в обломки, остальные остаются в кузове
            int subMeshes = body.triangles.Length;
            var remaps = new Dictionary<int, int>[broken.Count];
            var debrisVertices = new List<int>[broken.Count];
            var debrisTriangles = new List<int>[broken.Count][];
            for (int g = 0; g < broken.Count; g++)
            {
                remaps[g] = new Dictionary<int, int>();
                debrisVertices[g] = new List<int>();
                debrisTriangles[g] = new List<int>[subMeshes];
            }
            for (int s = 0; s < subMeshes; s++)
            {
                int[] tris = body.triangles[s];
                List<int> keep = null;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    if (!group.TryGetValue(layout.vertexIsland[tris[i]], out int g))
                    {
                        if (keep != null)
                        {
                            keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
                        }
                        continue;
                    }
                    if (keep == null)
                    {
                        keep = new List<int>(tris.Length);
                        for (int j = 0; j < i; j++) keep.Add(tris[j]);
                    }
                    List<int> cut = debrisTriangles[g][s] ??= new List<int>();
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[i + k];
                        if (!remaps[g].TryGetValue(v, out int nv))
                        {
                            nv = debrisVertices[g].Count;
                            remaps[g].Add(v, nv);
                            debrisVertices[g].Add(v);
                        }
                        cut.Add(nv);
                    }
                }
                if (keep == null) continue;
                body.triangles[s] = keep.ToArray();
                body.trianglesDirty = true;
            }

            body.channels ??= CarDebris.SourceChannels.Read(body.mesh);
            Rigidbody car = GetComponent<Rigidbody>();
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int g = 0; g < broken.Count; g++)
                if (debrisVertices[g].Count > 0)
                    CarDebris.Spawn(body.filter, body.mesh, body.channels, body.vertices, body.normals, debrisVertices[g], debrisTriangles[g],
                        car, colliders, debrisLifetime);
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

        // Фоновый поток: ячейки для поиска вершин и деление сетки на острова (связность по треугольникам и совпадающим точкам)
        private static Layout BuildLayout(Vector3[] vertices, int[][] triangles, float cell, Vector3 lossyScale, float minSize, float maxSize)
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

            int n = vertices.Length;
            var parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            var weld = new Dictionary<Vector3Int, int>(n);
            for (int i = 0; i < n; i++)
            {
                Vector3 v = vertices[i];
                var key = new Vector3Int(Mathf.RoundToInt(v.x * WeldPrecision), Mathf.RoundToInt(v.y * WeldPrecision), Mathf.RoundToInt(v.z * WeldPrecision));
                if (weld.TryGetValue(key, out int j)) Union(parent, i, j);
                else weld.Add(key, i);
            }
            foreach (int[] tris in triangles)
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Union(parent, tris[i], tris[i + 1]);
                    Union(parent, tris[i], tris[i + 2]);
                }

            var ids = new Dictionary<int, int>();
            var islands = new List<Island>();
            var vertexIsland = new int[n];
            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                if (!ids.TryGetValue(root, out int id))
                {
                    id = islands.Count;
                    ids.Add(root, id);
                    islands.Add(new Island());
                }
                vertexIsland[i] = id;
                Island island = islands[id];
                island.vertexCount++;
                island.min = Vector3.Min(island.min, vertices[i]);
                island.max = Vector3.Max(island.max, vertices[i]);
            }
            // Отрывается не мелочь и не сам кузов: по размеру в метрах и не больше четверти всех вершин
            foreach (Island island in islands)
            {
                Vector3 size = Vector3.Scale(island.max - island.min, lossyScale);
                float largest = Mathf.Max(Mathf.Abs(size.x), Mathf.Max(Mathf.Abs(size.y), Mathf.Abs(size.z)));
                island.detachable = largest >= minSize && largest <= maxSize && island.vertexCount <= n / 4;
            }
            return new Layout { grid = grid, vertexIsland = vertexIsland, islands = islands.ToArray() };
        }

        private static int Find(int[] parent, int a)
        {
            while (parent[a] != a)
            {
                parent[a] = parent[parent[a]];
                a = parent[a];
            }
            return a;
        }

        private static void Union(int[] parent, int a, int b)
        {
            a = Find(parent, a);
            b = Find(parent, b);
            if (a != b) parent[a] = b;
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
