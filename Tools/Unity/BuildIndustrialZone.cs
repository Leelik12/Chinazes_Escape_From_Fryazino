// Промзона вокруг старого ангара (Environment/Industrial).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Каждый запуск пересобирает Environment/Industrial с нуля. Выравнивание площадки идёт к одной и той же высоте,
// поэтому повторный запуск рельеф не портит.
var env = GameObject.Find("Environment").transform;
var terrain = Terrain.activeTerrain; var td = terrain.terrainData;
string P = "Assets/Content/SmallEnvironment/Road props for games/Prefabs/";
string WIRE = "Assets/Content/SmallEnvironment/Assets_FenceChained/models/chainlink_barbwire-2.FBX";
var log = new System.Text.StringBuilder();

// Площадка и забор (мировые координаты)
float padX0 = 1000f, padX1 = 1150f, padZ0 = 165f, padZ1 = 290f, padH = 68f, blend = 22f;
float fx0 = 1006f, fx1 = 1144f, fz0 = 171f, fz1 = 284f;
float gateX0 = 1010f, gateX1 = 1032f; // въезд с севера, куда подходит грунтовка от района

// --- 1. Выравниваем площадку с плавными откосами ---
int hr = td.heightmapResolution; var hm = td.GetHeights(0, 0, hr, hr);
for (int iz = 0; iz < hr; iz++) for (int ix = 0; ix < hr; ix++) {
    float x = ix / (float)(hr - 1) * td.size.x, z = iz / (float)(hr - 1) * td.size.z;
    float dx = Mathf.Max(0f, Mathf.Max(padX0 - x, x - padX1)), dz = Mathf.Max(0f, Mathf.Max(padZ0 - z, z - padZ1));
    float d = Mathf.Sqrt(dx * dx + dz * dz);
    if (d >= blend) continue;
    float w = Mathf.SmoothStep(0f, 1f, d / blend);
    hm[iz, ix] = Mathf.Lerp(padH / td.size.y, hm[iz, ix], w);
}
td.SetHeights(0, 0, hm);
// Земля площадки — городская (слой 1), деревья на площадке убираем
int res = td.alphamapResolution; var alpha = td.GetAlphamaps(0, 0, res, res);
for (int iz = 0; iz < res; iz++) for (int ix = 0; ix < res; ix++) {
    float x = (ix + 0.5f) / res * td.size.x, z = (iz + 0.5f) / res * td.size.z;
    float dx = Mathf.Max(0f, Mathf.Max(padX0 - x, x - padX1)), dz = Mathf.Max(0f, Mathf.Max(padZ0 - z, z - padZ1));
    float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / (blend * 0.6f));
    if (w <= 0f) continue;
    for (int l = 0; l < 3; l++) alpha[iz, ix, l] *= 1f - w;
    alpha[iz, ix, 1] += w;
}
td.SetAlphamaps(0, 0, alpha);
var trees = new System.Collections.Generic.List<TreeInstance>(td.treeInstances); int treesBefore = trees.Count;
trees.RemoveAll(ti => { var w = Vector3.Scale(ti.position, td.size); return w.x > padX0 - 8 && w.x < padX1 + 8 && w.z > padZ0 - 8 && w.z < padZ1 + 8; });
td.treeInstances = trees.ToArray();
log.AppendLine("terrain trees removed: " + (treesBefore - trees.Count));

// --- 2. Коллайдер ангара: у модели его не было, машины и пули проходили насквозь ---
var hangarMesh = GameObject.Find("Environment/Old Hangar/Cube");
var smr = hangarMesh.GetComponent<SkinnedMeshRenderer>();
string colPath = "Assets/Content/Buildings/Old Hangar/OldHangar_Collider.asset";
var baked = AssetDatabase.LoadAssetAtPath<Mesh>(colPath);
if (baked == null) { baked = new Mesh(); smr.BakeMesh(baked, true); baked.name = "OldHangar_Collider"; AssetDatabase.CreateAsset(baked, colPath); }
var colT = hangarMesh.transform.Find("Collider");
if (colT == null) {
    colT = new GameObject("Collider").transform; colT.SetParent(hangarMesh.transform, false);
    colT.gameObject.AddComponent<MeshCollider>().sharedMesh = baked;
}
// BakeMesh с useScale учитывает масштаб, поэтому дочерний коллайдер компенсирует масштаб родителя
var ls = hangarMesh.transform.lossyScale; colT.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
log.AppendLine("hangar collider bounds " + colT.GetComponent<MeshCollider>().bounds.size.ToString("F0") + " renderer " + smr.bounds.size.ToString("F0"));

