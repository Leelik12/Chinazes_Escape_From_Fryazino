// Гаражный кооператив и придорожные объекты (Environment/Roadside).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Каждый запуск пересобирает Environment/Roadside с нуля. Модели — Tools/Blender/roadside, префабы — Assets/Prefabs/Roadside.
// У моделей перед смотрит в +Z, поэтому остановки, ларьки, будка и фонари повёрнуты LookRotation к оси дороги.
// Вдоль дорог Road Architect (сплайн GSDSplineC): за городом опоры ЛЭП справа через 45 м с проводами (один меш на дорогу,
// Assets/Models/Roadside/Wires_<дорога>.asset), фонари слева внутри города, остановки с ларьками и блокпост на Road1.
// После запуска перепечь NavMesh (Tools/Unity/BakeNavMesh.cs): гаражи и блоки блокпоста меняют проезды.
// Фонари — LooseProp (Rigidbody 150 кг): машины их сбивают, в NavMesh они не попадают (NavMeshModifier в префабе).
var env = GameObject.Find("Environment").transform;
var terrain = Terrain.activeTerrain; var td = terrain.terrainData;
string RS = "Assets/Prefabs/Roadside/";
var log = new System.Text.StringBuilder();
var rnd = new System.Random(1977);

var old = env.Find("Roadside"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var root = new GameObject("Roadside").transform; root.SetParent(env, false);
// Низкие объекты (блоки ФБС ниже подъёма агента 1,4 м) иначе попадут в NavMesh как ступенька
var navMod = root.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>(); navMod.overrideArea = true; navMod.area = 1;
System.Func<string, Transform> group = n => { var g = new GameObject(n).transform; g.SetParent(root, false); return g; };
var placed = new System.Collections.Generic.List<Bounds>();

System.Func<string, Vector3, Quaternion, Transform, Transform> put = (name, pos, rot, parent) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(RS + name + ".prefab");
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
    pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
    go.transform.SetPositionAndRotation(pos, rot);
    foreach (var r in go.GetComponentsInChildren<Renderer>()) placed.Add(r.bounds);
    return go.transform;
};
// Свободно ли место: никаких коллайдеров, кроме террейна и дорог (деревья террейна потом убираются)
System.Func<Vector3, Vector3, Quaternion, bool> free = (pos, ext, rot) => {
    pos.y = terrain.SampleHeight(pos) + ext.y + 0.3f;
    foreach (var c in Physics.OverlapBox(pos, ext, rot, ~0, QueryTriggerInteraction.Ignore)) {
        if (c is TerrainCollider) continue;
        var n = c.name; if (n.StartsWith("Road") || n.StartsWith("SCut") || n.StartsWith("Inter")) continue;
        return false;
    }
    return true;
};

// --- 1. Гаражный кооператив: два ряда вдоль Z с проездом, площадка выравнивается на 62 м ---
var garages = group("Garages");
float gx0 = 812f, gx1 = 884f, gz0 = 362f, gz1 = 468f, gh = 62f, gblend = 12f;
int hr = td.heightmapResolution; var hm = td.GetHeights(0, 0, hr, hr);
for (int iz = 0; iz < hr; iz++) for (int ix = 0; ix < hr; ix++) {
    float wx = ix / (float)(hr - 1) * td.size.x, wz = iz / (float)(hr - 1) * td.size.z;
    float dx = Mathf.Max(0f, Mathf.Max(gx0 - wx, wx - gx1)), dz = Mathf.Max(0f, Mathf.Max(gz0 - wz, wz - gz1));
    float d = Mathf.Sqrt(dx * dx + dz * dz); if (d >= gblend) continue;
    hm[iz, ix] = Mathf.Lerp(hm[iz, ix], gh / td.size.y, 1f - Mathf.SmoothStep(0f, 1f, d / gblend));
}
td.SetHeights(0, 0, hm);
// Ряд A смотрит на восток, ряд B — на запад; модели ряда длиной 43.6 м и 29.2 м вдоль локального X
var rowA = Quaternion.Euler(0, 90, 0); var rowB = Quaternion.Euler(0, -90, 0);
put("GarageRow_6", new Vector3(836f, 0, 387f), rowA, garages);
put("GarageRow_6b", new Vector3(836f, 0, 432f), rowA, garages);
put("GarageRow_6b", new Vector3(872f, 0, 387f), rowB, garages);
put("GarageRow_4", new Vector3(872f, 0, 425f), rowB, garages);
// В проезде — сгоревшая машина, бочка с огнём и пара блоков
var wl = env.Find("Wasteland");
var oldBarrels = new System.Collections.Generic.List<GameObject>();
foreach (Transform c in wl) if (c.name.StartsWith("BurningBarrel_Roadside")) oldBarrels.Add(c.gameObject);
foreach (var g in oldBarrels) UnityEngine.Object.DestroyImmediate(g);
System.Func<string, Vector3, float, float, Transform, Transform> putAny = (path, pos, yaw, s, parent) => {
    var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
    pos.y = terrain.SampleHeight(pos) + terrain.transform.position.y;
    go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
    go.transform.localScale = src.transform.localScale * s;
    return go.transform;
};
putAny("Assets/Prefabs/Wasteland/BurntSedan.prefab", new Vector3(850f, 0, 445f), 12f, 1.95f, garages);
var gb = putAny("Assets/Prefabs/Wasteland/BurningBarrel.prefab", new Vector3(846f, 0, 404f), 0f, 1f, wl); gb.name = "BurningBarrel_Roadside_Garages";
put("ConcreteBlock", new Vector3(858f, 0, 372f), Quaternion.Euler(0, 20, 0), garages);
Physics.SyncTransforms(); // иначе OverlapBox в free() не видит только что поставленные объекты

