// Поломки машины игрока, РПГ на турели стрелка, ящики выстрелов и остовы врагов.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// 1) импорт звуков Assets/Content/Sound/Generated/Damage (Tools/Audio/generate_damage_sounds.py) и запись TireBurst в SoundLibrary;
// 2) материал трещин стекла (GlassCracks.png из Tools/Textures/generate_glass_cracks.py);
// 3) VolgaCar: «Windshield» по треугольникам лобового стекла, пар из радиатора, CarDamageState, WheelForceFeedback;
// 4) RoofGun: RPG-7 (та же модель, что у «буханки»), точка выстрела, реактивная струя, RocketLauncher;
// 5) префаб ящика выстрелов (Tools/Blender/level_props, RpgCrate), ящики у канистр нитро, шаблон выпавшего ящика, RocketCrates;
// 6) надписи РПГ и поломок на приборах, настройки остова у префабов врагов, задержка рестарта под камеру смерти.
// Повторный запуск пересоздаёт свои объекты.
var log = new System.Text.StringBuilder();
string SND = "Assets/Content/Sound/Generated/Damage/";
AssetDatabase.Refresh();

// --- 1. Звуки ---
foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SND.TrimEnd('/') })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    var ai = (AudioImporter)AssetImporter.GetAtPath(path);
    var s = ai.defaultSampleSettings;
    bool loop = path.Contains("_Loop");
    s.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
    s.compressionFormat = AudioCompressionFormat.Vorbis;
    s.quality = 0.7f;
    ai.defaultSampleSettings = s;
    ai.forceToMono = true;
    ai.SaveAndReimport();
}
System.Func<string, AudioClip> clip = n => AssetDatabase.LoadAssetAtPath<AudioClip>(SND + n + ".wav");

var library = UnityEngine.Object.FindFirstObjectByType<RacingProject.Audio.SoundLibrary>(FindObjectsInactive.Include);
{
    var so = new SerializedObject(library);
    var entries = so.FindProperty("entries");
    int heavy = -1, burst = -1;
    int burstKind = (int)RacingProject.Audio.SoundLibrary.Sfx.TireBurst, heavyKind = (int)RacingProject.Audio.SoundLibrary.Sfx.ImpactHeavy;
    for (int i = 0; i < entries.arraySize; i++) {
        int kind = entries.GetArrayElementAtIndex(i).FindPropertyRelative("kind").enumValueIndex;
        if (kind == heavyKind) heavy = i;
        if (kind == burstKind) burst = i;
    }
    if (burst < 0) { entries.InsertArrayElementAtIndex(entries.arraySize); burst = entries.arraySize - 1; }
    var e = entries.GetArrayElementAtIndex(burst);
    e.FindPropertyRelative("kind").enumValueIndex = burstKind;
    var clips = e.FindPropertyRelative("clips");
    clips.arraySize = 2;
    clips.GetArrayElementAtIndex(0).objectReferenceValue = clip("Tire_Burst_0");
    clips.GetArrayElementAtIndex(1).objectReferenceValue = clip("Tire_Burst_1");
    e.FindPropertyRelative("volume").floatValue = 1f;
    e.FindPropertyRelative("minDistance").floatValue = 6f;
    e.FindPropertyRelative("maxDistance").floatValue = 150f;
    e.FindPropertyRelative("pitchJitter").floatValue = 0.08f;
    e.FindPropertyRelative("maxVoices").intValue = 2;
    e.FindPropertyRelative("minInterval").floatValue = 0.1f;
    if (heavy >= 0) e.FindPropertyRelative("output").objectReferenceValue = entries.GetArrayElementAtIndex(heavy).FindPropertyRelative("output").objectReferenceValue;
    so.ApplyModifiedProperties();
}
log.Append("sounds; ");

