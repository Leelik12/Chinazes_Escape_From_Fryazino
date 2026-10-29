// События боя (SovietCity должна быть открыта, не в Play): режиссёр забега, артналёты, рация, линии ЛЭП
// с падающими опорами, взрывоопасные резервуары и брошенные машины, динамическая музыка.
//  - импорт звуков: Assets/Content/Sound/Generated/Events (Tools/Audio/generate_event_sounds.py)
//    и Music (Tools/Audio/generate_music.py);
//  - на General/EnemyManager: ArtilleryStrikes, RadioChatter (голос — дочерний RadioVoice), GameDirector, PowerLines;
//  - на General: MusicDirector (выход — группа музыки MusikMixer, громкость музыки в настройках);
//  - PowerLines: линии из Environment/Roadside/PowerPoles (опоры подряд до объекта Wires_<дорога>) и деревянных
//    столбов Environment/LevelDressing/PrivateSector; у опор снимается статичный батчинг, иначе они не упадут;
//  - префабы Assets/Prefabs/LevelProps: FuelTank, Zhiguli_Red/White, Moskvich_Blue/Yellow, FuelTruck,
//    GasCylinderRack, GasCylinder получают ExplosiveBarrel
//    (серия взрывов, пожар после, обгоревший материал LP_Burnt); PropNetwork видит их через extraRoots.
// Повторный запуск пересобирает всё заново. После BuildLevelProps (он пересоздаёт префабы) — запустить снова.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var log = new System.Text.StringBuilder();
const string EV = "Assets/Content/Sound/Generated/Events/";
const string MU = "Assets/Content/Sound/Generated/Music/";

// --- Звуки ---
System.Action<string, bool, bool> importClip = (path, stream, mono) => {
    var imp = AssetImporter.GetAtPath(path) as AudioImporter;
    if (imp == null) throw new System.Exception("no audio " + path);
    var st = imp.defaultSampleSettings;
    st.loadType = stream ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
    st.compressionFormat = AudioCompressionFormat.Vorbis;
    st.quality = stream ? 0.7f : 0.85f;
    imp.defaultSampleSettings = st;
    imp.forceToMono = mono;
    imp.loadInBackground = stream;
    imp.SaveAndReimport();
};
AssetDatabase.Refresh();
foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { EV.TrimEnd('/') })) importClip(AssetDatabase.GUIDToAssetPath(g), false, true);
foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { MU.TrimEnd('/') })) importClip(AssetDatabase.GUIDToAssetPath(g), true, true);
System.Func<string, AudioClip> clip = p => { var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p + ".wav"); if (c == null) throw new System.Exception("missing " + p); return c; };
System.Func<string, int, AudioClip[]> clips = (p, n) => { var a = new AudioClip[n]; for (int i = 0; i < n; i++) a[i] = clip(p + i); return a; };

System.Action<SerializedObject, string, UnityEngine.Object[]> setArray = (so, name, items) => {
    var arr = so.FindProperty(name); arr.arraySize = items.Length;
    for (int i = 0; i < items.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
};

var em = GameObject.Find("General/EnemyManager");
var manager = em.GetComponent<RacingProject.Enemy.EnemyManager>();
var mso = new SerializedObject(manager);
var carHealth = mso.FindProperty("carHealth").objectReferenceValue as RacingProject.PlayerHealth;
var repairKits = mso.FindProperty("repairKits").objectReferenceValue as RacingProject.Pickups.RepairKits;
var explosion = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/EnemyExplosion.prefab");

// --- Артналёт: красный круг — частицы URP без освещения, цвет из вершин, сложение ---
string markerPath = "Assets/Content/Effects/Fx_ArtilleryMarker.mat";
if (!AssetDatabase.IsValidFolder("Assets/Content/Effects")) AssetDatabase.CreateFolder("Assets/Content", "Effects");
var marker = AssetDatabase.LoadAssetAtPath<Material>(markerPath);
if (marker == null) { marker = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(marker, markerPath); }
marker.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
marker.SetFloat("_Surface", 1f); marker.SetFloat("_Blend", 2f);
marker.SetColor("_BaseColor", new Color(4f, 0.6f, 0.3f, 1f));
marker.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); marker.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
marker.SetFloat("_ZWrite", 0f);
marker.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); marker.EnableKeyword("_BLENDMODE_ADD");
marker.renderQueue = 3000;
EditorUtility.SetDirty(marker);

