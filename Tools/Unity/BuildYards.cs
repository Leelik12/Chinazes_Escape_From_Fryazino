// Генератор дворов-кварталов для сцены SovietCity.
// Не компилируется Unity (лежит вне Assets): текст целиком выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Каждый запуск пересобирает Environment/Yards с нуля по списку кварталов ниже, поэтому раскладку правят здесь и перезапускают.
// Попутно: убирает старые дома западной половины района, деревья террейна и грунтовки внутри кварталов,
// горящие бочки дворов кладёт в Environment/Wasteland (иначе PropNetwork их не найдёт).
// В конце проверяет, что дома не стоят на дорогах, и возвращает список нарушений.
var env = GameObject.Find("Environment").transform;
var terrain = Terrain.activeTerrain; var td = terrain.terrainData;
float k = 0.35f * 1.5f; // масштаб домов: в 1.5 раза крупнее исходных 0.35
string B = "Assets/Content/Buildings/russian_buildings/prefabs/buildings/rus_build_";
string P = "Assets/Content/SmallEnvironment/Road props for games/Prefabs/";
string FENCE = "Assets/Content/SmallEnvironment/Assets_FenceChained/prefabs/chainlink_single.prefab";
string[] TREES = { "Assets/Content/Tree_Packs/URP_Tree_Pack/Prefabs/URP_Tree_1.prefab", "Assets/Content/Tree_Packs/URP_Tree_Pack/Prefabs/URP_Tree_3.prefab" };
string[] FIVE = { "5et_01", "5et_01a", "5et_02", "5et_02a", "5et_03", "5et_03a", "5et_03b", "5et_03c", "5et_03d", "5et_03e", "5et_03f", "5et_04" };
string[] NINE = { "9et_02", "9et_02a", "9et_02b", "9et_03", "9et_03a", "9et_03b" };
var log = new System.Text.StringBuilder();

// Кварталы: имя, шаблон, центр X, центр Z, поворот, зерно случайности.
// A — замкнутый двор (9-этажка + три 5-этажки), B — «П» (открыт с юга), C — узкий «П» из трёх 5-этажек
var blocks = new object[][] {
    new object[] { "Yard_01", "A", 523f, 640f, 0f, 1 },
    new object[] { "Yard_02", "B", 525f, 503f, 90f, 2 },
    new object[] { "Yard_03", "A", 505f, 355f, 90f, 3 },
    new object[] { "Yard_04", "C", 500f, 210f, 0f, 4 },
};

// Западная половина юго-западного района: между западной кольцевой и левой обочиной главной дороги
// (обочина: x ≈ 575 при z = 220, 592 при z = 370, 625 при z = 620)
System.Func<Vector3, bool> inWestPocket = p => p.x > 380f && p.z > 140f && p.z < 760f && p.x < 570f + 0.1f * (p.z - 190f);

// --- 1. Старые дома и мелочь западной половины удаляем ---
int removed = 0;
foreach (var city in new[] { "City1", "City1SmallEnvironment" }) {
    var list = new System.Collections.Generic.List<GameObject>();
    foreach (Transform c in env.Find(city)) if (inWestPocket(c.position)) list.Add(c.gameObject);
    foreach (var g in list) { UnityEngine.Object.DestroyImmediate(g); removed++; }
}
log.AppendLine("removed old: " + removed);

// --- 2. Пересобираем Yards ---
var oldYards = env.Find("Yards"); if (oldYards != null) UnityEngine.Object.DestroyImmediate(oldYards.gameObject);
var yards = new GameObject("Yards").transform; yards.SetParent(env, false);
var wasteland = env.Find("Wasteland");
var oldBarrels = new System.Collections.Generic.List<GameObject>();
foreach (Transform c in wasteland) if (c.name.StartsWith("BurningBarrel_Yard")) oldBarrels.Add(c.gameObject);
foreach (var g in oldBarrels) UnityEngine.Object.DestroyImmediate(g);

Transform yard = null;
System.Func<string, float, float, float, float, Transform, Transform> put = (path, x, z, rot, s, parent) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
    var w = yard.TransformPoint(new Vector3(x, 0, z)); w.y = terrain.SampleHeight(w) + terrain.transform.position.y;
    go.transform.position = w; go.transform.rotation = yard.rotation * Quaternion.Euler(0, rot, 0);
    go.transform.localScale = src.transform.localScale * s; // s — множитель к масштабу префаба
    return go.transform;
};
var houseRects = new System.Collections.Generic.List<Transform>();

