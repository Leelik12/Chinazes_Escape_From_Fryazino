// Ночной свет города (SovietCity должна быть открыта):
// 1) окна панелек — копии материалов rus_build_* с картами свечения Assets/Content/Buildings/NightWindows
//    (python Tools/Textures/generate_window_emission.py), по три варианта на текстуру; вариант дому выбирается
//    по его позиции, поэтому соседние дома светятся по-разному; повторный запуск переназначает те же варианты;
// 2) уличные фонари — General/StreetLights (StreetLights): все фонари Environment/StreetLamps, плафоны, конусы
//    света, мигающие и разбитые, искры при сбитом фонаре;
// 3) туман по районам — зоны FogZones на General/Weather;
// 4) пробы отражений Environment/ReflectionProbes над районами и озером (запекаются здесь же).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var log = new System.Text.StringBuilder();
var env = GameObject.Find("Environment").transform;
var general = GameObject.Find("General").transform;
System.Func<Transform, string, Transform> child = (parent, name) => {
    var t = parent.Find(name);
    if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
    return t;
};

// --- 1. Окна ---
string winDir = "Assets/Content/Buildings/NightWindows";
string matDir = winDir + "/Materials";
System.IO.Directory.CreateDirectory(matDir);
AssetDatabase.Refresh();
var variants = new System.Collections.Generic.Dictionary<string, Material[]>();
System.Func<Material, Material[]> nightOf = source => {
    Material[] list;
    if (variants.TryGetValue(source.name, out list)) return list;
    var tex = source.GetTexture("_BaseMap");
    list = null;
    if (tex != null) {
        string texName = tex.name;
        var made = new System.Collections.Generic.List<Material>();
        for (int k = 0; k < 3; k++) {
            string ePath = winDir + "/" + texName + "_win" + k + ".png";
            var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(ePath);
            if (emission == null) break;
            string mPath = matDir + "/" + source.name + "_night" + k + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mPath);
            if (m == null) { m = new Material(source); AssetDatabase.CreateAsset(m, mPath); }
            else m.CopyPropertiesFromMaterial(source);
            m.name = source.name + "_night" + k;
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", emission);
            m.SetColor("_EmissionColor", Color.white * 3.5f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(m);
            made.Add(m);
        }
        if (made.Count > 0) list = made.ToArray();
    }
    variants[source.name] = list;
    return list;
};
// Исходный материал по имени варианта (для повторного запуска)
System.Func<Material, Material> original = m => {
    int i = m.name.IndexOf("_night");
    if (i < 0) return m;
    string baseName = m.name.Substring(0, i);
    foreach (string guid in AssetDatabase.FindAssets(baseName + " t:Material", new[] { "Assets/Content/Buildings/russian_buildings" })) {
        var found = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
        if (found != null && found.name == baseName) return found;
    }
    return m;
};
int windows = 0;
foreach (var r in env.GetComponentsInChildren<MeshRenderer>(true)) {
    var mats = r.sharedMaterials;
    bool changed = false;
    // Вариант — по дому (прямой потомок группы района), чтобы все его части и LOD светились одинаково
    Transform house = r.transform;
    while (house.parent != null && house.parent != env && house.parent.parent != null && house.parent.parent != env) house = house.parent;
    Vector3 hp = house.position;
    int pick = Mathf.Abs(Mathf.FloorToInt(hp.x * 0.37f) * 73856093 ^ Mathf.FloorToInt(hp.z * 0.37f) * 19349663) % 3;
    for (int i = 0; i < mats.Length; i++) {
        if (mats[i] == null || !mats[i].name.StartsWith("rus_build_") || mats[i].name.StartsWith("rus_build_dec")) continue;
        var src = original(mats[i]);
        var list = nightOf(src);
        if (list == null) continue;
        mats[i] = list[pick % list.Length];
        changed = true;
    }
    if (changed) { r.sharedMaterials = mats; windows++; }
}
log.Append("window renderers ").Append(windows).Append("; ");