// --- 2. Материал трещин ---
string crackTex = "Assets/Content/Effects/GlassCracks.png";
{
    var ti = (TextureImporter)AssetImporter.GetAtPath(crackTex);
    ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 1024; ti.mipmapEnabled = true;
    ti.SaveAndReimport();
}
string crackPath = "Assets/Content/Effects/GlassCracks.mat";
var crackMat = AssetDatabase.LoadAssetAtPath<Material>(crackPath);
if (crackMat == null) { crackMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(crackMat, crackPath); }
crackMat.shader = Shader.Find("Universal Render Pipeline/Lit");
crackMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(crackTex));
crackMat.SetColor("_BaseColor", new Color(0.95f, 0.97f, 1f, 0.9f));
crackMat.SetFloat("_Surface", 1f); crackMat.SetFloat("_Blend", 0f); crackMat.SetFloat("_Cull", 0f);
crackMat.SetFloat("_Smoothness", 0.85f); crackMat.SetFloat("_Metallic", 0f);
// Обычное альфа-смешивание без сохранения блика: иначе прозрачная часть квада бликует тёмным квадратом
crackMat.SetFloat("_BlendModePreserveSpecular", 0f);
crackMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
crackMat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
crackMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
crackMat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
crackMat.SetFloat("_ZWrite", 0f);
crackMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
crackMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
crackMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 10;
crackMat.SetShaderPassEnabled("ShadowCaster", false);
EditorUtility.SetDirty(crackMat);
AssetDatabase.SaveAssets();

// --- 3. VolgaCar ---
var car = GameObject.Find("VolgaCar");
System.Func<Transform, string, Transform> deep = null;
deep = (r, n) => { if (r.name == n) return r; foreach (Transform c in r) { var f = deep(c, n); if (f != null) return f; } return null; };
System.Action<Transform, string> drop = (parent, n) => { var old = parent.Find(n); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject); };

// Лобовое стекло: треугольники стеклянного подмеша, смотрящие вперёд и вверх, в осях машины
var body = deep(car.transform, "TahoeBody");
var bodyMesh = body.GetComponent<MeshFilter>().sharedMesh;
var bodyMats = body.GetComponent<MeshRenderer>().sharedMaterials;
int glass = System.Array.FindIndex(bodyMats, m => m != null && m.name.Contains("glasswindows"));
var verts = bodyMesh.vertices;
var tris = bodyMesh.GetTriangles(glass);
var toCar = car.transform.worldToLocalMatrix * body.localToWorldMatrix;
Vector3 normalSum = Vector3.zero, centerSum = Vector3.zero; float area = 0f;
var picked = new System.Collections.Generic.List<Vector3>();
for (int i = 0; i < tris.Length; i += 3) {
    Vector3 a = toCar.MultiplyPoint3x4(verts[tris[i]]), b = toCar.MultiplyPoint3x4(verts[tris[i + 1]]), c = toCar.MultiplyPoint3x4(verts[tris[i + 2]]);
    Vector3 n = Vector3.Cross(b - a, c - a);
    float s = n.magnitude * 0.5f;
    if (s < 1e-6f) continue;
    n.Normalize();
    Vector3 m = (a + b + c) / 3f;
    // Без стёкол фар (ниже и впереди) и боковых окон
    if (n.z < 0.3f || n.y < 0.5f || Mathf.Abs(n.x) > 0.4f || m.y < 0.45f || m.z < 0.2f || m.z > 1.8f) continue;
    normalSum += n * s; centerSum += m * s; area += s;
    picked.Add(a); picked.Add(b); picked.Add(c);
}
Vector3 wsNormal = normalSum.normalized, wsCenter = centerSum / area;
Vector3 wsRight = Vector3.ProjectOnPlane(Vector3.right, wsNormal).normalized;
Vector3 wsUp = Vector3.Cross(wsNormal, wsRight).normalized;
float minX = 1e9f, maxX = -1e9f, minY = 1e9f, maxY = -1e9f;
foreach (var p in picked) {
    float x = Vector3.Dot(p - wsCenter, wsRight), y = Vector3.Dot(p - wsCenter, wsUp);
    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
}
drop(car.transform, "Windshield");
var windshield = new GameObject("Windshield").transform;
windshield.SetParent(car.transform, false);
windshield.localPosition = wsCenter + wsRight * (minX + maxX) * 0.5f + wsUp * (minY + maxY) * 0.5f + wsNormal * 0.012f;
windshield.localRotation = Quaternion.LookRotation(wsNormal, wsUp);
windshield.localScale = new Vector3(maxX - minX, maxY - minY, 1f);
log.Append("windshield ").Append(windshield.localPosition).Append(" size ").Append(windshield.localScale).Append(" tris ").Append(picked.Count / 3).Append("; ");