// --- 2. Дороги: опоры с проводами, фонари, остановки, ларьки, блокпост ---
var poles = group("PowerPoles"); var lamps = group("StreetLamps"); var stops = group("BusStops"); var post = group("Checkpoint");
var interPos = new System.Collections.Generic.List<Vector3>();
foreach (Transform c in env.Find("RoadArchitectSystem1/Intersections")) interPos.Add(c.position);
System.Func<Vector3, bool> nearInter = p => { foreach (var q in interPos) if (Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(q.x, 0, q.z)) < 40f) return true; return false; };
System.Func<Vector3, bool> inCity = p => p.x > 330f && p.x < 830f && p.z > 120f && p.z < 780f;
float half = 4 * 5f / 2f + 3f; // половина ширины дороги с обочинами: 4 полосы по 5 м и 3 м обочины

var wireMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/PS_Wire.mat");
if (wireMat == null) {
    wireMat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/PS_Rust.mat"));
    wireMat.SetTexture("_BaseMap", null); wireMat.SetTexture("_BumpMap", null); wireMat.SetColor("_BaseColor", new Color(0.06f, 0.06f, 0.06f));
    wireMat.SetFloat("_Smoothness", 0.3f);
    AssetDatabase.CreateAsset(wireMat, "Assets/Content/PrivateSector/Materials/PS_Wire.mat");
}

