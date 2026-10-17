// Сцена Tutorial — полигон режима «Обучение» (TutorialCourse): учебный автодром за городом. RoomController
// подгружает её поверх SovietCity, поэтому в ней нет ни камеры, ни неба, ни сети — только местность и экран подсказок.
// Местность: свой террейн 640×640 м с холмами (кольцо высоких холмов с лесом закрывает горизонт), деревья
// из террейна города, кольцевая дорога по рельефу с обочинами и разметкой, КПП с ангаром на старте, тупик между
// гаражами (задний ход), стрельбище с мишенями в поле, трамплин под левые колёса (на шаге «Трамплин» машина на нём переворачивается),
// засада с гранатомётчиком на холме перед ремонтом, трава и кусты (VegetationGenerator),
// фонари вдоль дороги, ЛЭП и панельки на холмах.
// Полигон стоит далеко за краем карты (террейн города — 0..2000 по X и Z): там нет NavMesh, и «поставить на колёса»
// (CarRecovery) не уносит машину на дороги города.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск пересобирает сцену и её ассеты (террейн, меши дороги) с нуля. SovietCity при этом остаётся открытой
var log = new System.Text.StringBuilder();
const string scenePath = "Assets/Scenes/Tutorial.unity";
const string dataDir = "Assets/Content/Tutorial";
const string terrainPath = dataDir + "/TutorialTerrain.asset";
const string meshPath = dataDir + "/TutorialMeshes.asset";
// Центр полигона; все координаты ниже — от него, в метрах
var origin = new Vector3(-1100f, 0f, 1000f);
const float TerrainSize = 640f, TerrainHeight = 120f;
const float RoadHalf = 4f, ShoulderWidth = 2f, RoadSink = 0.25f, PaintLift = 0.02f;
var rng = new System.Random(17);

var bg = new Color(0.035f, 0.075f, 0.045f, 1f);
var phosphor = new Color(0.6f, 0.88f, 0.42f);
var dim = new Color(0.38f, 0.58f, 0.33f);
var amber = new Color(1f, 0.75f, 0.3f);
var bodyFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
var titleFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Content/BlackOpsOne-Regular.asset");
var scanTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/UI/Terminal/Scanlines.png");
System.Func<string, Material> roadMat = nm => AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/Roads/Materials/" + nm + ".mat");
var pmAsphalt = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Content/Roads/Physics/RoadAsphalt.physicMaterial");
var pmGravel = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Content/Roads/Physics/RoadGravel.physicMaterial");
System.Func<string, GameObject> P = p => AssetDatabase.LoadAssetAtPath<GameObject>(p);
var conePrefab = P("Assets/Content/Roads/Props/GSDTrafficCone_Static.prefab");
var roadBarrelPrefab = P("Assets/Content/Roads/Props/GSDRoadBarrel_Static.prefab");
var sedanPrefab = P("Assets/Content/Cars/Hatchback and Sedan/prefabs/SEDAN.prefab");
var burntPrefab = P("Assets/Prefabs/Wasteland/BurntSedan.prefab");
var burntHatchPrefab = P("Assets/Prefabs/Wasteland/BurntHatchback.prefab");
var barrelPrefab = P("Assets/Prefabs/Wasteland/BurningBarrel.prefab");
var kitPrefab = P("Assets/Prefabs/Pickups/RepairKit.prefab");
var lampPrefab = P("Assets/Prefabs/Roadside/StreetLamp.prefab");
var polePrefab = P("Assets/Prefabs/Roadside/PowerPole.prefab");
var boothPrefab = P("Assets/Prefabs/Roadside/GuardBooth.prefab");
var garagePrefab = P("Assets/Prefabs/Roadside/GarageRow_6.prefab");
var garage4Prefab = P("Assets/Prefabs/Roadside/GarageRow_4.prefab");
var blockPrefab = P("Assets/Prefabs/Roadside/ConcreteBlock.prefab");
var hangarPrefab = P("Assets/Content/Buildings/Old Hangar/Old Hangar.prefab");
var fencePrefab = P("Assets/Content/SmallEnvironment/Assets_FenceChained/prefabs/chainlink_single.prefab");
var tetrapodPrefab = P("Assets/Content/SmallEnvironment/Road props for games/Prefabs/Tetrapod.prefab");
var signPrefab = P("Assets/Content/SmallEnvironment/Road props for games/Prefabs/RoadSign_Triangular.prefab");
var transformerPrefab = P("Assets/Content/SmallEnvironment/Transformer_substation_booth/Prefabs/Transformer_substation_booth.prefab");
var panelPrefabs = new[] { P("Assets/Content/Buildings/russian_buildings/prefabs/buildings/rus_build_9et_01.prefab"),
                           P("Assets/Content/Buildings/russian_buildings/prefabs/buildings/rus_build_9et_02.prefab"),
                           P("Assets/Content/Buildings/russian_buildings/prefabs/buildings/rus_build_5et_01.prefab") };
var explosion = P("Assets/Prefabs/Effects/EnemyExplosion.prefab");
var pickupClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Content/Sound/Generated/Pickup.wav");
Terrain cityTerrain = null;
foreach (var tr in UnityEngine.Object.FindObjectsOfType<Terrain>()) if (tr.gameObject.scene.path == "Assets/Scenes/SovietCity.unity") cityTerrain = tr;

// --- Материал маяка ---
if (!AssetDatabase.IsValidFolder(dataDir)) AssetDatabase.CreateFolder("Assets/Content", "Tutorial");
const string beaconMatPath = dataDir + "/BeaconGlow.mat";
var beaconMat = AssetDatabase.LoadAssetAtPath<Material>(beaconMatPath);
if (beaconMat == null) { beaconMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(beaconMat, beaconMatPath); }
// Прозрачный светящийся столб: Unlit, аддитивное смешивание, без записи глубины
beaconMat.shader = Shader.Find("Universal Render Pipeline/Unlit");
beaconMat.SetFloat("_Surface", 1f); beaconMat.SetFloat("_Blend", 2f); beaconMat.SetFloat("_Cull", 0f);
beaconMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
beaconMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
beaconMat.SetFloat("_ZWrite", 0f);
beaconMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
beaconMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
beaconMat.SetColor("_BaseColor", new Color(0.45f, 1f, 0.35f, 0.35f));
EditorUtility.SetDirty(beaconMat);

// Контейнер мешей дороги (прежняя версия полигона хранила здесь плитку площадки)
if (AssetDatabase.LoadMainAssetAtPath(meshPath) == null && AssetDatabase.LoadMainAssetAtPath(dataDir + "/Ground.asset") != null)
    AssetDatabase.MoveAsset(dataDir + "/Ground.asset", meshPath);
var container = AssetDatabase.LoadMainAssetAtPath(meshPath);
if (container == null) { container = new Mesh(); AssetDatabase.CreateAsset(container, meshPath); }
container.name = "TutorialMeshes";
foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(meshPath)) if (sub != container) AssetDatabase.RemoveObjectFromAsset(sub);
var containerMesh = container as Mesh; if (containerMesh != null) containerMesh.Clear();

