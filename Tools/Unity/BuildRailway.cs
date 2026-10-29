// Железная дорога через северный лес (Environment/Railway) и электричка ЭД4М (Tools/Blender/train).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// 1) Материалы TR_* (Assets/Content/Train/Materials) — копии PS_Rust с текстурами generate_train_textures.py,
//    стёкла фонарей светятся; TR_LampRed_Off — погашенный фонарь переезда;
// 2) импорт FBX Assets/Models/Train: масштаб 2, материалы переназначаются на TR_*, PS_*, LP_*;
// 3) линия — прямая от O по D, от S0 до S1 (запад — за забором края карты, восток — в лесу у края);
//    высота — сглаженный рельеф, на переездах головка рельса на 6 см выше асфальта дороги 2 (выше разметки)
//    и тот же уклон ещё 20 м за краем, дальше переход к рельефу на 60 м;
// 4) террейн: коридор ±8 м опускается под балласт (переход до 26 м), деревья ближе 14 м и трава ближе 8 м убираются;
//    у дорог (ближе 5 м от края проезжей части, переход до 14 м) рельеф не трогается;
// 5) звенья: RailTrack (Assets/Prefabs/LevelProps) на балластной призме RailBed каждые 24 м, опоры контактной сети
//    каждые 60 м справа по ходу к востоку, провода — тонкие цилиндры PS_Wire без коллайдеров;
// 6) переезды: настилы, по шлагбауму и светофору с каждой стороны дороги (RailCrossing), звонок;
// 7) поезд: четыре вагона (кинематические тела с BoxCollider, в сцене выключены) и ElectricTrain на General/EnemyManager.
// Повторный запуск пересобирает Environment/Railway; профиль высот берётся из уже собранного ElectricTrain,
// поэтому рельеф второй раз не опускается (чтобы пересчитать профиль, верните террейн из git и очистите track). После сборки — Tools/Unity/BakeNavMesh.cs (насыпь меняет NavMesh)
var log = new System.Text.StringBuilder();
string MD = "Assets/Models/Train/", MAT = "Assets/Content/Train/Materials/", TEX = "Assets/Content/Train/Textures/";
System.IO.Directory.CreateDirectory(MAT);
AssetDatabase.Refresh();
var O = new Vector3(856f, 0f, 1752f); var D = new Vector3(0.99f, 0f, 0.13f).normalized;
var R = Vector3.Cross(Vector3.up, D); // вправо по ходу к востоку
float S0 = -600f, S1 = 560f, STEP = 4f, RAILTOP = 0.68f;
float[] CROSS = { -398.6f, -0.3f };

// --- 1. Материалы ---
var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/PS_Rust.mat");
var mats = new System.Collections.Generic.Dictionary<string, Material>();
foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TEX.TrimEnd('/') })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
    bool normal = path.EndsWith("_Normal.png");
    ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
    ti.sRGBTexture = !normal; ti.maxTextureSize = 2048; ti.wrapMode = TextureWrapMode.Repeat;
    ti.SaveAndReimport();
}
System.Func<string, string, Color, Material> makeMat = (name, tex, emission) => {
    string mPath = MAT + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(mPath);
    if (m == null) { m = new Material(template); AssetDatabase.CreateAsset(m, mPath); }
    else m.CopyPropertiesFromMaterial(template);
    m.name = name;
    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + tex + ".png"));
    m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + tex + "_Normal.png"));
    m.SetFloat("_Metallic", tex == "TR_Ballast" ? 0f : 0.35f);
    if (emission.maxColorComponent > 0f) {
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    } else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
    EditorUtility.SetDirty(m);
    mats[name] = m;
    return m;
};
foreach (var n in new[] { "TR_BodyGrey", "TR_Red", "TR_DarkMetal", "TR_Ballast" }) makeMat(n, n, Color.black);
makeMat("TR_LampWhite", "TR_LampWhite", new Color(1f, 0.95f, 0.85f) * 2.5f);
var lampOn = makeMat("TR_LampRed", "TR_LampRed", new Color(1f, 0.12f, 0.06f) * 3f);
var lampOff = makeMat("TR_LampRed_Off", "TR_LampRed", Color.black);
lampOff.SetColor("_BaseColor", new Color(0.35f, 0.3f, 0.3f));
AssetDatabase.SaveAssets();

