// Наполнение карты (Environment/LevelDressing) префабами Assets/Prefabs/LevelProps по правилам районов.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Каждый запуск пересобирает Environment/LevelDressing с нуля; случайность детерминирована (seed), кроме воронок
// рельеф не меняется, а воронки не копаются повторно (проверяется глубина на месте).
// Правила:
//  Дороги — отбойники над обрывами, бетонные блоки у воды, знаки перед перекрёстками и на въездах в города, люки и лужи
//           на проезжей части, брошенные машины на обочинах, покрышки, плиты, мусор и агитщиты за обочиной,
//           «Проезд закрыт» на концах Road1/Road2 у ограды, баллоны нитро и дополнительные ремкомплекты;
//  Дворы (Yards, City1, City2, City3) — детские площадки, «ракушки», сушилки, лавки, баки, машины во дворах;
//           на крышах панелек — антенны, на фасадах — копоть от попаданий;
//  Промзона — трубы с дымом, башенный кран, резервуары, контейнеры, рельсы с вагонами, эстакада труб, мачты прожекторов;
//  Частный сектор — поленницы, колодцы, теплицы, будки на участках; деревянные столбы с проводами вдоль улиц;
//  Пустыри — окопы из мешков с блиндажами, воронки, сгоревшая техника, ежи, колючая проволока, площадка-укрепление;
//  Ориентиры (слой Landmark, мигающие огни AviationLights) — телевышка на самом высоком месте, труба ГРЭС у промзоны,
//           водонапорная башня у частного сектора, Дом культуры в City2;
//  Озеро — причал с лодками, обрушенный мост (если есть второй пологий берег в 200 м от причала), камыш и валуны по берегу;
//  Геймплей — трамплины у дорог, сужения дороги баррикадами, ломающиеся ящики и баррикады.
// После запуска перепечь NavMesh (Tools/Unity/BakeNavMesh.cs) и точки засад (Tools/Unity/BuildAmbushPoints.cs).
var log = new System.Text.StringBuilder();
var env = GameObject.Find("Environment").transform;
var general = GameObject.Find("General").transform;
var terrain = Terrain.activeTerrain; var td = terrain.terrainData;
string LP = "Assets/Prefabs/LevelProps/", RS = "Assets/Prefabs/Roadside/";
var rnd = new System.Random(1984);
System.Func<float> rf = () => (float)rnd.NextDouble();
System.Func<float, float, float> rr = (a, b) => a + (b - a) * (float)rnd.NextDouble();
int landmark = LayerMask.NameToLayer("Landmark");

var old = env.Find("LevelDressing"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var root = new GameObject("LevelDressing").transform; root.SetParent(env, false);
System.Func<Transform, string, Transform> group = (parent, n) => { var g = new GameObject(n).transform; g.SetParent(parent, false); return g; };
var placed = new System.Collections.Generic.List<Bounds>();
var roadsRoot = env.Find("Roads");
var net = env.Find("RoadNetwork").GetComponent<RacingProject.Enemy.RoadNetwork>();
net.SetRoads(net.Roads);
var water = GameObject.Find("water");
Bounds lake = water.GetComponent<BoxCollider>().bounds;
float waterY = lake.max.y;
var scorchMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_Scorch.mat");
var glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_GlowNoFog.mat");
var fireTemplate = env.Find("HorizonFires/HorizonFire_0");

// --- Общие проверки ---
System.Func<Vector3, float> groundY = p => {
    float y = terrain.SampleHeight(p) + terrain.transform.position.y;
    foreach (var h in Physics.RaycastAll(new Vector3(p.x, y + 5f, p.z), Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        if (h.collider.transform.IsChildOf(roadsRoot) && h.point.y > y) y = h.point.y;
    return y; };
System.Func<Vector3, float, bool> clearOfRoads = (p, minDist) => {
    Vector3 c, d; float hw;
    if (!net.Nearest(p, minDist, out c, out d, out hw)) return true;
    var off = p - c; off.y = 0f; return off.magnitude >= minDist; };
// Внутри ограды и не в озере
System.Func<Vector3, bool> inside = p => p.x > 305f && p.x < 1925f && p.z > 95f && p.z < 1875f;
System.Func<Vector3, bool> inLake = p => p.x > lake.min.x && p.x < lake.max.x && p.z > lake.min.z && p.z < lake.max.z && terrain.SampleHeight(p) < waterY + 0.6f;
System.Func<Vector3, Vector3, Quaternion, bool> free = (pos, ext, rot) => {
    pos.y = groundY(pos) + ext.y + 0.3f;
    foreach (var c in Physics.OverlapBox(pos, ext, rot, ~0, QueryTriggerInteraction.Ignore)) {
        if (c is TerrainCollider || c.transform.IsChildOf(roadsRoot)) continue;
        return false;
    }
    return true; };
// Перепад высот рельефа в квадрате ±r
System.Func<Vector3, float, float> slope = (p, r) => {
    float mn = 1e6f, mx = -1e6f;
    for (int i = -1; i <= 1; i++) for (int j = -1; j <= 1; j++) { float h = terrain.SampleHeight(p + new Vector3(i * r, 0f, j * r)); mn = Mathf.Min(mn, h); mx = Mathf.Max(mx, h); }
    return mx - mn; };
System.Func<string, Vector3, Quaternion, Transform, Transform> put = (path, pos, rot, parent) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path.Contains("/") ? path : LP + path + ".prefab");
    if (src == null) throw new System.Exception("no prefab " + path);
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
    if (float.IsNaN(pos.y)) pos.y = groundY(pos);
    go.transform.SetPositionAndRotation(pos, rot);
    foreach (var r in go.GetComponentsInChildren<Renderer>()) placed.Add(r.bounds);
    return go.transform; };
var NaNy = float.NaN;
System.Func<float, float, float, Vector3> P = (x, y, z) => new Vector3(x, y, z);
// Поиск свободного места в прямоугольнике: не ближе roadGap к осям дорог, перепад рельефа меньше maxSlope
System.Func<Rect, Vector3, float, float, Quaternion, Vector3> spot = (rect, ext, roadGap, maxSlope, rot) => {
    for (int t = 0; t < 120; t++) {
        var p = new Vector3(rr(rect.xMin, rect.xMax), 0f, rr(rect.yMin, rect.yMax));
        if (!inside(p) || inLake(p)) continue;
        if (!clearOfRoads(p, roadGap + Mathf.Max(ext.x, ext.z))) continue;
        if (slope(p, Mathf.Max(ext.x, ext.z)) > maxSlope) continue;
        if (!free(p, ext, rot)) continue;
        return p;
    }
    return Vector3.positiveInfinity; };
System.Func<Vector3, bool> bad = v => float.IsInfinity(v.x);
System.Func<Quaternion> yaw = () => Quaternion.Euler(0f, rr(0f, 360f), 0f);
System.Func<Quaternion> yaw90 = () => Quaternion.Euler(0f, rnd.Next(4) * 90f + rr(-6f, 6f), 0f);
// Копоть: квадрат Fx_Scorch на поверхности (фасад или земля)
System.Action<Transform, Vector3, Vector3, float> scorch = (parent, point, normal, size) => {
    if (scorchMat == null) return;
    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
    q.name = "Scorch"; q.transform.SetParent(parent, false);
    q.transform.position = point + normal * 0.08f;
    q.transform.rotation = Quaternion.LookRotation(-normal, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up) * Quaternion.Euler(0f, 0f, rr(0f, 360f));
    q.transform.localScale = new Vector3(size, size * rr(0.8f, 1.3f), 1f);
    var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = scorchMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    if (size < 10f) q.layer = LayerMask.NameToLayer("SmallProps");
    GameObjectUtility.SetStaticEditorFlags(q, (StaticEditorFlags)~0 & ~StaticEditorFlags.NavigationStatic); };
// Дым из копии системы горизонтного пожара (как в BuildCityFires)
System.Func<Transform, Vector3, float, Transform> smoke = (parent, pos, scale) => {
    var src = fireTemplate.Find("Smoke");
    var go = UnityEngine.Object.Instantiate(src.gameObject, parent);
    go.name = "Smoke"; go.transform.position = pos; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one * scale;
    var ps = go.GetComponent<ParticleSystem>(); if (ps != null) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
    return go.transform; };
var beacons = new System.Collections.Generic.List<Transform>();
System.Action<Transform, Vector3> beacon = (parent, pos) => {
    var b = new GameObject("Beacon").transform; b.SetParent(parent, false); b.position = pos; beacons.Add(b); };
System.Action<Transform> toLandmark = t => { foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = landmark; };
System.Func<Transform, Bounds> boundsOf = t => {
    var rs = t.GetComponentsInChildren<Renderer>(); Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; };

// Районы (мировые прямоугольники x, z, ширина, глубина)
var rYards = new Rect(445f, 176f, 143f, 524f);
var rCity1 = new Rect(603f, 128f, 170f, 566f);
var rCity2 = new Rect(1467f, 480f, 390f, 566f);
var rCity3 = new Rect(450f, 1570f, 100f, 100f);
var rIndustrial = new Rect(960f, 130f, 260f, 210f);
System.Func<Vector3, bool> inTown = p => rYards.Contains(new Vector2(p.x, p.z)) || rCity1.Contains(new Vector2(p.x, p.z)) || rCity2.Contains(new Vector2(p.x, p.z)) || rCity3.Contains(new Vector2(p.x, p.z));
System.Func<Vector3, float, bool> nearDistrict = (p, m) => {
    foreach (var r in new[] { rYards, rCity1, rCity2, rCity3, rIndustrial, new Rect(1000f, 1270f, 480f, 340f), new Rect(800f, 350f, 100f, 130f) }) {
        var e = new Rect(r.xMin - m, r.yMin - m, r.width + 2 * m, r.height + 2 * m);
        if (e.Contains(new Vector2(p.x, p.z))) return true;
    }
    return false; };

// Перекрёстки (как в BuildRoadside)
var interPos = new System.Collections.Generic.List<Vector3>();
for (int ra = 0; ra < net.Roads.Length; ra++) for (int rb = ra + 1; rb < net.Roads.Length; rb++) {
    var A = net.Roads[ra].points; var B = net.Roads[rb].points;
    for (int i = 0; i < A.Length; i++) for (int j = 0; j < B.Length; j++) {
        float dx = A[i].x - B[j].x, dz = A[i].z - B[j].z; if (dx * dx + dz * dz > 9f) continue;
        bool joint = (i < 3 || i > A.Length - 4) && (j < 3 || j > B.Length - 4); if (joint) continue;
        bool known = false; foreach (var q in interPos) if (Vector3.Distance(q, A[i]) < 40f) known = true;
        if (!known) interPos.Add(A[i]);
    }
}
System.Func<Vector3, float, bool> nearInter = (p, r) => { foreach (var q in interPos) if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(q.x, 0, q.z)) < r) return true; return false; };

