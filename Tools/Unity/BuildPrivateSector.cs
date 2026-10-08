// Частный сектор вдоль северной грунтовки (Environment/PrivateSector).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Каждый запуск пересобирает Environment/PrivateSector с нуля. Участки идут по обе стороны улицы вплотную,
// под каждый участок рельеф выравнивается террасой на высоте ворот (с плавным откосом), поэтому повторный запуск
// рельеф не меняет. Модели и префабы — Tools/Blender/private_sector и Assets/Prefabs/PrivateSector.
// У моделей перед смотрит в +Z, поэтому дома и ворота повёрнуты на 180° к улице.
var env = GameObject.Find("Environment").transform;
var terrain = Terrain.activeTerrain; var td = terrain.terrainData;
string PS = "Assets/Prefabs/PrivateSector/";
string[] TREES = { "Assets/Content/Tree_Packs/URP_Tree_Pack/Prefabs/URP_Tree_1.prefab", "Assets/Content/Tree_Packs/URP_Tree_Pack/Prefabs/URP_Tree_3.prefab" };
var log = new System.Text.StringBuilder();

// Отрезки улицы (по центру грунтовки) и размеры участка в мировых метрах
var streets = new[] { new[] { new Vector2(1000f, 1336f), new Vector2(1132f, 1340f) }, new[] { new Vector2(1205f, 1487f), new Vector2(1478f, 1542f) } };
float setback = 9f;            // от оси улицы до забора
float plotW = 39.2f;           // ворота 9.2 м + 5 секций по 6 м
float plotD = 54f;             // 9 секций по 6 м
float seg = 6f, gateHalf = 4.6f, blend = 9f;
var rnd = new System.Random(1957);
System.Func<float> rf = () => (float)rnd.NextDouble();

var old = env.Find("PrivateSector"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var root = new GameObject("PrivateSector").transform; root.SetParent(env, false);
var wasteland = env.Find("Wasteland");
var oldBarrels = new System.Collections.Generic.List<GameObject>();
foreach (Transform c in wasteland) if (c.name.StartsWith("BurningBarrel_Village")) oldBarrels.Add(c.gameObject);
foreach (var g in oldBarrels) UnityEngine.Object.DestroyImmediate(g);

// --- 1. Участки: рамка (начало — середина переднего забора, +Z — вглубь участка) ---
var plots = new System.Collections.Generic.List<Transform>();
foreach (var st in streets) {
    var a = st[0]; var b = st[1]; var d2 = (b - a).normalized; float len = (b - a).magnitude;
    int n = Mathf.FloorToInt(len / plotW);
    float start = (len - n * plotW) / 2f;
    var dir = new Vector3(d2.x, 0, d2.y);
    foreach (var side in new[] { -1f, 1f }) {
        var outward = Vector3.Cross(Vector3.up, dir) * side;
        for (int i = 0; i < n; i++) {
            var c2 = a + d2 * (start + plotW * (i + 0.5f));
            var p = new Vector3(c2.x, 0, c2.y) + outward * setback;
            var plot = new GameObject("Plot_" + plots.Count.ToString("00")).transform; plot.SetParent(root, false);
            plot.position = p; plot.rotation = Quaternion.LookRotation(outward);
            plots.Add(plot);
        }
    }
}

// --- 2. Террасы: каждый участок выравниваем по высоте у ворот ---
int hr = td.heightmapResolution; var hm = td.GetHeights(0, 0, hr, hr);
var level = new float[plots.Count];
for (int i = 0; i < plots.Count; i++) level[i] = terrain.SampleHeight(plots[i].position);
// Сначала считаем вес и высоту от всех участков, потом смешиваем: соседние террасы не затирают друг друга
for (int iz = 0; iz < hr; iz++) for (int ix = 0; ix < hr; ix++) {
    var w = new Vector3(ix / (float)(hr - 1) * td.size.x, 0, iz / (float)(hr - 1) * td.size.z);
    float best = 1e9f, hsum = 0f, wsum = 0f;
    for (int i = 0; i < plots.Count; i++) {
        var l = plots[i].InverseTransformPoint(w);
        float dx = Mathf.Max(0f, Mathf.Abs(l.x) - plotW / 2f - 1f), dz = Mathf.Max(0f, Mathf.Max(-1f - l.z, l.z - plotD - 1f));
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        if (dist >= blend) continue;
        float k = 1f - Mathf.SmoothStep(0f, 1f, dist / blend);
        hsum += level[i] * k; wsum += k; best = Mathf.Min(best, dist);
    }
    if (wsum <= 0f) continue;
    float target = hsum / wsum / td.size.y;
    float kk = 1f - Mathf.SmoothStep(0f, 1f, best / blend);
    hm[iz, ix] = Mathf.Lerp(hm[iz, ix], target, kk);
}
td.SetHeights(0, 0, hm);

System.Func<string, Transform, float, float, float, float, Transform> put = (path, plot, x, z, rot, s) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src, plot);
    var wp = plot.TransformPoint(new Vector3(x, 0, z)); wp.y = terrain.SampleHeight(wp) + terrain.transform.position.y;
    go.transform.position = wp; go.transform.rotation = plot.rotation * Quaternion.Euler(0, rot, 0);
    go.transform.localScale = src.transform.localScale * s;
    return go.transform;
};

