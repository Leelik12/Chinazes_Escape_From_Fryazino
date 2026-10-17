using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingProject.EditorTools
{
    // Генератор травы и кустов для любого террейна: Tools → Racing → Трава и кусты.
    // Трава — детали террейна (текстуры травы из Content/Nature), кусты — деревья террейна из сгенерированных
    // префабов (кучка листовых карточек с листвой деревьев проекта, без коллайдера — машина проезжает насквозь).
    // Не растёт там, где сверху лежит чужой коллайдер (дороги, тротуары, здания, заборы) — с запасом,
    // на крутых склонах и там, где запрещает keepOut. Повторный запуск заменяет траву и кусты, деревья не трогает
    public static class VegetationGenerator
    {
        public class Settings
        {
            public int Seed = 1;
            // Трава: сколько пучков в клетке деталей на самых густых местах
            public int GrassPerCell = 6;
            // Доля площади без травы (проплешины), 0..1
            public float Bare = 0.25f;
            // Кустов на гектар в среднем; растут кучками
            public float BushesPerHectare = 30f;
            // Не ближе к дорогам и постройкам, м: трава / кусты
            public float GrassMargin = 0.8f;
            public float BushMargin = 5f;
            // Самый крутой склон, градусы: трава / кусты
            public float GrassMaxSlope = 42f;
            public float BushMaxSlope = 32f;
            // Где ничего не сажать (мировая точка → true), например площадки и линии огня
            public Func<Vector3, bool> KeepOut;
        }

        private const string Folder = "Assets/Content/Nature/Bushes";
        private const string MeshPath = Folder + "/BushMeshes.asset";
        private static readonly string[] GrassTextures =
        {
            "Assets/Content/Nature/Textures/Grass_Tuft.png",
            "Assets/Content/Nature/Textures/Grass_Dry.png",
            "Assets/Content/Nature/Textures/Weeds.png",
        };
        // Куст: имя, листва, радиус по горизонтали и высота, м, число карточек
        private static readonly (string name, string leaf, float radius, float height, int cards)[] BushKinds =
        {
            ("Bush_Round", "Assets/Content/Tree_Packs/URP_Tree_Pack/Textures/Leaf/URP_1_Leaf.png", 1.3f, 1.6f, 34),
            ("Bush_Wide", "Assets/Content/Tree_Packs/URP_Tree_Pack/Textures/Leaf/URP_3_Leaf.png", 2.0f, 1.3f, 40),
            ("Bush_Small", "Assets/Content/Tree_Packs/URP_Tree_Pack/Textures/Leaf/URP_1_Leaf.png", 0.8f, 0.9f, 20),
        };

        [MenuItem("Tools/Racing/Трава и кусты")]
        private static void OpenWindow()
        {
            EditorWindow.GetWindow<VegetationWindow>("Трава и кусты");
        }

        // Заселяет террейн; возвращает краткий отчёт
        public static string Populate(Terrain terrain, Settings settings)
        {
            TerrainData data = terrain.terrainData;
            var rng = new System.Random(settings.Seed);
            Physics.SyncTransforms();

            // --- Трава ---
            var grass = new List<DetailPrototype>();
            foreach (string path in GrassTextures)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null) continue;
                bool weeds = path.EndsWith("Weeds.png");
                bool dry = path.EndsWith("Grass_Dry.png");
                grass.Add(new DetailPrototype
                {
                    prototypeTexture = texture,
                    renderMode = DetailRenderMode.Grass,
                    usePrototypeMesh = false,
                    minWidth = weeds ? 0.6f : 0.7f, maxWidth = weeds ? 1.1f : 1.3f,
                    minHeight = weeds ? 0.8f : 0.5f, maxHeight = weeds ? 1.4f : 0.9f,
                    noiseSpread = 0.3f,
                    healthyColor = dry ? new Color(0.62f, 0.6f, 0.38f) : new Color(0.42f, 0.55f, 0.28f),
                    dryColor = dry ? new Color(0.7f, 0.6f, 0.36f) : new Color(0.55f, 0.56f, 0.32f),
                });
            }
            data.detailPrototypes = grass.ToArray();
            // Значение клетки — число пучков (в режиме покрытия оно означало бы долю 0..255)
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            int res = data.detailResolution;
            float cell = data.size.x / res;
            bool[,] grassBlocked = BlockedMask(terrain, res, settings.GrassMargin);
            var layers = new int[grass.Count][,];
            for (int layer = 0; layer < grass.Count; layer++) layers[layer] = new int[res, res];
            float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;
            int tufts = 0;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    if (grassBlocked[z, x]) continue;
                    float nx = (x + 0.5f) / res, nz = (z + 0.5f) / res;
                    if (data.GetSteepness(nx, nz) > settings.GrassMaxSlope) continue;
                    Vector3 world = terrain.transform.position + new Vector3(nx * data.size.x, 0f, nz * data.size.z);
                    if (settings.KeepOut != null && settings.KeepOut(world)) continue;
                    // Крупные пятна густой и редкой травы, мелкая рябь, сухие и сорные участки — своим шумом
                    float patch = Mathf.PerlinNoise(world.x / 22f + ox, world.z / 22f + oz);
                    float ripple = Mathf.PerlinNoise(world.x / 5f + oz, world.z / 5f + ox);
                    float density = Mathf.InverseLerp(settings.Bare, 1f, patch * 0.75f + ripple * 0.35f);
                    if (density <= 0f) continue;
                    float dryShare = Mathf.PerlinNoise(world.x / 40f + 300f + ox, world.z / 40f + oz);
                    float weedShare = Mathf.PerlinNoise(world.x / 15f + 600f + ox, world.z / 15f + oz);
                    int total = Mathf.RoundToInt(density * settings.GrassPerCell * (0.7f + 0.6f * (float)rng.NextDouble()));
                    for (int i = 0; i < total; i++)
                    {
                        double pick = rng.NextDouble();
                        int layer = pick < dryShare * 0.6f ? 1 : pick < dryShare * 0.6f + weedShare * 0.35f ? 2 : 0;
                        if (layer >= grass.Count) layer = 0;
                        layers[layer][z, x]++;
                        tufts++;
                    }
                }
            for (int layer = 0; layer < grass.Count; layer++)
                data.SetDetailLayer(0, 0, layer, layers[layer]);

            // --- Кусты ---
            List<GameObject> bushPrefabs = EnsureBushPrefabs();
            var prototypes = new List<TreePrototype>(data.treePrototypes);
            var bushIndex = new List<int>();
            foreach (GameObject prefab in bushPrefabs)
            {
                int index = prototypes.FindIndex(p => p.prefab == prefab);
                if (index < 0) { prototypes.Add(new TreePrototype { prefab = prefab }); index = prototypes.Count - 1; }
                bushIndex.Add(index);
            }
            var trees = new List<TreeInstance>();
            foreach (TreeInstance tree in data.treeInstances)
                if (!bushIndex.Contains(tree.prototypeIndex)) trees.Add(tree);
            data.treePrototypes = prototypes.ToArray();

            bool[,] bushBlocked = BlockedMask(terrain, res, settings.BushMargin);
            float hectares = data.size.x * data.size.z / 10000f;
            int target = Mathf.RoundToInt(hectares * settings.BushesPerHectare);
            int bushes = 0;
            for (int attempt = 0; attempt < target * 30 && bushes < target; attempt++)
            {
                float nx = (float)rng.NextDouble(), nz = (float)rng.NextDouble();
                Vector3 world = terrain.transform.position + new Vector3(nx * data.size.x, 0f, nz * data.size.z);
                // Кучками: шанс по шуму, между кучками — редкие одиночные кусты
                float clump = Mathf.PerlinNoise(world.x / 35f + oz + 50f, world.z / 35f + ox);
                if (rng.NextDouble() > Mathf.Clamp01((clump - 0.45f) * 3f) + 0.04f) continue;
                int cx = Mathf.Min(res - 1, (int)(nx * res)), cz = Mathf.Min(res - 1, (int)(nz * res));
                if (bushBlocked[cz, cx]) continue;
                if (data.GetSteepness(nx, nz) > settings.BushMaxSlope) continue;
                if (settings.KeepOut != null && settings.KeepOut(world)) continue;
                float scale = 0.7f + (float)rng.NextDouble() * 0.7f;
                trees.Add(new TreeInstance
                {
                    prototypeIndex = bushIndex[rng.Next(bushIndex.Count)],
                    position = new Vector3(nx, data.GetInterpolatedHeight(nx, nz) / data.size.y, nz),
                    widthScale = scale,
                    heightScale = scale * (0.85f + (float)rng.NextDouble() * 0.3f),
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                    color = Color.white,
                    lightmapColor = Color.white,
                });
                bushes++;
            }
            data.SetTreeInstances(trees.ToArray(), true);
            terrain.Flush();
            EditorUtility.SetDirty(data);
            return "трава: " + tufts + " пучков, кусты: " + bushes;
        }

        // Убирает траву и сгенерированные кусты, деревья оставляет
        public static void Clear(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            int res = data.detailResolution;
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
                data.SetDetailLayer(0, 0, layer, new int[res, res]);
            var trees = new List<TreeInstance>();
            foreach (TreeInstance tree in data.treeInstances)
            {
                GameObject prefab = data.treePrototypes[tree.prototypeIndex].prefab;
                if (prefab == null || !AssetDatabase.GetAssetPath(prefab).StartsWith(Folder)) trees.Add(tree);
            }
            data.SetTreeInstances(trees.ToArray(), true);
            terrain.Flush();
            EditorUtility.SetDirty(data);
        }

        // Клетки, над которыми лежит чужой коллайдер (луч сверху попадает не в террейн), расширенные на margin метров
        private static bool[,] BlockedMask(Terrain terrain, int res, float margin)
        {
            TerrainData data = terrain.terrainData;
            Collider own = terrain.GetComponent<TerrainCollider>();
            var hit = new bool[res, res];
            float top = terrain.transform.position.y + data.size.y + 50f;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    var origin = new Vector3(terrain.transform.position.x + (x + 0.5f) / res * data.size.x, top,
                                             terrain.transform.position.z + (z + 0.5f) / res * data.size.z);
                    if (Physics.Raycast(origin, Vector3.down, out RaycastHit h, data.size.y + 100f, ~0, QueryTriggerInteraction.Ignore))
                        hit[z, x] = h.collider != own;
                }
            int r = Mathf.CeilToInt(margin / (data.size.x / res));
            if (r <= 0) return hit;
            // Расширение квадратом: сначала по X, потом по Z
            var tmp = new bool[res, res];
            var mask = new bool[res, res];
            for (int z = 0; z < res; z++)
            {
                int last = -100000;
                for (int x = 0; x < res; x++) { if (hit[z, x]) last = x; tmp[z, x] = x - last <= r; }
                last = 100000;
                for (int x = res - 1; x >= 0; x--) { if (hit[z, x]) last = x; tmp[z, x] |= last - x <= r; }
            }
            for (int x = 0; x < res; x++)
            {
                int last = -100000;
                for (int z = 0; z < res; z++) { if (tmp[z, x]) last = z; mask[z, x] = z - last <= r; }
                last = 100000;
                for (int z = res - 1; z >= 0; z--) { if (tmp[z, x]) last = z; mask[z, x] |= last - z <= r; }
            }
            return mask;
        }

        // Префабы кустов: создаются один раз (меши — в BushMeshes.asset), дальше берутся готовые
        public static List<GameObject> EnsureBushPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Content/Nature", "Bushes");
            var result = new List<GameObject>();
            UnityEngine.Object container = AssetDatabase.LoadMainAssetAtPath(MeshPath);
            for (int k = 0; k < BushKinds.Length; k++)
            {
                var kind = BushKinds[k];
                string prefabPath = Folder + "/" + kind.name + ".prefab";
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing != null) { LeafMaterial(kind.leaf, k); result.Add(AddLodGroup(existing, prefabPath)); continue; }

                if (container == null)
                {
                    container = new Mesh { name = "BushMeshes" };
                    AssetDatabase.CreateAsset(container, MeshPath);
                }
                Mesh mesh = BuildBushMesh(kind.name, kind.radius, kind.height, kind.cards, new System.Random(100 + k));
                AssetDatabase.AddObjectToAsset(mesh, container);

                Material material = LeafMaterial(kind.leaf, k);
                var go = new GameObject(kind.name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                result.Add(AddLodGroup(PrefabUtility.SaveAsPrefabAsset(go, prefabPath), prefabPath));
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            return result;
        }

        // Террейн рисует меш-деревья без шейдера Nature/Soft Occlusion только с LODGroup (иначе предупреждает
        // о билбордах): один уровень, куст исчезает, когда занимает меньше 1,5% высоты экрана
        private static GameObject AddLodGroup(GameObject prefab, string prefabPath)
        {
            if (prefab.GetComponent<LODGroup>() != null) return prefab;
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            var lodGroup = contents.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[] { new LOD(0.015f, contents.GetComponentsInChildren<Renderer>()) });
            lodGroup.RecalculateBounds();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            PrefabUtility.UnloadPrefabContents(contents);
            return saved;
        }

        // Купол из листовых карточек: каждая повёрнута случайно, нормали — от центра куста (мягкий свет как у кроны)
        private static Mesh BuildBushMesh(string name, float radius, float height, int cards, System.Random rng)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var center = new Vector3(0f, height * 0.45f, 0f);
            for (int i = 0; i < cards; i++)
            {
                // Точка внутри полуэллипсоида, ближе к поверхности
                float u = (float)rng.NextDouble(), v = (float)rng.NextDouble() * 2f - 1f, w = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Sqrt(1f - v * v) * Mathf.Cos(w), Mathf.Abs(v) * 0.9f + 0.1f, Mathf.Sqrt(1f - v * v) * Mathf.Sin(w)).normalized;
                float depth = 0.45f + 0.5f * Mathf.Sqrt(u);
                Vector3 pos = new Vector3(dir.x * radius, dir.y * height * 0.6f, dir.z * radius) * depth + new Vector3(0f, height * 0.25f, 0f);
                float size = Mathf.Lerp(0.8f, 1.25f, (float)rng.NextDouble()) * Mathf.Max(radius, height) * 0.75f;
                Quaternion rot = Quaternion.Euler((float)rng.NextDouble() * 70f - 35f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
                Vector3 right = rot * Vector3.right * size * 0.5f, up = rot * Vector3.up * size * 0.5f;
                int b = vertices.Count;
                Vector3[] corners = { pos - right - up, pos - right + up, pos + right + up, pos + right - up };
                foreach (Vector3 c in corners)
                {
                    vertices.Add(c);
                    normals.Add((c - center).normalized);
                }
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);
                triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 3);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Листва: URP Lit с отсечением по альфе, двусторонняя, чуть темнее и разного оттенка у разных кустов
        private static Material LeafMaterial(string texturePath, int variant)
        {
            string path = Folder + "/BushLeaf_" + variant + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BushLeaf_" + variant };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            // Приглушённые тона: светлые листья ночью выглядят ярче травы и кажутся светящимися
            Color[] tints = { new Color(0.36f, 0.44f, 0.28f), new Color(0.32f, 0.4f, 0.26f), new Color(0.42f, 0.45f, 0.28f) };
            material.SetColor("_BaseColor", tints[variant % tints.Length]);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            if (created) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private class VegetationWindow : EditorWindow
        {
            private Terrain terrain;
            private readonly Settings settings = new Settings();
            private string report = "";

            private void OnGUI()
            {
                EditorGUILayout.HelpBox("Трава и кусты для террейна: не растут на дорогах, у построек и на крутых склонах. " +
                                        "Повторный запуск заменяет траву и кусты, деревья не трогает.", MessageType.Info);
                terrain = (Terrain)EditorGUILayout.ObjectField("Террейн", terrain, typeof(Terrain), true);
                if (terrain == null && Selection.activeGameObject != null) terrain = Selection.activeGameObject.GetComponent<Terrain>();
                settings.Seed = EditorGUILayout.IntField("Зерно", settings.Seed);
                settings.GrassPerCell = EditorGUILayout.IntSlider("Густота травы", settings.GrassPerCell, 1, 16);
                settings.Bare = EditorGUILayout.Slider("Проплешины", settings.Bare, 0f, 0.9f);
                settings.BushesPerHectare = EditorGUILayout.Slider("Кустов на гектар", settings.BushesPerHectare, 0f, 200f);
                settings.GrassMargin = EditorGUILayout.Slider("Отступ травы от дорог, м", settings.GrassMargin, 0f, 5f);
                settings.BushMargin = EditorGUILayout.Slider("Отступ кустов от дорог, м", settings.BushMargin, 0f, 20f);
                using (new EditorGUI.DisabledScope(terrain == null))
                {
                    if (GUILayout.Button("Заселить"))
                    {
                        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Трава и кусты");
                        report = Populate(terrain, settings);
                    }
                    if (GUILayout.Button("Убрать траву и кусты"))
                    {
                        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Убрать траву и кусты");
                        Clear(terrain);
                        report = "убрано";
                    }
                }
                if (report.Length > 0) EditorGUILayout.LabelField(report);
            }
        }
    }
}