// --- 1. Дороги ---
var gRoads = group(root, "Roads");
var gRails = group(gRoads, "Guardrails"); var gSigns = group(gRoads, "Signs"); var gSurface = group(gRoads, "Surface");
var gWrecks = group(gRoads, "Wrecks"); var gClutter = group(gRoads, "Clutter"); var gClosed = group(gRoads, "Closed");
var gPick = group(root, "Pickups");
int nRails = 0, nBlocks = 0, nSigns = 0, nSurface = 0, nWrecks = 0, nClutter = 0;
var pickupSpots = new System.Collections.Generic.List<Vector3>();
var rk = UnityEngine.Object.FindObjectsByType<RacingProject.Pickups.RepairKits>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
var rkSo = new SerializedObject(rk); var rkArr = rkSo.FindProperty("kits");
var oldKits = new System.Collections.Generic.List<GameObject>();
for (int i = 0; i < rkArr.arraySize; i++) { var k = rkArr.GetArrayElementAtIndex(i).objectReferenceValue as GameObject; if (k != null && !k.name.StartsWith("RepairKit_Road")) oldKits.Add(k); }
foreach (var k in oldKits) pickupSpots.Add(k.transform.position);
var newKitSpots = new System.Collections.Generic.List<Vector3>(); var nitroSpots = new System.Collections.Generic.List<Vector3>();
string[] wreckNames = { "Zhiguli_Red", "Zhiguli_White", "Moskvich_Blue", "Moskvich_Yellow", "Zhiguli_Burnt" };
string[] clutterNames = { "TireStack", "TireStack_Low", "RoadSlab", "TrashPile_0", "TrashPile_1", "Boulder_1", RS + "ConcreteBlock.prefab" };