// --- Трасса: замкнутая кривая Катмулла — Рома через опорные точки, выборка через 2 м ---
var control = new[] {
    new Vector2(-30f, -175f), new Vector2(-29f, -130f), new Vector2(-27f, -100f), new Vector2(-10f, -72f), new Vector2(18f, -52f),
    new Vector2(14f, -22f), new Vector2(-16f, -2f), new Vector2(-32f, 28f), new Vector2(-20f, 58f), new Vector2(12f, 78f),
    new Vector2(70f, 86f), new Vector2(130f, 82f), new Vector2(178f, 64f), new Vector2(204f, 26f), new Vector2(207f, -30f),
    new Vector2(198f, -88f), new Vector2(168f, -140f), new Vector2(110f, -176f), new Vector2(40f, -196f), new Vector2(-8f, -206f),
    new Vector2(-28f, -196f) };
var dense = new System.Collections.Generic.List<Vector2>();
int nc = control.Length;
for (int i = 0; i < nc; i++) {
    Vector2 p0 = control[(i - 1 + nc) % nc], p1 = control[i], p2 = control[(i + 1) % nc], p3 = control[(i + 2) % nc];
    for (int k = 0; k < 40; k++) {
        float t = k / 40f, tq = t * t, tc = tq * t;
        dense.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * tq + (-p0 + 3f * p1 - 3f * p2 + p3) * tc));
    }
}
// Равный шаг вдоль кривой
var path = new System.Collections.Generic.List<Vector2>();
{
    float acc = 0f; path.Add(dense[0]);
    for (int i = 1; i <= dense.Count; i++) {
        Vector2 a = dense[i - 1], b = dense[i % dense.Count];
        float seg = Vector2.Distance(a, b), pos = 0f;
        while (acc + (seg - pos) >= 2f) { pos += 2f - acc; acc = 0f; path.Add(Vector2.Lerp(a, b, pos / seg)); }
        acc += seg - pos;
    }
    if (Vector2.Distance(path[path.Count - 1], path[0]) < 1f) path.RemoveAt(path.Count - 1);
}
int n = path.Count;
System.Func<Vector2, int> nearest = q => { int best = 0; float bd = float.MaxValue; for (int i = 0; i < n; i++) { float d = (path[i] - q).sqrMagnitude; if (d < bd) { bd = d; best = i; } } return best; };

// --- Рельеф: шум холмов, к краям — кольцо высоких холмов ---
System.Func<float, float, float> natural = (x, z) => {
    float n1 = Mathf.PerlinNoise(x / 150f + 31.7f, z / 150f + 12.3f) - 0.5f;
    float n2 = Mathf.PerlinNoise(x / 48f + 5.1f, z / 48f + 77.7f) - 0.5f;
    float n3 = Mathf.PerlinNoise(x / 14f + 9.9f, z / 14f + 3.3f) - 0.5f;
    float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / (TerrainSize / 2f);
    float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.97f, r)) * 38f;
    return 30f + n1 * 18f + n2 * 5f + n3 * 0.6f + ring;
};
// Высота дороги — рельеф, сглаженный вдоль трассы (окно ±40 м): дорога идёт по холмам, но без ям
var roadH = new float[n];
{
    var raw = new float[n]; for (int i = 0; i < n; i++) raw[i] = natural(path[i].x, path[i].y);
    for (int pass = 0; pass < 3; pass++) {
        for (int i = 0; i < n; i++) { float s = 0f; for (int k = -20; k <= 20; k++) s += raw[(i + k + n) % n]; roadH[i] = s / 41f; }
        System.Array.Copy(roadH, raw, n);
    }
}
// Тупик между гаражами: ответвление на север от трассы, ровное, на высоте примыкания
int junction = nearest(new Vector2(70f, 86f));
float spurH = roadH[junction];
var spurA = new Vector2(path[junction].x, path[junction].y); var spurB = new Vector2(spurA.x, spurA.y + 66f);
// Площадки: старт у КПП и поле стрельбища — ровные (центр, радиус, высота, ширина перехода)
int rangeIdx = nearest(new Vector2(206f, 0f));
// Трамплин под левые колёса: длина и высота левого края, м
const float RampLength = 8f, RampHeight = 2.2f;
// Засада: точка остановки на дороге между трамплином и ремкомплектом и гнездо гранатомётчика на холмике слева
// впереди по ходу — в 40 м (дальше его скрывает туман), на 5 м выше дороги
int stopIdx = nearest(new Vector2(148f, -156f));
var nest2 = path[stopIdx] + new Vector2(-6f, -40f);
var pads = new[] {
    new Vector4(-55f, -165f, 30f, roadH[nearest(new Vector2(-30f, -165f))]),
    new Vector4(165f, -5f, 48f, roadH[rangeIdx] - 0.2f),
    new Vector4(nest2.x, nest2.y, 4f, roadH[stopIdx] + 5f + RoadSink),
};
System.Func<Vector2, Vector2, Vector2, float> segDist = (q, a, b) => {
    Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f)); return Vector2.Distance(q, a + ab * t);
};
// Итоговая высота местности: дорога и площадки вдавлены в рельеф, к ним он плавно спускается
System.Func<float, float, float> ground = (x, z) => {
    var q = new Vector2(x, z);
    float h = natural(x, z);
    foreach (var pad in pads) {
        float d = Vector2.Distance(q, new Vector2(pad.x, pad.y));
        float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(pad.z, pad.z + 25f, d));
        h = Mathf.Lerp(h, pad.w - RoadSink, w);
    }
    // Ближайшая точка трассы (грубо по выборке, затем по отрезку)
    int bi = 0; float bd = float.MaxValue;
    for (int i = 0; i < n; i += 2) { float d = (path[i] - q).sqrMagnitude; if (d < bd) { bd = d; bi = i; } }
    float dr = Mathf.Min(segDist(q, path[(bi - 1 + n) % n], path[bi]), segDist(q, path[bi], path[(bi + 1) % n]));
    dr = Mathf.Min(dr, Mathf.Min(segDist(q, path[(bi - 2 + n) % n], path[(bi - 1 + n) % n]), segDist(q, path[(bi + 1) % n], path[(bi + 2) % n])));
    float wr = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoadHalf + ShoulderWidth + 0.5f, RoadHalf + ShoulderWidth + 16f, dr));
    h = Mathf.Lerp(h, roadH[bi] - RoadSink, wr);
    float ds = segDist(q, spurA, spurB);
    float ws = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(26f, 40f, ds));
    h = Mathf.Lerp(h, spurH - RoadSink, ws);
    return h;
};
System.Func<float, float, float> roadDist = (x, z) => {
    var q = new Vector2(x, z); float bd = float.MaxValue;
    for (int i = 0; i < n; i += 2) bd = Mathf.Min(bd, (path[i] - q).sqrMagnitude);
    return Mathf.Min(Mathf.Sqrt(bd), segDist(q, spurA, spurB));
};