foreach (var bl in blocks) {
    string name = (string)bl[0], tpl = (string)bl[1]; var rnd = new System.Random((int)bl[5]);
    yard = new GameObject(name).transform; yard.SetParent(yards, false);
    yard.position = new Vector3((float)bl[2], 0, (float)bl[3]); yard.rotation = Quaternion.Euler(0, (float)bl[4], 0);
    var houses = new GameObject("Houses").transform; houses.SetParent(yard, false);
    var props = new GameObject("Props").transform; props.SetParent(yard, false);
    System.Func<string[], string> pick = arr => B + arr[rnd.Next(arr.Length)] + ".prefab";

    // Внутренний прямоугольник двора (полуразмеры) и дома по его краям
    float ax, az;
    if (tpl == "C") {
        ax = 22f; az = 31f;
        houseRects.Add(put(pick(FIVE), -32f, 0f, 90f, 1.5f, houses));
        houseRects.Add(put(pick(FIVE), 32f, 0f, -90f, 1.5f, houses));
        houseRects.Add(put(pick(FIVE), 0f, 50f, 0f, 1.5f, houses));
    } else {
        ax = 45f; az = 40f;
        houseRects.Add(put(pick(NINE), 0f, az + 10f, 0f, 1.5f, houses));
        houseRects.Add(put(pick(FIVE), -ax - 10f, 0f, 90f, 1.5f, houses));
        houseRects.Add(put(pick(FIVE), ax + 10f, 0f, -90f, 1.5f, houses));
        if (tpl == "A") houseRects.Add(put(pick(FIVE), 0f, -az - 10f, 180f, 1.5f, houses));
    }

    // Занятые места: x, z, радиус — чтобы мелочь не налезала друг на друга
    var taken = new System.Collections.Generic.List<Vector3>();
    System.Func<float, float, float, bool> free = (x, z, r) => {
        if (Mathf.Abs(x) > ax - r || z > az - r || (tpl == "A" && z < -az + r)) return false;
        foreach (var o in taken) if (new Vector2(x - o.x, z - o.y).magnitude < r + o.z) return false;
        taken.Add(new Vector3(x, z, r)); return true;
    };
    System.Func<float> rf = () => (float)rnd.NextDouble();

    // Хоккейная коробка из рабицы с проходом и коллайдерами (сетка сама без коллайдеров)
    if (ax >= 40f) {
        float cx = -6f + rf() * 12f, cz = -6f + rf() * 6f;
        taken.Add(new Vector3(cx, cz, 20f));
        var court = new GameObject("Court").transform; court.SetParent(props, false); court.localPosition = new Vector3(cx, 0, cz);
        float panel = 2.8f * 2f; int nx = 5, nz = 3; float hx = nx * panel / 2f, hz = nz * panel / 2f;
        for (int i = 0; i < nx; i++) { float x = cx - hx + panel * (i + 0.5f); put(FENCE, x, cz + hz, 0, 2, court); put(FENCE, x, cz - hz, 180, 2, court); }
        for (int i = 0; i < nz; i++) { float z = cz - hz + panel * (i + 0.5f); put(FENCE, cx + hx, z, 90, 2, court); if (i != 1) put(FENCE, cx - hx, z, -90, 2, court); }
        foreach (var side in new[] { new Vector4(0, hz, hx * 2, 0.4f), new Vector4(0, -hz, hx * 2, 0.4f), new Vector4(hx, 0, 0.4f, hz * 2) }) {
            var bc = court.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(side.x, 2.1f, side.y); bc.size = new Vector3(side.z, 4.2f, side.w);
        }
        var cp = court.position; cp.y = terrain.SampleHeight(cp); court.position = cp;
        foreach (Transform c in court) { var p = c.position; p.y = cp.y; c.position = p; }
    }

    // Лавочки с урнами вдоль северного дома
    var benches = new GameObject("Benches").transform; benches.SetParent(props, false);
    for (int i = 0; i < 3; i++) {
        float x = -ax + 10f + rf() * (ax * 2f - 20f);
        if (!free(x, az - 4f, 3.5f)) continue;
        put(P + "Benche.prefab", x, az - 4f, 90f, 2f, benches);
        put(P + (rnd.Next(2) == 0 ? "Urn.prefab" : "Urn_2.prefab"), x + 3.5f, az - 4f, 0f, 2f, benches);
    }
    // Деревья по углам и у стен
    var trees = new GameObject("Trees").transform; trees.SetParent(props, false);
    var spots = new System.Collections.Generic.List<Vector2> { new Vector2(-ax + 9, az - 10), new Vector2(ax - 9, az - 10), new Vector2(-ax + 9, -az + 10), new Vector2(ax - 9, -az + 10), new Vector2(-ax + 7, 0), new Vector2(ax - 7, 4) };
    foreach (var s in spots)
        if (free(s.x, s.y, 6f)) put(TREES[rnd.Next(2)], s.x, s.y, rf() * 360f, 0.75f + rf() * 0.25f, trees);
    // Трансформаторная будка, подстанция, сгоревшие машины — на свободных местах
    var extras = new[] {
        new object[] { "Assets/Content/SmallEnvironment/transformer_box/prefab/transformer_box.prefab", 1.5f, 6f },
        new object[] { "Assets/Content/SmallEnvironment/Transformer_substation_booth/Prefabs/Transformer_substation_booth.prefab", 2f, 5f },
        new object[] { "Assets/Prefabs/Wasteland/BurntSedan.prefab", 1.95f, 6f },
        new object[] { "Assets/Prefabs/Wasteland/BurntHatchback.prefab", 1.95f, 5.5f },
    };
    foreach (var e in extras) {
        for (int tries = 0; tries < 30; tries++) {
            float x = (rf() * 2f - 1f) * ax, z = (rf() * 2f - 1f) * az;
            if (!free(x, z, (float)e[2])) continue;
            put((string)e[0], x, z, rf() * 360f, (float)e[1], props); break;
        }
    }
    // Горящая бочка у половины дворов
    if (rnd.Next(2) == 0)
        for (int tries = 0; tries < 30; tries++) {
            float x = (rf() * 2f - 1f) * ax, z = (rf() * 2f - 1f) * az;
            if (!free(x, z, 3f)) continue;
            put("Assets/Prefabs/Wasteland/BurningBarrel.prefab", x, z, 0f, 1f, wasteland).name = "BurningBarrel_" + name; break;
        }
}