foreach (var road in net.Roads) {
    string rn = road.name.EndsWith("#0") ? road.name.Substring(0, road.name.Length - 2) : road.name.Replace('#', '_');
    var pts = road.points; var arc = new float[pts.Length];
    for (int i = 1; i < pts.Length; i++) arc[i] = arc[i - 1] + Vector3.Distance(pts[i], pts[i - 1]);
    float len = arc[pts.Length - 1];
    float hw = road.halfWidth;
    System.Func<float, Vector3> at = dist => {
        dist = Mathf.Clamp(dist, 0f, len); int i = System.Array.BinarySearch(arc, dist); if (i < 0) i = ~i; i = Mathf.Clamp(i, 1, pts.Length - 1);
        return Vector3.Lerp(pts[i - 1], pts[i], Mathf.InverseLerp(arc[i - 1], arc[i], dist)); };
    System.Func<float, Vector3> dirAt = dist => { var a = at(dist - 2f); var b = at(dist + 2f); var dd = b - a; dd.y = 0; return dd.normalized; };

    // Отбойники над обрывами, у воды — бетонные блоки
    foreach (int side in new[] { -1, 1 }) {
        for (float d = 15f; d < len - 15f; d += 8f) {
            var c = at(d); if (nearInter(c, 45f) || inTown(c)) continue;
            var f = dirAt(d); var s = Vector3.Cross(Vector3.up, f) * side;
            var q = c + s * (hw + 2f);
            var far = c + s * (hw + 16f);
            bool wet = inLake(far) || inLake(c + s * (hw + 8f));
            float drop = groundY(q) - terrain.SampleHeight(far);
            if (!wet && drop < 5f) continue;
            if (!clearOfRoads(q, hw + 0.5f)) continue;
            if (wet) { put(RS + "ConcreteBlock.prefab", P(q.x, NaNy, q.z), Quaternion.LookRotation(f), gRails); nBlocks++; }
            else { put("Guardrail", P(q.x, NaNy, q.z), Quaternion.LookRotation(-s, Vector3.up), gRails); nRails++; }
        }
    }

    // Знаки «Прочие опасности» за 60 м до перекрёстка по обе стороны
    foreach (var ip in interPos) {
        float best = 1e9f, bd = -1f;
        for (int i = 0; i < pts.Length; i++) { float dd = Vector3.Distance(pts[i], ip); if (dd < best) { best = dd; bd = arc[i]; } }
        if (best > 4f) continue;
        foreach (int dir in new[] { -1, 1 }) {
            float d = bd - dir * 70f; if (d < 5f || d > len - 5f) continue;
            var c = at(d); var f = dirAt(d) * dir; var s = Vector3.Cross(Vector3.up, f);
            var q = c + s * (hw + 3f);
            if (!clearOfRoads(q, hw + 1f) || !free(q, P(0.6f, 2f, 0.6f), Quaternion.identity)) continue;
            put("Sign_Danger", P(q.x, NaNy, q.z), Quaternion.LookRotation(-f), gSigns); nSigns++;
        }
    }
    // Указатели на въездах в город: на месте, где ось входит в прямоугольник района
    for (float d = 10f; d < len - 10f; d += 5f) {
        bool a = inTown(at(d)), b = inTown(at(d + 5f));
        if (a == b) continue;
        int dir = b ? 1 : -1; // направление движения, при котором машина въезжает
        float ds = d - dir * 40f; if (ds < 5f || ds > len - 5f) continue;
        var c = at(ds); var f = dirAt(ds) * dir; var s = Vector3.Cross(Vector3.up, f);
        var q = c + s * (hw + 4f);
        if (!clearOfRoads(q, hw + 1f) || !free(q, P(2.6f, 2f, 0.6f), Quaternion.LookRotation(-f))) continue;
        put("Sign_City", P(q.x, NaNy, q.z), Quaternion.LookRotation(-f), gSigns); nSigns++;
    }

    // Люки (в городе) и лужи-выбоины (везде) на покрытии, по наклону дороги
    for (float d = 12f; d < len - 12f; d += rr(28f, 70f)) {
        var c = at(d); var f = dirAt(d); var s = Vector3.Cross(Vector3.up, f);
        bool town = inTown(c);
        string what = town && rf() < 0.55f ? "Manhole" : rf() < 0.5f ? "Puddle_0" : "Puddle_1";
        var q = c + s * rr(-hw + 3f, hw - 3f);
        RaycastHit hit;
        if (!Physics.Raycast(q + Vector3.up * 30f, Vector3.down, out hit, 60f, ~0, QueryTriggerInteraction.Ignore) || !hit.collider.transform.IsChildOf(roadsRoot)) continue;
        var rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.LookRotation(f) * Quaternion.Euler(0f, rr(-30f, 30f), 0f);
        var t = put(what, hit.point, rot, gSurface);
        if (what != "Manhole") t.localScale = Vector3.one * rr(0.45f, 0.9f);
        nSurface++;
    }

    // Брошенные машины: на обочине или краем на проезжей части
    for (float d = 60f; d < len - 60f; d += rr(120f, 260f)) {
        var c = at(d); if (nearInter(c, 50f)) continue;
        var f = dirAt(d); int side = rf() < 0.5f ? -1 : 1; var s = Vector3.Cross(Vector3.up, f) * side;
        float off = rf() < 0.3f ? hw - 2f : hw + rr(3f, 7f);
        var q = c + s * off;
        var rot = Quaternion.LookRotation(f * (rf() < 0.5f ? 1f : -1f)) * Quaternion.Euler(0f, rr(-25f, 25f), 0f);
        if (inLake(q) || !free(q, P(1.8f, 1.5f, 4.4f), rot) || slope(q, 4f) > 2.5f) continue;
        put(wreckNames[rnd.Next(wreckNames.Length)], P(q.x, NaNy, q.z), rot, gWrecks); nWrecks++;
    }

    // Хлам за обочиной (за городом) и агитщиты
    float nextBoard = rr(150f, 300f);
    for (float d = 20f; d < len - 20f; d += rr(25f, 55f)) {
        var c = at(d); if (inTown(c) || nearInter(c, 40f)) continue;
        var f = dirAt(d); int side = rf() < 0.5f ? -1 : 1; var s = Vector3.Cross(Vector3.up, f) * side;
        if (d > nextBoard) {
            var qb = c + s * (hw + 14f);
            var rotb = Quaternion.LookRotation(-s * 0.85f - f * 0.5f);
            if (clearOfRoads(qb, hw + 6f) && !inLake(qb) && free(qb, P(6.5f, 6f, 1f), rotb) && slope(qb, 6f) < 4f) {
                put("Billboard_" + rnd.Next(3), P(qb.x, NaNy, qb.z), rotb, gClutter); nClutter++;
                nextBoard = d + rr(300f, 550f);
                continue;
            }
        }
        if (rf() < 0.45f) continue;
        var q = c + s * (hw + rr(5f, 16f));
        if (!clearOfRoads(q, hw + 3f) || inLake(q) || slope(q, 3f) > 3f || !free(q, P(2.5f, 1.5f, 2.5f), Quaternion.identity)) continue;
        put(clutterNames[rnd.Next(clutterNames.Length)], P(q.x, NaNy, q.z), yaw(), gClutter); nClutter++;
    }

    // Баллоны нитро и дополнительные ремкомплекты: на полосе, не ближе 230 м к другим подбираемым предметам
    for (float d = 80f; d < len - 80f; d += 25f) {
        var c = at(d); if (nearInter(c, 40f)) continue;
        bool near = false; foreach (var q0 in pickupSpots) if (Vector3.Distance(q0, c) < 230f) near = true;
        if (near) continue;
        var f = dirAt(d); var s = Vector3.Cross(Vector3.up, f);
        var q = c + s * (hw * 0.45f * (rf() < 0.5f ? -1f : 1f));
        q.y = groundY(q);
        pickupSpots.Add(q);
        if (nitroSpots.Count <= newKitSpots.Count) nitroSpots.Add(q); else newKitSpots.Add(q);
    }

    // «Проезд закрыт» на концах дорог у ограды
    if (rn == "Road1" || rn == "Road2") {
        // Концы дорог лежат за оградой: шлагбаум ставится в 30 м от места, где ось выходит за ограду
        int dir = rn == "Road1" ? 1 : -1; // направление движения к ограде
        float d = rn == "Road1" ? len - 1f : 1f;
        // У ограды Road1 уходит под холм: точка должна быть внутри ограды и на открытом покрытии (рельеф не выше оси)
        for (int guard = 0; guard < 400 && (!inside(at(d)) || terrain.SampleHeight(at(d)) > at(d).y + 0.5f); guard++) d -= dir * 5f;
        d -= dir * 30f;
        var c = at(d); var f = dirAt(d) * dir; var s = Vector3.Cross(Vector3.up, f);
        foreach (float o in new[] { -hw * 0.5f - 0.5f, hw * 0.5f + 0.5f }) put("RoadClosed", P(c.x + s.x * o, NaNy, c.z + s.z * o), Quaternion.LookRotation(-f), gClosed);
        foreach (int side in new[] { -1, 1 }) {
            var q = c + s * side * (hw + 3f) - f * 6f;
            put("Sign_NoEntry", P(q.x, NaNy, q.z), Quaternion.LookRotation(-f), gClosed);
        }
        for (int k = -1; k <= 1; k++) put(RS + "ConcreteBlock.prefab", P(c.x + f.x * 8f + s.x * k * 6f, NaNy, c.z + f.z * 8f + s.z * k * 6f), Quaternion.LookRotation(s), gClosed);
        log.Append(rn).Append(" closed @").Append(c.ToString("F0")).Append("; ");
    }
    Physics.SyncTransforms();
}
log.Append("rails ").Append(nRails).Append(", lake blocks ").Append(nBlocks).Append(", signs ").Append(nSigns).Append(", surface ").Append(nSurface)
   .Append(", wrecks ").Append(nWrecks).Append(", clutter ").Append(nClutter).Append("; ");

