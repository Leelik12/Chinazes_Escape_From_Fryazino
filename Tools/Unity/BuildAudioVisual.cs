// Звук и визуал города (SovietCity): библиотека разовых звуков, мотор из слоёв, шины и пыль, удары кузова, ветер,
// сердцебиение, пост-эффекты по событиям, погода, зоны эха, прожекторы КПП, пожары на горизонте, стрелки на врагов,
// шрифты HUD волн. Префабы врагов, взрыва и ракеты — Tools/Unity/BuildEnemyFx.cs, материалы — BuildFxAssets.cs.
// Запуск: execute_code (MCP for Unity, C# 6) вне Play; сцена сохраняется. Повторный запуск ничего не дублирует
var log = new System.Text.StringBuilder();
string gen = "Assets/Content/Sound/Generated/";
System.Func<string, AudioClip> clip = name => {
    foreach (string folder in new[] { "Vehicle/", "Combat/", "Weather/", "" }) {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(gen + folder + name + ".wav");
        if (c != null) return c;
    }
    throw new System.Exception("missing clip " + name);
};
System.Func<string, AudioClip[]> variants = prefix => {
    var list = new System.Collections.Generic.List<AudioClip>();
    for (int i = 0; i < 8; i++) {
        AudioClip c = null;
        foreach (string folder in new[] { "Vehicle/", "Combat/", "Weather/" })
            if (c == null) c = AssetDatabase.LoadAssetAtPath<AudioClip>(gen + folder + prefix + "_" + i + ".wav");
        if (c != null) list.Add(c);
    }
    if (list.Count == 0) list.Add(clip(prefix));
    return list.ToArray();
};
System.Func<string, UnityEngine.Audio.AudioMixerGroup> mixer = name => {
    var m = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Content/Sound/" + name + ".mixer");
    return m.FindMatchingGroups("Master")[0];
};
var carMix = mixer("CarMixer");
var ambientMix = mixer("AmbientMixer");
System.Func<Transform, string, Transform> child = (parent, name) => {
    var t = parent.Find(name);
    if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
    return t;
};
System.Func<string, Material> fxMat = name => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/" + name + ".mat");
System.Func<string, Material> findMat = name => {
    foreach (string guid in AssetDatabase.FindAssets(name + " t:Material")) {
        string p = AssetDatabase.GUIDToAssetPath(guid);
        if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
    }
    return null;
};
System.Func<float, float, Gradient> fade = (alphaIn, alphaOut) => {
    var g = new Gradient();
    g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
        new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alphaIn, 0.15f), new GradientAlphaKey(alphaOut, 0.6f), new GradientAlphaKey(0f, 1f) });
    return g;
};
// Система частиц с общими настройками: мировые координаты, без автозапуска лишнего, свой материал
System.Func<Transform, string, Material, ParticleSystem> particles = (parent, name, material) => {
    var t = child(parent, name);
    var ps = t.GetComponent<ParticleSystem>();
    if (ps == null) ps = t.gameObject.AddComponent<ParticleSystem>();
    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    var main = ps.main;
    main.simulationSpace = ParticleSystemSimulationSpace.World;
    main.playOnAwake = true;
    main.loop = true;
    var renderer = t.GetComponent<ParticleSystemRenderer>();
    renderer.sharedMaterial = material;
    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    renderer.receiveShadows = false;
    var emission = ps.emission;
    emission.rateOverTime = 0f;
    emission.rateOverDistance = 0f;
    emission.SetBursts(new ParticleSystem.Burst[0]);
    return ps;
};
System.Func<Vector3, float> groundY = p => {
    RaycastHit hit;
    if (Physics.Raycast(new Vector3(p.x, 600f, p.z), Vector3.down, out hit, 800f, ~0, QueryTriggerInteraction.Ignore)) return hit.point.y;
    var terrain = Terrain.activeTerrain;
    return terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : 50f;
};

var general = GameObject.Find("General").transform;
var car = GameObject.Find("VolgaCar");
var controller = car.GetComponent<RacingProject.Car.CarControllerSample>();
var health = car.GetComponent<RacingProject.PlayerHealth>();