foreach (var rn in new[] { "Road1", "Road2", "Road3" }) {
    var sp = env.Find("RoadArchitectSystem1/" + rn + "/Spline").GetComponent("GSDSplineC"); var T = sp.GetType();
    float len = (float)T.GetField("distance").GetValue(sp);
    var toParam = T.GetMethod("TranslateDistBasedToParam"); var value = T.GetMethod("GetSplineValue", new[] { typeof(float), typeof(bool) });
    System.Func<float, Vector3> at = dist => (Vector3)value.Invoke(sp, new object[] { (float)toParam.Invoke(sp, new object[] { Mathf.Clamp(dist, 0f, len) }), false });
    System.Func<float, Vector3> dirAt = dist => { var a = at(dist - 2f); var b = at(dist + 2f); var dd = b - a; dd.y = 0; return dd.normalized; };

    // Опоры ЛЭП: справа по ходу сплайна; провода только между соседними поставленными опорами
    var verts = new System.Collections.Generic.List<Vector3>(); var tris = new System.Collections.Generic.List<int>();
    Transform prev = null; int nPoles = 0;
    for (float d = 20f; d < len - 20f; d += 45f) {
        var c = at(d); var f = dirAt(d); var right = Vector3.Cross(Vector3.up, f);
        var p = c + right * (half + 4f);
        bool ok = !nearInter(c) && !inCity(p) && terrain.SampleHeight(p) > 11f && free(p, new Vector3(0.6f, 3f, 0.6f), Quaternion.identity);
        if (!ok) { prev = null; continue; }
        var pole = put("PowerPole", p, Quaternion.LookRotation(f), poles); nPoles++;
        if (prev != null) {
            foreach (var off in new[] { new Vector3(-1.4f, 18.5f, 0), new Vector3(0, 19.4f, 0), new Vector3(1.4f, 18.5f, 0) }) {
                var a = prev.TransformPoint(off); var b = pole.TransformPoint(off);
                float sag = Vector3.Distance(a, b) * 0.02f; int segs = 8; float w = 0.03f;
                for (int s = 0; s < segs; s++) {
                    float t0 = s / (float)segs, t1 = (s + 1) / (float)segs;
                    var p0 = Vector3.Lerp(a, b, t0) - Vector3.up * sag * 4f * t0 * (1 - t0);
                    var p1 = Vector3.Lerp(a, b, t1) - Vector3.up * sag * 4f * t1 * (1 - t1);
                    var ax = (p1 - p0).normalized; var u = Vector3.Cross(ax, Vector3.up).normalized * w; var v = Vector3.up * w;
                    int bi = verts.Count;
                    verts.Add(p0 + u); verts.Add(p0 - u); verts.Add(p1 + u); verts.Add(p1 - u);
                    verts.Add(p0 + v); verts.Add(p0 - v); verts.Add(p1 + v); verts.Add(p1 - v);
                    // Две перекрещенные ленты с треугольниками в обе стороны: провод виден с любого ракурса без двустороннего материала
                    foreach (var q in new[] { 0, 4 }) {
                        tris.AddRange(new[] { bi + q, bi + q + 2, bi + q + 1, bi + q + 1, bi + q + 2, bi + q + 3 });
                        tris.AddRange(new[] { bi + q, bi + q + 1, bi + q + 2, bi + q + 1, bi + q + 3, bi + q + 2 });
                    }
                }
            }
        }
        prev = pole;
    }
    if (verts.Count > 0) {
        var mesh = new Mesh(); mesh.name = "Wires_" + rn; mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        string mp = "Assets/Models/Roadside/Wires_" + rn + ".asset";
        var oldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
        if (oldMesh != null) { EditorUtility.CopySerialized(mesh, oldMesh); mesh = oldMesh; } else AssetDatabase.CreateAsset(mesh, mp);
        // Вершины в мировых координатах, а Environment смещён: ставим объект проводов в начало мира
        var wgo = new GameObject("Wires_" + rn); wgo.transform.SetParent(poles, false); wgo.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        wgo.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = wgo.AddComponent<MeshRenderer>(); mr.sharedMaterial = wireMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(wgo, (StaticEditorFlags)~0);
    }

    // Фонари в городе: слева, рукой к дороге
    int nLamps = 0;
    for (float d = 10f; d < len - 10f; d += 40f) {
        var c = at(d); if (!inCity(c) || nearInter(c)) continue;
        var f = dirAt(d); var left = -Vector3.Cross(Vector3.up, f);
        var p = c + left * (half + 4f);
        if (!free(p, new Vector3(1.5f, 3f, 1.5f), Quaternion.identity)) continue; // с запасом: фонарь не должен сужать проезды между домами
        put("StreetLamp", p, Quaternion.LookRotation(-left), lamps); nLamps++;
    }
    log.AppendLine(rn + ": poles " + nPoles + ", lamps " + nLamps);

    // Остановки с ларьком рядом: на заданных расстояниях вдоль дороги, справа; если места нет — пробуем чуть дальше
    float[] stopAt = rn == "Road1" ? new[] { 520f, 1450f } : rn == "Road2" ? new[] { 900f, 2600f, 3700f } : new[] { 600f, 1500f };
    foreach (var d0 in stopAt) {
        for (float d = d0; d < d0 + 200f; d += 15f) {
            var c = at(d); if (nearInter(c)) continue;
            var f = dirAt(d); var right = Vector3.Cross(Vector3.up, f);
            var p = c + right * (half + 2.2f); var pk = c + right * (half + 2.8f) + f * 9f;
            var rot = Quaternion.LookRotation(-right);
            if (!free(p, new Vector3(6f, 3f, 3f), rot) || !free(pk, new Vector3(4f, 3f, 3f), rot) || terrain.SampleHeight(p) < 11f) continue;
            put("BusStop", p, rot, stops);
            put(rnd.Next(2) == 0 ? "Kiosk" : "Kiosk_Yellow", pk, rot, stops);
            log.AppendLine("  stop " + rn + " @" + d.ToString("0") + " " + p.ToString("F0"));
            break;
        }
    }

    // Блокпост на Road1 к северу от города: будка у обочины, бочка и блоки ФБС на половине проезжей части
    if (rn == "Road1") {
        float dp = 1000f;
        for (; dp < 1300f; dp += 10f) { if (!nearInter(at(dp))) break; }
        var c = at(dp); var f = dirAt(dp); var right = Vector3.Cross(Vector3.up, f);
        put("GuardBooth", c + right * (half + 3f), Quaternion.LookRotation(-right), post);
        // Ряд блоков ФБС перекрывает половину проезжей части; змейка из двух рядов оказалась ловушкой для ИИ врагов
        for (int k = 0; k < 3; k++) put("ConcreteBlock", c + f * 4f + right * (-10f + k * 4f), Quaternion.LookRotation(f), post);
        var cb = putAny("Assets/Prefabs/Wasteland/BurningBarrel.prefab", c + right * (half + 1f) - f * 3f, 0f, 1f, wl); cb.name = "BurningBarrel_Roadside_Checkpoint";
        log.AppendLine("  checkpoint @" + dp.ToString("0") + " " + c.ToString("F0"));
    }
    Physics.SyncTransforms();
}