// --- 2. Импорт моделей ---
var models = new System.Collections.Generic.Dictionary<string, GameObject>();
foreach (string fbx in System.IO.Directory.GetFiles(MD, "*.fbx")) {
    string path = fbx.Replace('\\', '/');
    string name = System.IO.Path.GetFileNameWithoutExtension(path);
    var imp = (ModelImporter)AssetImporter.GetAtPath(path);
    imp.globalScale = 2f; imp.useFileScale = true; imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
    imp.addCollider = false; imp.isReadable = name == "RailBed";
    imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
    foreach (var r in AssetDatabase.LoadAllAssetsAtPath(path)) {
        var m = r as Material; if (m == null) continue;
        Material target;
        if (!mats.TryGetValue(m.name, out target))
            target = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/" + m.name + ".mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/LevelProps/Materials/" + m.name + ".mat");
        if (target != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
        else log.Append("no material ").Append(m.name).Append(" in ").Append(name).Append("; ");
    }
    imp.SaveAndReimport();
    models[name] = AssetDatabase.LoadAssetAtPath<GameObject>(path);
}
var railTrack = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LevelProps/RailTrack.prefab");
var wireMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/PS_Wire.mat");

// --- 3. Профиль линии (высота подошвы шпал) ---
var terrain = Terrain.activeTerrain; var td = terrain.terrainData;
var manager = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.EnemyManager>();
var train = manager.GetComponent<RacingProject.Props.ElectricTrain>();
int count = Mathf.RoundToInt((S1 - S0) / STEP) + 1;
var baseY = new float[count];
var oldTrack = train != null ? new SerializedObject(train).FindProperty("track") : null;
bool reuse = oldTrack != null && oldTrack.arraySize == count;
// Асфальт под точкой: дороги — коллайдеры Asphalt в Environment/Roads
System.Func<Vector3, float> asphalt = p => {
    float best = float.NaN;
    foreach (var hit in Physics.RaycastAll(new Vector3(p.x, 400f, p.z), Vector3.down, 500f, ~0, QueryTriggerInteraction.Ignore))
        if (hit.collider.name == "Asphalt" && (float.IsNaN(best) || hit.point.y > best)) best = hit.point.y;
    return best;
};
if (reuse) for (int i = 0; i < count; i++) baseY[i] = oldTrack.GetArrayElementAtIndex(i).vector3Value.y;
else {
    var raw = new float[count];
    for (int i = 0; i < count; i++) raw[i] = terrain.SampleHeight(O + D * (S0 + i * STEP));
    int win = Mathf.RoundToInt(48f / STEP);
    for (int i = 0; i < count; i++) {
        float sum = 0f; int n = 0;
        for (int k = -win; k <= win; k++) { int j = Mathf.Clamp(i + k, 0, count - 1); sum += raw[j]; n++; }
        baseY[i] = sum / n - 0.1f;
    }
    // На переездах — по асфальту (за краем дороги — с тем же уклоном до 20 м, под настилом), переход к рельефу на 60 м
    foreach (float sc in CROSS) {
        float ac = asphalt(O + D * sc), slope = (asphalt(O + D * (sc + 6f)) - asphalt(O + D * (sc - 6f))) / 12f;
        for (int i = 0; i < count; i++) {
            float s = S0 + i * STEP, d = Mathf.Abs(s - sc);
            if (d > 80f) continue;
            float a = asphalt(O + D * s);
            if (float.IsNaN(a) || d > 8f) a = ac + slope * Mathf.Clamp(s - sc, -20f, 20f);
            float w = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(20f, 80f, d));
            baseY[i] = Mathf.Lerp(baseY[i], a + 0.06f - RAILTOP, w);
        }
    }
}
System.Func<float, Vector3> at = s => {
    float f = Mathf.Clamp((s - S0) / STEP, 0f, count - 1.001f); int i = (int)f;
    return O + D * s + Vector3.up * Mathf.Lerp(baseY[i], baseY[i + 1], f - i);
};
System.Func<float, float> nearCross = s => { float m = 1e9f; foreach (float sc in CROSS) m = Mathf.Min(m, Mathf.Abs(s - sc)); return m; };