// --- 2. Подбираемые предметы: баллоны нитро (новые) и ремкомплекты (копии первого) ---
string nitroPrefabPath = "Assets/Prefabs/Pickups/NitroCanister.prefab";
{
    // Префаб баллона: модель на вращающемся Visual, синий свет — как у ремкомплекта
    var proot = new GameObject("NitroCanister");
    var visual = new GameObject("Visual").transform; visual.SetParent(proot.transform, false); visual.localPosition = Vector3.up;
    visual.gameObject.AddComponent<RacingProject.Pickups.PickupSpin>();
    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/LevelProps/NitroCanister.fbx"), visual);
    model.transform.localPosition = Vector3.down * 0.55f; model.transform.localRotation = Quaternion.Euler(0f, 0f, 0f); model.transform.localScale = Vector3.one * 0.6f;
    foreach (var mr in model.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    var glow = new GameObject("Glow"); glow.transform.SetParent(proot.transform, false); glow.transform.localPosition = Vector3.up * 1.2f;
    var gl = glow.AddComponent<Light>(); gl.type = LightType.Point; gl.color = new Color(0.3f, 0.55f, 1f); gl.intensity = 3f; gl.range = 7f; gl.shadows = LightShadows.None;
    PrefabUtility.SaveAsPrefabAsset(proot, nitroPrefabPath);
    UnityEngine.Object.DestroyImmediate(proot);
}
var nitroRoot = group(gPick, "NitroCanisters");
var canisters = new System.Collections.Generic.List<GameObject>();
for (int i = 0; i < nitroSpots.Count && i < 16; i++) { var t = put(nitroPrefabPath, nitroSpots[i], Quaternion.identity, nitroRoot); t.name = "NitroCanister_" + i; canisters.Add(t.gameObject); }
// Компонент — рядом с RepairKits (на EnemyManager): машина та же, звук подбора — тот же
var nitroComp = rk.GetComponent<RacingProject.Pickups.NitroPickups>();
if (nitroComp == null) nitroComp = rk.gameObject.AddComponent<RacingProject.Pickups.NitroPickups>();
var nso = new SerializedObject(nitroComp);
nso.FindProperty("carNitro").objectReferenceValue = UnityEngine.Object.FindObjectsByType<RacingProject.Car.CarNitro>(FindObjectsInactive.Include, FindObjectsSortMode.None)[0];
nso.FindProperty("pickupSound").objectReferenceValue = rkSo.FindProperty("pickupSound").objectReferenceValue;
var carr = nso.FindProperty("canisters"); carr.arraySize = canisters.Count;
for (int i = 0; i < canisters.Count; i++) carr.GetArrayElementAtIndex(i).objectReferenceValue = canisters[i];
nso.ApplyModifiedPropertiesWithoutUndo();
// Ремкомплекты: старые остаются, новые — экземпляры того же префаба в группе ремкомплектов
var kitParent = oldKits[0].transform.parent;
var stale = new System.Collections.Generic.List<GameObject>();
foreach (Transform c in kitParent) if (c.name.StartsWith("RepairKit_Road")) stale.Add(c.gameObject);
foreach (var g in stale) UnityEngine.Object.DestroyImmediate(g);
var kitPrefab = PrefabUtility.GetCorrespondingObjectFromSource(oldKits[0]);
var allKits = new System.Collections.Generic.List<GameObject>(oldKits);
for (int i = 0; i < newKitSpots.Count && allKits.Count < 31; i++) {
    var k = (GameObject)PrefabUtility.InstantiatePrefab(kitPrefab, kitParent);
    k.name = "RepairKit_Road" + i; k.transform.position = newKitSpots[i]; allKits.Add(k);
}
rkArr.arraySize = allKits.Count;
for (int i = 0; i < allKits.Count; i++) rkArr.GetArrayElementAtIndex(i).objectReferenceValue = allKits[i];
rkSo.ApplyModifiedPropertiesWithoutUndo();
log.Append("nitro ").Append(canisters.Count).Append(", repair kits ").Append(allKits.Count).Append("; ");

// --- 3. Дворы ---
var gYards = group(root, "Courtyards");
int nYard = 0;
// Площадка: горка-ракета, качели, песочница, лавки и бак вокруг центра
System.Func<Transform, Vector3, bool> playground = (parent, c) => {
    var rot = yaw90();
    var g = group(parent, "Playground");
    var items = new object[][] {
        new object[] { "RocketSlide", P(-7f, 0f, -3f), 0f }, new object[] { "Swings", P(7f, 0f, -5f), 0f }, new object[] { "Sandbox", P(-6f, 0f, 9f), 0f },
        new object[] { "YardBench", P(4f, 0f, 11f), 180f }, new object[] { "YardBench", P(11f, 0f, 6f), 270f }, new object[] { "TrashBin", P(13f, 0f, 12f), 0f } };
    int n = 0;
    foreach (var it in items) {
        var q = c + rot * (Vector3)it[1];
        var r = rot * Quaternion.Euler(0f, (float)it[2], 0f);
        if (!free(q, P(2.5f, 1.5f, 2.5f), r)) continue;
        put((string)it[0], P(q.x, NaNy, q.z), r, g); n++;
    }
    Physics.SyncTransforms();
    return n > 0; };
var yardPlan = new object[][] {   // район, площадок, рядов «ракушек», сушилок, машин, баков, лавок
    new object[] { rYards, 4, 2, 3, 3, 5, 5 }, new object[] { rCity1, 3, 2, 2, 3, 4, 5 },
    new object[] { rCity2, 5, 3, 3, 5, 6, 8 }, new object[] { rCity3, 1, 1, 1, 1, 2, 2 } };
foreach (var plan in yardPlan) {
    var rect = (Rect)plan[0];
    for (int i = 0; i < (int)plan[1]; i++) {
        var c = spot(rect, P(15f, 3f, 15f), 16f, 2.5f, Quaternion.identity);
        if (!bad(c) && playground(gYards, c)) nYard++;
    }
    for (int i = 0; i < (int)plan[2]; i++) {
        var rot = yaw90(); int count = 2 + rnd.Next(3);
        var c = spot(rect, P(3.8f * count, 2.5f, 6f), 14f, 2f, rot);
        if (bad(c)) continue;
        var g = group(gYards, "ShellGarages");
        for (int k = 0; k < count; k++) {
            var q = c + rot * P((k - (count - 1) * 0.5f) * 7.2f, 0f, 0f);
            put(rf() < 0.5f ? "ShellGarage_Green" : "ShellGarage_Blue", P(q.x, NaNy, q.z), rot * Quaternion.Euler(0f, rr(-4f, 4f), 0f), g); nYard++;
        }
        Physics.SyncTransforms();
    }
    var singles = new object[][] { new object[] { "Clothesline", plan[3], P(8f, 3f, 2f) }, new object[] { "Car", plan[4], P(2f, 1.5f, 4.5f) },
        new object[] { "TrashBin", plan[5], P(2.5f, 1.5f, 2f) }, new object[] { "YardBench", plan[6], P(2.2f, 1.2f, 1.2f) } };
    foreach (var sgl in singles) {
        for (int i = 0; i < (int)sgl[1]; i++) {
            var rot = yaw90();
            var c = spot(rect, (Vector3)sgl[2], 13f, 2f, rot);
            if (bad(c)) continue;
            string n = (string)sgl[0];
            if (n == "Car") n = wreckNames[rnd.Next(4)];
            put(n, P(c.x, NaNy, c.z), rot, gYards); nYard++;
            if (n == "TrashBin") { var q = c + rot * P(3.4f, 0f, 0f); if (free(q, P(1.6f, 1.2f, 1.2f), rot)) put("TrashBin", P(q.x, NaNy, q.z), rot, gYards); }
            Physics.SyncTransforms();
        }
    }
}
log.Append("courtyard items ").Append(nYard).Append("; ");

// --- 4. Панельки: антенны на крышах, копоть от попаданий на фасадах ---
var gHouses = group(root, "PanelHouses");
var houses = new System.Collections.Generic.List<Transform>();
foreach (var district in new[] { "City1", "City2", "City3", "Yards" }) {
    var d = env.Find(district); if (d == null) continue;
    foreach (Transform h in d) {
        var rs = h.GetComponentsInChildren<MeshRenderer>(); if (rs.Length == 0) continue;
        bool panel = false; Bounds b = rs[0].bounds;
        foreach (var r in rs) { b.Encapsulate(r.bounds); foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith("rus_build_")) panel = true; }
        if (panel && b.size.y > 12f && b.size.x < 150f && b.size.z < 150f) houses.Add(h);
    }
}
int nAnt = 0, nScorch = 0;
foreach (var h in houses) {
    var b = boundsOf(h);
    int want = 1 + rnd.Next(3);
    for (int t = 0; t < 10 && want > 0; t++) {
        var p = P(rr(b.min.x + 2f, b.max.x - 2f), b.max.y + 5f, rr(b.min.z + 2f, b.max.z - 2f));
        RaycastHit hit;
        if (!Physics.Raycast(p, Vector3.down, out hit, 30f, ~0, QueryTriggerInteraction.Ignore) || !hit.transform.IsChildOf(h) || hit.normal.y < 0.9f || hit.point.y < b.max.y - 8f) continue;
        put("TvAntenna", hit.point, yaw(), gHouses); want--; nAnt++;
    }
    if (rf() > 0.5f) continue;
    int marks = 1 + rnd.Next(3);
    for (int t = 0; t < 12 && marks > 0; t++) {
        float a = rr(0f, 360f); var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
        var target = P(b.center.x + rr(-0.3f, 0.3f) * b.size.x, b.min.y + b.size.y * rr(0.2f, 0.9f), b.center.z + rr(-0.3f, 0.3f) * b.size.z);
        RaycastHit hit;
        if (!Physics.Raycast(target + dir * (b.extents.magnitude + 10f), -dir, out hit, b.extents.magnitude * 3f, ~0, QueryTriggerInteraction.Ignore)) continue;
        if (!hit.transform.IsChildOf(h) || Mathf.Abs(hit.normal.y) > 0.3f) continue;
        scorch(gHouses, hit.point, hit.normal, rr(3f, 7f)); marks--; nScorch++;
    }
}
log.Append("houses ").Append(houses.Count).Append(", antennas ").Append(nAnt).Append(", facade scorch ").Append(nScorch).Append("; ");