// Пар из радиатора: белая копия дыма у решётки
var effects = car.transform.Find("DamageEffects");
drop(car.transform, "RadiatorSteam");
var steamGo = UnityEngine.Object.Instantiate(effects.Find("Smoke").gameObject, car.transform);
steamGo.name = "RadiatorSteam";
steamGo.transform.localPosition = new Vector3(0f, 0.75f, 2.15f);
steamGo.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
var steam = steamGo.GetComponent<ParticleSystem>();
{
    var main = steam.main;
    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.95f, 0.96f, 0.97f, 0.55f), new Color(0.85f, 0.87f, 0.9f, 0.35f));
    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
    main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
    main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
    main.playOnAwake = true;
    main.loop = true;
    var em = steam.emission; em.rateOverTime = 0f;
    var shape = steam.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 18f; shape.radius = 0.35f;
    var col = steam.colorOverLifetime; col.enabled = true;
    var g = new Gradient();
    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
              new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.12f), new GradientAlphaKey(0f, 1f) });
    col.color = g;
}
var hissGo = new GameObject("SteamHiss");
drop(car.transform, "SteamHiss");
hissGo.transform.SetParent(car.transform, false);
hissGo.transform.localPosition = steamGo.transform.localPosition;
var engineAudio = car.transform.Find("EngineSound").GetComponent<AudioSource>();
var hiss = hissGo.AddComponent<AudioSource>();
hiss.clip = clip("Steam_Hiss_Loop"); hiss.loop = true; hiss.playOnAwake = false; hiss.volume = 0f;
hiss.spatialBlend = 1f; hiss.minDistance = 4f; hiss.maxDistance = 60f; hiss.rolloffMode = AudioRolloffMode.Linear; hiss.dopplerLevel = 0f;
hiss.outputAudioMixerGroup = engineAudio.outputAudioMixerGroup;

var controller = car.GetComponent<RacingProject.Car.CarControllerSample>();
var wheelList = new System.Collections.Generic.List<WheelCollider>();
{
    var axles = new SerializedObject(controller).FindProperty("axleInfos");
    for (int i = 0; i < axles.arraySize; i++) {
        var a = axles.GetArrayElementAtIndex(i);
        wheelList.Add((WheelCollider)a.FindPropertyRelative("leftWheel").objectReferenceValue);
        wheelList.Add((WheelCollider)a.FindPropertyRelative("rightWheel").objectReferenceValue);
    }
    wheelList.Sort((x, y) => -x.transform.localPosition.z.CompareTo(y.transform.localPosition.z));
}
var radio = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.RadioChatter>(FindObjectsInactive.Include);
var health = car.GetComponent<RacingProject.PlayerHealth>();
var sparkRenderer = car.transform.Find("ImpactSparks").GetComponent<ParticleSystemRenderer>();