// --- 4. Террейн, деревья, трава ---
var roads = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.RoadNetwork>();
roads.SetRoads(roads.Roads); // сетка поиска строится в Awake, в редакторе её нет
if (!reuse) {
    int hr = td.heightmapResolution; var hm = td.GetHeights(0, 0, hr, hr);
    for (int iz = 0; iz < hr; iz++) for (int ix = 0; ix < hr; ix++) {
        var w = new Vector3(ix / (float)(hr - 1) * td.size.x, 0f, iz / (float)(hr - 1) * td.size.z);
        var r = w - O; float s = Vector3.Dot(r, D), l = Mathf.Abs(Vector3.Dot(r, R));
        if (s < S0 - 20f || s > S1 + 20f || l > 26f) continue;
        // Под дорогой и её обочинами рельеф не трогаем
        Vector3 rc0, rd0; float rhw;
        float roadGap = 1e9f;
        if (roads.Nearest(w, 40f, out rc0, out rd0, out rhw)) { var g0 = w - rc0; g0.y = 0f; roadGap = g0.magnitude - rhw; }
        if (roadGap < 5f) continue;
        float target = (at(s).y - 0.5f) / td.size.y;
        float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 26f, l));
        k *= Mathf.InverseLerp(S0 - 20f, S0, s) * (1f - Mathf.InverseLerp(S1, S1 + 20f, s));
        k *= Mathf.InverseLerp(5f, 14f, roadGap);
        hm[iz, ix] = Mathf.Lerp(hm[iz, ix], target, k);
    }
    td.SetHeights(0, 0, hm);
}
var trees = new System.Collections.Generic.List<TreeInstance>(td.treeInstances); int treesBefore = trees.Count;
trees.RemoveAll(ti => { var r = Vector3.Scale(ti.position, td.size) - O; float s = Vector3.Dot(r, D);
    return s > S0 - 10f && s < S1 + 10f && Mathf.Abs(Vector3.Dot(r, R)) < 14f; });
td.treeInstances = trees.ToArray();
int dres = td.detailResolution; int grassCleared = 0;
for (int layer = 0; layer < td.detailPrototypes.Length; layer++) {
    var det = td.GetDetailLayer(0, 0, dres, dres, layer);
    for (int iz = 0; iz < dres; iz++) for (int ix = 0; ix < dres; ix++) {
        if (det[iz, ix] == 0) continue;
        var r = new Vector3((ix + 0.5f) / dres * td.size.x, 0f, (iz + 0.5f) / dres * td.size.z) - O;
        float s = Vector3.Dot(r, D);
        if (s > S0 - 10f && s < S1 + 10f && Mathf.Abs(Vector3.Dot(r, R)) < 8f) { det[iz, ix] = 0; grassCleared++; }
    }
    td.SetDetailLayer(0, 0, layer, det);
}
log.Append("trees removed ").Append(treesBefore - trees.Count).Append(", grass cells ").Append(grassCleared).Append("; ");