// --- 5. Промзона ---
var gInd = group(root, "Industrial");
int nInd = 0;
System.Func<string, Rect, Vector3, Quaternion, Transform> putSpot = (name, rect, ext, rot) => {
    var c = spot(rect, ext, 8f, 3f, rot);
    if (bad(c)) { log.Append("no spot ").Append(name).Append("; "); return null; }
    var t = put(name, P(c.x, NaNy, c.z), rot, gInd); nInd++; Physics.SyncTransforms(); return t; };
var indRect = rIndustrial;
var lights = new System.Collections.Generic.List<Transform>();
for (int i = 0; i < 2; i++) {
    var t = putSpot("FactoryChimney", indRect, P(7.5f, 10f, 7.5f), Quaternion.identity);
    if (t == null) continue;
    var b = boundsOf(t);
    toLandmark(t);
    var sm = smoke(t, P(b.center.x, b.max.y + 2f, b.center.z), 0.5f); sm.gameObject.layer = landmark;
    beacon(t, P(b.center.x, b.max.y + 1f, b.center.z));
}
{
    var t = putSpot("TowerCrane", indRect, P(4f, 10f, 4f), yaw90());
    if (t != null) { var b = boundsOf(t); toLandmark(t); beacon(t, P(t.position.x, b.max.y + 0.5f, t.position.z)); }
}
for (int i = 0; i < 3; i++) putSpot("FuelTank", indRect, P(11f, 6f, 11f), Quaternion.identity);
string[] cont = { "Container_Green", "Container_Blue", "Container_Rust" };
for (int i = 0; i < 9; i++) {
    var rot = yaw90();
    var t = putSpot(cont[rnd.Next(3)], indRect, P(6.5f, 3f, 2.8f), rot);
    if (t != null && rf() < 0.45f) { put(cont[rnd.Next(3)], t.position + Vector3.up * 5.18f, rot * Quaternion.Euler(0f, rr(-5f, 5f), 0f), gInd); nInd++; }
}
{
    // Рельсы: пять звеньев в линию и два вагона на них
    // Место ищется своим перебором: рельеф проверяется только вдоль пути (концы и середина), а не квадратом
    var rot = Quaternion.identity; var c = Vector3.positiveInfinity;
    for (int t = 0; t < 600 && bad(c); t++) {
        rot = Quaternion.Euler(0f, rr(0f, 180f), 0f);
        var p = P(rr(indRect.xMin - 80f, indRect.xMax + 80f), 0f, rr(indRect.yMin - 30f, indRect.yMax + 80f));
        var e0 = p + rot * P(0f, 0f, -60f); var e1 = p + rot * P(0f, 0f, 60f);
        float h0 = terrain.SampleHeight(e0), h1 = terrain.SampleHeight(p), h2 = terrain.SampleHeight(e1);
        if (Mathf.Max(h0, Mathf.Max(h1, h2)) - Mathf.Min(h0, Mathf.Min(h1, h2)) > 5f) continue;
        if (!inside(e0) || !inside(e1) || inLake(e0) || inLake(e1) || inLake(p)) continue;
        if (!clearOfRoads(p, 22f) || !clearOfRoads(e0, 22f) || !clearOfRoads(e1, 22f) || !free(p, P(2.8f, 2f, 62f), rot)) continue;
        c = p;
    }
    if (!bad(c)) {
        var g = group(gInd, "Railway");
        for (int k = -2; k <= 2; k++) { var q = c + rot * P(0f, 0f, k * 24f); put("RailTrack", P(q.x, NaNy, q.z), rot, g); }
        foreach (float z in new[] { -22f, 9f }) { var q = c + rot * P(0f, 0f, z); q.y = groundY(q) + 0.68f; put("Wagon", q, rot, g); }
        nInd += 7; Physics.SyncTransforms();
    } else log.Append("no spot Railway; ");
}
{
    var rot = yaw90();
    var c = spot(indRect, P(3.2f, 6f, 38f), 8f, 3f, rot);
    if (!bad(c)) { var g = group(gInd, "PipeRack"); for (int k = -1; k <= 1; k++) { var q = c + rot * P(0f, 0f, k * 24f); put("PipeRack", P(q.x, NaNy, q.z), rot, g); } nInd += 3; Physics.SyncTransforms(); }
}
for (int i = 0; i < 3; i++) {
    var rot = yaw();
    var t = putSpot("FloodlightMast", indRect, P(2.5f, 10f, 2.5f), rot);
    if (t == null) continue;
    var b = boundsOf(t);
    var lt = new GameObject("FloodLight").transform; lt.SetParent(t, false);
    lt.position = P(t.position.x, b.max.y - 0.6f, t.position.z) + t.forward * 1.6f;
    lt.rotation = Quaternion.LookRotation(t.forward * 0.7f + Vector3.down);
    var l = lt.gameObject.AddComponent<Light>(); l.type = LightType.Spot; l.color = new Color(1f, 0.85f, 0.65f);
    l.intensity = 3000f; l.range = 90f; l.spotAngle = 100f; l.innerSpotAngle = 50f; l.shadows = LightShadows.None;
}
log.Append("industrial ").Append(nInd).Append("; ");

// --- 6. Частный сектор: дворы и столбы с проводами ---
var gPriv = group(root, "PrivateSector");
int nPriv = 0;
string[] yardItems = { "Woodpile", "Well", "Greenhouse", "DogHouse" };
foreach (Transform plot in env.Find("PrivateSector")) {
    if (!plot.name.StartsWith("Plot_")) continue;
    int want = 2 + rnd.Next(2);
    for (int t = 0; t < 20 && want > 0; t++) {
        string n = yardItems[rnd.Next(yardItems.Length)];
        var local = P(rr(-14f, 14f), 0f, rr(8f, 48f));
        var q = plot.TransformPoint(local);
        var rot = plot.rotation * Quaternion.Euler(0f, rnd.Next(4) * 90f, 0f);
        var ext = n == "Greenhouse" ? P(3.6f, 2f, 6.4f) : n == "Woodpile" ? P(3.2f, 2f, 1.8f) : P(1.8f, 1.5f, 1.8f);
        if (slope(q, 3f) > 2f || !free(q, ext, rot)) continue;
        put(n, P(q.x, NaNy, q.z), rot, gPriv); want--; nPriv++;
        Physics.SyncTransforms();
    }
}
var wireMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/PS_Wire.mat");
var wv = new System.Collections.Generic.List<Vector3>(); var wt = new System.Collections.Generic.List<int>(); var wn = new System.Collections.Generic.List<Vector3>();
System.Action<Vector3, Vector3> wire = (a, b) => {
    float sag = Vector3.Distance(a, b) * 0.03f; int segs = 8; float w = 0.025f;
    for (int s = 0; s < segs; s++) {
        float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
        var p0 = Vector3.Lerp(a, b, t0) - Vector3.up * sag * 4f * t0 * (1 - t0);
        var p1 = Vector3.Lerp(a, b, t1) - Vector3.up * sag * 4f * t1 * (1 - t1);
        var ax = (p1 - p0).normalized; var u = Vector3.Cross(ax, Vector3.up).normalized * w; var v = Vector3.up * w;
        int bi = wv.Count;
        wv.Add(p0 + u); wv.Add(p0 - u); wv.Add(p1 + u); wv.Add(p1 - u);
        wv.Add(p0 + v); wv.Add(p0 - v); wv.Add(p1 + v); wv.Add(p1 - v);
        var nu = Vector3.Cross(ax, u).normalized; var nv = Vector3.Cross(ax, v).normalized;
        for (int k = 0; k < 4; k++) wn.Add(nu);
        for (int k = 0; k < 4; k++) wn.Add(nv);
        foreach (var q in new[] { 0, 4 }) {
            wt.AddRange(new[] { bi + q, bi + q + 2, bi + q + 1, bi + q + 1, bi + q + 2, bi + q + 3 });
            wt.AddRange(new[] { bi + q, bi + q + 1, bi + q + 2, bi + q + 1, bi + q + 3, bi + q + 2 });
        }
    } };