// --- Террейн ---
var td = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
if (td == null) { td = new TerrainData(); AssetDatabase.CreateAsset(td, terrainPath); }
td.heightmapResolution = 513;
td.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
td.alphamapResolution = 512;
td.SetDetailResolution(512, 32);
var terrainCorner = origin - new Vector3(TerrainSize / 2f, 0f, TerrainSize / 2f);
{
    int res = td.heightmapResolution; var hs = new float[res, res];
    for (int iz = 0; iz < res; iz++) for (int ix = 0; ix < res; ix++) {
        float x = ix / (float)(res - 1) * TerrainSize - TerrainSize / 2f, z = iz / (float)(res - 1) * TerrainSize - TerrainSize / 2f;
        hs[iz, ix] = Mathf.Clamp01(ground(x, z) / TerrainHeight);
    }
    td.SetHeights(0, 0, hs);
}
// Слои — из террейна города: 0 — трава, 1 и 2 — земля и гравий (обочины, склоны, стрельбище)
td.terrainLayers = cityTerrain.terrainData.terrainLayers;
{
    int res = td.alphamapResolution; var a = new float[res, res, 3];
    for (int iz = 0; iz < res; iz++) for (int ix = 0; ix < res; ix++) {
        float x = (ix + 0.5f) / res * TerrainSize - TerrainSize / 2f, z = (iz + 0.5f) / res * TerrainSize - TerrainSize / 2f;
        float steep = td.GetSteepness((ix + 0.5f) / res, (iz + 0.5f) / res);
        float dr = roadDist(x, z);
        float dirt = Mathf.Clamp01(1f - (dr - RoadHalf - 2f) / 5f);
        dirt = Mathf.Max(dirt, Mathf.InverseLerp(22f, 35f, steep));
        dirt = Mathf.Max(dirt, (Mathf.PerlinNoise(x / 25f + 50f, z / 25f) - 0.55f) * 2.5f);
        float rangeD = Vector2.Distance(new Vector2(x, z), new Vector2(pads[1].x, pads[1].y));
        float gravel = Mathf.Clamp01(1f - (rangeD - 30f) / 15f) * 0.7f + Mathf.Clamp01(1f - (dr - RoadHalf - 0.5f) / 2.5f) * 0.6f;
        dirt = Mathf.Clamp01(dirt); gravel = Mathf.Clamp01(gravel);
        float grass = Mathf.Clamp01(1f - dirt - gravel);
        float sum = grass + dirt + gravel;
        a[iz, ix, 0] = grass / sum; a[iz, ix, 1] = dirt / sum; a[iz, ix, 2] = gravel / sum;
    }
    td.SetAlphamaps(0, 0, a);
}
// Трава и кусты — генератор VegetationGenerator, после сборки сцены (ему нужны коллайдеры дороги и построек)
// Деревья: лес по кольцу холмов и рощи внутри, не ближе 14 м к дороге и не на площадках
// Прежние экземпляры (в том числе кусты прошлой сборки) убираем до смены прототипов, иначе Unity ругается на них
td.treeInstances = new TreeInstance[0];
td.treePrototypes = cityTerrain.terrainData.treePrototypes;
{
    var trees = new System.Collections.Generic.List<TreeInstance>();
    for (int attempt = 0; attempt < 30000 && trees.Count < 2600; attempt++) {
        float x = (float)rng.NextDouble() * (TerrainSize - 20f) - (TerrainSize / 2f - 10f), z = (float)rng.NextDouble() * (TerrainSize - 20f) - (TerrainSize / 2f - 10f);
        float r = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) / (TerrainSize / 2f);
        float chance = Mathf.Lerp(0.05f, 0.9f, Mathf.InverseLerp(0.5f, 0.85f, r));
        chance = Mathf.Max(chance, Mathf.Clamp01((Mathf.PerlinNoise(x / 60f + 200f, z / 60f + 100f) - 0.58f) * 4f));
        if (rng.NextDouble() > chance) continue;
        if (roadDist(x, z) < 14f) continue;
        bool blocked = false; foreach (var pad in pads) if (Vector2.Distance(new Vector2(x, z), new Vector2(pad.x, pad.y)) < pad.z + 18f) blocked = true;
        if (Vector2.Distance(new Vector2(x, z), new Vector2(165f, -5f)) < 75f) blocked = true; // линия огня стрельбища
        if (Mathf.Abs(x - spurA.x) < 40f && z > spurA.y - 10f && z < spurB.y + 20f) blocked = true; // гаражи
        if (segDist(new Vector2(x, z), path[stopIdx], nest2) < 20f) blocked = true; // линия огня засады: просека, чтобы видеть гнездо
        if (blocked) continue;
        var ti = new TreeInstance();
        ti.prototypeIndex = rng.Next(td.treePrototypes.Length);
        // Высота дерева в TreeInstance — доля высоты террейна, иначе дерево окажется под холмом
        ti.position = new Vector3((x + TerrainSize / 2f) / TerrainSize, ground(x, z) / TerrainHeight, (z + TerrainSize / 2f) / TerrainSize);
        float s = 0.8f + (float)rng.NextDouble() * 0.6f;
        ti.widthScale = s; ti.heightScale = s * (0.9f + (float)rng.NextDouble() * 0.2f);
        ti.rotation = (float)rng.NextDouble() * Mathf.PI * 2f;
        ti.color = Color.white; ti.lightmapColor = Color.white;
        trees.Add(ti);
    }
    td.treeInstances = trees.ToArray();
    log.AppendLine("деревьев " + trees.Count);
}
EditorUtility.SetDirty(td);
AssetDatabase.SaveAssets();

// --- Сцена ---
var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
var existing = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
if (existing.IsValid() && existing.isLoaded) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(existing, true);
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

var root = new GameObject("TutorialCourse");
root.transform.position = origin;
var terrainGo = Terrain.CreateTerrainGameObject(td);
terrainGo.name = "Terrain"; terrainGo.transform.SetParent(root.transform, true); terrainGo.transform.position = terrainCorner; terrainGo.isStatic = true;
var terrain = terrainGo.GetComponent<Terrain>();
terrain.materialTemplate = cityTerrain.materialTemplate;
terrain.drawInstanced = cityTerrain.drawInstanced;
terrain.heightmapPixelError = 8f;
terrain.basemapDistance = cityTerrain.basemapDistance;
terrain.treeDistance = 700f;
terrain.treeBillboardDistance = cityTerrain.treeBillboardDistance;
terrain.detailObjectDistance = cityTerrain.detailObjectDistance;
terrain.detailObjectDensity = cityTerrain.detailObjectDensity;
terrain.Flush();

System.Func<string, Transform, Vector3, GameObject> empty = (nm, parent, local) => {
    var g = new GameObject(nm); g.transform.SetParent(parent, false); g.transform.localPosition = local; return g;
};
// Высота земли (террейна) под точкой полигона
System.Func<float, float, float> groundAt = (x, z) => terrain.SampleHeight(origin + new Vector3(x, 0f, z)) + terrainCorner.y - origin.y;
// Префаб на землю: поворот по Y, по желанию — масштаб
System.Func<GameObject, Transform, float, float, float, GameObject> put = (prefab, parent, x, z, yaw) => {
    var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
    g.transform.SetParent(parent, false); g.transform.localPosition = new Vector3(x, groundAt(x, z), z); g.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    return g;
};
// Направление трассы в точке и её правая сторона
System.Func<int, Vector2> dirAt = i => (path[(i + 1) % n] - path[(i - 1 + n) % n]).normalized;
System.Func<int, float> yawAt = i => { var d = dirAt(i); return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; };
System.Func<int, Vector3> roadPoint = i => new Vector3(path[i].x, roadH[i], path[i].y);