var art = em.GetComponent<RacingProject.Enemy.ArtilleryStrikes>(); if (art == null) art = em.AddComponent<RacingProject.Enemy.ArtilleryStrikes>();
var aso = new SerializedObject(art);
aso.FindProperty("explosionPrefab").objectReferenceValue = explosion;
aso.FindProperty("markerMaterial").objectReferenceValue = marker;
setArray(aso, "launchSounds", clips(EV + "Artillery_Launch_", 2));
setArray(aso, "whistleSounds", clips(EV + "Artillery_Whistle_", 3));
aso.ApplyModifiedPropertiesWithoutUndo();

// --- Рация: голос в эфире — 2D-источник ---
var voiceT = em.transform.Find("RadioVoice");
if (voiceT == null) { voiceT = new GameObject("RadioVoice").transform; voiceT.SetParent(em.transform, false); }
var voice = voiceT.GetComponent<AudioSource>(); if (voice == null) voice = voiceT.gameObject.AddComponent<AudioSource>();
voice.playOnAwake = false; voice.spatialBlend = 0f; voice.volume = 1f;
var radio = em.GetComponent<RacingProject.Enemy.RadioChatter>(); if (radio == null) radio = em.AddComponent<RacingProject.Enemy.RadioChatter>();
var rso = new SerializedObject(radio);
rso.FindProperty("enemyManager").objectReferenceValue = manager;
rso.FindProperty("carHealth").objectReferenceValue = carHealth;
rso.FindProperty("voice").objectReferenceValue = voice;
rso.FindProperty("squelchIn").objectReferenceValue = clip(EV + "Radio_Squelch_In");
rso.FindProperty("squelchOut").objectReferenceValue = clip(EV + "Radio_Squelch_Out");
setArray(rso, "babble", clips(EV + "Radio_Hq_", 4));
setArray(rso, "interceptBabble", clips(EV + "Radio_Intercept_", 3));
rso.ApplyModifiedPropertiesWithoutUndo();

// --- Режиссёр ---
var director = em.GetComponent<RacingProject.Enemy.GameDirector>(); if (director == null) director = em.AddComponent<RacingProject.Enemy.GameDirector>();
var dso = new SerializedObject(director);
dso.FindProperty("enemyManager").objectReferenceValue = manager;
dso.FindProperty("carHealth").objectReferenceValue = carHealth;
dso.FindProperty("artillery").objectReferenceValue = art;
dso.FindProperty("radio").objectReferenceValue = radio;
dso.FindProperty("repairKits").objectReferenceValue = repairKits;
dso.ApplyModifiedPropertiesWithoutUndo();