var damage = car.GetComponent<RacingProject.Car.CarDamageState>() ?? car.AddComponent<RacingProject.Car.CarDamageState>();
{
    var so = new SerializedObject(damage);
    so.FindProperty("health").objectReferenceValue = health;
    so.FindProperty("controller").objectReferenceValue = controller;
    so.FindProperty("headlights").objectReferenceValue = car.GetComponentInChildren<RacingProject.Car.CarHeadlights>(true);
    so.FindProperty("wheelVisuals").objectReferenceValue = car.GetComponentInChildren<RacingProject.Car.CarWheelVisuals>(true);
    so.FindProperty("radio").objectReferenceValue = radio;
    var w = so.FindProperty("wheels"); w.arraySize = wheelList.Count;
    for (int i = 0; i < wheelList.Count; i++) w.GetArrayElementAtIndex(i).objectReferenceValue = wheelList[i];
    so.FindProperty("sparkMaterial").objectReferenceValue = sparkRenderer.sharedMaterial;
    so.FindProperty("rimScrape").objectReferenceValue = clip("Rim_Scrape_Loop");
    so.FindProperty("steam").objectReferenceValue = steam;
    so.FindProperty("steamHiss").objectReferenceValue = hiss;
    so.FindProperty("windshield").objectReferenceValue = windshield;
    so.FindProperty("crackMaterial").objectReferenceValue = crackMat;
    so.ApplyModifiedProperties();
}
var ffb = car.GetComponent<RacingProject.Car.WheelForceFeedback>() ?? car.AddComponent<RacingProject.Car.WheelForceFeedback>();
{
    var so = new SerializedObject(ffb);
    so.FindProperty("controller").objectReferenceValue = controller;
    so.FindProperty("deformation").objectReferenceValue = car.GetComponentInChildren<RacingProject.Car.CarDeformation>(true);
    so.FindProperty("health").objectReferenceValue = health;
    so.FindProperty("damage").objectReferenceValue = damage;
    var w = so.FindProperty("wheels"); w.arraySize = wheelList.Count;
    for (int i = 0; i < wheelList.Count; i++) w.GetArrayElementAtIndex(i).objectReferenceValue = wheelList[i];
    so.ApplyModifiedProperties();
}
var repair = UnityEngine.Object.FindFirstObjectByType<RacingProject.Pickups.RepairKits>(FindObjectsInactive.Include);
{
    var so = new SerializedObject(repair);
    so.FindProperty("carDamage").objectReferenceValue = damage;
    so.ApplyModifiedProperties();
}
log.Append("car damage; ");