// --- 5. Путь и контактная сеть ---
var env = GameObject.Find("Environment").transform;
var old = env.Find("Railway"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var railway = new GameObject("Railway").transform; railway.SetParent(env, false);
var trackRoot = new GameObject("Track").transform; trackRoot.SetParent(railway, false);
var flags = (StaticEditorFlags)~0 & ~StaticEditorFlags.NavigationStatic;
System.Func<GameObject, Transform, Vector3, Quaternion, string, GameObject> place = (prefab, parent, p, q, name) => {
    var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    g.name = name; g.transform.SetPositionAndRotation(p, q);
    return g;
};
// Наклон звена: вдоль линии; на переезде — по плоскости асфальта
System.Func<float, float, Quaternion> pieceRot = (s, half) => {
    Vector3 f = at(s + half) - at(s - half);
    Vector3 up = Vector3.up;
    if (nearCross(s) < 16f) {
        foreach (var h in Physics.RaycastAll(at(s) + Vector3.up * 50f, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore))
            if (h.collider.name == "Asphalt") { up = h.normal; break; }
    }
    return Quaternion.LookRotation(Vector3.ProjectOnPlane(f, up), up);
};
int pieces = 0;
for (float s = S0 + 12f; s < S1; s += 24f, pieces++) {
    var q = pieceRot(s, 12f);
    place(railTrack, trackRoot, at(s), q, "RailTrack");
    if (nearCross(s) < 16f) continue;
    var bed = place(models["RailBed"], trackRoot, at(s), q, "RailBed");
    var mf = bed.GetComponentInChildren<MeshFilter>();
    var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh;
}
// На переездах балласта нет: шпалы под асфальтом
var cat = new GameObject("Catenary").transform; cat.SetParent(railway, false);
Vector3 prevContact = Vector3.zero, prevMessenger = Vector3.zero; bool hasPrev = false; int masts = 0;
System.Action<Vector3, Vector3, float> wire = (a, b, radius) => {
    var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
    g.name = "Wire"; g.transform.SetParent(cat, false);
    g.transform.position = (a + b) * 0.5f;
    g.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
    g.transform.localScale = new Vector3(radius * 2f, (b - a).magnitude * 0.5f, radius * 2f);
    var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = wireMat;
    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
};
for (float s = S0 + 30f; s < S1; s += 60f) {
    if (nearCross(s) < 18f) s += 24f;
    var axis = at(s);
    var mast = place(models["CatenaryMast"], cat, axis + R * 6.2f, Quaternion.LookRotation(D), "CatenaryMast");
    var cap = mast.AddComponent<CapsuleCollider>(); cap.center = new Vector3(0f, 9f, 0f); cap.height = 19f; cap.radius = 0.45f;
    masts++;
    Vector3 contact = axis + Vector3.up * 13.08f, messenger = axis + Vector3.up * 15.9f;
    if (hasPrev) { wire(prevContact, contact, 0.03f); wire(prevMessenger, messenger, 0.035f); }
    prevContact = contact; prevMessenger = messenger; hasPrev = true;
}
foreach (var t in railway.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);

// --- 6. Переезды ---
var output = new SerializedObject(manager.GetComponent<RacingProject.Props.PowerLines>()).FindProperty("output").objectReferenceValue;
var bellClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Content/Sound/Generated/Train/Crossing_Bell.wav");
var crossings = new System.Collections.Generic.List<RacingProject.Props.RailCrossing>();
for (int ci = 0; ci < CROSS.Length; ci++) {
    float sc = CROSS[ci];
    var c = at(sc);
    Vector3 center, roadDir; float hw;
    roads.Nearest(c, 30f, out center, out roadDir, out hw);
    roadDir.y = 0f; roadDir.Normalize();
    var root = new GameObject("Crossing_" + ci); root.transform.SetParent(railway, false); root.transform.position = c;
    // Две половины настила в одной плоскости, иначе на стыке щель
    var deckRot = pieceRot(sc, 16f);
    foreach (float off in new[] { -8f, 8f }) place(models["CrossingDeck"], root.transform, c + deckRot * Vector3.forward * off, deckRot, "CrossingDeck");
    var arms = new System.Collections.Generic.List<Transform>();
    var lamps = new System.Collections.Generic.List<Renderer>();
    var lights = new System.Collections.Generic.List<Light>();
    // Подходы с обеих сторон: шлагбаум перекрывает правую половину дороги, светофор смотрит на водителя
    foreach (int side in new[] { -1, 1 }) {
        Vector3 along = roadDir * side;                      // куда едет водитель, подъезжающий с этой стороны
        Vector3 right = Vector3.Cross(Vector3.up, along);
        float sin = Mathf.Abs(Vector3.Cross(along, D).y);
        Vector3 near = c - along * (11f / Mathf.Max(0.3f, sin));
        Vector3 postPos = near + right * (hw + 1.6f); postPos.y = terrain.SampleHeight(postPos);
        var asp = asphalt(postPos - right * 2f); if (!float.IsNaN(asp)) postPos.y = Mathf.Max(postPos.y, asp - 0.1f);
        var q = Quaternion.LookRotation(Vector3.Cross(right, Vector3.up), Vector3.up);
        var post = place(models["CrossingBarrier"], root.transform, postPos, q, "CrossingBarrier");
        var bc = post.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 1.25f, 0f); bc.size = new Vector3(1.2f, 2.5f, 1.2f);
        // Стрела лежит поперёк дороги (её −X — к оси дороги); RailCrossing поднимает её на старте
        var arm = place(models["CrossingArm"], post.transform, postPos + Vector3.up * 2.05f, q, "CrossingArm");
        arms.Add(arm.transform);
        Vector3 sigPos = near + along * 2.5f + right * (hw + 2.8f); sigPos.y = terrain.SampleHeight(sigPos);
        if (!float.IsNaN(asp)) sigPos.y = Mathf.Max(sigPos.y, asp - 0.1f);
        var sq = Quaternion.LookRotation(-along, Vector3.up);
        var signal = place(models["CrossingSignal"], root.transform, sigPos, sq, "CrossingSignal");
        var cc = signal.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0f, 3.4f, 0f); cc.height = 6.8f; cc.radius = 0.3f;
        foreach (float lx in new[] { -0.72f, 0.72f }) {
            var lamp = place(models["CrossingLamp"], signal.transform, signal.transform.TransformPoint(new Vector3(lx, 4.8f, 0.62f)), sq, "CrossingLamp");
            var lr = lamp.GetComponentInChildren<MeshRenderer>(); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lamps.Add(lr);
            var lg = new GameObject("LampLight"); lg.transform.SetParent(lamp.transform, false); lg.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            var light = lg.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, 0.15f, 0.08f);
            light.range = 9f; light.intensity = 4f; light.shadows = LightShadows.None; light.enabled = false;
            lights.Add(light);
        }
    }
    var bell = new GameObject("Bell").AddComponent<AudioSource>();
    bell.transform.SetParent(root.transform, false); bell.transform.localPosition = Vector3.up * 4f;
    bell.clip = bellClip; bell.loop = true; bell.playOnAwake = false; bell.spatialBlend = 1f;
    bell.rolloffMode = AudioRolloffMode.Linear; bell.minDistance = 8f; bell.maxDistance = 140f; bell.dopplerLevel = 0f; bell.volume = 0.8f;
    bell.outputAudioMixerGroup = (UnityEngine.Audio.AudioMixerGroup)output;
    var rc = root.AddComponent<RacingProject.Props.RailCrossing>();
    var so = new SerializedObject(rc);
    so.FindProperty("trackDistance").floatValue = sc - S0;
    var pa = so.FindProperty("arms"); pa.arraySize = arms.Count; for (int i = 0; i < arms.Count; i++) pa.GetArrayElementAtIndex(i).objectReferenceValue = arms[i];
    // Чётные и нечётные фонари мигают попеременно: в каждом светофоре один чётный, другой нечётный
    var pl = so.FindProperty("lamps"); pl.arraySize = lamps.Count; for (int i = 0; i < lamps.Count; i++) pl.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
    var pg = so.FindProperty("lights"); pg.arraySize = lights.Count; for (int i = 0; i < lights.Count; i++) pg.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
    so.FindProperty("lampOn").objectReferenceValue = lampOn;
    so.FindProperty("lampOff").objectReferenceValue = lampOff;
    so.FindProperty("bell").objectReferenceValue = bell;
    so.ApplyModifiedPropertiesWithoutUndo();
    foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    foreach (var a in arms) foreach (var t in a.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    foreach (var l in lamps) GameObjectUtility.SetStaticEditorFlags(l.gameObject, 0);
    crossings.Add(rc);
}