// --- Библиотека разовых звуков ---
var libraryGo = child(general, "SoundLibrary").gameObject;
var library = libraryGo.GetComponent<RacingProject.Audio.SoundLibrary>();
if (library == null) library = libraryGo.AddComponent<RacingProject.Audio.SoundLibrary>();
var lso = new SerializedObject(library);
var entries = lso.FindProperty("entries");
// вид, клипы, громкость, минимальная и максимальная дальность, разброс тона, голоса, интервал, микшер
var specs = new object[][] {
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.ImpactHeavy, variants("Impact_Heavy"), 1f, 6f, 150f, 0.06f, 4, 0.05f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.ImpactLight, variants("Impact_Light"), 0.8f, 4f, 80f, 0.08f, 4, 0.04f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.Glass, variants("Glass_Break"), 0.9f, 4f, 90f, 0.05f, 3, 0.05f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.DebrisClatter, variants("Debris_Clatter"), 0.7f, 3f, 60f, 0.1f, 6, 0.03f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.Backfire, variants("Backfire"), 0.9f, 5f, 120f, 0.08f, 3, 0.05f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.GearShift, variants("GearShift"), 0.6f, 2f, 30f, 0.05f, 2, 0.1f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.BulletWhizz, variants("Bullet_Whizz"), 0.8f, 2f, 30f, 0.1f, 4, 0.04f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.HitMetal, variants("Hit_Metal"), 0.7f, 3f, 70f, 0.1f, 8, 0.015f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.HitConcrete, variants("Hit_Concrete"), 0.55f, 3f, 60f, 0.1f, 8, 0.015f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.HitDirt, variants("Hit_Dirt"), 0.5f, 3f, 50f, 0.1f, 6, 0.015f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.Casing, variants("Casing"), 0.45f, 1.5f, 20f, 0.12f, 4, 0.03f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.GunOverheat, variants("Gun_Overheat"), 0.8f, 2f, 30f, 0.02f, 1, 0.3f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.GunCooled, variants("Gun_Cooled"), 0.7f, 2f, 30f, 0.02f, 2, 0.05f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.BossHorn, variants("Boss_Horn"), 0.9f, 5f, 500f, 0f, 1, 2f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.DeafRing, variants("Deaf_Ring"), 0.7f, 5f, 500f, 0f, 1, 0.5f, carMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.Thunder, variants("Thunder"), 1f, 5f, 2000f, 0.03f, 2, 1f, ambientMix },
    new object[] { RacingProject.Audio.SoundLibrary.Sfx.Splash, variants("Water_Splash"), 1f, 6f, 200f, 0.05f, 3, 0.2f, carMix },
};
entries.arraySize = specs.Length;
for (int i = 0; i < specs.Length; i++) {
    var e = entries.GetArrayElementAtIndex(i);
    var s = specs[i];
    e.FindPropertyRelative("kind").enumValueIndex = (int)(RacingProject.Audio.SoundLibrary.Sfx)s[0];
    var clips = (AudioClip[])s[1];
    var arr = e.FindPropertyRelative("clips");
    arr.arraySize = clips.Length;
    for (int c = 0; c < clips.Length; c++) arr.GetArrayElementAtIndex(c).objectReferenceValue = clips[c];
    e.FindPropertyRelative("volume").floatValue = (float)s[2];
    e.FindPropertyRelative("minDistance").floatValue = (float)s[3];
    e.FindPropertyRelative("maxDistance").floatValue = (float)s[4];
    e.FindPropertyRelative("pitchJitter").floatValue = (float)s[5];
    e.FindPropertyRelative("maxVoices").intValue = (int)s[6];
    e.FindPropertyRelative("minInterval").floatValue = (float)s[7];
    e.FindPropertyRelative("output").objectReferenceValue = (UnityEngine.Audio.AudioMixerGroup)s[8];
}
lso.FindProperty("poolSize").intValue = 32;
lso.ApplyModifiedPropertiesWithoutUndo();
log.Append("library " + specs.Length + "; ");