// --- 3. Наполнение участков ---
string[] housesP = { "House_Wood_A", "House_Wood_A", "House_Wood_B", "House_Wood_B", "House_Wood_C", "House_Wood_C", "House_Brick" };
var footprints = new System.Collections.Generic.List<Bounds>(); // для травы: пятна построек
int counts = 0;
for (int i = 0; i < plots.Count; i++) {
    var plot = plots[i];
    bool gateLeft = rnd.Next(2) == 0;
    float gx = gateLeft ? -plotW / 2f + gateHalf : plotW / 2f - gateHalf;
    string fence = new[] { "Fence_Picket", "Fence_Picket", "Fence_Solid", "Fence_Metal" }[rnd.Next(4)];
    var fences = new GameObject("Fences").transform; fences.SetParent(plot, false);
    put(PS + "Gate.prefab", plot, gx, 0f, 180f, 1f).SetParent(fences, true);
    // Передний забор вокруг ворот; одна секция у части участков «выпала» — пролом в заборе
    float from = gateLeft ? gx + gateHalf : -plotW / 2f;
    for (int k = 0; k < 5; k++) if (rf() > 0.08f) put(PS + fence + ".prefab", plot, from + seg * (k + 0.5f), 0f, 0f, 1f).SetParent(fences, true);
    // Левый бок у каждого участка (общий с соседом), правый — если справа соседа нет; задний — у всех
    var rightSpot = plot.position + plot.right * plotW; bool lastInRow = true;
    foreach (var o in plots) if (o != plot && Vector3.Distance(o.position, rightSpot) < 1f) lastInRow = false;
    for (int k = 0; k < 9; k++) {
        put(PS + "Fence_Picket.prefab", plot, -plotW / 2f, seg * (k + 0.5f), 90f, 1f).SetParent(fences, true);
        if (lastInRow) put(PS + "Fence_Picket.prefab", plot, plotW / 2f, seg * (k + 0.5f), 90f, 1f).SetParent(fences, true);
    }
    for (int k = 0; k < 6; k++) put(PS + "Fence_Picket.prefab", plot, -plotW / 2f + 0.6f + seg * (k + 0.5f), plotD, 0f, 1f).SetParent(fences, true);

    // Дом — с противоположной от ворот стороны, фасадом к улице
    var yard = new GameObject("Yard").transform; yard.SetParent(plot, false);
    string hn = housesP[rnd.Next(housesP.Length)];
    float hx = gateLeft ? 6f : -6f;
    var house = put(PS + hn + ".prefab", plot, hx, 7f + 9f, 180f, 1f); house.SetParent(yard, true);
    // Гараж у ворот у трети участков, иначе — иногда брошенная машина на въезде
    if (rf() < 0.33f) put(PS + "Garage.prefab", plot, gx, 4f + 6.6f, 180f, 1f).SetParent(yard, true);
    else if (rf() < 0.3f) put(rf() < 0.5f ? "Assets/Prefabs/Wasteland/BurntSedan.prefab" : "Assets/Prefabs/Wasteland/BurntHatchback.prefab", plot, gx + (rf() - 0.5f) * 3f, 10f, 180f + (rf() - 0.5f) * 40f, 1.95f).SetParent(yard, true);
    // Сарай и туалет в глубине участка
    put(PS + "Shed.prefab", plot, gateLeft ? -plotW / 2f + 6f : plotW / 2f - 6f, plotD - 6f, rnd.Next(2) == 0 ? 90f : 180f, 1f).SetParent(yard, true);
    put(PS + "Outhouse.prefab", plot, hx + (gateLeft ? 8f : -8f), plotD - 4f, 180f, 1f).SetParent(yard, true);
    // Сад: 3–5 деревьев за домом
    int nt = 3 + rnd.Next(3);
    for (int k = 0; k < nt; k++) {
        float x = (rf() - 0.5f) * (plotW - 10f), z = 32f + rf() * 12f;
        put(TREES[rnd.Next(2)], plot, x, z, rf() * 360f, 0.45f + rf() * 0.25f).SetParent(yard, true);
    }
    if (rf() < 0.18f) {
        var barrel = put("Assets/Prefabs/Wasteland/BurningBarrel.prefab", plot, gx + (gateLeft ? 5f : -5f), -3f, 0f, 1f);
        barrel.SetParent(wasteland, true); barrel.name = "BurningBarrel_Village_" + i.ToString("00");
    }
    foreach (var r in yard.GetComponentsInChildren<Renderer>()) if (!r.name.StartsWith("URP_Tree")) footprints.Add(r.bounds);
    counts++;
}
log.AppendLine("plots: " + counts);

// --- 4. Террейн: деревья с участков убираем, траву из-под построек тоже, на въездах — грунт ---
var trees = new System.Collections.Generic.List<TreeInstance>(td.treeInstances); int tb = trees.Count;
trees.RemoveAll(ti => {
    var w = Vector3.Scale(ti.position, td.size);
    foreach (var p in plots) { var l = p.InverseTransformPoint(w); if (Mathf.Abs(l.x) < plotW / 2f + 5f && l.z > -14f && l.z < plotD + 5f) return true; }
    return false;
});
td.treeInstances = trees.ToArray();
log.AppendLine("terrain trees removed: " + (tb - trees.Count));
int dres = td.detailResolution;
for (int layer = 0; layer < td.detailPrototypes.Length; layer++) {
    var dl = td.GetDetailLayer(0, 0, dres, dres, layer);
    foreach (var b in footprints) {
        int x0 = Mathf.Max(0, (int)(b.min.x / td.size.x * dres)), x1 = Mathf.Min(dres - 1, (int)(b.max.x / td.size.x * dres));
        int z0 = Mathf.Max(0, (int)(b.min.z / td.size.z * dres)), z1 = Mathf.Min(dres - 1, (int)(b.max.z / td.size.z * dres));
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) dl[z, x] = 0;
    }
    td.SetDetailLayer(0, 0, layer, dl);
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
