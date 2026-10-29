// Тени от упрощённых копий тяжёлых чужих моделей (кузов и колёса Tahoe у машины игроков, пулемёты врагов и стрелка).
// Каждая тень (солнце в двух каскадах, фары игрока и ближайших врагов, прожекторы) рисует модель ещё раз, и машина
// игроков (~460 тыс. треугольников с тенями) давала больше половины треугольников кадра. Сам рендерер тень больше
// не отбрасывает, её рисует дочерний ShadowProxy (ShadowsOnly) с прореженным мешем.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Два шага, STEP в начале текста:
//   "dump"  — меши в %TEMP%/RacingProject_ShadowProxies/in, затем Tools/Blender/shadow_proxies/build.py в Blender;
//   "build" — меши Assets/Models/ShadowProxies/<имя>_Shadow.asset из .../out, ShadowProxy в VolgaCar (сцена)
//             и во всех префабах Assets/Prefabs и Assets/Resources (враги). Повторный запуск обновляет меши и не дублирует ShadowProxy.
string STEP = "build";
string WORK = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "RacingProject_ShadowProxies");
string OUT = "Assets/Models/ShadowProxies/";
var sources = new System.Collections.Generic.List<Mesh>();
foreach (var n in new[] { "Tahoe_Body", "Tahoe_FL", "Tahoe_FR", "Tahoe_RL", "Tahoe_RR" })
    sources.Add(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Tahoe/Meshes/" + n + ".asset"));
sources.Add(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Turret/MachineGun_NoScope.asset"));
foreach (var o in AssetDatabase.LoadAllAssetsAtPath("Assets/PBR Machine Gun/Art/Models/Machine Gun.fbx"))
    if (o is Mesh) sources.Add((Mesh)o);
var ci = System.Globalization.CultureInfo.InvariantCulture;
var log = new System.Text.StringBuilder();

if (STEP == "dump") {
    System.IO.Directory.CreateDirectory(System.IO.Path.Combine(WORK, "in"));
    foreach (var m in sources) {
        var w = new System.Text.StringBuilder();
        foreach (var v in m.vertices) w.Append("v ").Append(v.x.ToString("R", ci)).Append(' ').Append(v.y.ToString("R", ci)).Append(' ').Append(v.z.ToString("R", ci)).Append('\n');
        var tr = m.triangles;
        for (int i = 0; i < tr.Length; i += 3) w.Append("f ").Append(tr[i] + 1).Append(' ').Append(tr[i + 1] + 1).Append(' ').Append(tr[i + 2] + 1).Append('\n');
        System.IO.File.WriteAllText(System.IO.Path.Combine(WORK, "in", m.name + ".obj"), w.ToString());
        log.Append(m.name + " " + tr.Length / 3 + "; ");
    }
    return "dumped " + log;
}

// --- build: меши ---
System.IO.Directory.CreateDirectory(OUT);
var proxies = new System.Collections.Generic.Dictionary<Mesh, Mesh>();
foreach (var src in sources) {
    string file = System.IO.Path.Combine(WORK, "out", src.name + ".obj");
    if (!System.IO.File.Exists(file)) throw new System.Exception("no proxy for " + src.name + ", run Tools/Blender/shadow_proxies/build.py");
    var verts = new System.Collections.Generic.List<Vector3>();
    var tris = new System.Collections.Generic.List<int>();
    foreach (var line in System.IO.File.ReadAllLines(file)) {
        var p = line.Split(' ');
        if (p[0] == "v") verts.Add(new Vector3(float.Parse(p[1], ci), float.Parse(p[2], ci), float.Parse(p[3], ci)));
        else if (p[0] == "f") { tris.Add(int.Parse(p[1]) - 1); tris.Add(int.Parse(p[2]) - 1); tris.Add(int.Parse(p[3]) - 1); }
    }
    string path = OUT + src.name.Replace(" ", "") + "_Shadow.asset";
    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
    if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
    mesh.Clear();
    mesh.name = src.name.Replace(" ", "") + "_Shadow";
    mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
    mesh.SetVertices(verts);
    mesh.SetTriangles(tris, 0);
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();
    EditorUtility.SetDirty(mesh);
    proxies[src] = mesh;
    log.Append(mesh.name + " " + tris.Count / 3 + "; ");
}
AssetDatabase.SaveAssets();

// --- build: ShadowProxy рядом с каждым тяжёлым рендерером ---
System.Func<GameObject, int> attach = root => {
    int count = 0;
    foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true)) {
        if (mr.name == "ShadowProxy") continue;
        var mf = mr.GetComponent<MeshFilter>();
        Mesh proxy;
        if (mf == null || mf.sharedMesh == null || !proxies.TryGetValue(mf.sharedMesh, out proxy)) continue;
        var t = mr.transform.Find("ShadowProxy");
        if (t == null) {
            // Рендерер выключен (запасной вид, другая роль) — тень ему не нужна
            if (mr.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
            t = new GameObject("ShadowProxy").transform;
            t.SetParent(mr.transform, false);
            t.gameObject.layer = mr.gameObject.layer;
            t.gameObject.AddComponent<MeshFilter>();
            t.gameObject.AddComponent<MeshRenderer>();
        }
        t.GetComponent<MeshFilter>().sharedMesh = proxy;
        var pr = t.GetComponent<MeshRenderer>();
        pr.sharedMaterial = mr.sharedMaterial;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
        pr.receiveShadows = false;
        pr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        pr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        pr.enabled = mr.enabled;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        count++;
    }
    return count;
};

var car = GameObject.Find("VolgaCar");
log.Append("VolgaCar " + attach(car) + "; ");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(car.scene);
foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    var contents = PrefabUtility.LoadPrefabContents(path);
    int n = attach(contents);
    if (n > 0) { PrefabUtility.SaveAsPrefabAsset(contents, path); log.Append(System.IO.Path.GetFileNameWithoutExtension(path) + " " + n + "; "); }
    PrefabUtility.UnloadPrefabContents(contents);
}
return log.ToString();