// --- 7. Поезд ---
var trainRoot = new GameObject("Train").transform; trainRoot.SetParent(railway, false);
string[] consist = { "ED4M_Head", "ED4M_Motor", "ED4M_Trailer", "ED4M_Head" };
var cars = new System.Collections.Generic.List<Rigidbody>();
for (int i = 0; i < consist.Length; i++) {
    var car = new GameObject("Car" + i + "_" + consist[i]);
    car.transform.SetParent(trainRoot, false);
    var model = place(models[consist[i]], car.transform, car.transform.position, car.transform.rotation, consist[i]);
    var b = model.GetComponentInChildren<MeshRenderer>().bounds;
    var box = car.AddComponent<BoxCollider>(); box.center = car.transform.InverseTransformPoint(b.center); box.size = b.size;
    var rb = car.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.interpolation = RigidbodyInterpolation.Interpolate;
    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    car.SetActive(false);
    cars.Add(rb);
}
if (train == null) train = manager.gameObject.AddComponent<RacingProject.Props.ElectricTrain>();
var tso = new SerializedObject(train);
var pt = tso.FindProperty("track"); pt.arraySize = count;
for (int i = 0; i < count; i++) pt.GetArrayElementAtIndex(i).vector3Value = at(S0 + i * STEP);
var pc = tso.FindProperty("cars"); pc.arraySize = cars.Count; for (int i = 0; i < cars.Count; i++) pc.GetArrayElementAtIndex(i).objectReferenceValue = cars[i];
var px = tso.FindProperty("crossings"); px.arraySize = crossings.Count; for (int i = 0; i < crossings.Count; i++) px.GetArrayElementAtIndex(i).objectReferenceValue = crossings[i];
tso.FindProperty("output").objectReferenceValue = output;
tso.FindProperty("horn").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Content/Sound/Generated/Train/Train_Horn.wav");
tso.FindProperty("passLoop").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Content/Sound/Generated/Train/Train_Pass.wav");
tso.ApplyModifiedPropertiesWithoutUndo();

// Что стоит в габарите поезда (вагон — 4 м от оси): сообщить, сцена не правится
var rot = Quaternion.LookRotation(D); var blockers = new System.Collections.Generic.Dictionary<Collider, string>();
for (float s = S0; s <= S1; s += 6f)
    foreach (var col in Physics.OverlapBox(at(s) + Vector3.up * 5f, new Vector3(4.6f, 4.2f, 3f), rot, ~0, QueryTriggerInteraction.Ignore)) {
        if (col is TerrainCollider || col.transform.IsChildOf(railway) || col.name == "Asphalt" || col.name.StartsWith("Shoulder") || blockers.ContainsKey(col)) continue;
        blockers[col] = (col.transform.parent != null ? col.transform.parent.name + "/" : "") + col.name + " @" + Mathf.RoundToInt(s);
    }
log.Append("pieces ").Append(pieces).Append(", masts ").Append(masts).Append(", crossings ").Append(crossings.Count)
   .Append(", reuse ").Append(reuse).Append("; in clearance: ").Append(string.Join(", ", new System.Collections.Generic.List<string>(blockers.Values).ToArray()));
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(railway.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(railway.gameObject.scene);
return log.ToString();
