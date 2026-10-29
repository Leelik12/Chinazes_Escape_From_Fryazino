// Взрывоопасное наполнение карты (Environment/LevelDressing/Hazards): бензовозы у резервуаров промзоны и на обочинах,
// шкафы с газовыми баллонами в промзоне и дворах, одиночные баллоны у домов частного сектора. Префабы — из
// Tools/Unity/BuildLevelProps.cs, взрыв и цепная реакция (ExplosiveBarrel) — из Tools/Unity/BuildBattleEvents.cs.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Запускать после BuildLevelDressing (он пересобирает LevelDressing с нуля и удаляет Hazards); повторный запуск
// пересобирает Hazards так же (seed). После — перепечь NavMesh (Tools/Unity/BakeNavMesh.cs).
var log = new System.Text.StringBuilder();
var env = GameObject.Find("Environment").transform;
var dressing = env.Find("LevelDressing");
var terrain = Terrain.activeTerrain;
string LP = "Assets/Prefabs/LevelProps/";
var rnd = new System.Random(1986);
System.Func<float, float, float> rr = (a, b) => a + (b - a) * (float)rnd.NextDouble();
var old = dressing.Find("Hazards"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var root = new GameObject("Hazards").transform; root.SetParent(dressing, false);
var roadsRoot = env.Find("Roads");
var net = env.Find("RoadNetwork").GetComponent<RacingProject.Enemy.RoadNetwork>();
net.SetRoads(net.Roads);
Physics.SyncTransforms();

System.Func<Vector3, float> groundY = p => {
    float y = terrain.SampleHeight(p) + terrain.transform.position.y;
    foreach (var h in Physics.RaycastAll(new Vector3(p.x, y + 5f, p.z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        if (h.collider.transform.IsChildOf(roadsRoot) && h.point.y > y) y = h.point.y;
    return y; };
// Деревья террейна коллайдеров для OverlapBox не дают — проверяются по списку
var trees = new System.Collections.Generic.List<Vector3>();
foreach (var ti in terrain.terrainData.treeInstances) trees.Add(Vector3.Scale(ti.position, terrain.terrainData.size) + terrain.transform.position);
System.Func<Vector3, float, bool> nearTree = (p, r) => {
    foreach (var t in trees) { float dx = t.x - p.x, dz = t.z - p.z; if (dx * dx + dz * dz < r * r) return true; }
    return false; };
System.Func<Vector3, bool> inside = p => p.x > 305f && p.x < 1925f && p.z > 95f && p.z < 1875f;
// Свободно ли: коробка ext над землёй не задевает ничего, кроме террейна и дорог
System.Func<Vector3, Vector3, Quaternion, bool> free = (pos, ext, rot) => {
    pos.y = groundY(pos) + ext.y + 0.3f;
    foreach (var c in Physics.OverlapBox(pos, ext, rot, ~0, QueryTriggerInteraction.Ignore)) {
        if (c is TerrainCollider || c.transform.IsChildOf(roadsRoot)) continue;
        return false;
    }
    return true; };
System.Func<Vector3, float, float> slope = (p, r) => {
    float mn = 1e6f, mx = -1e6f;
    for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) { float h = terrain.SampleHeight(p + new Vector3(i * r, 0f, j * r)); mn = Mathf.Min(mn, h); mx = Mathf.Max(mx, h); }
    return mx - mn; };
System.Func<Vector3, float, bool> clearOfRoads = (p, minDist) => {
    Vector3 c, d; float hw;
    if (!net.Nearest(p, minDist, out c, out d, out hw)) return true;
    var off = p - c; off.y = 0f; return off.magnitude >= minDist; };
System.Func<string, Vector3, Quaternion, Transform, Transform> put = (name, pos, rot, parent) => {
    var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LP + name + ".prefab"), parent);
    pos.y = groundY(pos);
    go.transform.SetPositionAndRotation(pos, rot);
    Physics.SyncTransforms();
    return go.transform; };
System.Func<Transform, string, Transform> group = (parent, n) => { var g = new GameObject(n).transform; g.SetParent(parent, false); return g; };

// --- Бензовозы: у резервуаров и на обочинах ---
var gTrucks = group(root, "FuelTrucks");
var truckExt = new Vector3(2.8f, 2.6f, 7f);
int nTrucks = 0;
foreach (Transform t in dressing.GetComponentsInChildren<Transform>()) {
    if (t.name != "FuelTank" || nTrucks >= 2) continue;
    for (int attempt = 0; attempt < 40; attempt++) {
        float a = rr(0f, 360f);
        var p = t.position + Quaternion.Euler(0f, a, 0f) * Vector3.forward * rr(15f, 22f);
        var rot = Quaternion.Euler(0f, a + 90f + rr(-10f, 10f), 0f);
        if (!inside(p) || slope(p, 6f) > 1.5f || nearTree(p, 8f) || !free(p, truckExt, rot)) continue;
        put("FuelTruck", p, rot, gTrucks); nTrucks++; break;
    }
}
for (int k = 0; k < 40 && nTrucks < 5; k++) {
    var road = net.Roads[rnd.Next(net.Roads.Length)];
    if (road.points.Length < 6) continue;
    int i = rnd.Next(2, road.points.Length - 2);
    var dir = road.points[i + 1] - road.points[i - 1]; dir.y = 0f; dir.Normalize();
    var side = Vector3.Cross(Vector3.up, dir) * (rnd.Next(2) == 0 ? 1f : -1f);
    var p = road.points[i] + side * (road.halfWidth + 4f);
    var rot = Quaternion.LookRotation(dir * (rnd.Next(2) == 0 ? 1f : -1f)) * Quaternion.Euler(0f, rr(-6f, 6f), 0f);
    if (!inside(p) || !clearOfRoads(p, road.halfWidth + 2.5f) || slope(p, 6f) > 1.5f || nearTree(p, 8f) || !free(p, truckExt, rot)) continue;
    put("FuelTruck", p, rot, gTrucks); nTrucks++;
}

// --- Шкафы с баллонами: промзона и дворы ---
var gRacks = group(root, "GasRacks");
var rackExt = new Vector3(1.8f, 1.3f, 0.9f);
int nRacks = 0;
var rackPlan = new object[] { new Rect(960f, 130f, 260f, 210f), 4, new Rect(445f, 176f, 143f, 524f), 2, new Rect(603f, 128f, 170f, 566f), 2, new Rect(1467f, 480f, 390f, 566f), 3 };
for (int r = 0; r < rackPlan.Length; r += 2) {
    var rect = (Rect)rackPlan[r]; int want = (int)rackPlan[r + 1];
    for (int attempt = 0; attempt < 150 && want > 0; attempt++) {
        var p = new Vector3(rr(rect.xMin, rect.xMax), 0f, rr(rect.yMin, rect.yMax));
        var rot = Quaternion.Euler(0f, rnd.Next(4) * 90f + rr(-5f, 5f), 0f);
        if (!inside(p) || !clearOfRoads(p, 9f) || slope(p, 2f) > 0.8f || nearTree(p, 3f) || !free(p, rackExt, rot)) continue;
        put("GasCylinderRack", p, rot, gRacks); want--; nRacks++;
    }
}

// --- Баллоны у домов частного сектора: у боковой или задней стены ---
var gCyl = group(root, "GasCylinders");
var cylExt = new Vector3(0.45f, 1.1f, 0.45f);
int nCyl = 0;
foreach (Transform plot in env.Find("PrivateSector")) {
    var yard = plot.Find("Yard"); if (yard == null) continue;
    Transform house = null; foreach (Transform c in yard) if (c.name.StartsWith("House")) house = c;
    if (house == null || rnd.NextDouble() > 0.6) continue;
    // Границы дома в его осях: по мешу модели
    var mf = house.GetComponentInChildren<MeshFilter>();
    if (mf == null || mf.sharedMesh == null) continue;
    var b = mf.sharedMesh.bounds;
    var mt = mf.transform;
    for (int attempt = 0; attempt < 12; attempt++) {
        int sideIdx = rnd.Next(3);
        Vector3 local = sideIdx == 0 ? new Vector3(b.min.x - 0.5f, 0f, rr(b.min.z + 1f, b.max.z - 1f))
                      : sideIdx == 1 ? new Vector3(b.max.x + 0.5f, 0f, rr(b.min.z + 1f, b.max.z - 1f))
                      : new Vector3(rr(b.min.x + 1f, b.max.x - 1f), 0f, b.min.z - 0.5f);
        var p = mt.TransformPoint(local);
        // Отступ от стены в мировых метрах (меш дома может быть масштабирован)
        var wall = mt.TransformPoint(new Vector3(sideIdx == 0 ? b.min.x : sideIdx == 1 ? b.max.x : local.x, 0f, sideIdx == 2 ? b.min.z : local.z));
        var outDir = p - wall; outDir.y = 0f;
        if (outDir.sqrMagnitude < 1e-4f) continue;
        p = wall + outDir.normalized * 0.75f;
        if (!free(p, cylExt, Quaternion.identity) || slope(p, 1f) > 0.5f) continue;
        put("GasCylinder", p, Quaternion.Euler(0f, rr(0f, 360f), 0f), gCyl); nCyl++; break;
    }
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);
return log.Append("fuel trucks ").Append(nTrucks).Append(", gas racks ").Append(nRacks).Append(", gas cylinders ").Append(nCyl).ToString();