// Улицы частного сектора (оси грунтовок из BuildPrivateSector): столбы вдоль ворот с одной стороны, через 32 м
var streets = new[] { new[] { new Vector2(1000f, 1336f), new Vector2(1132f, 1340f) }, new[] { new Vector2(1205f, 1487f), new Vector2(1478f, 1542f) } };
int nPoles = 0;
foreach (var st in streets) {
    var a = new Vector3(st[0].x, 0f, st[0].y); var b2 = new Vector3(st[1].x, 0f, st[1].y);
    var f = (b2 - a).normalized; var s = Vector3.Cross(Vector3.up, f);
    Transform prev = null;
    for (float d = 4f; d < Vector3.Distance(a, b2); d += 32f) {
        var q = a + f * d + s * 6.5f;
        if (!free(q, P(0.4f, 3f, 0.4f), Quaternion.identity)) { prev = null; continue; }
        var pole = put("WoodenPole", P(q.x, NaNy, q.z), Quaternion.LookRotation(f), gPriv); nPoles++;
        if (prev != null) foreach (float x in new[] { -1.0f, 1.0f }) wire(prev.TransformPoint(P(x, 14.7f, 0f)), pole.TransformPoint(P(x, 14.7f, 0f)));
        prev = pole;
    }
}
if (wv.Count > 0) {
    var mesh = new Mesh(); mesh.name = "Wires_PrivateSector"; mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
    mesh.SetVertices(wv); mesh.SetTriangles(wt, 0); mesh.SetNormals(wn); mesh.SetUVs(0, new Vector2[wv.Count]); mesh.RecalculateBounds();
    string mp = "Assets/Models/LevelProps/Wires_PrivateSector.asset";
    var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
    if (oldMesh != null) { EditorUtility.CopySerialized(mesh, oldMesh); mesh = oldMesh; } else AssetDatabase.CreateAsset(mesh, mp);
    var wgo = new GameObject("Wires_PrivateSector"); wgo.transform.SetParent(gPriv, false); wgo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    wgo.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = wgo.AddComponent<MeshRenderer>(); mr.sharedMaterial = wireMat;
    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    GameObjectUtility.SetStaticEditorFlags(wgo, (StaticEditorFlags)~0 & ~StaticEditorFlags.NavigationStatic);
}
log.Append("private yard items ").Append(nPriv).Append(", wooden poles ").Append(nPoles).Append("; ");

// --- 7. Пустыри и поле боя ---
var gWar = group(root, "Wasteland");
var wild = new Rect(310f, 100f, 1610f, 1770f);
System.Func<Vector3, float, Quaternion, Vector3> wildSpot = (ext, gap, rot) => {
    for (int t = 0; t < 200; t++) {
        var c = spot(wild, ext, gap, 3f, rot);
        if (bad(c)) return c;
        if (!nearDistrict(c, 25f)) return c;
    }
    return Vector3.positiveInfinity; };
int nWar = 0;
// Окопы: ломаная линия бруствера из мешков, рядом блиндаж
for (int i = 0; i < 5; i++) {
    var rot0 = yaw();
    var c = wildSpot(P(22f, 2f, 6f), 30f, rot0);
    if (bad(c)) continue;
    var g = group(gWar, "Trench");
    var p = c - rot0 * P(18f, 0f, 0f); var heading = rot0;
    for (int k = 0; k < 6; k++) {
        var q = p + heading * P(3f, 0f, 0f);
        if (free(q, P(3.2f, 1f, 1f), heading)) { put("SandbagWall", P(q.x, NaNy, q.z), heading, g); nWar++; }
        p += heading * P(6f, 0f, 0f);
        heading = heading * Quaternion.Euler(0f, k % 2 == 0 ? 35f : -35f, 0f);
    }
    Physics.SyncTransforms();
    if (rf() < 0.7f) { var q = c + rot0 * P(0f, 0f, 12f); if (free(q, P(7f, 2f, 7f), rot0)) { put("Dugout", P(q.x, NaNy, q.z), rot0, g); nWar++; } }
}
// Воронки: чаша в рельефе с валиком и пятно копоти; на уже выкопанном месте рельеф не трогается
int hr = td.heightmapResolution; var hm = td.GetHeights(0, 0, hr, hr); bool dug = false; int nCraters = 0;
for (int i = 0; i < 14; i++) {
    var c = wildSpot(P(15f, 2f, 15f), 25f, Quaternion.identity);
    if (bad(c)) continue;
    float R = rr(9f, 14f), depth = rr(1.5f, 2.5f);
    float center = terrain.SampleHeight(c), rim = 0f;
    for (int k = 0; k < 8; k++) rim += terrain.SampleHeight(c + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * R) / 8f;
    if (rim - center < 0.8f) {
        int ix0 = Mathf.Max(0, (int)((c.x - R * 1.6f) / td.size.x * (hr - 1))), ix1 = Mathf.Min(hr - 1, (int)((c.x + R * 1.6f) / td.size.x * (hr - 1)) + 1);
        int iz0 = Mathf.Max(0, (int)((c.z - R * 1.6f) / td.size.z * (hr - 1))), iz1 = Mathf.Min(hr - 1, (int)((c.z + R * 1.6f) / td.size.z * (hr - 1)) + 1);
        for (int iz = iz0; iz <= iz1; iz++) for (int ix = ix0; ix <= ix1; ix++) {
            float wx = ix / (float)(hr - 1) * td.size.x, wz = iz / (float)(hr - 1) * td.size.z;
            float r = Mathf.Sqrt((wx - c.x) * (wx - c.x) + (wz - c.z) * (wz - c.z)) / R;
            float dh = r < 1f ? -depth * (1f - r * r) + 0.5f * Mathf.SmoothStep(0f, 1f, r) : r < 1.6f ? 0.5f * (1f - Mathf.SmoothStep(0f, 1f, (r - 1f) / 0.6f)) : 0f;
            hm[iz, ix] += dh / td.size.y;
        }
        dug = true;
    }
    nCraters++;
    var g = group(gWar, "Crater");
    if (dug) { td.SetHeights(0, 0, hm); dug = false; }
    var p = c; p.y = terrain.SampleHeight(c) + terrain.transform.position.y;
    scorch(g, p, Vector3.up, R * 2.4f);
}
// Сгоревшая техника с копотью вокруг
foreach (var n in new[] { "BurntBTR", "BurntBTR", "BurntBTR", "BurntBTR", "BurntTruck", "BurntTruck", "BurntTruck", "Zhiguli_Burnt", "Zhiguli_Burnt" }) {
    var rot = yaw();
    var c = wildSpot(P(4f, 2f, 8f), 18f, rot);
    if (bad(c)) continue;
    var t = put(n, P(c.x, NaNy, c.z), rot * Quaternion.Euler(rr(-3f, 3f), 0f, rr(-4f, 4f)), gWar); nWar++;
    var p = c; p.y = groundY(c);
    scorch(t, p, Vector3.up, 14f);
    Physics.SyncTransforms();
}
// Ежи и колючая проволока рядами
for (int i = 0; i < 6; i++) {
    var rot = yaw(); int count = 4 + rnd.Next(3);
    var c = wildSpot(P(4f * count, 2f, 3f), 20f, rot);
    if (bad(c)) continue;
    var g = group(gWar, "Hedgehogs");
    for (int k = 0; k < count; k++) { var q = c + rot * P((k - count * 0.5f) * 8f + rr(-1f, 1f), 0f, rr(-1.5f, 1.5f)); put("Hedgehog", P(q.x, NaNy, q.z), yaw(), g); nWar++; }
    Physics.SyncTransforms();
}
for (int i = 0; i < 6; i++) {
    var rot = yaw(); int count = 3 + rnd.Next(3);
    var c = wildSpot(P(6.2f * count, 1.5f, 2f), 20f, rot);
    if (bad(c)) continue;
    var g = group(gWar, "BarbedWire");
    for (int k = 0; k < count; k++) { var q = c + rot * P((k - (count - 1) * 0.5f) * 12f, 0f, 0f); put("BarbedWire", P(q.x, NaNy, q.z), rot, g); nWar++; }
    Physics.SyncTransforms();
}
log.Append("wasteland ").Append(nWar).Append(", craters ").Append(nCraters).Append("; ");

