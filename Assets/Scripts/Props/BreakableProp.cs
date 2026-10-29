using System.Collections.Generic;
using RacingProject.Audio;
using RacingProject.Car;
using RacingProject.Network;
using UnityEngine;

namespace RacingProject.Props
{
    // Разрушаемая постройка-мелочь (ларёк, остановка, будка, сарай, ворота, деревянный столб, билборд, поленница):
    // стоит неподвижно, как стена, но от удара тяжёлой машины на скорости или взрыва рядом разваливается на куски,
    // а машина проезжает дальше, потеряв часть скорости. Куски режутся из меша на лету (по клеткам сетки) и летят
    // по физике у каждого игрока своей, как LooseProp. Удар машины засчитывает тот, кто считает её физику (хост),
    // и рассылает через PropNetwork; взрыв ломает предметы у каждого игрока на месте (LooseProp.Blast)
    public class BreakableProp : MonoBehaviour
    {
        [Tooltip("Ломается от удара со скоростью по нормали не меньше этой, м/с")]
        [SerializeField] private float breakSpeed = 9f;
        [Tooltip("Доля скорости, которая остаётся у машины после пролома")]
        [SerializeField, Range(0f, 1f)] private float keepSpeed = 0.75f;
        [Tooltip("Ломается от взрыва ближе этого, м")]
        [SerializeField] private float blastRadius = 7f;
        [Tooltip("Масса всех кусков вместе, кг")]
        [SerializeField] private float debrisMass = 300f;
        [Tooltip("Примерный размер куска, м")]
        [SerializeField] private float chunkSize = 1.6f;
        [Tooltip("Есть стёкла — при поломке звенят")]
        [SerializeField] private bool glass;
        [Tooltip("Исходные меши модели (у статичного рендерера в игре вместо них склеенный меш сцены)")]
        [SerializeField] private MeshFilter[] parts = new MeshFilter[0];
        [SerializeField] private Mesh[] sourceMeshes = new Mesh[0];

        // Тела легче этого предмет не ломают (сами куски, бочки, конусы), кг
        private const float MinBreakerMass = 500f;
        private const float DebrisLifetime = 25f;
        private const int MaxDebris = 260;

        private static readonly List<BreakableProp> All = new List<BreakableProp>();
        private static readonly Queue<GameObject> Debris = new Queue<GameObject>();
        private static readonly List<Collider> CarColliders = new List<Collider>();
        // Обломки не задерживают машину, которая проломила постройку: столько секунд они проходят сквозь неё
        private const float PassThroughTime = 2f;

        public int Index { get; set; } = -1;
        public bool Broken { get; private set; }
        public float BreakSpeed => breakSpeed;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        // Порядок для сети: позиции статичных построек у обоих игроков одинаковы
        public static List<BreakableProp> Ordered()
        {
            var list = new List<BreakableProp>(FindObjectsByType<BreakableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            list.Sort((a, b) =>
            {
                Vector3 pa = a.transform.position, pb = b.transform.position;
                int c = Mathf.RoundToInt(pa.x * 10f).CompareTo(Mathf.RoundToInt(pb.x * 10f));
                if (c != 0) return c;
                c = Mathf.RoundToInt(pa.z * 10f).CompareTo(Mathf.RoundToInt(pb.z * 10f));
                return c != 0 ? c : Mathf.RoundToInt(pa.y * 10f).CompareTo(Mathf.RoundToInt(pb.y * 10f));
            });
            return list;
        }

        private void OnCollisionEnter(Collision collision) => OnHit(collision);

        // Удар по коллайдеру постройки (свой или переданный BreakableContact с дочерней модели)
        public void OnHit(Collision collision)
        {
            if (Broken || !LocalPlayerRole.SimulatesPhysics || collision.contactCount == 0) return;
            Rigidbody other = collision.rigidbody;
            if (other == null || other.isKinematic || other.mass < MinBreakerMass) return;
            ContactPoint contact = collision.GetContact(0);
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed < breakSpeed) return;

            // Удар уже погасил скорость машины: возвращаем её долю, будто постройка не устояла
            CarCrashDamage crash = other.GetComponent<CarCrashDamage>();
            Vector3 before = crash != null ? crash.LastVelocity : -collision.relativeVelocity;
            other.linearVelocity = before * keepSpeed;
            if (PropNetwork.Instance != null && Index >= 0)
                PropNetwork.Instance.BreakProp(this, before, contact.point);
            else
                Break(before, contact.point);
        }

