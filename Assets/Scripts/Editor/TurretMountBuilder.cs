using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingProject.EditorTools
{
    // Пулемётная позиция на крыше Tahoe: меши собираются здесь из коробок и цилиндров и сохраняются
    // в ассеты, чтобы их можно было пересобрать после правки размеров.
    // Размеры — в локальных осях объекта VolgaCar/RoofTurret (центр кольца люка), у машины масштаб 2.
    // Пулемёт стоит на кольце на расстоянии MountRadius от центра, ось его наклона — на высоте 0
    public static class TurretMountBuilder
    {
        private const string OutputFolder = "Assets/Models/Turret";
        private const string GunModelPath = "Assets/PBR Machine Gun/Art/Models/Machine Gun.fbx";

        private const float MountRadius = 0.42f;
        private const float RoofY = -0.235f;       // крыша под кольцом люка
        private const float ShieldZ = 0.66f;       // плоскость переднего щита
        private const float ShieldTop = 0.12f;     // заметно ниже глаз стрелка (0,33): он смотрит поверх щита на дорогу
        private const float ShieldBottom = -0.22f;
        private const float ShieldHalfWidth = 0.42f;
        private const float SlotHalfWidth = 0.09f; // прорезь под ствол открыта сверху: ствол ходит по тангажу
        private const float Plate = 0.02f;

        [MenuItem("RacingProject/Собрать пулемётную позицию")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(OutputFolder);
            SaveMesh(BuildRing(), "TurretRing");
            SaveMesh(BuildMount(), "TurretMount");
            SaveMesh(BuildGunWithoutScope(), "MachineGun_NoScope");
            EnsureArmorMaterial();
            EnsureBrassMaterial();
            AssetDatabase.SaveAssets();
            Debug.Log("Пулемётная позиция собрана в " + OutputFolder);
        }

        // Неподвижное кольцо люка на крыше
        private static Mesh BuildRing()
        {
            var builder = new MeshBuilder();
            const int segments = 24;
            float segmentLength = 2f * Mathf.PI * MountRadius / segments * 1.04f;
            for (int i = 0; i < segments; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, 360f * i / segments, 0f);
                builder.AddBox(rotation * new Vector3(0f, RoofY + 0.03f, MountRadius), rotation, new Vector3(segmentLength, 0.06f, 0.07f));
                // Наружная юбка ниже кольца, закрывает стык с крышей
                builder.AddBox(rotation * new Vector3(0f, RoofY + 0.005f, MountRadius + 0.035f), rotation, new Vector3(segmentLength * 1.08f, 0.03f, 0.03f));
            }
            return builder.ToMesh("TurretRing");
        }

        // Поворотная часть: каретка на кольце, вилка с осью наклона, щиты.
        // Поворачивается вместе с пулемётом только по рысканью
        private static Mesh BuildMount()
        {
            var builder = new MeshBuilder();
            Quaternion none = Quaternion.identity;

            // Каретка на кольце и тумба под вилкой
            builder.AddBox(new Vector3(0f, RoofY + 0.08f, MountRadius), none, new Vector3(0.30f, 0.04f, 0.16f));
            builder.AddCylinder(new Vector3(0f, RoofY + 0.1f, MountRadius), new Vector3(0f, -0.15f, MountRadius), 0.035f, 12);

            // Вилка: щёки по бокам пулемёта, перемычка снизу и ось наклона
            foreach (float side in new[] { -1f, 1f })
                builder.AddBox(new Vector3(side * 0.125f, -0.075f, MountRadius), none, new Vector3(Plate, 0.17f, 0.12f));
            builder.AddBox(new Vector3(0f, -0.155f, MountRadius), none, new Vector3(0.27f, 0.02f, 0.12f));
            builder.AddCylinder(new Vector3(-0.14f, 0f, MountRadius), new Vector3(0.14f, 0f, MountRadius), 0.018f, 10);

            // Кронштейны от каретки к щиту
            foreach (float side in new[] { -1f, 1f })
                builder.AddBetween(new Vector3(side * 0.13f, RoofY + 0.09f, MountRadius + 0.06f), new Vector3(side * 0.2f, -0.05f, ShieldZ - Plate), new Vector2(0.03f, 0.02f));

            // Передний щит из двух половин с прорезью под ствол посередине, внизу прорезь закрыта
            float sideWidth = ShieldHalfWidth - SlotHalfWidth;
            float height = ShieldTop - ShieldBottom;
            foreach (float side in new[] { -1f, 1f })
            {
                float centerX = side * (SlotHalfWidth + sideWidth / 2f);
                builder.AddBox(new Vector3(centerX, ShieldBottom + height / 2f, ShieldZ), none, new Vector3(sideWidth, height, Plate));
                // Верхний край загнут назад
                builder.AddBox(new Vector3(centerX, ShieldTop + 0.025f, ShieldZ - 0.02f), Quaternion.Euler(-40f, 0f, 0f), new Vector3(sideWidth, 0.07f, Plate));
                // Окантовка прорези
                builder.AddBox(new Vector3(side * (SlotHalfWidth + 0.012f), ShieldBottom + height / 2f, ShieldZ - 0.02f), none, new Vector3(0.024f, height, 0.03f));
            }
            builder.AddBox(new Vector3(0f, ShieldBottom + 0.06f, ShieldZ), none, new Vector3(SlotHalfWidth * 2f, 0.12f, Plate));

            // Боковые щиты, развёрнутые назад от краёв переднего
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 front = new Vector3(side * ShieldHalfWidth, 0f, ShieldZ);
                Vector3 back = new Vector3(side * 0.53f, 0f, 0.30f);
                Vector3 middle = (front + back) / 2f;
                builder.AddBox(new Vector3(middle.x, ShieldBottom + height / 2f - 0.02f, middle.z), Quaternion.LookRotation(back - front),
                    new Vector3(Plate, height - 0.04f, (back - front).magnitude));
                // Подкос от бокового щита к каретке
                builder.AddBetween(new Vector3(back.x * 0.92f, ShieldBottom + 0.02f, back.z), new Vector3(side * 0.16f, RoofY + 0.1f, MountRadius - 0.05f), new Vector2(0.025f, 0.025f));
            }

            return builder.ToMesh("TurretMount");
        }

        // Модель пулемёта без оптического прицела: прицел — отдельный submesh с материалом Mat_Scope,
        // он загораживал стрелку центр обзора
        private static Mesh BuildGunWithoutScope()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(GunModelPath);
            MeshFilter filter = model.GetComponentInChildren<MeshFilter>();
            Mesh source = filter.sharedMesh;
            Material[] materials = filter.GetComponent<MeshRenderer>().sharedMaterials;

            int gunSubMesh = -1;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && !materials[i].name.Contains("Scope")) gunSubMesh = i;

            Mesh mesh = Object.Instantiate(source);
            mesh.name = "MachineGun_NoScope";
            int[] triangles = source.GetTriangles(gunSubMesh);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void EnsureArmorMaterial()
        {
            string path = OutputFolder + "/TurretArmor.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", new Color(0.2f, 0.215f, 0.17f));
            material.SetFloat("_Metallic", 0.6f);
            material.SetFloat("_Smoothness", 0.35f);
            AssetDatabase.CreateAsset(material, path);
        }

        // Латунь гильз: материал частиц, потому что гильзы — частицы-меши выбрасывателя у MachineGunRecoil
        private static void EnsureBrassMaterial()
        {
            string path = OutputFolder + "/BrassCasing.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Lit"));
            material.SetColor("_BaseColor", new Color(0.85f, 0.62f, 0.25f));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 0.7f);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
        }

        private static void SaveMesh(Mesh mesh, string name)
        {
            string path = OutputFolder + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Обновляем на месте, чтобы ссылки из сцены не потерялись. CopySerialized не обновляет
                // уже загруженный в видеопамять меш, поэтому данные копируются явно
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.SetVertices(mesh.vertices);
                existing.SetNormals(mesh.normals);
                existing.SetTangents(mesh.tangents);
                existing.SetUVs(0, mesh.uv);
                if (mesh.uv2.Length > 0) existing.SetUVs(1, mesh.uv2);
                if (mesh.colors32.Length > 0) existing.SetColors(mesh.colors32);
                existing.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    existing.SetTriangles(mesh.GetTriangles(i), i);
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                return;
            }
            AssetDatabase.CreateAsset(mesh, path);
        }

        // Сборщик меша из коробок и цилиндров с жёсткими гранями
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void AddBox(Vector3 center, Quaternion rotation, Vector3 size)
            {
                Vector3 half = size / 2f;
                Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
                for (int axis = 0; axis < 3; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 normal = axes[axis] * sign;
                        Vector3 faceCenter = Vector3.Scale(normal, half);
                        Vector3 du = Vector3.Scale(axes[(axis + 1) % 3], half);
                        Vector3 dv = Vector3.Scale(axes[(axis + 2) % 3], half);
                        AddQuad(
                            center + rotation * (faceCenter - du - dv),
                            center + rotation * (faceCenter + du - dv),
                            center + rotation * (faceCenter + du + dv),
                            center + rotation * (faceCenter - du + dv),
                            rotation * normal);
                    }
                }
            }

            // Брус между двумя точками
            public void AddBetween(Vector3 from, Vector3 to, Vector2 section)
            {
                Vector3 direction = to - from;
                AddBox((from + to) / 2f, Quaternion.LookRotation(direction), new Vector3(section.x, section.y, direction.magnitude));
            }

            public void AddCylinder(Vector3 from, Vector3 to, float radius, int sides)
            {
                Vector3 axis = (to - from).normalized;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
                Vector3 side2 = Vector3.Cross(axis, side);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = 2f * Mathf.PI * i / sides;
                    float a1 = 2f * Mathf.PI * (i + 1) / sides;
                    Vector3 r0 = (side * Mathf.Cos(a0) + side2 * Mathf.Sin(a0)) * radius;
                    Vector3 r1 = (side * Mathf.Cos(a1) + side2 * Mathf.Sin(a1)) * radius;
                    AddQuad(from + r0, from + r1, to + r1, to + r0, (r0 + r1).normalized);
                    AddTriangle(to, to + r0, to + r1, axis);
                    AddTriangle(from, from + r1, from + r0, -axis);
                }
            }

            private void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(0f, 1f));
                // Лицевая сторона треугольника (a, b, c) в Unity — та, куда смотрит Cross(b - a, c - a)
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f)
                    triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                else
                    triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            private void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
            {
                int start = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                for (int i = 0; i < 3; i++) normals.Add(normal);
                uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.up);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f)
                    triangles.AddRange(new[] { start, start + 1, start + 2 });
                else
                    triangles.AddRange(new[] { start, start + 2, start + 1 });
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