// --- Линии ЛЭП ---
var lines = new System.Collections.Generic.List<object[]>(); // опоры, крепления, запечённые провода, пролёт, высота
var concrete = new[] { new Vector3(-1.4f, 18.5f, 0f), new Vector3(0f, 19.4f, 0f), new Vector3(1.4f, 18.5f, 0f) };
var roadPoles = GameObject.Find("Environment/Roadside/PowerPoles").transform;
var run = new System.Collections.Generic.List<Transform>();
foreach (Transform c in roadPoles) {
    if (c.name.StartsWith("Wires_")) { lines.Add(new object[] { run.ToArray(), concrete, c.GetComponent<Renderer>(), 50f, 19.5f }); run.Clear(); }
    else if (c.name.StartsWith("PowerPole")) run.Add(c);
}
var wooden = new System.Collections.Generic.List<Transform>(); Renderer woodWires = null;
foreach (Transform c in GameObject.Find("Environment/LevelDressing/PrivateSector").transform) {
    if (c.name.StartsWith("WoodenPole")) wooden.Add(c);
    if (c.name == "Wires_PrivateSector") woodWires = c.GetComponent<Renderer>();
}
lines.Add(new object[] { wooden.ToArray(), new[] { new Vector3(-1f, 14.7f, 0f), new Vector3(1f, 14.7f, 0f) }, woodWires, 36f, 15f });
var powerLines = em.GetComponent<RacingProject.Props.PowerLines>(); if (powerLines == null) powerLines = em.AddComponent<RacingProject.Props.PowerLines>();
var pso = new SerializedObject(powerLines);
var lp = pso.FindProperty("lines"); lp.arraySize = lines.Count;
int nPoles = 0;
for (int i = 0; i < lines.Count; i++) {
    var e = lp.GetArrayElementAtIndex(i); var poles = (Transform[])lines[i][0]; var att = (Vector3[])lines[i][1];
    var pa = e.FindPropertyRelative("poles"); pa.arraySize = poles.Length;
    for (int k = 0; k < poles.Length; k++) {
        pa.GetArrayElementAtIndex(k).objectReferenceValue = poles[k]; nPoles++;
        // Статичный батчинг склеивает меши — такая опора не сможет упасть
        foreach (var t in poles[k].GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(t.gameObject) & ~StaticEditorFlags.BatchingStatic);
    }
    var aa = e.FindPropertyRelative("attach"); aa.arraySize = att.Length;
    for (int k = 0; k < att.Length; k++) aa.GetArrayElementAtIndex(k).vector3Value = att[k];
    e.FindPropertyRelative("bakedWires").objectReferenceValue = (Renderer)lines[i][2];
    e.FindPropertyRelative("maxSpan").floatValue = (float)lines[i][3];
    e.FindPropertyRelative("height").floatValue = (float)lines[i][4];
}
pso.FindProperty("wireMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/PS_Wire.mat");
pso.FindProperty("sparksTemplate").objectReferenceValue = GameObject.Find("General/StreetLights").transform.Find("Sparks").GetComponent<ParticleSystem>();
pso.FindProperty("arcLoop").objectReferenceValue = clip(EV + "Arc_Loop");
setArray(pso, "arcCracks", clips(EV + "Arc_Crack_", 3));
pso.FindProperty("fallCrack").objectReferenceValue = clip(EV + "Pole_Crack");
pso.FindProperty("fallCrash").objectReferenceValue = clip(EV + "Pole_Crash");
pso.ApplyModifiedPropertiesWithoutUndo();
log.Append("power lines ").Append(lines.Count).Append(", poles ").Append(nPoles).Append("; ");