// --- Дорога: асфальт, гравийные обочины и разметка, куски по 40 м (на кусок светят только ближайшие фонари) ---
var V = new System.Collections.Generic.List<Vector3>(); var UV = new System.Collections.Generic.List<Vector2>(); var T = new System.Collections.Generic.List<int>();
System.Action<Vector3[,], Vector2[,]> strip = (pts, uvs) => {
    int rows = pts.GetLength(0), cols = pts.GetLength(1), b0 = V.Count;
    for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) { V.Add(pts[i, j]); UV.Add(uvs[i, j]); }
    for (int i = 0; i < rows - 1; i++) for (int j = 0; j < cols - 1; j++) {
        int v00 = b0 + i * cols + j, v01 = v00 + 1, v10 = v00 + cols, v11 = v10 + 1;
        T.Add(v00); T.Add(v10); T.Add(v11); T.Add(v00); T.Add(v11); T.Add(v01);
    }
};
int meshCount = 0;
System.Func<Transform, string, Material, PhysicsMaterial, GameObject> flush = (parent, nm, mat, pm) => {
    if (T.Count == 0) { V.Clear(); UV.Clear(); return null; }
    var mesh = new Mesh { name = parent.name + "_" + nm };
    mesh.SetVertices(V); mesh.SetUVs(0, UV); mesh.SetTriangles(T, 0);
    mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
    AssetDatabase.AddObjectToAsset(mesh, container);
    var go = new GameObject(nm); go.transform.SetParent(parent, false); go.isStatic = true;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    if (pm != null) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.sharedMaterial = pm; }
    meshCount++;
    V.Clear(); UV.Clear(); T.Clear();
    return go;
};
// Лента вдоль ломаной pts (x, высота, z) с правыми векторами rights: поперечные отступы offs, подъёмы lifts
System.Action<Vector3[], Vector3[], float[], float[], float> ribbon = (pts, rights, offs, lifts, tile) => {
    var q = new Vector3[pts.Length, offs.Length]; var u = new Vector2[pts.Length, offs.Length];
    for (int i = 0; i < pts.Length; i++) for (int j = 0; j < offs.Length; j++) {
        q[i, j] = pts[i] + rights[i] * offs[j] + Vector3.up * lifts[j];
        u[i, j] = new Vector2(q[i, j].x / tile, q[i, j].z / tile);
    }
    strip(q, u);
};
var roads = empty("Road", root.transform, Vector3.zero);
var asphaltMat = roadMat("Road_Asphalt"); var gravelMat = roadMat("Road_Gravel"); var paintMat = roadMat("Road_PaintCity");
System.Action<string, Vector3[], Vector3[], bool> buildRoad = (nm, pts, rights, closed) => {
    int count = pts.Length, chunkRows = 20, ci = 0;
    int last = closed ? count : count - 1;
    for (int c0 = 0; c0 < last; c0 += chunkRows, ci++) {
        int rows = Mathf.Min(chunkRows, last - c0) + 1;
        var cp = new Vector3[rows]; var cr = new Vector3[rows];
        for (int r = 0; r < rows; r++) { cp[r] = pts[(c0 + r) % count]; cr[r] = rights[(c0 + r) % count]; }
        var chunk = empty(nm + "_" + ci.ToString("00"), roads.transform, Vector3.zero); chunk.isStatic = true;
        ribbon(cp, cr, new[] { -RoadHalf, RoadHalf }, new[] { 0f, 0f }, 16f);
        flush(chunk.transform, "Asphalt", asphaltMat, pmAsphalt);
        ribbon(cp, cr, new[] { -RoadHalf - ShoulderWidth, -RoadHalf }, new[] { -RoadSink, 0f }, 8f);
        ribbon(cp, cr, new[] { RoadHalf, RoadHalf + ShoulderWidth }, new[] { 0f, -RoadSink }, 8f);
        flush(chunk.transform, "Shoulder", gravelMat, pmGravel);
        // Разметка: сплошные по краям и пунктир по оси (штрих 3 м через 6 м)
        ribbon(cp, cr, new[] { -RoadHalf + 0.3f, -RoadHalf + 0.45f }, new[] { PaintLift, PaintLift }, 4f);
        ribbon(cp, cr, new[] { RoadHalf - 0.45f, RoadHalf - 0.3f }, new[] { PaintLift, PaintLift }, 4f);
        for (int r = 0; r + 1 < rows; r += 3) {
            var dp = new[] { cp[r], Vector3.Lerp(cp[r], cp[Mathf.Min(r + 1, rows - 1)], 0.5f) };
            var dr = new[] { cr[r], cr[r] };
            ribbon(dp, dr, new[] { -0.07f, 0.07f }, new[] { PaintLift, PaintLift }, 4f);
        }
        flush(chunk.transform, "Paint", paintMat, null);
    }
};
{
    var pts = new Vector3[n]; var rights = new Vector3[n];
    for (int i = 0; i < n; i++) { pts[i] = roadPoint(i); var d = dirAt(i); rights[i] = new Vector3(d.y, 0f, -d.x); }
    buildRoad("Ring", pts, rights, true);
    // Тупик: от оси трассы на север, 2 м шаг
    int sn = Mathf.RoundToInt(Vector2.Distance(spurA, spurB) / 2f) + 1;
    var sp = new Vector3[sn]; var sr = new Vector3[sn];
    for (int i = 0; i < sn; i++) { var p2 = Vector2.Lerp(spurA, spurB, i / (float)(sn - 1)); sp[i] = new Vector3(p2.x, spurH + 0.005f, p2.y); sr[i] = Vector3.right; }
    buildRoad("DeadEnd", sp, sr, false);
}

// --- Маяки ---
System.Func<string, Vector3, Transform> beacon = (nm, local) => {
    var b = empty(nm, root.transform, local);
    var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder); column.name = "Column";
    UnityEngine.Object.DestroyImmediate(column.GetComponent<Collider>());
    column.transform.SetParent(b.transform, false); column.transform.localPosition = new Vector3(0f, 20f, 0f); column.transform.localScale = new Vector3(3f, 20f, 3f);
    var cr2 = column.GetComponent<MeshRenderer>(); cr2.sharedMaterial = beaconMat; cr2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "Ring";
    UnityEngine.Object.DestroyImmediate(ring.GetComponent<Collider>());
    ring.transform.SetParent(b.transform, false); ring.transform.localPosition = new Vector3(0f, 0.05f, 0f); ring.transform.localScale = new Vector3(14f, 0.02f, 14f);
    var rr = ring.GetComponent<MeshRenderer>(); rr.sharedMaterial = beaconMat; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    var glow = empty("Glow", b.transform, new Vector3(0f, 3f, 0f)).AddComponent<Light>();
    glow.type = LightType.Point; glow.range = 20f; glow.intensity = 150f; glow.color = new Color(0.5f, 1f, 0.4f); glow.shadows = LightShadows.None;
    return b.transform;
};
System.Func<Vector2, Vector3> onRoad = q => roadPoint(nearest(q));
int startIdx = nearest(new Vector2(-30f, -168f));
var start = empty("Start", root.transform, roadPoint(startIdx) + Vector3.up * 0.8f);
start.transform.localRotation = Quaternion.Euler(0f, yawAt(startIdx), 0f);
var driveBeacon = beacon("DriveBeacon", onRoad(new Vector2(-27f, -100f)));
var turnBeacon = beacon("TurnBeacon", onRoad(new Vector2(-20f, 58f)));
var deadEndBeacon = beacon("DeadEndBeacon", new Vector3(spurB.x, spurH, spurB.y - 8f));
var rangeBeacon = beacon("RangeBeacon", roadPoint(rangeIdx));
int rampIdx = nearest(new Vector2(186f, -112f));
var rampBeacon = beacon("RampBeacon", roadPoint((rampIdx - 13 + n) % n));
var kitBeacon = beacon("KitBeacon", onRoad(new Vector2(110f, -176f)));
var kit = (GameObject)PrefabUtility.InstantiatePrefab(kitPrefab, scene);
kit.transform.SetParent(kitBeacon, false); kit.transform.localPosition = new Vector3(0f, 1f, 0f);