// --- 3. Пересобираем Industrial ---
var old = env.Find("Industrial"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var zone = new GameObject("Industrial").transform; zone.SetParent(env, false);
var wasteland = env.Find("Wasteland");
var oldBarrels = new System.Collections.Generic.List<GameObject>();
foreach (Transform c in wasteland) if (c.name.StartsWith("BurningBarrel_Industrial")) oldBarrels.Add(c.gameObject);
foreach (var g in oldBarrels) UnityEngine.Object.DestroyImmediate(g);
var rnd = new System.Random(42);
System.Func<float> rf = () => (float)rnd.NextDouble();
System.Func<string, Vector3, Quaternion, float, Transform, Transform> put = (path, pos, rot, s, parent) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
    if (pos.y == 0f) pos.y = terrain.SampleHeight(pos);
    go.transform.SetPositionAndRotation(pos, rot); go.transform.localScale = src.transform.localScale * s;
    return go.transform;
};

// Забор: со стороны холма (юг, запад) — бетонные плиты стоймя, со стороны озера (север, восток) — сетка с колючкой
var fence = new GameObject("Fence").transform; fence.SetParent(zone, false);
float slab = 3.5f * 2f, wire = 2.8f * 2f;
System.Action<Vector3, Vector3, bool, float, float> side = (a, b, concrete, skip0, skip1) => {
    var dir = (b - a); float len = dir.magnitude; dir /= len;
    float step = concrete ? slab : wire; int n = Mathf.CeilToInt(len / step);
    // У LookRotation(нормаль) локальная X идёт против направления стороны: длинная сторона панели ложится вдоль забора
    var rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir));
    var part = new GameObject(concrete ? "Concrete" : "Wire").transform; part.SetParent(fence, false);
    var mid = (a + b) / 2f; part.SetPositionAndRotation(new Vector3(mid.x, terrain.SampleHeight(mid), mid.z), rot);
    for (int i = 0; i < n; i++) {
        float along = step * (i + 0.5f);
        if (along > skip0 && along < skip1) continue;
        var p = a + dir * along; p.y = terrain.SampleHeight(p);
        // Плита лежит плашмя (1.7 м в ширину): ставим её на ребро и поднимаем на половину ширины
        if (concrete) put(P + "Road_slab_2.prefab", p + Vector3.up * 1.7f, rot * Quaternion.Euler(90f, 0f, 0f), 2f, part);
        else put(WIRE, p, rot, 2f, part);
    }
    if (!concrete) { // у сетки нет своих коллайдеров — коробки на отрезки стороны (с проёмом ворот, если он есть)
        var pieces = skip1 > skip0 ? new[] { new Vector2(0f, skip0), new Vector2(skip1, len) } : new[] { new Vector2(0f, len) };
        foreach (var pc in pieces) {
            var bc = part.gameObject.AddComponent<BoxCollider>();
            bc.center = new Vector3(len / 2f - (pc.x + pc.y) / 2f, 2.6f, 0f); bc.size = new Vector3(pc.y - pc.x, 5.2f, 0.4f);
        }
    }
};
side(new Vector3(fx0, 0, fz0), new Vector3(fx1, 0, fz0), true, 0, 0);
side(new Vector3(fx0, 0, fz1), new Vector3(fx0, 0, fz0), true, 0, 0);
side(new Vector3(fx0, 0, fz1), new Vector3(fx1, 0, fz1), false, gateX0 - fx0, gateX1 - fx0);
side(new Vector3(fx1, 0, fz1), new Vector3(fx1, 0, fz0), false, 0, 0);

