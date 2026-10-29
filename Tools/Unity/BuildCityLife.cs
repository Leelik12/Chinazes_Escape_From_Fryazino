// Жизнь города (SovietCity): вороны на опорах ЛЭП (Assets/Scripts/Effects/CityBirds.cs), верёвки с бельём на задних
// дворах частного сектора, красные флаги на крышах девятиэтажек (WindCloth) и бездомные собаки у домов
// и подъездов (StrayDogs, модели StrayDog_Body/Leg из Tools/Blender/level_props, импорт — Tools/Unity/BuildLevelProps.cs). Звуки — python Tools/Audio/
// generate_city_life_sounds.py. Всё в Environment/CityLife; бельё и флаги без коллайдеров (машина их не замечает).
// Места для верёвок проверяются на свободу от домов, деревьев и заборов. Случайность с постоянным зерном —
// повторный запуск пересобирает CityLife так же.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
string FX = "Assets/Content/Effects/";
string SND = "Assets/Content/Sound/Generated/CityLife/";
AssetDatabase.Refresh();
var lit = Shader.Find("Universal Render Pipeline/Lit");
System.Func<string, Color, float, Material> mat = (name, color, smooth) => {
    var m = AssetDatabase.LoadAssetAtPath<Material>(FX + name + ".mat");
    if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, FX + name + ".mat"); }
    m.shader = lit; m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f);
    m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
};
var crowMat = mat("Crow", new Color(0.03f, 0.03f, 0.035f), 0.35f);
crowMat.SetFloat("_Cull", 0f);
var clothMat = mat("Cloth", Color.white, 0.08f);
var woodMat = mat("LaundryPost", new Color(0.32f, 0.24f, 0.16f), 0.1f);
var ropeMat = mat("LaundryRope", new Color(0.55f, 0.52f, 0.45f), 0.1f);
AssetDatabase.SaveAssets();

var env = GameObject.Find("Environment").transform;
var old = env.Find("CityLife"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var root = new GameObject("CityLife").transform; root.SetParent(env, false);
var rnd = new System.Random(4242);
System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);