// --- 8. Ориентиры ---
var gLand = group(root, "Landmarks");
// Телевышка на самом высоком месте вне районов и дорог
{
    Vector3 best = Vector3.positiveInfinity; float bestH = -1f;
    for (float x = 340f; x < 1900f; x += 30f) for (float z = 130f; z < 1850f; z += 30f) {
        var p = P(x, 0f, z);
        if (nearDistrict(p, 40f) || !clearOfRoads(p, 60f) || inLake(p)) continue;
        float h = terrain.SampleHeight(p);
        if (h > bestH && slope(p, 14f) < 6f && free(p, P(14f, 5f, 14f), Quaternion.identity)) { bestH = h; best = p; }
    }
    if (!bad(best)) {
        var t = put("TvMast", P(best.x, NaNy, best.z), Quaternion.Euler(0f, 15f, 0f), gLand);
        var b = boundsOf(t); toLandmark(t);
        foreach (float k in new[] { 0.33f, 0.66f, 0.82f, 1f }) beacon(t, P(b.center.x, b.min.y + (b.size.y - 36f) * k, b.center.z));
        log.Append("TV mast ").Append(best.ToString("F0")).Append(" h").Append(b.size.y.ToString("0")).Append("; ");
    } else log.Append("no spot TvMast; ");
}
System.Func<string, Rect, Vector3, Transform> landmarkAt = (name, rect, ext) => {
    var c = spot(rect, ext, 12f, 4f, Quaternion.identity);
    if (bad(c)) { log.Append("no spot ").Append(name).Append("; "); return null; }
    var t = put(name, P(c.x, NaNy, c.z), Quaternion.Euler(0f, rr(0f, 360f), 0f), gLand);
    toLandmark(t); Physics.SyncTransforms();
    log.Append(name).Append(" ").Append(c.ToString("F0")).Append("; ");
    return t; };
{
    var t = landmarkAt("PowerChimney", new Rect(1150f, 110f, 250f, 260f), P(15f, 10f, 15f));
    if (t != null) { var b = boundsOf(t); smoke(t, P(b.center.x, b.max.y + 4f, b.center.z), 0.9f).gameObject.layer = landmark; beacon(t, P(b.center.x, b.max.y + 1f, b.center.z)); beacon(t, P(b.center.x, b.min.y + b.size.y * 0.5f, b.center.z)); }
}
{
    var t = landmarkAt("WaterTower", new Rect(1120f, 1360f, 140f, 150f), P(10f, 10f, 10f));
    if (t != null) { var b = boundsOf(t); beacon(t, P(b.center.x, b.max.y + 0.5f, b.center.z)); }
}
{
    // Дом культуры в City2 фасадом к ближайшей дороге
    var c = spot(rCity2, P(38f, 12f, 30f), 14f, 4f, Quaternion.identity);
    for (int t = 0; t < 4 && bad(c); t++) c = spot(new Rect(rCity2.xMin - 120f, rCity2.yMin, rCity2.width + 240f, rCity2.height), P(38f, 12f, 30f), 14f, 4f, Quaternion.identity);
    if (!bad(c)) {
        Vector3 rc, rd; float rhw;
        var rot = net.Nearest(c, 300f, out rc, out rd, out rhw) ? Quaternion.LookRotation(Vector3.ProjectOnPlane(rc - c, Vector3.up)) : Quaternion.identity;
        if (free(c, P(38f, 12f, 30f), rot)) {
            var t = put("CultureHouse", P(c.x, groundY(c) - 0.3f, c.z), rot, gLand); toLandmark(t);
            log.Append("culture house ").Append(c.ToString("F0")).Append("; ");
        } else log.Append("culture house blocked; ");
    } else log.Append("no spot CultureHouse; ");
}
// Заградительные огни
var avT = general.Find("AviationLights"); if (avT == null) { avT = new GameObject("AviationLights").transform; avT.SetParent(general, false); }
var av = avT.GetComponent<RacingProject.Effects.AviationLights>(); if (av == null) av = avT.gameObject.AddComponent<RacingProject.Effects.AviationLights>();
var aso = new SerializedObject(av);
var parr = aso.FindProperty("points"); parr.arraySize = beacons.Count;
for (int i = 0; i < beacons.Count; i++) { beacons[i].gameObject.layer = landmark; parr.GetArrayElementAtIndex(i).objectReferenceValue = beacons[i]; }
aso.FindProperty("glowMaterial").objectReferenceValue = glowMat;
aso.ApplyModifiedPropertiesWithoutUndo();
log.Append("beacons ").Append(beacons.Count).Append("; ");

// --- 9. Озеро ---
var gLake = group(root, "Lake");
var shore = new System.Collections.Generic.List<Vector3>();
for (float x = lake.min.x - 30f; x < lake.max.x + 30f; x += 6f) for (float z = lake.min.z - 30f; z < lake.max.z + 30f; z += 6f) {
    var p = P(x, 0f, z); float h = terrain.SampleHeight(p);
    if (h < waterY - 0.6f || h > waterY + 1.2f) continue;
    bool wet = false, dry = false;
    for (int k = 0; k < 8; k++) { var q = p + Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward * 8f; float hq = terrain.SampleHeight(q); if (hq < waterY - 1f) wet = true; if (hq > waterY + 1.5f) dry = true; }
    if (wet && dry && inside(p)) shore.Add(p);
}
// Перемешиваем детерминированно
for (int i = shore.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var tmp = shore[i]; shore[i] = shore[j]; shore[j] = tmp; }
int nReeds = 0, nRocks = 0;
var used = new System.Collections.Generic.List<Vector3>();
System.Func<Vector3, float, bool> farFrom = (p, r) => { foreach (var u in used) if (Vector3.Distance(u, p) < r) return false; return true; };
foreach (var p in shore) {
    if (nReeds >= 40 && nRocks >= 30) break;
    if (!farFrom(p, 14f)) continue;
    bool reed = nReeds < 40 && (nRocks >= 30 || rf() < 0.6f);
    var q = p; q.y = terrain.SampleHeight(p) + terrain.transform.position.y;
    if (reed) { put("Reeds_" + rnd.Next(2), q, yaw(), gLake).localScale = Vector3.one * rr(0.8f, 1.3f); nReeds++; }
    else { put("Boulder_" + rnd.Next(3), q - Vector3.up * 0.4f, yaw(), gLake).localScale = Vector3.one * rr(0.7f, 1.4f); nRocks++; }
    used.Add(p);
}
// Направление к воде: среднее по лучам, где рельеф ниже уровня воды
System.Func<Vector3, Vector3> toWater = p => {
    var sum = Vector3.zero;
    for (int k = 0; k < 16; k++) { var d = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward; if (terrain.SampleHeight(p + d * 20f) < waterY - 1f) sum += d; }
    return sum.sqrMagnitude > 0.01f ? sum.normalized : Vector3.zero; };
// Пологие места берега для причала и моста: суша за спиной до 8 м выше воды, впереди глубоко
var gentle = new System.Collections.Generic.List<Vector3>();
foreach (var p in shore) {
    var w = toWater(p); if (w == Vector3.zero) continue;
    if (terrain.SampleHeight(p - w * 30f) < waterY + 9f && terrain.SampleHeight(p + w * 25f) < waterY - 2f && free(p - w * 4f, P(4f, 2f, 4f), Quaternion.identity)) gentle.Add(p);
}
if (gentle.Count > 0) {
    var p = gentle[0]; var w = toWater(p);
    put("Pier", P(p.x, waterY + 0.3f, p.z), Quaternion.LookRotation(-w), gLake);
    var side = Vector3.Cross(Vector3.up, w);
    put("Boat_Blue", P(p.x, waterY - 0.25f, p.z) + w * 14f + side * 6f, Quaternion.LookRotation(side) * Quaternion.Euler(0f, rr(-20f, 20f), 0f), gLake);
    put("Boat_Green", P(p.x, NaNy, p.z) - w * 6f - side * 5f, Quaternion.LookRotation(w) * Quaternion.Euler(0f, rr(-30f, 30f), 4f), gLake);
    log.Append("pier ").Append(p.ToString("F0")).Append("; ");
    Vector3 bridge = Vector3.positiveInfinity;
    foreach (var q in gentle) if (Vector3.Distance(q, p) > 200f) { bridge = q; break; }
    if (!bad(bridge)) {
        var wb = toWater(bridge);
        put("BridgeRuin", P(bridge.x, terrain.SampleHeight(bridge) + terrain.transform.position.y - 0.3f, bridge.z), Quaternion.LookRotation(-wb), gLake);
        log.Append("bridge ").Append(bridge.ToString("F0")).Append("; ");
    }
} else log.Append("no gentle shore; ");
log.Append("reeds ").Append(nReeds).Append(", boulders ").Append(nRocks).Append("; ");