// --- Трамплин под левые колёса: бетонный кикер на левой полосе, вход пологий, выход крутой (профиль — парабола).
// Правая полоса перед ним перегорожена блоками, так что проехать можно только через него. На шаге «Трамплин»
// TutorialCourse ослабляет у трамплина стабилизацию машины, и она заваливается на бок или крышу ---
Transform rampT;
{
    var ramp = empty("Ramp", root.transform, roadPoint(rampIdx));
    ramp.transform.localRotation = Quaternion.Euler(0f, yawAt(rampIdx), 0f);
    rampT = ramp.transform;
    const float L = RampLength, x0 = -3.2f, x1 = -0.1f, hl = RampHeight;
    int N = 12;
    var v = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var tr = new System.Collections.Generic.List<int>();
    System.Action<Vector3, Vector3, Vector3, Vector3> quad = (a0, a1, a2, a3) => {
        int b0 = v.Count; v.Add(a0); v.Add(a1); v.Add(a2); v.Add(a3);
        foreach (var p in new[] { a0, a1, a2, a3 }) uv.Add(new Vector2(p.x + p.y, p.z) / 2f);
        tr.Add(b0); tr.Add(b0 + 1); tr.Add(b0 + 2); tr.Add(b0); tr.Add(b0 + 2); tr.Add(b0 + 3);
    };
    // Верх наклонён поперёк: у левого края полная высота, у правого — ноль
    for (int i = 0; i < N; i++) {
        float z0 = L * i / N, z1 = L * (i + 1) / N, f0 = (z0 / L) * (z0 / L), f1 = (z1 / L) * (z1 / L);
        var l0 = new Vector3(x0, hl * f0, z0); var r0 = new Vector3(x1, 0f, z0);
        var l1 = new Vector3(x0, hl * f1, z1); var r1 = new Vector3(x1, 0f, z1);
        quad(l0, l1, r1, r0);
        quad(new Vector3(x0, -0.3f, z0), new Vector3(x0, -0.3f, z1), l1, l0);
        quad(r0, r1, new Vector3(x1, -0.3f, z1), new Vector3(x1, -0.3f, z0));
    }
    quad(new Vector3(x0, -0.3f, L), new Vector3(x1, -0.3f, L), new Vector3(x1, 0f, L), new Vector3(x0, hl, L));
    var rmesh = new Mesh { name = "Ramp" }; rmesh.SetVertices(v); rmesh.SetUVs(0, uv); rmesh.SetTriangles(tr, 0);
    rmesh.RecalculateNormals(); rmesh.RecalculateTangents(); rmesh.RecalculateBounds();
    AssetDatabase.AddObjectToAsset(rmesh, container);
    ramp.AddComponent<MeshFilter>().sharedMesh = rmesh;
    ramp.AddComponent<MeshRenderer>().sharedMaterial = roadMat("Road_Curb");
    // Вогнутый профиль — коллайдер невыпуклый (трамплин статичный)
    var rc = ramp.AddComponent<MeshCollider>(); rc.sharedMesh = rmesh; rc.sharedMaterial = pmAsphalt;
    ramp.isStatic = true;
    // Блоки поперёк правой полосы у начала трамплина
    var d = dirAt(rampIdx); var right = new Vector3(d.y, 0f, -d.x);
    var bp = roadPoint(rampIdx) + right * 2.6f;
    put(blockPrefab, root.transform, bp.x, bp.z, yawAt(rampIdx) + 90f);
    // Конусы по краю, знак перед трамплином
    for (int k = 1; k <= 3; k++) {
        var cp = roadPoint((rampIdx - k * 2 + n) % n) - right * 3.9f;
        put(conePrefab, root.transform, cp.x, cp.z, 0f);
    }
    var sp = roadPoint((rampIdx - 20 + n) % n) + right * 6f;
    put(signPrefab, root.transform, sp.x, sp.z, yawAt(rampIdx) + 180f);
}

// --- Засада: маяк остановки и гнездо гранатомётчика на холме — бетонные блоки и гранатомёт врагов (из EnemyUaz).
// TutorialRpg стреляет в остановившуюся машину самонаводящейся гранатой ---
var stopBeacon = beacon("StopBeacon", roadPoint(stopIdx));
RacingProject.Tutorial.TutorialRpg rpg;
{
    var nest = empty("RpgNest", root.transform, new Vector3(nest2.x, groundAt(nest2.x, nest2.y), nest2.y));
    var aim = roadPoint(stopIdx) + Vector3.up * 1f;
    var toRoad = aim - nest.transform.localPosition; toRoad.y = 0f;
    float yaw = Mathf.Atan2(toRoad.x, toRoad.z) * Mathf.Rad2Deg;
    var fwd = toRoad.normalized; var side = new Vector3(fwd.z, 0f, -fwd.x);
    // Бруствер из блоков буквой П: поперёк спереди и вдоль по бокам
    foreach (var o in new[] { new Vector3(0f, 0f, 2.2f), new Vector3(-2.6f, 90f, 0.4f), new Vector3(2.6f, 90f, 0.4f) }) {
        var bp = nest.transform.localPosition + side * o.x + fwd * o.z;
        put(blockPrefab, nest.transform.parent, bp.x, bp.z, yaw + o.y);
    }
    var dp = nest.transform.localPosition - side * 3f - fwd * 2f; put(roadBarrelPrefab, nest.transform.parent, dp.x, dp.z, 0f);
    var uazPrefab = P("Assets/Prefabs/Enemy/EnemyUaz.prefab");
    Transform src = null; foreach (var t in uazPrefab.GetComponentsInChildren<Transform>(true)) if (t.name == "RocketLauncher") src = t;
    var launcher = (GameObject)UnityEngine.Object.Instantiate(src.gameObject);
    launcher.name = "Rpg";
    UnityEngine.Object.DestroyImmediate(launcher.GetComponent<RacingProject.Enemy.EnemyRocketLauncher>());
    foreach (var t in launcher.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("GunnerGrip")) UnityEngine.Object.DestroyImmediate(t.gameObject);
    launcher.transform.SetParent(nest.transform, false);
    // Над бруствером (блок ~1,2 м), труба смотрит на точку остановки
    launcher.transform.localPosition = new Vector3(0f, 1.9f, 0f);
    launcher.transform.rotation = Quaternion.LookRotation((root.transform.TransformPoint(aim) - launcher.transform.position).normalized);
    var audio = launcher.GetComponent<AudioSource>();
    audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Content/Sound/Generated/HeavyShot.wav");
    audio.playOnAwake = false; audio.spatialBlend = 0.5f; audio.volume = 1f;
    // Красный огонёк — видно, откуда стреляли
    var mark = empty("Light", nest.transform, new Vector3(0f, 3f, 0f)).AddComponent<Light>();
    mark.type = LightType.Point; mark.range = 20f; mark.intensity = 700f; mark.color = new Color(1f, 0.35f, 0.25f); mark.shadows = LightShadows.None;
    rpg = launcher.AddComponent<RacingProject.Tutorial.TutorialRpg>();
    Transform firePoint = null, loaded = null; ParticleSystem blast = null;
    foreach (var t in launcher.GetComponentsInChildren<Transform>(true)) {
        if (t.name == "FirePoint") firePoint = t;
        if (t.name == "pCylinder8") loaded = t;
        if (t.name == "Backblast") blast = t.GetComponent<ParticleSystem>();
    }
    var so = new SerializedObject(rpg);
    so.FindProperty("firePoint").objectReferenceValue = firePoint;
    so.FindProperty("rocketPrefab").objectReferenceValue = P("Assets/Prefabs/Enemy/EnemyRocket.prefab").GetComponent<RacingProject.Enemy.EnemyRocket>();
    so.FindProperty("explosionPrefab").objectReferenceValue = explosion;
    so.FindProperty("backblast").objectReferenceValue = blast;
    so.FindProperty("launchAudio").objectReferenceValue = audio;
    so.FindProperty("loadedRocket").objectReferenceValue = loaded.gameObject;
    so.ApplyModifiedPropertiesWithoutUndo();
    // Просвет линии огня над землёй
    float minClear = float.MaxValue; var from = nest.transform.localPosition + Vector3.up * 1.9f;
    for (int k = 1; k < 40; k++) { var q = Vector3.Lerp(from, aim, k / 40f); minClear = Mathf.Min(minClear, q.y - groundAt(q.x, q.z)); }
    log.AppendLine("засада: до дороги " + toRoad.magnitude.ToString("0") + " м, над дорогой " + (from.y - aim.y).ToString("0.0") + " м, просвет " + minClear.ToString("0.0") + " м");
}