// --- 3. Террейн: деревья и трава из-под построек ---
var trees = new System.Collections.Generic.List<TreeInstance>(td.treeInstances); int tb = trees.Count;
trees.RemoveAll(ti => {
    var w = Vector3.Scale(ti.position, td.size) + terrain.transform.position;
    if (w.x > gx0 - 6f && w.x < gx1 + 6f && w.z > gz0 - 6f && w.z < gz1 + 6f) return true;
    foreach (var b in placed) { var e = b; e.Expand(new Vector3(6f, 100f, 6f)); if (e.Contains(w)) return true; }
    return false;
});
td.treeInstances = trees.ToArray();
int dres = td.detailResolution;
for (int layer = 0; layer < td.detailPrototypes.Length; layer++) {
    var dl = td.GetDetailLayer(0, 0, dres, dres, layer);
    foreach (var b in placed) {
        int x0 = Mathf.Max(0, (int)(b.min.x / td.size.x * dres)), x1 = Mathf.Min(dres - 1, (int)(b.max.x / td.size.x * dres));
        int z0 = Mathf.Max(0, (int)(b.min.z / td.size.z * dres)), z1 = Mathf.Min(dres - 1, (int)(b.max.z / td.size.z * dres));
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) dl[z, x] = 0;
    }
    td.SetDetailLayer(0, 0, layer, dl);
}
// Проезд между рядами гаражей: без травы, грунт (слой 2)
{
    int lx0 = (int)(838f / td.size.x * dres), lx1 = (int)(870f / td.size.x * dres), lz0 = (int)((gz0 - 6f) / td.size.z * dres), lz1 = (int)((gz1 + 6f) / td.size.z * dres);
    for (int layer = 0; layer < td.detailPrototypes.Length; layer++) {
        var dl = td.GetDetailLayer(0, 0, dres, dres, layer);
        for (int z = lz0; z <= lz1; z++) for (int x = lx0; x <= lx1; x++) dl[z, x] = 0;
        td.SetDetailLayer(0, 0, layer, dl);
    }
}
var am = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
for (int z = 0; z < td.alphamapHeight; z++) for (int x = 0; x < td.alphamapWidth; x++) {
    float wx = x / (float)(td.alphamapWidth - 1) * td.size.x, wz = z / (float)(td.alphamapHeight - 1) * td.size.z;
    if (wx < 842f || wx > 866f || wz < gz0 - 4f || wz > gz1 + 4f) continue;
    for (int l = 0; l < td.alphamapLayers; l++) am[z, x, l] = l == 2 ? 1f : 0f;
}
td.SetAlphamaps(0, 0, am);
log.AppendLine("terrain trees removed: " + (tb - trees.Count));
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
