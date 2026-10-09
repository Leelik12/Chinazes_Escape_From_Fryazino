using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingProject.Car
{
    // Оторванная деталь кузова (её вырезает CarDeformation): своя сетка из треугольников детали, коробка-коллайдер
    // и физика. Не сетевой объект — каждый игрок бросает свою копию. Полежав, уменьшается и исчезает;
    // обломков в мире не больше MaxAlive, лишние убираются начиная со старых
    public class CarDebris : MonoBehaviour
    {
        private const int MaxAlive = 40;
        private const float ShrinkTime = 1f;
        private static readonly Queue<CarDebris> alive = new Queue<CarDebris>();

        // Каналы исходной сетки, кроме позиций и нормалей (их мнёт CarDeformation): читаются один раз на кузов,
        // у крупных сеток это сотни тысяч вершин
        public class SourceChannels
        {
            public Vector2[] uv0;
            public Vector2[] uv1;
            public Vector4[] tangents;
            public Color32[] colors;

            public static SourceChannels Read(Mesh mesh)
            {
                return new SourceChannels
                {
                    uv0 = mesh.HasVertexAttribute(VertexAttribute.TexCoord0) ? mesh.uv : null,
                    uv1 = mesh.HasVertexAttribute(VertexAttribute.TexCoord1) ? mesh.uv2 : null,
                    tangents = mesh.HasVertexAttribute(VertexAttribute.Tangent) ? mesh.tangents : null,
                    colors = mesh.HasVertexAttribute(VertexAttribute.Color) ? mesh.colors32 : null,
                };
            }
        }

        private Mesh mesh;
        private float lifetime;
        private float age;
        private Vector3 fullScale;

        // vertexIndices — вершины исходной сетки, triangles — индексы в этом списке по подсеткам (null — подсетка пуста)
        public static CarDebris Spawn(MeshFilter source, Mesh sourceMesh, SourceChannels channels, Vector3[] vertices, Vector3[] normals,
            List<int> vertexIndices, List<int>[] triangles, Rigidbody car, Collider[] carColliders, float lifetime)
        {
            int count = vertexIndices.Count;
            var positions = new Vector3[count];
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            for (int i = 0; i < count; i++)
            {
                positions[i] = vertices[vertexIndices[i]];
                min = Vector3.Min(min, positions[i]);
                max = Vector3.Max(max, positions[i]);
            }
            // Опорная точка — центр детали, чтобы она вращалась вокруг себя
            Vector3 center = (min + max) * 0.5f;
            for (int i = 0; i < count; i++)
                positions[i] -= center;

            var mesh = new Mesh { name = sourceMesh.name + " debris" };
            if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = positions;
            if (normals != null) mesh.normals = Pick(normals, vertexIndices);
            if (channels.uv0 != null) mesh.uv = Pick(channels.uv0, vertexIndices);
            if (channels.uv1 != null) mesh.uv2 = Pick(channels.uv1, vertexIndices);
            if (channels.tangents != null) mesh.tangents = Pick(channels.tangents, vertexIndices);
            if (channels.colors != null) mesh.colors32 = Pick(channels.colors, vertexIndices);

            Material[] sourceMaterials = source.GetComponent<Renderer>() != null ? source.GetComponent<Renderer>().sharedMaterials : new Material[0];
            var materials = new List<Material>();
            var used = new List<List<int>>();
            for (int s = 0; s < triangles.Length; s++)
            {
                if (triangles[s] == null) continue;
                used.Add(triangles[s]);
                materials.Add(s < sourceMaterials.Length ? sourceMaterials[s] : null);
            }
            mesh.subMeshCount = used.Count;
            for (int s = 0; s < used.Count; s++)
                mesh.SetTriangles(used[s], s, false);
            mesh.RecalculateBounds();

            Transform t = source.transform;
            var go = new GameObject("Debris " + sourceMesh.name);
            go.layer = t.gameObject.layer;
            go.transform.SetPositionAndRotation(t.TransformPoint(center), t.rotation);
            go.transform.localScale = t.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials.ToArray();

            // Коробка по габариту, не тоньше 10 см: плоские детали иначе проваливаются сквозь дорогу
            var box = go.AddComponent<BoxCollider>();
            Vector3 scale = t.lossyScale;
            Vector3 size = max - min;
            for (int a = 0; a < 3; a++)
                size[a] = Mathf.Max(size[a], 0.1f / Mathf.Max(1e-4f, Mathf.Abs(scale[a])));
            box.size = size;
            foreach (Collider collider in carColliders)
                if (collider != null && !collider.isTrigger)
                    Physics.IgnoreCollision(box, collider);

            Vector3 worldSize = Vector3.Scale(size, scale);
            float volume = Mathf.Abs(worldSize.x * worldSize.y * worldSize.z);
            var body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(volume * 60f, 10f, 120f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            // Деталь сохраняет скорость машины и отскакивает от неё вбок и вверх
            Vector3 position = go.transform.position;
            Vector3 velocity = Vector3.zero;
            Vector3 outward = Vector3.up;
            if (car != null)
            {
                if (!car.isKinematic) velocity = car.GetPointVelocity(position);
                outward = Vector3.ProjectOnPlane(position - car.worldCenterOfMass, Vector3.up).normalized;
            }
            body.linearVelocity = velocity * 0.85f + outward * Random.Range(2f, 4f) + Vector3.up * Random.Range(1.5f, 3.5f);
            body.angularVelocity = Random.insideUnitSphere * 5f;

            var debris = go.AddComponent<CarDebris>();
            debris.mesh = mesh;
            debris.lifetime = lifetime;
            debris.fullScale = go.transform.localScale;
            alive.Enqueue(debris);
            while (alive.Count > MaxAlive)
            {
                CarDebris oldest = alive.Dequeue();
                if (oldest != null) Destroy(oldest.gameObject);
            }
            return debris;
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age < lifetime) return;
            float k = 1f - (age - lifetime) / ShrinkTime;
            if (k <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            transform.localScale = fullScale * k;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        private static T[] Pick<T>(T[] source, List<int> indices)
        {
            var result = new T[indices.Count];
            for (int i = 0; i < result.Length; i++) result[i] = source[indices[i]];
            return result;
        }
    }
}