// --- Фонари вдоль трассы (на фонаре — свет) и ЛЭП вдоль длинной прямой ---
var lamps = empty("Lamps", root.transform, Vector3.zero);
for (int i = 0, side = 0; i < n; i += 22, side++) {
    var d = dirAt(i); var right = new Vector3(d.y, 0f, -d.x); float sgn = side % 2 == 0 ? 1f : -1f;
    var p = roadPoint(i) + right * sgn * (RoadHalf + ShoulderWidth + 1.2f);
    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(path[rampIdx].x, path[rampIdx].y)) < 12f) continue;
    // Стрела фонаря смотрит вдоль +Z префаба — разворачиваем к дороге
    float yaw = Mathf.Atan2(-right.x * sgn, -right.z * sgn) * Mathf.Rad2Deg;
    var lamp = put(lampPrefab, lamps.transform, p.x, p.z, yaw);
    var l = empty("Light", lamp.transform, new Vector3(0f, 15.2f, 4.4f)).AddComponent<Light>();
    l.type = LightType.Point; l.range = 42f; l.intensity = 900f; l.color = new Color(1f, 0.82f, 0.55f); l.shadows = LightShadows.None;
}
var poles = empty("PowerLine", root.transform, Vector3.zero);
for (int i = nearest(new Vector2(20f, 80f)); i < nearest(new Vector2(200f, 40f)); i += 24) {
    var d = dirAt(i); var right = new Vector3(d.y, 0f, -d.x);
    var p = roadPoint(i) - right * 18f;
    put(polePrefab, poles.transform, p.x, p.z, yawAt(i) + 90f);
}

// --- КПП на старте: будка, сетка-рабица по обе стороны дороги, ангар и бетонные блоки ---
var props = empty("Props", root.transform, Vector3.zero);
{
    int gi = nearest(new Vector2(-30f, -150f)); var d = dirAt(gi); var right = new Vector3(d.y, 0f, -d.x); var fwd = new Vector3(d.x, 0f, d.y);
    var c = roadPoint(gi);
    var booth = c + right * 9f; put(boothPrefab, props.transform, booth.x, booth.z, yawAt(gi) - 90f);
    for (int sgn = -1; sgn <= 1; sgn += 2)
        for (int k = 0; k < 16; k++) {
            // Секция вытянута вдоль своей X, а её меш смещён от опоры на 2,72..5,56 м — ставим опору так, чтобы
            // секции шли поперёк дороги одна за другой; на левой стороне секция развёрнута на 180°
            var fp = c + right * sgn * (RoadHalf + ShoulderWidth + 1.5f + k * 2.84f - 2.72f);
            var f = put(fencePrefab, props.transform, fp.x, fp.z, yawAt(gi) + (sgn < 0 ? 180f : 0f));
            var bc = f.AddComponent<BoxCollider>(); bc.center = new Vector3(4.14f, 1.05f, 0f); bc.size = new Vector3(2.84f, 2.1f, 0.2f);
        }
    foreach (float off in new[] { 7.5f, -7.5f }) { var bp = c + right * off + fwd * 4f; put(blockPrefab, props.transform, bp.x, bp.z, yawAt(gi)); }
    // Ангар (модель в масштабе 1:10 к нужному) — на землю по нижней грани
    var hangar = put(hangarPrefab, props.transform, -78f, -175f, 95f);
    hangar.transform.localScale = Vector3.one * 0.1f;
    var hb = new Bounds(); bool first = true;
    foreach (var r in hangar.GetComponentsInChildren<Renderer>()) { if (first) { hb = r.bounds; first = false; } else hb.Encapsulate(r.bounds); }
    hangar.transform.position += Vector3.up * (origin.y + groundAt(-78f, -175f) - hb.min.y - 0.3f);
    var hcol = hangar.AddComponent<BoxCollider>();
    hcol.center = hangar.transform.InverseTransformPoint(hb.center + Vector3.up * (origin.y + groundAt(-78f, -175f) - hb.min.y - 0.3f));
    hcol.size = new Vector3(hb.size.x / 0.1f, hb.size.y / 0.1f, hb.size.z / 0.1f);
    // Трансформаторная будка и сгоревшие машины у ангара
    put(transformerPrefab, props.transform, -62f, -140f, 10f);
    put(burntPrefab, props.transform, -50f, -182f, 37f);
    put(burntHatchPrefab, props.transform, -58f, -150f, 120f);
}
// Конусы на внутренних краях поворотов «змейки»
for (int i = nearest(new Vector2(-10f, -72f)); i < nearest(new Vector2(-20f, 58f)); i += 6) {
    var d0 = dirAt(i); var d1 = dirAt((i + 3) % n); float turn = d0.x * d1.y - d0.y * d1.x;
    if (Mathf.Abs(turn) < 0.02f) continue;
    var right = new Vector3(d0.y, 0f, -d0.x);
    var cp = roadPoint(i) + right * (turn > 0f ? 1f : -1f) * (RoadHalf + 0.9f);
    put(conePrefab, props.transform, cp.x, cp.z, 0f);
}
// Знак перед «змейкой»
{
    int si = nearest(new Vector2(-28f, -95f)); var d = dirAt(si); var right = new Vector3(d.y, 0f, -d.x); var sp = roadPoint(si) + right * 6f;
    put(signPrefab, props.transform, sp.x, sp.z, yawAt(si) + 180f);
}

// --- Тупик: два ряда гаражей воротами к проезду, в конце — бетонные блоки ---
{
    var garages = empty("Garages", root.transform, Vector3.zero);
    float gx = spurA.x; float z0 = spurA.y + 18f;
    var west = put(garagePrefab, garages.transform, gx - 13.5f, z0 + 24f, 90f);
    var east = put(garagePrefab, garages.transform, gx + 13.5f, z0 + 24f, -90f);
    west.transform.localPosition = new Vector3(west.transform.localPosition.x, spurH - RoadSink, west.transform.localPosition.z);
    east.transform.localPosition = new Vector3(east.transform.localPosition.x, spurH - RoadSink, east.transform.localPosition.z);
    for (int k = -1; k <= 1; k++) {
        var b = put(blockPrefab, garages.transform, gx + k * 4.9f, spurB.y + 1.5f, 0f);
        b.transform.localPosition = new Vector3(b.transform.localPosition.x, spurH, b.transform.localPosition.z);
    }
    put(roadBarrelPrefab, garages.transform, gx - 5.5f, spurB.y - 3f, 0f);
    put(burntHatchPrefab, garages.transform, gx + 9f, spurA.y + 10f, 15f);
}