// --- Вороны ---
var birds = root.gameObject.AddComponent<RacingProject.Effects.CityBirds>();
var bso = new SerializedObject(birds);
bso.FindProperty("material").objectReferenceValue = crowMat;
var caws = bso.FindProperty("caws"); caws.arraySize = 3;
for (int i = 0; i < 3; i++) caws.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SND + "Crow_Caw_" + i + ".wav");
bso.FindProperty("flockClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SND + "Crow_Flock.wav");
var ambient = UnityEngine.Object.FindFirstObjectByType<RacingProject.Ambience.AmbientSoundscape>();
if (ambient != null) bso.FindProperty("output").objectReferenceValue = new SerializedObject(ambient).FindProperty("output").objectReferenceValue;
bso.ApplyModifiedPropertiesWithoutUndo();

System.Func<Transform, string, Vector3, Vector3, Material, Transform> box = (parent, name, pos, scale, m) => {
    var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
    UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
    g.name = name; g.transform.SetParent(parent, false); g.transform.position = pos; g.transform.localScale = scale;
    g.GetComponent<Renderer>().sharedMaterial = m; return g.transform;
};
System.Action<GameObject, RacingProject.Effects.WindCloth.Edge, Vector2, Color, float> cloth = (g, edge, size, color, amp) => {
    var c = g.AddComponent<RacingProject.Effects.WindCloth>();
    var so = new SerializedObject(c);
    so.FindProperty("pinned").enumValueIndex = (int)edge;
    so.FindProperty("size").vector2Value = size;
    so.FindProperty("material").objectReferenceValue = clothMat;
    so.FindProperty("color").colorValue = color;
    so.FindProperty("amplitude").floatValue = amp;
    so.ApplyModifiedPropertiesWithoutUndo();
};
System.Func<Vector3, float> ground = p => {
    RaycastHit h; return Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out h, 80f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : float.NaN;
};
Physics.SyncTransforms();

// --- Бельё ---
Color[] linen = { new Color(0.92f, 0.92f, 0.88f), new Color(0.7f, 0.78f, 0.9f), new Color(0.85f, 0.55f, 0.5f), new Color(0.6f, 0.72f, 0.55f),
                  new Color(0.95f, 0.85f, 0.55f), new Color(0.55f, 0.5f, 0.62f), new Color(0.8f, 0.8f, 0.82f) };
var laundry = new GameObject("Laundry").transform; laundry.SetParent(root, false);
int lines = 0;
foreach (Transform plot in GameObject.Find("PrivateSector").transform) {
    var yard = plot.Find("Yard"); if (yard == null) continue;
    Transform house = null; foreach (Transform c in yard) if (c.name.StartsWith("House")) house = c;
    if (house == null) continue;
    for (int attempt = 0; attempt < 30; attempt++) {
        float ang = R(0f, 360f);
        Vector3 mid = house.position + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * R(11f, 20f);
        Vector3 dir = Quaternion.Euler(0f, R(0f, 180f), 0f) * Vector3.forward;
        float len = R(5f, 7.5f);
        Vector3 a = mid - dir * len * 0.5f, b = mid + dir * len * 0.5f;
        float ga = ground(a), gb = ground(b);
        if (float.IsNaN(ga) || float.IsNaN(gb) || Mathf.Abs(ga - gb) > 0.8f) continue;
        a.y = ga; b.y = gb;
        // Свободно ли: капсула вдоль верёвки на высоте человека, террейн не считается
        bool blocked = false;
        foreach (var col in Physics.OverlapCapsule(a + Vector3.up * 1.6f - dir * 0.5f, b + Vector3.up * 1.6f + dir * 0.5f, 1.1f, ~0, QueryTriggerInteraction.Ignore))
            if (!(col is TerrainCollider)) { blocked = true; break; }
        if (blocked) continue;
        var line = new GameObject("LaundryLine").transform; line.SetParent(laundry, false);
        float top = 2.1f;
        box(line, "Post", a + Vector3.up * top * 0.5f, new Vector3(0.09f, top, 0.09f), woodMat);
        box(line, "Post", b + Vector3.up * top * 0.5f, new Vector3(0.09f, top, 0.09f), woodMat);
        var rope = line.gameObject.AddComponent<LineRenderer>();
        rope.useWorldSpace = true; rope.widthMultiplier = 0.025f; rope.sharedMaterial = ropeMat;
        rope.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        int seg = 12; rope.positionCount = seg + 1;
        Vector3 ta = a + Vector3.up * (top - 0.05f), tb = b + Vector3.up * (top - 0.05f);
        System.Func<float, Vector3> at = t => Vector3.Lerp(ta, tb, t) + Vector3.down * 0.18f * 4f * t * (1f - t);
        for (int i = 0; i <= seg; i++) rope.SetPosition(i, at(i / (float)seg));
        // Вещи вдоль верёвки с промежутками
        float u = R(0.08f, 0.15f);
        while (u < 0.9f) {
            float w = R(0.45f, 1.1f), hgt = R(0.5f, 1.0f);
            float du = w / len;
            if (u + du > 0.95f) break;
            var piece = new GameObject("Cloth"); piece.transform.SetParent(line, false);
            piece.transform.position = at(u + du * 0.5f);
            piece.transform.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));
            cloth(piece, RacingProject.Effects.WindCloth.Edge.Top, new Vector2(w, hgt), linen[rnd.Next(linen.Length)], 0.08f);
            u += du + R(0.04f, 0.12f);
        }
        lines++;
        break;
    }
}