// --- Мотор ---
var engine = car.GetComponent<RacingProject.Audio.CarEngineAudio>();
if (engine == null) engine = car.AddComponent<RacingProject.Audio.CarEngineAudio>();
var exhaust = child(car.transform, "Exhaust");
exhaust.localPosition = new Vector3(0.6f, -0.9f, -4.8f);
var eso = new SerializedObject(engine);
eso.FindProperty("car").objectReferenceValue = controller;
var layers = eso.FindProperty("layers");
var layerClips = new[] { clip("Engine_Idle"), clip("Engine_Low"), clip("Engine_Mid"), clip("Engine_High") };
layers.arraySize = layerClips.Length;
for (int i = 0; i < layerClips.Length; i++) layers.GetArrayElementAtIndex(i).objectReferenceValue = layerClips[i];
var rpms = eso.FindProperty("layerRpm");
var rpmValues = new[] { 820f, 1900f, 3400f, 5400f };
rpms.arraySize = rpmValues.Length;
for (int i = 0; i < rpmValues.Length; i++) rpms.GetArrayElementAtIndex(i).floatValue = rpmValues[i];
eso.FindProperty("output").objectReferenceValue = carMix;
eso.FindProperty("spatialBlend").floatValue = 0.2f;
eso.FindProperty("exhaust").objectReferenceValue = exhaust;
eso.ApplyModifiedPropertiesWithoutUndo();
// Старый одиночный звук мотора молчит: CarControllerSample по-прежнему крутит ему тон, но слышны слои
var oldEngine = car.transform.Find("EngineSound").GetComponent<AudioSource>();
oldEngine.mute = true;
EditorUtility.SetDirty(oldEngine);

// --- Шины и пыль ---
var wheelFx = car.GetComponent<RacingProject.Car.WheelEffects>();
var smokeTemplate = car.transform.Find("WheelSmokeTemplate");
var dust = particles(car.transform, "WheelDustTemplate", fxMat("Fx_Dust"));
{
    var main = dust.main;
    main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
    main.startSize = new ParticleSystem.MinMaxCurve(1f, 2.2f);
    main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
    main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
    main.gravityModifier = -0.03f;
    main.maxParticles = 300;
    main.startColor = new Color(0.55f, 0.48f, 0.38f, 0.35f);
    var shape = dust.shape;
    shape.shapeType = ParticleSystemShapeType.Cone;
    shape.angle = 35f;
    shape.radius = 0.3f;
    shape.rotation = new Vector3(-60f, 180f, 0f);
    var col = dust.colorOverLifetime;
    col.enabled = true;
    col.color = fade(1f, 0.6f);
    var size = dust.sizeOverLifetime;
    size.enabled = true;
    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
}
dust.transform.localPosition = Vector3.zero;
var wso = new SerializedObject(wheelFx);
wso.FindProperty("skidClip").objectReferenceValue = clip("Tyre_Skid");
wso.FindProperty("roadClip").objectReferenceValue = clip("Tyre_Road");
wso.FindProperty("gravelClip").objectReferenceValue = clip("Tyre_Gravel");
wso.FindProperty("grassClip").objectReferenceValue = clip("Tyre_Grass");
wso.FindProperty("audioOutput").objectReferenceValue = carMix;
wso.FindProperty("dustTemplate").objectReferenceValue = dust;
wso.ApplyModifiedPropertiesWithoutUndo();

// --- Удары кузова (машина игроков; враги — BuildEnemyFx) ---
var shards = particles(car.transform, "GlassShards", fxMat("Fx_GlassShard"));
var sparks = particles(car.transform, "ImpactSparks", findMat("MetalSparks"));
foreach (var ps in new[] { shards, sparks }) {
    var main = ps.main;
    bool glass = ps == shards;
    main.startLifetime = glass ? new ParticleSystem.MinMaxCurve(1f, 1.8f) : new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
    main.startSize = glass ? new ParticleSystem.MinMaxCurve(0.03f, 0.08f) : new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
    main.startSpeed = glass ? new ParticleSystem.MinMaxCurve(2f, 6f) : new ParticleSystem.MinMaxCurve(6f, 14f);
    main.gravityModifier = glass ? 1.5f : 1f;
    main.maxParticles = 200;
    main.startColor = glass ? new Color(0.85f, 0.95f, 1f, 1f) : new Color(1f, 0.7f, 0.3f, 1f);
    var shape = ps.shape;
    shape.shapeType = ParticleSystemShapeType.Sphere;
    shape.radius = 0.25f;
    var collision = ps.collision;
    collision.enabled = true;
    collision.type = ParticleSystemCollisionType.World;
    collision.bounce = glass ? 0.3f : 0.4f;
    collision.dampen = 0.4f;
    collision.lifetimeLoss = glass ? 0.1f : 0.3f;
    var renderer = ps.GetComponent<ParticleSystemRenderer>();
    renderer.renderMode = glass ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Stretch;
    renderer.velocityScale = glass ? 0f : 0.04f;
    renderer.lengthScale = glass ? 1f : 2f;
}
var impact = car.GetComponent<RacingProject.Audio.CarImpactAudio>();
if (impact == null) impact = car.AddComponent<RacingProject.Audio.CarImpactAudio>();
var iso = new SerializedObject(impact);
iso.FindProperty("scrapeClip").objectReferenceValue = clip("Scrape");
iso.FindProperty("output").objectReferenceValue = carMix;
iso.FindProperty("glassShards").objectReferenceValue = shards;
iso.FindProperty("impactSparks").objectReferenceValue = sparks;
iso.ApplyModifiedPropertiesWithoutUndo();