// --- Стрельбище: мишени в поле (одна — за спиной у машины на маяке), бетонные блоки и противотанковые тетраподы ---
var targetsRoot = empty("Targets", root.transform, Vector3.zero);
System.Func<string, float, float, float, RacingProject.Tutorial.TutorialTarget> target = (nm, x, z, yaw) => {
    var t = empty(nm, targetsRoot.transform, new Vector3(x, groundAt(x, z), z)); t.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    var intact = (GameObject)PrefabUtility.InstantiatePrefab(sedanPrefab, scene); intact.transform.SetParent(t.transform, false);
    var wreck = (GameObject)PrefabUtility.InstantiatePrefab(burntPrefab, scene); wreck.transform.SetParent(t.transform, false);
    // Взрыв бочки меряет расстояние до коллайдера (ClosestPoint), а он работает только с выпуклыми MeshCollider
    foreach (var mc in t.GetComponentsInChildren<MeshCollider>(true)) mc.convex = true;
    var tt = t.AddComponent<RacingProject.Tutorial.TutorialTarget>();
    var so = new SerializedObject(tt);
    so.FindProperty("intact").objectReferenceValue = intact;
    so.FindProperty("wreck").objectReferenceValue = wreck;
    so.FindProperty("explosionPrefab").objectReferenceValue = explosion;
    so.ApplyModifiedPropertiesWithoutUndo();
    wreck.SetActive(false);
    // Красноватая подсветка сверху: ночью мишень видно издалека
    var mark = empty("Light", t.transform, new Vector3(0f, 7f, 0f)).AddComponent<Light>();
    mark.type = LightType.Point; mark.range = 18f; mark.intensity = 500f; mark.color = new Color(1f, 0.55f, 0.4f); mark.shadows = LightShadows.None;
    return tt;
};
var t1 = target("TargetField", 150f, 8f, 70f);
var t2 = target("TargetSouth", 172f, -48f, 160f);
var t3 = target("TargetBehind", 228f, 52f, 20f);
var t4 = target("TargetBarrel", 140f, -30f, 110f);
var barrel = put(barrelPrefab, root.transform, 144f, -26f, 0f);
{
    var range = empty("Range", root.transform, Vector3.zero);
    foreach (var bp in new[] { new Vector3(160f, 0f, 22f), new Vector3(185f, 0f, -24f), new Vector3(132f, 0f, -8f) }) put(blockPrefab, range.transform, bp.x, bp.z, (float)rng.NextDouble() * 40f - 20f);
    for (int k = 0; k < 9; k++) { float a = k * 0.5f; put(tetrapodPrefab, range.transform, 128f + Mathf.Cos(a) * 6f + k * 2.5f, 25f + Mathf.Sin(a) * 3f, k * 37f); }
    put(burntPrefab, range.transform, 196f, -60f, 200f);
    put(roadBarrelPrefab, range.transform, 211f, 8f, 0f);
    put(roadBarrelPrefab, range.transform, 212f, 5f, 0f);
}

// --- Панельки на холмах вдали: город за лесом ---
{
    var city = empty("DistantCity", root.transform, Vector3.zero);
    var spots = new[] { new Vector3(-250f, 0f, 255f), new Vector3(-205f, 0f, 280f), new Vector3(240f, 0f, 270f), new Vector3(285f, 0f, -190f), new Vector3(-285f, 0f, -120f) };
    for (int k = 0; k < spots.Length; k++) {
        var b = put(panelPrefabs[k % panelPrefabs.Length], city.transform, spots[k].x, spots[k].z, Mathf.Atan2(-spots[k].x, -spots[k].z) * Mathf.Rad2Deg);
        b.transform.localPosition += Vector3.down * 1f;
    }
}

// --- Экран подсказок ---
int uiLayer = LayerMask.NameToLayer("UI");
System.Func<string, Transform, RectTransform> node = (nm, parent) => {
    var g = new GameObject(nm, typeof(RectTransform)); g.layer = uiLayer;
    var r = (RectTransform)g.transform; r.SetParent(parent, false); return r;
};
System.Func<RectTransform, RectTransform> stretch = r => {
    r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; return r;
};
System.Action<RectTransform, Vector2, float, float, float, float> place = (r, anchor, x, y, w, h) => {
    r.anchorMin = r.anchorMax = r.pivot = anchor;
    r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
};
System.Func<string, Transform, Color, UnityEngine.UI.Image> image = (nm, parent, color) => {
    var im = node(nm, parent).gameObject.AddComponent<UnityEngine.UI.Image>(); im.color = color; im.raycastTarget = false; return im;
};
System.Func<string, Transform, string, float, Color, TMPro.TextMeshProUGUI> text = (nm, parent, s, size, color) => {
    var t = node(nm, parent).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
    t.font = bodyFont; t.text = s; t.fontSize = size; t.color = color; t.alignment = TMPro.TextAlignmentOptions.Center;
    t.raycastTarget = false; t.enableWordWrapping = true;
    return t;
};
System.Action<Transform, float, float> frame = (parent, w, h) => {
    foreach (var e in new[] { new Vector4(0f, h / 2f, w, 3f), new Vector4(0f, -h / 2f, w, 3f), new Vector4(-w / 2f, 0f, 3f, h), new Vector4(w / 2f, 0f, 3f, h) }) {
        var l = image("Frame", parent, phosphor);
        l.rectTransform.anchorMin = l.rectTransform.anchorMax = l.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        l.rectTransform.anchoredPosition = new Vector2(e.x, e.y); l.rectTransform.sizeDelta = new Vector2(e.z, e.w);
    }
};
System.Func<string, Transform, string, float, float, float, UnityEngine.UI.Button> button = (nm, parent, label, x, y, w) => {
    var r = node(nm, parent); place(r, new Vector2(0.5f, 0.5f), x, y, w, 64f);
    var bar = r.gameObject.AddComponent<UnityEngine.UI.Image>(); bar.color = phosphor;
    var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = bar;
    var cb = b.colors; cb.normalColor = new Color(1f, 1f, 1f, 0.06f); cb.highlightedColor = new Color(1f, 1f, 1f, 0.25f);
    cb.pressedColor = new Color(1f, 1f, 1f, 0.45f); cb.selectedColor = new Color(1f, 1f, 1f, 0.06f); cb.fadeDuration = 0.05f; b.colors = cb;
    var nav = b.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.None; b.navigation = nav;
    var lb = text("Text (TMP)", r, label, 30f, phosphor); stretch(lb.rectTransform);
    lb.fontStyle = TMPro.FontStyles.UpperCase | TMPro.FontStyles.Bold; lb.enableWordWrapping = false;
    return b;
};

var hud = node("TutorialHud", root.transform).gameObject;
var canvas = hud.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
var scaler = hud.AddComponent<UnityEngine.UI.CanvasScaler>();
scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
hud.AddComponent<UnityEngine.UI.GraphicRaycaster>();