// --- 3. Террейн: деревья внутри кварталов убираем, грунтовки западной половины закрашиваем городской землёй ---
var trees2 = new System.Collections.Generic.List<TreeInstance>(td.treeInstances); int treesBefore = trees2.Count;
trees2.RemoveAll(ti => {
    var w = Vector3.Scale(ti.position, td.size) + terrain.transform.position;
    if (inWestPocket(w)) return true;
    foreach (Transform y in yards) { var l = y.InverseTransformPoint(w); if (Mathf.Abs(l.x) < 75 && Mathf.Abs(l.z) < 75) return true; }
    return false;
});
td.treeInstances = trees2.ToArray();
int res = td.alphamapResolution; var alpha = td.GetAlphamaps(0, 0, res, res);
for (int iz = 0; iz < res; iz++) for (int ix = 0; ix < res; ix++) {
    var w = new Vector3((ix + 0.5f) / res * td.size.x, 0, (iz + 0.5f) / res * td.size.z);
    if (!inWestPocket(w)) continue;
    alpha[iz, ix, 1] += alpha[iz, ix, 2]; alpha[iz, ix, 2] = 0f; // слой 2 (Ground002) — грунтовка, слой 1 (Ground003) — городская земля
}
td.SetAlphamaps(0, 0, alpha);
log.AppendLine("terrain trees removed: " + (treesBefore - trees2.Count));

// --- 4. Проверка: дома не должны стоять на дорогах (растр 2 м по мешам Road Architect) ---
int N = 1000; var road = new bool[N * N];
foreach (var mf in env.Find("RoadArchitectSystem1").GetComponentsInChildren<MeshFilter>()) {
    if (mf.sharedMesh == null || mf.GetComponent<Renderer>() == null) continue;
    var v = mf.sharedMesh.vertices; var tris = mf.sharedMesh.triangles; var tf = mf.transform;
    for (int i = 0; i < tris.Length; i += 3) {
        var a = tf.TransformPoint(v[tris[i]]); var b = tf.TransformPoint(v[tris[i + 1]]); var c = tf.TransformPoint(v[tris[i + 2]]);
        int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / 2)), x1 = Mathf.Min(N - 1, (int)(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / 2));
        int z0 = Mathf.Max(0, (int)(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / 2)), z1 = Mathf.Min(N - 1, (int)(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / 2));
        if (x1 - x0 > 40 || z1 - z0 > 40) continue;
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) road[z * N + x] = true;
    }
}
foreach (var h in houseRects) {
    var bounds = new Bounds(h.position, Vector3.zero); bool any = false;
    foreach (var r in h.GetComponentsInChildren<Renderer>()) { if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds); }
    int hits = 0;
    for (float z = bounds.min.z; z <= bounds.max.z; z += 2f) for (float x = bounds.min.x; x <= bounds.max.x; x += 2f) {
        int ix = (int)(x / 2), iz = (int)(z / 2);
        if (ix >= 0 && iz >= 0 && ix < N && iz < N && road[iz * N + ix]) hits++;
    }
    if (hits > 0) log.AppendLine("ON ROAD: " + h.parent.parent.name + "/" + h.name + " cells " + hits + " bounds " + bounds.min.ToString("F0") + "-" + bounds.max.ToString("F0"));
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