// --- Взрывоопасные резервуары и машины ---
var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Wasteland/BurningBarrel.prefab");
var burnt = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/LevelProps/Materials/LP_Burnt.mat");
// имя, прочность, радиус, урон врагу, урон машине игроков, взрывов в серии, разброс, масштаб огня
var explosive = new object[][] {
    new object[] { "FuelTank", 250, 26f, 1600, 380, 6, 9f, 7f },
    new object[] { "Zhiguli_Red", 140, 11f, 1000, 220, 2, 1.5f, 2.2f },
    new object[] { "Zhiguli_White", 140, 11f, 1000, 220, 2, 1.5f, 2.2f },
    new object[] { "Moskvich_Blue", 140, 11f, 1000, 220, 2, 1.5f, 2.2f },
    new object[] { "Moskvich_Yellow", 140, 11f, 1000, 220, 2, 1.5f, 2.2f },
    new object[] { "FuelTruck", 220, 22f, 1500, 340, 5, 4f, 5f },
    new object[] { "GasCylinderRack", 90, 16f, 1200, 260, 4, 1.2f, 2.5f },
    new object[] { "GasCylinder", 40, 9f, 700, 160, 1, 0.5f, 1.2f },
};
foreach (var ex in explosive) {
    string path = "Assets/Prefabs/LevelProps/" + ex[0] + ".prefab";
    var root = PrefabUtility.LoadPrefabContents(path);
    var old = root.transform.Find("Burning"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    var eb = root.GetComponent<RacingProject.Props.ExplosiveBarrel>(); if (eb != null) UnityEngine.Object.DestroyImmediate(eb);
    var renderers = root.GetComponentsInChildren<Renderer>();
    var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
    // Пожар после взрыва: огонь, дым и свет горящей бочки, увеличенные
    var burning = new GameObject("Burning"); burning.transform.SetParent(root.transform, false);
    float k2 = (float)ex[7];
    // Резервуар горит по крыше, машина — в салоне
    burning.transform.position = new Vector3(bounds.center.x, k2 > 5f ? bounds.max.y - 0.3f : bounds.max.y * 0.75f, bounds.center.z);
    foreach (var part in new[] { "Fire", "Smoke", "FireLight" }) {
        var src = barrelPrefab.transform.Find(part);
        var copy = UnityEngine.Object.Instantiate(src.gameObject, burning.transform); copy.name = part;
        copy.transform.localPosition = Vector3.up * (part == "Fire" ? 0f : k2 * 0.4f);
        if (part != "FireLight") copy.transform.localScale = Vector3.one * k2;
        else { var l = copy.GetComponent<Light>(); l.range *= k2 * 0.7f; l.intensity *= 1.5f; }
    }
    burning.SetActive(false);
    eb = root.AddComponent<RacingProject.Props.ExplosiveBarrel>();
    var so = new SerializedObject(eb);
    so.FindProperty("hitPoints").intValue = (int)ex[1];
    so.FindProperty("radius").floatValue = (float)ex[2];
    so.FindProperty("enemyDamage").intValue = (int)ex[3];
    so.FindProperty("playerDamage").intValue = (int)ex[4];
    so.FindProperty("carPush").floatValue = 9f;
    so.FindProperty("propPush").floatValue = 18f;
    so.FindProperty("chainDelay").floatValue = 0.4f;
    so.FindProperty("explosionPrefab").objectReferenceValue = explosion;
    so.FindProperty("blastCount").intValue = (int)ex[5];
    so.FindProperty("blastSpread").floatValue = (float)ex[6];
    so.FindProperty("blastInterval").floatValue = 0.17f;
    setArray(so, "afterObjects", new UnityEngine.Object[] { burning });
    var model = new System.Collections.Generic.List<UnityEngine.Object>();
    foreach (var r in renderers) model.Add(r);
    setArray(so, "charRenderers", model.ToArray());
    so.FindProperty("charMaterial").objectReferenceValue = burnt;
    so.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
}
var propNet = UnityEngine.Object.FindFirstObjectByType<RacingProject.Props.PropNetwork>();
var pnso = new SerializedObject(propNet);
setArray(pnso, "extraRoots", new UnityEngine.Object[] { GameObject.Find("Environment/LevelDressing").transform });
pnso.ApplyModifiedPropertiesWithoutUndo();
int nExplosive = GameObject.Find("Environment/LevelDressing").GetComponentsInChildren<RacingProject.Props.ExplosiveBarrel>(true).Length;
log.Append("explosive props in dressing ").Append(nExplosive).Append("; ");

// --- Музыка ---
var general = GameObject.Find("General");
var music = general.GetComponent<RacingProject.Audio.MusicDirector>(); if (music == null) music = general.AddComponent<RacingProject.Audio.MusicDirector>();
var muso = new SerializedObject(music);
muso.FindProperty("enemyManager").objectReferenceValue = manager;
muso.FindProperty("carHealth").objectReferenceValue = carHealth;
var musikMixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Content/Sound/MusikMixer.mixer");
muso.FindProperty("output").objectReferenceValue = musikMixer.FindMatchingGroups("Master")[0];
muso.FindProperty("calm").objectReferenceValue = clip(MU + "Music_Calm");
muso.FindProperty("pulse").objectReferenceValue = clip(MU + "Music_Pulse");
muso.FindProperty("drums").objectReferenceValue = clip(MU + "Music_Drums");
muso.FindProperty("tension").objectReferenceValue = clip(MU + "Music_Tension");
muso.FindProperty("boss").objectReferenceValue = clip(MU + "Music_Boss");
muso.FindProperty("clearStinger").objectReferenceValue = clip(MU + "Music_Clear");
muso.ApplyModifiedPropertiesWithoutUndo();

AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(em.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(em.scene);
return log.ToString();