// --- Флаги на девятиэтажках ---
var flags = new GameObject("Flags").transform; flags.SetParent(root, false);
Color[] reds = { new Color(0.72f, 0.07f, 0.06f), new Color(0.6f, 0.12f, 0.1f), new Color(0.55f, 0.2f, 0.17f) };
int flagCount = 0;
foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.InstanceID)) {
    if (flagCount >= 8 || !t.name.StartsWith("rus_build_9et")) continue;
    if (rnd.NextDouble() > 0.55) continue;
    var parts = t.GetComponentsInChildren<Renderer>();
    if (parts.Length == 0) continue;
    Bounds bd = parts[0].bounds;
    foreach (var part in parts) bd.Encapsulate(part.bounds);
    // Верх крыши — луч сверху у края здания
    Vector3 corner = new Vector3(Mathf.Lerp(bd.min.x, bd.max.x, rnd.NextDouble() < 0.5 ? 0.12f : 0.88f), bd.max.y + 5f, Mathf.Lerp(bd.min.z, bd.max.z, rnd.NextDouble() < 0.5 ? 0.15f : 0.85f));
    RaycastHit hit;
    if (!Physics.Raycast(corner, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore)) continue;
    var flag = new GameObject("Flag").transform; flag.SetParent(flags, false);
    float poleH = 4.5f;
    box(flag, "Pole", hit.point + Vector3.up * poleH * 0.5f, new Vector3(0.07f, poleH, 0.07f), ropeMat);
    var f = new GameObject("Cloth"); f.transform.SetParent(flag, false);
    f.transform.position = hit.point + Vector3.up * (poleH - 0.05f);
    f.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
    cloth(f, RacingProject.Effects.WindCloth.Edge.Left, new Vector2(2.2f, 1.4f), reds[rnd.Next(reds.Length)], 0.22f);
    flagCount++;
}

// --- Бездомные собаки: точки у домов частного сектора и у девятиэтажек ---
var homes = new System.Collections.Generic.List<Vector3>();
foreach (Transform plot in GameObject.Find("PrivateSector").transform) {
    if (homes.Count >= 9) break;
    if (plot.Find("Yard") == null || rnd.NextDouble() > 0.3) continue;
    // Улица перед участком: участок уходит от неё по +z (так его заполняет BuildLevelDressing)
    var p = plot.position - plot.forward * R(2f, 6f) + plot.right * R(-8f, 8f);
    if (!float.IsNaN(ground(p))) homes.Add(new Vector3(p.x, ground(p), p.z));
}
int blocks = 0;
foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.InstanceID)) {
    if (blocks >= 6 || !t.name.StartsWith("rus_build_9et") || rnd.NextDouble() > 0.35) continue;
    var parts = t.GetComponentsInChildren<Renderer>();
    if (parts.Length == 0) continue;
    Bounds bd = parts[0].bounds;
    foreach (var part in parts) bd.Encapsulate(part.bounds);
    // У подъезда: середина длинной стороны, на 6 м от стены
    var side = bd.size.x > bd.size.z ? new Vector3(0f, 0f, (rnd.NextDouble() < 0.5 ? -1f : 1f) * (bd.extents.z + 6f)) : new Vector3((rnd.NextDouble() < 0.5 ? -1f : 1f) * (bd.extents.x + 6f), 0f, 0f);
    var p = new Vector3(bd.center.x, 0f, bd.center.z) + side;
    float gy = ground(p);
    if (float.IsNaN(gy)) continue;
    homes.Add(new Vector3(p.x, gy, p.z)); blocks++;
}
var dogs = root.gameObject.AddComponent<RacingProject.Effects.StrayDogs>();
var dso = new SerializedObject(dogs);
string LPM = "Assets/Models/LevelProps/";
dso.FindProperty("bodyMesh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(LPM + "StrayDog_Body.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
dso.FindProperty("legMesh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(LPM + "StrayDog_Leg.fbx").GetComponentInChildren<MeshFilter>().sharedMesh;
dso.FindProperty("material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/LevelProps/Materials/LP_Dirt.mat");
dso.FindProperty("darkMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/LevelProps/Materials/LP_Rubber.mat");
var barks = dso.FindProperty("barks"); barks.arraySize = 3;
for (int i = 0; i < 3; i++) barks.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SND + "Dog_Bark_" + i + ".wav");
dso.FindProperty("yelp").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SND + "Dog_Yelp.wav");
if (ambient != null) dso.FindProperty("output").objectReferenceValue = new SerializedObject(ambient).FindProperty("output").objectReferenceValue;
var hp = dso.FindProperty("homes"); hp.arraySize = homes.Count;
for (int i = 0; i < homes.Count; i++) hp.GetArrayElementAtIndex(i).vector3Value = homes[i];
dso.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(root.gameObject.scene);
return "city life: laundry lines " + lines + ", flags " + flagCount + ", dogs " + homes.Count;