        // У каждого игрока: постройка исчезает, на её месте разлетаются куски
        public void Break(Vector3 velocity, Vector3 point)
        {
            if (Broken) return;
            Broken = true;
            foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            CarColliders.Clear();
            CarCrashDamage car = FindFirstObjectByType<CarCrashDamage>();
            if (car != null)
                foreach (Collider collider in car.GetComponentsInChildren<Collider>())
                    if (!collider.isTrigger && !(collider is WheelCollider)) CarColliders.Add(collider);
            for (int i = 0; i < parts.Length && i < sourceMeshes.Length; i++)
                if (parts[i] != null && sourceMeshes[i] != null)
                    SpawnChunks(parts[i], sourceMeshes[i], velocity, point);
            SoundLibrary.Play(SoundLibrary.Sfx.ImpactHeavy, point, 1f, 0.8f);
            SoundLibrary.Play(SoundLibrary.Sfx.DebrisClatter, point, 1f, 0.9f);
            if (glass) SoundLibrary.Play(SoundLibrary.Sfx.Glass, point, 0.8f, 0.9f);
        }

        // Взрыв рядом: ломает у каждого игрока на месте (одинаково — взрывы рассылаются всем)
        public static void BlastBreak(Vector3 point, float scale)
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                BreakableProp prop = All[i];
                if (prop.Broken) continue;
                Vector3 away = prop.transform.position - point;
                float radius = prop.blastRadius * scale;
                if (away.sqrMagnitude > radius * radius) continue;
                away.y = 0f;
                prop.Break(away.normalized * 6f + Vector3.up * 4f, point);
            }
        }

        private void SpawnChunks(MeshFilter part, Mesh mesh, Vector3 velocity, Vector3 point)
        {
            if (!mesh.isReadable) return;
            Transform source = part.transform;
            Material[] materials = part.GetComponent<MeshRenderer>().sharedMaterials;
            Bounds bounds = mesh.bounds;
            Vector3 scale = source.lossyScale;
            // Сколько клеток по каждой оси: куски примерно chunkSize в мире
            int nx = Mathf.Clamp(Mathf.RoundToInt(bounds.size.x * Mathf.Abs(scale.x) / chunkSize), 1, 4);
            int ny = Mathf.Clamp(Mathf.RoundToInt(bounds.size.y * Mathf.Abs(scale.y) / chunkSize), 1, 5);
            int nz = Mathf.Clamp(Mathf.RoundToInt(bounds.size.z * Mathf.Abs(scale.z) / chunkSize), 1, 4);
            int cells = nx * ny * nz;

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            var chunkVerts = new List<Vector3>[cells];
            var chunkNormals = new List<Vector3>[cells];
            var chunkUvs = new List<Vector2>[cells];
            var chunkTris = new List<int>[cells, mesh.subMeshCount];
            var remap = new Dictionary<int, int>[cells];
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 c = (vertices[tris[t]] + vertices[tris[t + 1]] + vertices[tris[t + 2]]) / 3f - bounds.min;
                    int cx = Mathf.Clamp((int)(c.x / bounds.size.x * nx), 0, nx - 1);
                    int cy = Mathf.Clamp((int)(c.y / Mathf.Max(1e-4f, bounds.size.y) * ny), 0, ny - 1);
                    int cz = Mathf.Clamp((int)(c.z / Mathf.Max(1e-4f, bounds.size.z) * nz), 0, nz - 1);
                    int cell = (cx * ny + cy) * nz + cz;
                    if (chunkVerts[cell] == null)
                    {
                        chunkVerts[cell] = new List<Vector3>();
                        chunkNormals[cell] = new List<Vector3>();
                        chunkUvs[cell] = new List<Vector2>();
                        remap[cell] = new Dictionary<int, int>();
                    }
                    if (chunkTris[cell, sub] == null) chunkTris[cell, sub] = new List<int>();
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[t + k];
                        if (!remap[cell].TryGetValue(v, out int index))
                        {
                            index = chunkVerts[cell].Count;
                            remap[cell][v] = index;
                            chunkVerts[cell].Add(vertices[v]);
                            chunkNormals[cell].Add(normals.Length > v ? normals[v] : Vector3.up);
                            chunkUvs[cell].Add(uvs.Length > v ? uvs[v] : Vector2.zero);
                        }
                        chunkTris[cell, sub].Add(index);
                    }
                }
            }

            int used = 0;
            for (int cell = 0; cell < cells; cell++) if (chunkVerts[cell] != null) used++;
            for (int cell = 0; cell < cells; cell++)
            {
                if (chunkVerts[cell] == null) continue;
                // Вершины куска — вокруг его центра, чтобы тело вращалось вокруг себя
                Vector3 center = Vector3.zero;
                foreach (Vector3 v in chunkVerts[cell]) center += v;
                center /= chunkVerts[cell].Count;
                var local = new List<Vector3>(chunkVerts[cell].Count);
                foreach (Vector3 v in chunkVerts[cell]) local.Add(v - center);

                var chunkMesh = new Mesh { name = mesh.name + " chunk" };
                if (local.Count > 65000) chunkMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                chunkMesh.SetVertices(local);
                chunkMesh.SetNormals(chunkNormals[cell]);
                chunkMesh.SetUVs(0, chunkUvs[cell]);
                var subs = new List<int>();
                var mats = new List<Material>();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    if (chunkTris[cell, sub] != null) subs.Add(sub);
                chunkMesh.subMeshCount = subs.Count;
                for (int s = 0; s < subs.Count; s++)
                {
                    chunkMesh.SetTriangles(chunkTris[cell, subs[s]], s);
                    mats.Add(materials[Mathf.Min(subs[s], materials.Length - 1)]);
                }
                chunkMesh.RecalculateBounds();

                var go = new GameObject("Debris " + name, typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = gameObject.layer;
                go.transform.SetPositionAndRotation(source.TransformPoint(center), source.rotation);
                go.transform.localScale = scale;
                go.GetComponent<MeshFilter>().sharedMesh = chunkMesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = mats.ToArray();
                var box = go.AddComponent<BoxCollider>();
                box.center = chunkMesh.bounds.center;
                box.size = Vector3.Max(chunkMesh.bounds.size, Vector3.one * 0.05f);
                foreach (Collider carCollider in CarColliders) Physics.IgnoreCollision(box, carCollider, true);
                var body = go.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(5f, debrisMass / used);
                // Куски у удара летят по ходу машины, дальние — слабее; сверху — чуть выше
                Vector3 position = go.transform.position;
                float near = Mathf.Clamp01(1f - (position - point).magnitude / 6f);
                body.linearVelocity = velocity * Random.Range(0.35f, 0.8f) * (0.4f + 0.6f * near)
                    + Random.insideUnitSphere * 1.5f + Vector3.up * Random.Range(0.5f, 2.5f) * near;
                body.angularVelocity = Random.insideUnitSphere * 4f;
                go.AddComponent<DebrisPiece>().Init(chunkMesh, DebrisLifetime, box, CarColliders, PassThroughTime);
                Debris.Enqueue(go);
                while (Debris.Count > MaxDebris)
                {
                    GameObject old = Debris.Dequeue();
                    if (old != null) Destroy(old);
                }
            }
        }
    }

    // Кусок разрушенной постройки: через время тонет в земле и исчезает вместе со своим мешем
    public class DebrisPiece : MonoBehaviour
    {
        private Mesh mesh;
        private float sinkAt;
        private float sinkEnd;
        private Collider own;
        private Collider[] ignored;
        private float solidAt;

        public void Init(Mesh chunkMesh, float lifetime, Collider collider, List<Collider> passThrough, float passTime)
        {
            mesh = chunkMesh;
            sinkAt = Time.time + lifetime;
            sinkEnd = sinkAt + 3f;
            own = collider;
            ignored = passThrough.ToArray();
            solidAt = Time.time + passTime;
        }

        private void Update()
        {
            // Машина уже проехала: обломок снова твёрдый и для неё
            if (ignored != null && Time.time >= solidAt)
            {
                foreach (Collider carCollider in ignored)
                    if (carCollider != null && own != null) Physics.IgnoreCollision(own, carCollider, false);
                ignored = null;
            }
            if (Time.time < sinkAt) return;
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                body.isKinematic = true;
                foreach (Collider collider in GetComponents<Collider>()) collider.enabled = false;
            }
            transform.position += Vector3.down * Time.deltaTime * 0.6f;
            if (Time.time >= sinkEnd) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