// Подсказка шага вверху по центру: на месте табло волны и счёта
float sw = 1040f, sh = 200f;
var stepPanel = node("StepPanel", hud.transform); place(stepPanel, new Vector2(0.5f, 1f), 0f, -20f, sw, sh);
stepPanel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(bg.r, bg.g, bg.b, 0.88f);
stepPanel.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
frame(stepPanel, sw, sh);
var counter = text("Counter", stepPanel, "Обучение · шаг 1 из 13", 18f, dim); place(counter.rectTransform, new Vector2(0.5f, 1f), 0f, -12f, sw - 40f, 24f);
counter.fontStyle = TMPro.FontStyles.UpperCase | TMPro.FontStyles.Bold; counter.characterSpacing = 6f;
var stepTitle = text("Title", stepPanel, "Газ", 40f, phosphor); place(stepTitle.rectTransform, new Vector2(0.5f, 1f), 0f, -38f, sw - 40f, 48f);
stepTitle.font = titleFont; stepTitle.fontStyle = TMPro.FontStyles.UpperCase;
var stepText = text("Text", stepPanel, "", 25f, new Color(0.88f, 0.94f, 0.84f)); place(stepText.rectTransform, new Vector2(0.5f, 1f), 0f, -88f, sw - 60f, 74f);
stepText.richText = true;
var progress = text("Progress", stepPanel, "", 22f, amber); place(progress.rectTransform, new Vector2(0.5f, 0f), 0f, 8f, sw - 40f, 28f);
progress.fontStyle = TMPro.FontStyles.Bold;

// «Выполнено» над прицелом
var done = text("DoneBanner", hud.transform, "Выполнено", 64f, phosphor); place(done.rectTransform, new Vector2(0.5f, 0.5f), 0f, 170f, 900f, 90f);
done.font = titleFont; done.fontStyle = TMPro.FontStyles.UpperCase; done.enableWordWrapping = false;
done.outlineWidth = 0.15f; done.outlineColor = new Color32(0, 0, 0, 200);

// Экран «Обучение пройдено»
var finish = node("FinishScreen", hud.transform); stretch(finish);
var shade = finish.gameObject.AddComponent<UnityEngine.UI.Image>(); shade.color = new Color(0f, 0f, 0f, 0.6f);
float pw = 820f, ph = 470f;
var panel = node("Panel", finish); place(panel, new Vector2(0.5f, 0.5f), 0f, 0f, pw, ph);
panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(bg.r, bg.g, bg.b, 0.97f);
frame(panel, pw, ph);
var fTitle = text("Title", panel, "Обучение пройдено", 56f, phosphor); place(fTitle.rectTransform, new Vector2(0.5f, 0.5f), 0f, 160f, pw - 40f, 80f);
fTitle.font = titleFont; fTitle.fontStyle = TMPro.FontStyles.UpperCase; fTitle.enableWordWrapping = false;
var fText = text("Text", panel, "Машина, пулемёт и ремонт освоены. Город ждёт — пора прорываться из Фрязино.", 26f, new Color(0.88f, 0.94f, 0.84f));
place(fText.rectTransform, new Vector2(0.5f, 0.5f), 0f, 60f, pw - 100f, 100f);
var bPlay = button("StartGame", panel, "Начать игру", 0f, -60f, 520f);
var bMenu = button("ToMenu", panel, "В меню", 0f, -140f, 520f);
var scan = node("Scanlines", panel).gameObject.AddComponent<UnityEngine.UI.RawImage>();
stretch(scan.rectTransform); scan.texture = scanTex; scan.raycastTarget = false; scan.uvRect = new Rect(0f, 0f, 1f, ph / 4f);

// Звуки шага и ремкомплекта
System.Func<string, float, AudioSource> sound = (nm, volume) => {
    var a = empty(nm, root.transform, Vector3.zero).AddComponent<AudioSource>();
    a.clip = pickupClip; a.playOnAwake = false; a.spatialBlend = 0f; a.volume = volume;
    return a;
};
var doneSound = sound("DoneSound", 0.6f);
var pickupSound = sound("PickupSound", 1f);

var course = root.AddComponent<RacingProject.Tutorial.TutorialCourse>();
{
    var so = new SerializedObject(course);
    so.FindProperty("start").objectReferenceValue = start.transform;
    so.FindProperty("driveBeacon").objectReferenceValue = driveBeacon;
    so.FindProperty("turnBeacon").objectReferenceValue = turnBeacon;
    so.FindProperty("deadEndBeacon").objectReferenceValue = deadEndBeacon;
    so.FindProperty("rangeBeacon").objectReferenceValue = rangeBeacon;
    so.FindProperty("rampBeacon").objectReferenceValue = rampBeacon;
    so.FindProperty("ramp").objectReferenceValue = rampT;
    so.FindProperty("stopBeacon").objectReferenceValue = stopBeacon;
    so.FindProperty("rpg").objectReferenceValue = rpg;
    so.FindProperty("kitBeacon").objectReferenceValue = kitBeacon;
    so.FindProperty("repairKit").objectReferenceValue = kit;
    var ts = so.FindProperty("targets"); ts.arraySize = 3;
    ts.GetArrayElementAtIndex(0).objectReferenceValue = t1; ts.GetArrayElementAtIndex(1).objectReferenceValue = t2; ts.GetArrayElementAtIndex(2).objectReferenceValue = t3;
    so.FindProperty("barrel").objectReferenceValue = barrel.GetComponent<RacingProject.Props.ExplosiveBarrel>();
    so.FindProperty("stepPanel").objectReferenceValue = stepPanel.gameObject;
    so.FindProperty("stepCounter").objectReferenceValue = counter;
    so.FindProperty("stepTitle").objectReferenceValue = stepTitle;
    so.FindProperty("stepText").objectReferenceValue = stepText;
    so.FindProperty("stepProgress").objectReferenceValue = progress;
    so.FindProperty("doneBanner").objectReferenceValue = done.gameObject;
    so.FindProperty("finishScreen").objectReferenceValue = finish.gameObject;
    so.FindProperty("doneSound").objectReferenceValue = doneSound;
    so.FindProperty("pickupSound").objectReferenceValue = pickupSound;
    so.ApplyModifiedPropertiesWithoutUndo();
}
UnityEditor.Events.UnityEventTools.AddPersistentListener(bPlay.onClick, course.StartGame);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bMenu.onClick, course.ToMenu);
done.gameObject.SetActive(false);
finish.gameObject.SetActive(false);

// Трава и кусты: генератор обходит коллайдеры (дорога, постройки, блоки); кроме того — ни травы, ни кустов
// на стрельбище и у гнезда засады
var vs = new RacingProject.EditorTools.VegetationGenerator.Settings();
vs.Seed = 17;
vs.KeepOut = w => {
    var q = new Vector2(w.x - origin.x, w.z - origin.z);
    if (Vector2.Distance(q, new Vector2(pads[1].x, pads[1].y)) < pads[1].z + 10f) return true;
    return Vector2.Distance(q, nest2) < 6f;
};
log.AppendLine(RacingProject.EditorTools.VegetationGenerator.Populate(terrain, vs));

AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);

// Сцена в сборке после SovietCity
var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
if (!list.Exists(s => s.path == scenePath)) list.Add(new EditorBuildSettingsScene(scenePath, true));
EditorBuildSettings.scenes = list.ToArray();

log.AppendLine("Tutorial: сцена " + scenePath + ", трасса " + (n * 2) + " м, мешей дороги " + meshCount);
return log.ToString();