// --- 4. РПГ на турели ---
var roofGun = deep(car.transform, "RoofGun");
var vrGun = roofGun.GetComponent<RacingProject.Turret.VRGun>();
drop(roofGun, "RocketLauncher");
var launcherGo = new GameObject("RocketLauncher");
launcherGo.transform.SetParent(roofGun, false);
// Слева от пулемёта, чуть выше ствола; в единицах RoofGun (масштаб 2)
launcherGo.transform.localPosition = new Vector3(-0.3f, 0.06f, 0.05f);
var uaz = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy/EnemyUaz.prefab");
var uazLauncher = uaz.GetComponentInChildren<RacingProject.Enemy.EnemyRocketLauncher>(true);
var uazSo = new SerializedObject(uazLauncher);
var rpgAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/RPG7/RPG7.fbx");
var model = (GameObject)PrefabUtility.InstantiatePrefab(rpgAsset, launcherGo.transform);
model.name = "RPG";
model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
model.transform.localPosition = Vector3.zero;
model.transform.localScale = Vector3.one;
// Длина трубы ~1,8 м мира, как у гранатомёта «буханки»
Transform tube = deep(model.transform, "pCube12");
float tubeLength = tube.GetComponent<Renderer>().bounds.size.z;
model.transform.localScale = Vector3.one * (1.77f / tubeLength);
Bounds tb = tube.GetComponent<Renderer>().bounds;
model.transform.position += launcherGo.transform.position - tb.center;
foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
var warhead = deep(model.transform, "pCylinder8").gameObject;
tb = tube.GetComponent<Renderer>().bounds;
var firePoint = new GameObject("FirePoint").transform;
firePoint.SetParent(launcherGo.transform, false);
firePoint.position = tb.center + roofGun.forward * (tb.extents.z + 0.35f);
firePoint.rotation = roofGun.rotation;
var uazBackblast = (ParticleSystem)uazSo.FindProperty("backblast").objectReferenceValue;
var backblastGo = UnityEngine.Object.Instantiate(uazBackblast.gameObject, launcherGo.transform);
backblastGo.name = "Backblast";
backblastGo.transform.position = tb.center - roofGun.forward * (tb.extents.z + 0.1f);
backblastGo.transform.rotation = Quaternion.LookRotation(-roofGun.forward, roofGun.up);
var uazAudio = (AudioSource)uazSo.FindProperty("launchAudio").objectReferenceValue;
var launchAudio = launcherGo.AddComponent<AudioSource>();
launchAudio.playOnAwake = false; launchAudio.spatialBlend = 1f; launchAudio.minDistance = 8f; launchAudio.maxDistance = 250f;
launchAudio.rolloffMode = AudioRolloffMode.Logarithmic; launchAudio.dopplerLevel = 0f;
launchAudio.outputAudioMixerGroup = uazAudio != null ? uazAudio.outputAudioMixerGroup : null;
model.SetActive(false);
var launcher = launcherGo.AddComponent<RacingProject.Turret.RocketLauncher>();
{
    var so = new SerializedObject(launcher);
    so.FindProperty("gun").objectReferenceValue = vrGun;
    so.FindProperty("firePoint").objectReferenceValue = firePoint;
    so.FindProperty("launcherModel").objectReferenceValue = model;
    so.FindProperty("loadedWarhead").objectReferenceValue = warhead;
    so.FindProperty("radio").objectReferenceValue = radio;
    so.FindProperty("hitLayerMask").intValue = uazSo.FindProperty("hitLayerMask").intValue;
    so.FindProperty("rocketPrefab").objectReferenceValue = uazSo.FindProperty("rocketPrefab").objectReferenceValue;
    so.FindProperty("explosionPrefab").objectReferenceValue = uazSo.FindProperty("explosionPrefab").objectReferenceValue;
    so.FindProperty("backblast").objectReferenceValue = backblastGo.GetComponent<ParticleSystem>();
    so.FindProperty("launchAudio").objectReferenceValue = launchAudio;
    so.FindProperty("launchSound").objectReferenceValue = clip("Rpg_Launch");
    so.FindProperty("reloadSound").objectReferenceValue = clip("Rpg_Reload");
    so.ApplyModifiedProperties();
}
log.Append("rpg scale ").Append(model.transform.localScale.x.ToString("F2")).Append(" fire ").Append(firePoint.localPosition).Append("; ");