// --- 2. Фонари ---
var lightsT = child(general, "StreetLights");
var street = lightsT.GetComponent<RacingProject.Effects.StreetLights>();
if (street == null) street = lightsT.gameObject.AddComponent<RacingProject.Effects.StreetLights>();
var lamps = new System.Collections.Generic.List<Transform>();
foreach (var lp in UnityEngine.Object.FindObjectsByType<RacingProject.Props.LooseProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
    if (lp.name.StartsWith("StreetLamp")) lamps.Add(lp.transform);
lamps.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
// Искры сбитого фонаря
var sparksT = child(lightsT, "Sparks");
var sparks = sparksT.GetComponent<ParticleSystem>();
if (sparks == null) sparks = sparksT.gameObject.AddComponent<ParticleSystem>();
sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
var main = sparks.main;
main.simulationSpace = ParticleSystemSimulationSpace.World;
main.playOnAwake = false; main.loop = false; main.duration = 1f;
main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 11f);
main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
main.startColor = new Color(1f, 0.7f, 0.3f, 1f);
main.gravityModifier = 1.6f;
main.maxParticles = 300;
var em = sparks.emission; em.rateOverTime = 0f;
var shape = sparks.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.3f;
var col = sparks.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.bounce = 0.3f; col.dampen = 0.4f; col.lifetimeLoss = 0.2f;
var spr = sparksT.GetComponent<ParticleSystemRenderer>();
spr.renderMode = ParticleSystemRenderMode.Stretch; spr.velocityScale = 0.06f; spr.lengthScale = 1f;
spr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/Prefabs/Weapon Effects/Materials/MetalSparks.mat");
spr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
var sso = new SerializedObject(street);
var arr = sso.FindProperty("lamps");
arr.arraySize = lamps.Count;
for (int i = 0; i < lamps.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
sso.FindProperty("glowMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_GlowNoFog.mat");
sso.FindProperty("beamMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_Beam.mat");
sso.FindProperty("sparks").objectReferenceValue = sparks;
sso.FindProperty("intensity").floatValue = 450f;
sso.ApplyModifiedPropertiesWithoutUndo();
log.Append("lamps ").Append(lamps.Count).Append("; ");

// --- 3. Туман по районам ---
var weatherGo = GameObject.Find("General/Weather");
var zones = weatherGo.GetComponent<RacingProject.Effects.FogZones>();
if (zones == null) zones = weatherGo.AddComponent<RacingProject.Effects.FogZones>();
var zso = new SerializedObject(zones);
var zarr = zso.FindProperty("zones");
Color fog = RenderSettings.fogColor;
// имя, центр, радиус, спад, плотность, цвет: сырость над озером, дым промзоны и City2, чистое поле на севере
var specs = new object[][] {
    new object[] { "Lake", new Vector3(1164f, 0f, 640f), 260f, 160f, 1.35f, Color.Lerp(fog, new Color(0.16f, 0.2f, 0.24f), 0.5f) },
    new object[] { "Industrial", new Vector3(1076f, 0f, 225f), 110f, 120f, 1.5f, Color.Lerp(fog, new Color(0.24f, 0.19f, 0.15f), 0.55f) },
    new object[] { "Fields", new Vector3(1150f, 0f, 1550f), 420f, 250f, 0.75f, fog },
    new object[] { "City2", new Vector3(1662f, 0f, 763f), 230f, 150f, 1.15f, Color.Lerp(fog, new Color(0.22f, 0.17f, 0.13f), 0.35f) },
};
zarr.arraySize = specs.Length;
for (int i = 0; i < specs.Length; i++) {
    var e = zarr.GetArrayElementAtIndex(i);
    e.FindPropertyRelative("name").stringValue = (string)specs[i][0];
    e.FindPropertyRelative("center").vector3Value = (Vector3)specs[i][1];
    e.FindPropertyRelative("radius").floatValue = (float)specs[i][2];
    e.FindPropertyRelative("falloff").floatValue = (float)specs[i][3];
    e.FindPropertyRelative("density").floatValue = (float)specs[i][4];
    e.FindPropertyRelative("color").colorValue = (Color)specs[i][5];
}
zso.ApplyModifiedPropertiesWithoutUndo();

// --- 4. Пробы отражений ---
var probes = child(env, "ReflectionProbes");
var terrain = Terrain.activeTerrain;
// имя, центр (x, z), размер области (x, y, z), высота пробы над землёй
var probeSpecs = new object[][] {
    new object[] { "City1", new Vector2(688f, 411f), new Vector3(260f, 80f, 600f), 6f },
    new object[] { "City2", new Vector2(1662f, 763f), new Vector3(420f, 80f, 600f), 6f },
    new object[] { "Yards", new Vector2(515f, 430f), new Vector3(160f, 80f, 460f), 5f },
    new object[] { "Industrial", new Vector2(1076f, 225f), new Vector3(220f, 80f, 200f), 6f },
    new object[] { "PrivateSector", new Vector2(1247f, 1438f), new Vector3(520f, 80f, 360f), 4f },
    new object[] { "Lake", new Vector2(1164f, 640f), new Vector3(620f, 120f, 1000f), 2f },
};
string probeDir = "Assets/Scenes/SovietCity";
int baked = 0;
foreach (var s in probeSpecs) {
    var t = child(probes, "Probe_" + (string)s[0]);
    Vector2 c = (Vector2)s[1];
    float ground = (string)s[0] == "Lake" ? 10.2f : terrain.SampleHeight(new Vector3(c.x, 0f, c.y)) + terrain.transform.position.y;
    t.position = new Vector3(c.x, ground + (float)s[3], c.y);
    var probe = t.GetComponent<ReflectionProbe>();
    if (probe == null) probe = t.gameObject.AddComponent<ReflectionProbe>();
    probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
    probe.size = (Vector3)s[2];
    probe.center = Vector3.zero;
    probe.resolution = 256;
    probe.hdr = true;
    probe.boxProjection = false;
    probe.blendDistance = 20f;
    probe.importance = 1;
    probe.farClipPlane = 600f;
    string path = probeDir + "/ReflectionProbe-" + (string)s[0] + ".exr";
    if (Lightmapping.BakeReflectionProbe(probe, path)) baked++;
}
log.Append("probes baked ").Append(baked).Append("; ");

AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(env.gameObject.scene);
return log.ToString();