// --- Ветер и сердцебиение ---
var ambience = car.GetComponent<RacingProject.Audio.CarAmbienceAudio>();
if (ambience == null) ambience = car.AddComponent<RacingProject.Audio.CarAmbienceAudio>();
var aso = new SerializedObject(ambience);
aso.FindProperty("health").objectReferenceValue = health;
aso.FindProperty("output").objectReferenceValue = carMix;
aso.FindProperty("windClip").objectReferenceValue = clip("Wind_Rush");
aso.FindProperty("heartbeatClip").objectReferenceValue = clip("Heartbeat");
aso.ApplyModifiedPropertiesWithoutUndo();

// --- Пост-эффекты по событиям ---
var postGo = child(general, "PostFxEvents").gameObject;
var post = postGo.GetComponent<RacingProject.Effects.PostFxEvents>();
if (post == null) post = postGo.AddComponent<RacingProject.Effects.PostFxEvents>();
var pso = new SerializedObject(post);
pso.FindProperty("nitro").objectReferenceValue = car.GetComponent<RacingProject.Car.CarNitro>();
pso.FindProperty("health").objectReferenceValue = health;
pso.ApplyModifiedPropertiesWithoutUndo();

// --- HUD: стрелки на врагов за экраном, шрифты волн ---
var hud = general.Find("Hud").gameObject;
if (hud.GetComponent<RacingProject.Hud.OffscreenEnemyIndicators>() == null) hud.AddComponent<RacingProject.Hud.OffscreenEnemyIndicators>();
var waveHud = hud.GetComponent<RacingProject.Hud.WaveHud>();
var hso = new SerializedObject(waveHud);
hso.FindProperty("titleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Content/BlackOpsOne-Regular.asset");
hso.FindProperty("textFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
hso.ApplyModifiedPropertiesWithoutUndo();

// --- Погода ---
var weatherGo = child(general, "Weather").gameObject;
var weather = weatherGo.GetComponent<RacingProject.Effects.WeatherController>();
if (weather == null) weather = weatherGo.AddComponent<RacingProject.Effects.WeatherController>();
var rain = particles(weatherGo.transform, "Rain", fxMat("Fx_Rain"));
{
    rain.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    var main = rain.main;
    main.startLifetime = 1.3f;
    main.startSpeed = new ParticleSystem.MinMaxCurve(20f, 24f);
    main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
    main.maxParticles = 14000;
    main.startColor = new Color(0.8f, 0.85f, 0.9f, 0.5f);
    var shape = rain.shape;
    shape.shapeType = ParticleSystemShapeType.Box;
    shape.scale = new Vector3(60f, 60f, 1f);
    var renderer = rain.GetComponent<ParticleSystemRenderer>();
    renderer.renderMode = ParticleSystemRenderMode.Stretch;
    renderer.velocityScale = 0.035f;
    renderer.lengthScale = 1f;
    var collision = rain.collision;
    collision.enabled = false;
}
var wet = new System.Collections.Generic.List<Material>();
foreach (string name in new[] { "Road_Asphalt", "Road_AsphaltWorn", "Road_Gravel", "Road_Sidewalk", "Road_Curb", "Road_PaintCity", "Road_PaintWorn" }) {
    var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/Roads/Materials/" + name + ".mat");
    if (m != null) wet.Add(m);
}
var wtso = new SerializedObject(weather);
wtso.FindProperty("room").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RacingProject.Network.RoomController>(FindObjectsInactive.Include);
wtso.FindProperty("rain").objectReferenceValue = rain;
var wetProp = wtso.FindProperty("wetMaterials");
wetProp.arraySize = wet.Count;
for (int i = 0; i < wet.Count; i++) wetProp.GetArrayElementAtIndex(i).objectReferenceValue = wet[i];
wtso.FindProperty("rainClip").objectReferenceValue = clip("Rain");
wtso.FindProperty("roofClip").objectReferenceValue = clip("Rain_Roof");
wtso.FindProperty("output").objectReferenceValue = ambientMix;
wtso.FindProperty("sun").objectReferenceValue = general.Find("Directional Light").GetComponent<Light>();
wtso.ApplyModifiedPropertiesWithoutUndo();

// --- Зоны эха ---
var env = GameObject.Find("Environment").transform;
var zones = child(env, "ReverbZones");
for (int i = zones.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(zones.GetChild(i).gameObject);
// имя, x, z, внутренний и внешний радиус, пресет
var zoneSpecs = new object[][] {
    new object[] { "City1_South", 650f, 220f, 35f, 80f, AudioReverbPreset.City },
    new object[] { "City1_Center", 670f, 400f, 35f, 80f, AudioReverbPreset.City },
    new object[] { "City1_North", 680f, 580f, 35f, 80f, AudioReverbPreset.City },
    new object[] { "City2_SW", 1560f, 580f, 45f, 100f, AudioReverbPreset.City },
    new object[] { "City2_SE", 1760f, 580f, 45f, 100f, AudioReverbPreset.City },
    new object[] { "City2_W", 1560f, 770f, 45f, 100f, AudioReverbPreset.City },
    new object[] { "City2_E", 1760f, 770f, 45f, 100f, AudioReverbPreset.City },
    new object[] { "City2_NW", 1560f, 960f, 45f, 100f, AudioReverbPreset.City },
    new object[] { "City2_NE", 1760f, 960f, 45f, 100f, AudioReverbPreset.City },
    new object[] { "Yard_01", 523f, 640f, 25f, 55f, AudioReverbPreset.Forest },
    new object[] { "Yard_02", 533f, 503f, 25f, 55f, AudioReverbPreset.Forest },
    new object[] { "Yard_03", 505f, 355f, 25f, 55f, AudioReverbPreset.Forest },
    new object[] { "Yard_04", 500f, 223f, 20f, 45f, AudioReverbPreset.Forest },
    new object[] { "Garages", 854f, 410f, 15f, 35f, AudioReverbPreset.Alley },
    new object[] { "Industrial", 1076f, 225f, 25f, 60f, AudioReverbPreset.ParkingLot },
    new object[] { "OldHangar", 1043f, 264f, 20f, 40f, AudioReverbPreset.Hangar },
};
foreach (var z in zoneSpecs) {
    var go = new GameObject((string)z[0]);
    go.transform.SetParent(zones, false);
    var p = new Vector3((float)z[1], 0f, (float)z[2]);
    p.y = groundY(p) + 2f;
    go.transform.position = p;
    var zone = go.AddComponent<AudioReverbZone>();
    zone.minDistance = (float)z[3];
    zone.maxDistance = (float)z[4];
    zone.reverbPreset = (AudioReverbPreset)z[5];
}
log.Append("zones " + zoneSpecs.Length + "; ");

// --- Прожекторы КПП ---
var booth = GameObject.Find("Checkpoint").transform.Find("GuardBooth");
var lights = child(booth, "Searchlights");
lights.localPosition = Vector3.zero;
lights.localRotation = Quaternion.identity;
// Верх будки — по её рендерерам (без самих прожекторов)
float roof = booth.position.y;
foreach (var r in booth.GetComponentsInChildren<Renderer>())
    if (!r.transform.IsChildOf(lights)) roof = Mathf.Max(roof, r.bounds.max.y);
for (int i = 0; i < 2; i++) {
    var t = child(lights, "Searchlight_" + i);
    // Над крышей будки, по её краям
    t.position = new Vector3(booth.position.x, roof + 1.2f, booth.position.z) + booth.right * (i == 0 ? -1.8f : 1.8f);
    t.rotation = Quaternion.LookRotation(booth.forward * (i == 0 ? 1f : -1f), Vector3.up);
    var light = t.GetComponent<Light>();
    if (light == null) light = t.gameObject.AddComponent<Light>();
    light.type = LightType.Spot;
    light.spotAngle = 16f;
    light.innerSpotAngle = 8f;
    light.range = 90f;
    light.intensity = 60f;
    light.color = new Color(1f, 0.96f, 0.86f);
    light.shadows = i == 0 ? LightShadows.Hard : LightShadows.None;
    var search = t.GetComponent<RacingProject.Effects.Searchlight>();
    if (search == null) search = t.gameObject.AddComponent<RacingProject.Effects.Searchlight>();
    var sso = new SerializedObject(search);
    sso.FindProperty("spot").objectReferenceValue = light;
    sso.FindProperty("beamMaterial").objectReferenceValue = fxMat("Fx_Beam");
    sso.FindProperty("phase").floatValue = i * 4.3f;
    sso.FindProperty("sweep").floatValue = 150f;
    sso.FindProperty("pitch").floatValue = 14f;
    sso.FindProperty("beamLength").floatValue = 80f;
    sso.FindProperty("beamRadius").floatValue = 11f;
    sso.FindProperty("beamAlpha").floatValue = 0.25f;
    sso.ApplyModifiedPropertiesWithoutUndo();
}

// --- Пожары на горизонте: огонь, столб дыма, зарево сквозь туман и мерцающий свет ---
var fires = child(env, "HorizonFires");
for (int i = fires.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(fires.GetChild(i).gameObject);
var fireMat = fxMat("Fx_Fire");
var smokeMat = fxMat("Fx_FireSmoke");
var glowMat = fxMat("Fx_GlowNoFog");
var center = new Vector3(1112f, 0f, 984f);
float[] angles = { 20f, 75f, 140f, 200f, 255f, 320f };
for (int i = 0; i < angles.Length; i++) {
    float a = angles[i] * Mathf.Deg2Rad;
    var p = center + new Vector3(Mathf.Cos(a) * 920f, 0f, Mathf.Sin(a) * 1000f);
    p.y = groundY(p);
    var root = new GameObject("HorizonFire_" + i).transform;
    root.SetParent(fires, false);
    root.position = p;
    float scale = 0.8f + 0.4f * ((i * 37) % 10) / 10f;

    var fire = particles(root, "Fire", fireMat);
    var fm = fire.main;
    fm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
    fm.startSize = new ParticleSystem.MinMaxCurve(14f * scale, 26f * scale);
    fm.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
    fm.startColor = new Color(1f, 0.6f, 0.25f, 0.9f);
    fm.maxParticles = 80;
    var fe = fire.emission; fe.rateOverTime = 22f;
    var fs = fire.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 12f; fs.radius = 10f * scale; fs.rotation = new Vector3(-90f, 0f, 0f);
    var fc = fire.colorOverLifetime; fc.enabled = true; fc.color = fade(1f, 0.5f);

    var smoke = particles(root, "Smoke", smokeMat);
    var sm = smoke.main;
    sm.startLifetime = new ParticleSystem.MinMaxCurve(14f, 20f);
    sm.startSize = new ParticleSystem.MinMaxCurve(30f * scale, 55f * scale);
    sm.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f);
    sm.startColor = new Color(0.12f, 0.11f, 0.1f, 0.7f);
    sm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
    sm.maxParticles = 120;
    var se = smoke.emission; se.rateOverTime = 4f;
    var ss = smoke.shape; ss.shapeType = ParticleSystemShapeType.Cone; ss.angle = 6f; ss.radius = 8f * scale; ss.rotation = new Vector3(-90f, 0f, 0f);
    var sv = smoke.velocityOverLifetime; sv.enabled = true; sv.space = ParticleSystemSimulationSpace.World;
    sv.x = new ParticleSystem.MinMaxCurve(1.5f, 3f); sv.y = new ParticleSystem.MinMaxCurve(0f, 0f); sv.z = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
    var sc = smoke.colorOverLifetime; sc.enabled = true; sc.color = fade(1f, 0.7f);
    var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.2f));

    var glow = particles(root, "Glow", glowMat);
    var gm = glow.main;
    gm.startLifetime = 6f;
    gm.startSpeed = 0f;
    gm.startSize = new ParticleSystem.MinMaxCurve(220f * scale, 300f * scale);
    gm.startColor = new Color(1f, 0.45f, 0.15f, 0.22f);
    gm.maxParticles = 4;
    var ge = glow.emission; ge.rateOverTime = 0.6f;
    var gs = glow.shape; gs.enabled = false;
    var gc = glow.colorOverLifetime; gc.enabled = true; gc.color = fade(1f, 1f);
    glow.transform.localPosition = Vector3.up * 30f;

    var lightGo = child(root, "Light");
    lightGo.localPosition = Vector3.up * 12f;
    var fl = lightGo.gameObject.AddComponent<Light>();
    fl.type = LightType.Point;
    fl.range = 160f;
    fl.intensity = 9f;
    fl.color = new Color(1f, 0.5f, 0.2f);
    fl.shadows = LightShadows.None;
    lightGo.gameObject.AddComponent<RacingProject.Effects.FireFlicker>();
}
log.Append("fires " + angles.Length + "; ");

var scene = car.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "ok: " + log;