// Заводоуправление вдоль восточного забора
string B = "Assets/Content/Buildings/russian_buildings/prefabs/buildings/rus_build_";
put(B + "4et_01a.prefab", new Vector3(1125f, 0f, 245f), Quaternion.Euler(0f, -90f, 0f), 1.5f, zone).name = "Office";

// Штабеля плит, сваи, подстанции, тетраподы, сгоревшие машины
var stock = new GameObject("Stock").transform; stock.SetParent(zone, false);
foreach (var c in new[] { new Vector3(1017f, 0, 215f), new Vector3(1017f, 0, 238f), new Vector3(1060f, 0, 186f), new Vector3(1100f, 0, 197f) }) {
    int layers = 3 + rnd.Next(4); float yaw = c.z < 200f ? 0f : 90f;
    for (int i = 0; i < layers; i++) {
        var p = c + new Vector3((rf() - 0.5f) * 0.6f, terrain.SampleHeight(c) + 0.4f * i + 0.02f, (rf() - 0.5f) * 0.6f);
        put(P + "Road_slab.prefab", p, Quaternion.Euler(0f, yaw + (rf() - 0.5f) * 6f, 0f), 2f, stock);
    }
}
foreach (var c in new[] { new Vector3(1080f, 0, 186f), new Vector3(1040f, 0, 190f) }) { // сваи пирамидкой
    float y0 = terrain.SampleHeight(c);
    for (int row = 0; row < 3; row++) for (int i = 0; i < 4 - row; i++)
        put(P + "Piles.prefab", c + new Vector3((i - (3 - row) / 2f) * 0.85f, y0 + 0.4f + row * 0.72f, 0f), Quaternion.identity, 2f, stock);
}
var power = new GameObject("Power").transform; power.SetParent(zone, false);
put("Assets/Content/SmallEnvironment/Transformer_substation_booth/Prefabs/Transformer_substation_booth.prefab", new Vector3(1137f, 0f, 182f), Quaternion.identity, 2f, power);
put("Assets/Content/SmallEnvironment/Transformer_substation_booth/Prefabs/Transformer_substation_booth.prefab", new Vector3(1128f, 0f, 182f), Quaternion.identity, 2f, power);
put("Assets/Content/SmallEnvironment/transformer_box/prefab/transformer_box.prefab", new Vector3(1020f, 0f, 192f), Quaternion.Euler(0f, 90f, 0f), 1.5f, power);
var blocks = new GameObject("Tetrapods").transform; blocks.SetParent(zone, false);
foreach (var c in new[] { new Vector3(1021f, 0, 292f), new Vector3(1048f, 0, 276f), new Vector3(1092f, 0, 276f) })
    for (int i = 0; i < 5; i++)
        put(P + (rnd.Next(2) == 0 ? "Tetrapod.prefab" : "TetrapodUp.prefab"), c + new Vector3((rf() - 0.5f) * 9f, 0f, (rf() - 0.5f) * 5f), Quaternion.Euler(0f, rf() * 360f, 0f), 2f, blocks);
var wrecks = new GameObject("Wrecks").transform; wrecks.SetParent(zone, false);
put("Assets/Prefabs/Wasteland/BurntSedan.prefab", new Vector3(1036f, 0f, 296f), Quaternion.Euler(0f, 70f, 0f), 1.95f, wrecks);
put("Assets/Prefabs/Wasteland/BurntHatchback.prefab", new Vector3(1100f, 0f, 225f), Quaternion.Euler(0f, 15f, 0f), 1.95f, wrecks);
put("Assets/Prefabs/Wasteland/BurntSedan.prefab", new Vector3(1045f, 0f, 200f), Quaternion.Euler(0f, 160f, 0f), 1.95f, wrecks);
int bi = 0;
foreach (var c in new[] { new Vector3(1030f, 0, 278f), new Vector3(1095f, 0, 260f), new Vector3(1070f, 0, 196f) })
    put("Assets/Prefabs/Wasteland/BurningBarrel.prefab", c, Quaternion.identity, 1f, wasteland).name = "BurningBarrel_Industrial_" + (bi++);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