// --- 10. Геймплей: трамплины, сужения, ломающиеся препятствия, укрепление ---
var gPlay = group(root, "Gameplay");
int nRamps = 0, nChoke = 0, nBreak = 0;
// Трамплины у дорог: въезд вдоль дороги, впереди 50 м свободного ровного места для приземления
foreach (var road in net.Roads) {
    var pts = road.points;
    for (int i = 40; i < pts.Length - 40 && nRamps < 8; i += 130) {
        var c = pts[i]; if (inTown(c) || nearInter(c, 60f)) continue;
        var f = pts[i + 3] - pts[i - 3]; f.y = 0f; f.Normalize();
        var s = Vector3.Cross(Vector3.up, f) * (rf() < 0.5f ? -1f : 1f);
        var q = c + s * (road.halfWidth + 26f);
        var rot = Quaternion.LookRotation(-f);
        var land = q + f * 40f;
        if (!inside(q) || inLake(q) || inLake(land) || slope(q, 10f) > 2.5f || slope(land, 16f) > 4f) continue;
        if (!clearOfRoads(q, road.halfWidth + 12f) || !free(q, P(5.5f, 2f, 10f), rot) || !free(land, P(8f, 3f, 22f), rot)) continue;
        put(rf() < 0.7f ? "Ramp" : "WoodRamp", P(q.x, NaNy, q.z), rot, gPlay); nRamps++;
        placed.Add(new Bounds(land, P(30f, 20f, 30f))); placed.Add(new Bounds((q + land) * 0.5f, P(24f, 20f, 24f)));
        Physics.SyncTransforms();
    }
}
// Сужения: бруствер и баррикада перекрывают правую половину, ящики у края; левая половина свободна
foreach (var road in net.Roads) {
    if (!road.name.StartsWith("Road2") && !road.name.StartsWith("Road1")) continue;
    var pts = road.points;
    foreach (float frac in new[] { 0.3f, 0.62f }) {
        int i = (int)(pts.Length * frac);
        for (; i < pts.Length - 10; i += 10) { if (!inTown(pts[i]) && !nearInter(pts[i], 80f)) break; }
        if (i >= pts.Length - 10) continue;
        var c = pts[i]; var f = pts[i + 3] - pts[i - 3]; f.y = 0f; f.Normalize();
        var s = Vector3.Cross(Vector3.up, f); float hw = road.halfWidth;
        var g = group(gPlay, "Choke");
        for (int k = 0; k < 2; k++) { var q = c + s * (hw * 0.25f + k * 6.2f) + s * 3f; put("SandbagWall", P(q.x, NaNy, q.z), Quaternion.LookRotation(f), g); }
        var qb = c + s * (hw * 0.5f) + f * 14f; put("Barricade", P(qb.x, NaNy, qb.z), Quaternion.LookRotation(f), g);
        for (int k = 0; k < 3; k++) { var q = c + s * (hw - 1.5f) - f * (6f + k * 2.6f); put("Crate", P(q.x, NaNy, q.z), yaw(), g); }
        nChoke++;
        Physics.SyncTransforms();
    }
}
// Ломающиеся препятствия на полосах: штабеля ящиков и баррикады
foreach (var road in net.Roads) {
    var pts = road.points;
    for (int i = 60; i < pts.Length - 60; i += 90) {
        if (rf() < 0.45f) continue;
        var c = pts[i]; if (nearInter(c, 50f)) continue;
        var f = pts[i + 2] - pts[i - 2]; f.y = 0f; f.Normalize();
        var s = Vector3.Cross(Vector3.up, f); float off = rr(-road.halfWidth + 3f, road.halfWidth - 3f);
        var q = c + s * off;
        var g = group(gPlay, "Breakable");
        if (rf() < 0.5f) {
            for (int k = 0; k < 3; k++) put("Crate", P(q.x + s.x * k * 2.3f, NaNy, q.z + s.z * k * 2.3f), Quaternion.LookRotation(f) * Quaternion.Euler(0f, rr(-10f, 10f), 0f), g);
            var top = q + s * 1.15f; top.y = groundY(top) + 2.2f; put("Crate", top, Quaternion.LookRotation(f) * Quaternion.Euler(0f, rr(-20f, 20f), 0f), g);
        } else put("Barricade", P(q.x, NaNy, q.z), Quaternion.LookRotation(s) * Quaternion.Euler(0f, rr(-15f, 15f), 0f), g);
        nBreak++;
    }
}
// Укрепление в поле: кольцо из контейнеров, мешков и ежей с тремя проходами, две мачты прожекторов
{
    var c = wildSpot(P(46f, 4f, 46f), 50f, Quaternion.identity);
    if (!bad(c) && slope(c, 40f) < 5f) {
        var g = group(gPlay, "Stronghold");
        for (int k = 0; k < 16; k++) {
            if (k % 5 == 0) continue; // проходы
            float a = k * 22.5f; var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            var q = c + dir * 40f; var rot = Quaternion.LookRotation(Vector3.Cross(Vector3.up, dir));
            string n = k % 3 == 0 ? cont[rnd.Next(3)] : k % 3 == 1 ? "SandbagWall" : "Hedgehog";
            put(n, P(q.x, NaNy, q.z), rot, g);
        }
        foreach (float a in new[] { 60f, 240f }) {
            var q = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 30f;
            var rot = Quaternion.LookRotation(c - q);
            var t = put("FloodlightMast", P(q.x, NaNy, q.z), rot, g);
            var b = boundsOf(t);
            var lt = new GameObject("FloodLight").transform; lt.SetParent(t, false);
            lt.position = P(t.position.x, b.max.y - 0.6f, t.position.z) + t.forward * 1.6f;
            lt.rotation = Quaternion.LookRotation(t.forward * 0.9f + Vector3.down);
            var l = lt.gameObject.AddComponent<Light>(); l.type = LightType.Spot; l.color = new Color(1f, 0.85f, 0.65f);
            l.intensity = 3000f; l.range = 90f; l.spotAngle = 100f; l.innerSpotAngle = 50f; l.shadows = LightShadows.None;
        }
        log.Append("stronghold ").Append(c.ToString("F0")).Append("; ");
    } else log.Append("no stronghold spot; ");
}
log.Append("ramps ").Append(nRamps).Append(", chokes ").Append(nChoke).Append(", breakables ").Append(nBreak).Append("; ");

// --- 11. Террейн: деревья и трава из-под поставленного ---
var trees = new System.Collections.Generic.List<TreeInstance>(td.treeInstances); int tb = trees.Count;
var blockers = new System.Collections.Generic.List<Bounds>();
foreach (var b in placed) if (b.size.x > 1.5f || b.size.z > 1.5f) blockers.Add(b);
trees.RemoveAll(ti => {
    var w = Vector3.Scale(ti.position, td.size) + terrain.transform.position;
    foreach (var b in blockers) { var e = b; e.Expand(new Vector3(4f, 400f, 4f)); if (e.Contains(w)) return true; }
    return false; });
td.treeInstances = trees.ToArray();
int dres = td.detailResolution;
for (int layer = 0; layer < td.detailPrototypes.Length; layer++) {
    var dl = td.GetDetailLayer(0, 0, dres, dres, layer);
    foreach (var b in blockers) {
        if (b.size.y > 100f) continue;
        int x0 = Mathf.Max(0, (int)(b.min.x / td.size.x * dres)), x1 = Mathf.Min(dres - 1, (int)(b.max.x / td.size.x * dres));
        int z0 = Mathf.Max(0, (int)(b.min.z / td.size.z * dres)), z1 = Mathf.Min(dres - 1, (int)(b.max.z / td.size.z * dres));
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) dl[z, x] = 0;
    }
    td.SetDetailLayer(0, 0, layer, dl);
}
log.Append("trees removed ").Append(tb - trees.Count).Append("; objects ").Append(root.GetComponentsInChildren<Transform>().Length);

AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(env.gameObject.scene);
return log.ToString();