// --- 5. Ящики выстрелов ---
string crateFbx = "Assets/Models/LevelProps/RpgCrate.fbx";
{
    var imp = (ModelImporter)AssetImporter.GetAtPath(crateFbx);
    imp.globalScale = 2f; imp.useFileScale = true; imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
    imp.addCollider = false; imp.isReadable = false;
    imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
    foreach (var r in AssetDatabase.LoadAllAssetsAtPath(crateFbx)) {
        var m = r as Material; if (m == null) continue;
        var target = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/LevelProps/Materials/" + m.name + ".mat")
                     ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/" + m.name + ".mat");
        if (target != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
        else log.Append("no material ").Append(m.name).Append("; ");
    }
    imp.SaveAndReimport();
}
string cratePrefabPath = "Assets/Prefabs/Pickups/RpgCrate.prefab";
{
    var root = new GameObject("RpgCrate");
    var visual = new GameObject("Visual");
    visual.transform.SetParent(root.transform, false);
    visual.transform.localPosition = new Vector3(0f, 0.6f, 0f);
    var spin = visual.AddComponent<RacingProject.Pickups.PickupSpin>();
    var spinSo = new SerializedObject(spin);
    spinSo.FindProperty("degreesPerSecond").floatValue = 45f;
    spinSo.FindProperty("bobHeight").floatValue = 0.15f;
    spinSo.ApplyModifiedPropertiesWithoutUndo();
    var crateModel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(crateFbx), visual.transform);
    crateModel.transform.localPosition = Vector3.zero; crateModel.transform.localRotation = Quaternion.identity;
    foreach (var r in crateModel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    var glow = new GameObject("Glow");
    glow.transform.SetParent(root.transform, false);
    glow.transform.localPosition = new Vector3(0f, 1.6f, 0f);
    var light = glow.AddComponent<Light>();
    light.type = LightType.Point; light.color = new Color(1f, 0.62f, 0.2f); light.intensity = 3f; light.range = 7f; light.shadows = LightShadows.None;
    PrefabUtility.SaveAsPrefabAsset(root, cratePrefabPath);
    UnityEngine.Object.DestroyImmediate(root);
}
var cratePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(cratePrefabPath);

var general = GameObject.Find("General").transform;
var oldCrates = general.Find("RocketCrates");
if (oldCrates != null) UnityEngine.Object.DestroyImmediate(oldCrates.gameObject);
var cratesRoot = new GameObject("RocketCrates").transform;
cratesRoot.SetParent(general, false);
var manager = repair.gameObject;
var nitro = manager.GetComponent<RacingProject.Pickups.NitroPickups>();
var canisters = new SerializedObject(nitro).FindProperty("canisters");
var crateList = new System.Collections.Generic.List<GameObject>();
var hits = new RaycastHit[16];
for (int i = 0; i < canisters.arraySize; i++) {
    var can = ((GameObject)canisters.GetArrayElementAtIndex(i).objectReferenceValue).transform;
    // Ящик в 6–8 м от канистры: первая сторона, где под ним земля на той же высоте и вокруг ничего нет
    bool placed = false;
    for (int k = 0; k < 16 && !placed; k++) {
        float ang = (k * 67.5f + i * 40f) * Mathf.Deg2Rad;
        float dist = k < 8 ? 7f : 5f;
        Vector3 probe = can.position + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * dist;
        RaycastHit hit;
        if (!Physics.Raycast(probe + Vector3.up * 3f, Vector3.down, out hit, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
        if (Mathf.Abs(hit.point.y - can.position.y) > 1.2f || hit.normal.y < 0.9f) continue;
        if (Physics.CheckBox(hit.point + Vector3.up * 1.2f, new Vector3(1.4f, 1f, 1.4f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
        var crate = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, cratesRoot);
        crate.name = "RpgCrate" + (crateList.Count + 1);
        crate.transform.position = hit.point;
        crate.transform.rotation = Quaternion.Euler(0f, ang * Mathf.Rad2Deg, 0f);
        crateList.Add(crate);
        placed = true;
    }
    if (!placed) log.Append("no place near ").Append(can.name).Append("; ");
}
var template = (GameObject)PrefabUtility.InstantiatePrefab(cratePrefab, cratesRoot);
template.name = "RpgCrateDrop";
template.SetActive(false);

var pickupGo = new GameObject("RocketCratePickup");
pickupGo.transform.SetParent(cratesRoot, false);
var oldPickup = (AudioSource)new SerializedObject(repair).FindProperty("pickupSound").objectReferenceValue;
var pickupSound = pickupGo.AddComponent<AudioSource>();
pickupSound.clip = clip("Crate_Pickup"); pickupSound.playOnAwake = false; pickupSound.spatialBlend = 0f;
pickupSound.outputAudioMixerGroup = oldPickup != null ? oldPickup.outputAudioMixerGroup : null;

var rc = manager.GetComponent<RacingProject.Pickups.RocketCrates>() ?? manager.AddComponent<RacingProject.Pickups.RocketCrates>();
{
    var so = new SerializedObject(rc);
    so.FindProperty("launcher").objectReferenceValue = launcher;
    so.FindProperty("enemyManager").objectReferenceValue = manager.GetComponent<RacingProject.Enemy.EnemyManager>();
    so.FindProperty("car").objectReferenceValue = car.transform;
    var arr = so.FindProperty("crates"); arr.arraySize = crateList.Count;
    for (int i = 0; i < crateList.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = crateList[i];
    so.FindProperty("pickupSound").objectReferenceValue = pickupSound;
    so.FindProperty("dropTemplate").objectReferenceValue = template;
    so.ApplyModifiedProperties();
}
log.Append("crates ").Append(crateList.Count).Append("; ");

// --- 6. Надписи на приборах ---
foreach (var cd in UnityEngine.Object.FindObjectsByType<RacingProject.Hud.CockpitDisplay>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
    var so = new SerializedObject(cd);
    var heatGauge = so.FindProperty("heatGauge").objectReferenceValue;
    var heatBar = so.FindProperty("heatBar").objectReferenceValue;
    bool gunner = heatGauge != null, driver = so.FindProperty("speedGauge").objectReferenceValue != null;
    if (!gunner && !driver) continue;
    var sample = gunner ? cd.transform.Find("Overheat") : cd.transform.Find("Label НИТРО");
    if (gunner) {
        drop(cd.transform, "Rockets");
        var t = UnityEngine.Object.Instantiate(sample.gameObject, cd.transform);
        t.name = "Rockets";
        var rt = (RectTransform)t.transform; rt.anchoredPosition = new Vector2(0f, 112f); rt.sizeDelta = new Vector2(280f, 44f);
        var tmp = t.GetComponent<TMPro.TMP_Text>(); tmp.text = "РПГ ×0"; tmp.color = new Color(1f, 0.82f, 0.48f); tmp.fontSize = 34f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.richText = true;
        t.SetActive(false);
        so.FindProperty("rockets").objectReferenceValue = launcher;
        so.FindProperty("rocketsText").objectReferenceValue = tmp;
    }
    if (driver) {
        drop(cd.transform, "DamageWarning");
        var t = UnityEngine.Object.Instantiate(sample.gameObject, cd.transform);
        t.name = "DamageWarning";
        var rt = (RectTransform)t.transform; rt.anchoredPosition = new Vector2(0f, -172f); rt.sizeDelta = new Vector2(420f, 40f);
        var tmp = t.GetComponent<TMPro.TMP_Text>(); tmp.text = "РАДИАТОР"; tmp.color = new Color(1f, 0.45f, 0.25f); tmp.fontSize = 30f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        t.SetActive(false);
        so.FindProperty("damage").objectReferenceValue = damage;
        so.FindProperty("damageWarning").objectReferenceValue = tmp;
    }
    so.ApplyModifiedProperties();
}
log.Append("hud; ");

// --- 7. Остовы врагов ---
var burnt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_Burnt.mat");
foreach (string path in new[] { "Assets/Resources/EnemySedan.prefab", "Assets/Prefabs/Enemy/EnemyUaz.prefab", "Assets/Prefabs/Enemy/EnemyUral.prefab" }) {
    var root = PrefabUtility.LoadPrefabContents(path);
    var eh = root.GetComponent<RacingProject.Enemy.EnemyHealth>();
    var so = new SerializedObject(eh);
    so.FindProperty("wreck.burnt").objectReferenceValue = burnt;
    so.FindProperty("wreck.scrapeLoop").objectReferenceValue = clip("Rim_Scrape_Loop");
    so.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
}
log.Append("wrecks; ");

// --- 8. Рестарт после камеры смерти ---
var room = UnityEngine.Object.FindFirstObjectByType<RacingProject.Network.RoomController>(FindObjectsInactive.Include);
{
    var so = new SerializedObject(room);
    so.FindProperty("restartDelay").floatValue = 7.5f;
    so.ApplyModifiedProperties();
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(car.scene);
return log.ToString();
