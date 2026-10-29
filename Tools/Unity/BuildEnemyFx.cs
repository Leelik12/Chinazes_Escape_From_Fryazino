// Префабы: звуки ударов и осколки у вражеских машин (CarImpactAudio), яркая вспышка и горящие обломки
// у взрыва (EnemyExplosion), заметный след ракеты РПГ (EnemyRocket — её же пускает РПГ в обучении).
// Запуск: execute_code (MCP for Unity, C# 6) вне Play после BuildFxAssets.cs. Повторный запуск ничего не дублирует
var log = new System.Text.StringBuilder();
System.Func<string, Material> fxMat = name => AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/" + name + ".mat");
System.Func<string, Material> findMat = name => {
    foreach (string guid in AssetDatabase.FindAssets(name + " t:Material")) {
        string p = AssetDatabase.GUIDToAssetPath(guid);
        if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
    }
    return null;
};
System.Func<Transform, string, Transform> child = (parent, name) => {
    var t = parent.Find(name);
    if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
    return t;
};
System.Func<Transform, string, Material, ParticleSystem> particles = (parent, name, material) => {
    var t = child(parent, name);
    var ps = t.GetComponent<ParticleSystem>();
    if (ps == null) ps = t.gameObject.AddComponent<ParticleSystem>();
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
// Осколки стекла и искры ударов — как на машине игроков (BuildAudioVisual.cs)
System.Func<ParticleSystem, bool, ParticleSystem> impactFx = (ps, glass) => {
    var main = ps.main;
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
    return ps;
};
var carMix = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Content/Sound/CarMixer.mixer").FindMatchingGroups("Master")[0];
var scrape = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Content/Sound/Generated/Vehicle/Scrape.wav");
var sparksMat = findMat("MetalSparks");

foreach (string path in new[] { "Assets/Resources/EnemySedan.prefab", "Assets/Prefabs/Enemy/EnemyUaz.prefab", "Assets/Prefabs/Enemy/EnemyUral.prefab", "Assets/Prefabs/Enemy/EnemyBoss.prefab" }) {
    var root = PrefabUtility.LoadPrefabContents(path);
    var shards = impactFx(particles(root.transform, "GlassShards", fxMat("Fx_GlassShard")), true);
    var sparks = impactFx(particles(root.transform, "ImpactSparks", sparksMat), false);
    var impact = root.GetComponent<RacingProject.Audio.CarImpactAudio>();
    if (impact == null) impact = root.AddComponent<RacingProject.Audio.CarImpactAudio>();
    var so = new SerializedObject(impact);
    so.FindProperty("scrapeClip").objectReferenceValue = scrape;
    so.FindProperty("output").objectReferenceValue = carMix;
    so.FindProperty("glassShards").objectReferenceValue = shards;
    so.FindProperty("impactSparks").objectReferenceValue = sparks;
    so.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
    log.Append(System.IO.Path.GetFileNameWithoutExtension(path) + " ");
}

// Взрыв: вспышка освещает дома вокруг, горящие обломки летят с огненным следом
{
    string path = "Assets/Prefabs/Effects/EnemyExplosion.prefab";
    var root = PrefabUtility.LoadPrefabContents(path);
    var flash = root.GetComponentInChildren<Light>(true);
    flash.intensity = 30f;
    flash.range = 40f;
    var embers = particles(root.transform, "Embers", fxMat("Fx_Fire"));
    var main = embers.main;
    main.loop = false;
    main.duration = 1f;
    main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.8f);
    main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 17f);
    main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
    main.startColor = new Color(1f, 0.6f, 0.25f, 1f);
    main.gravityModifier = 1.3f;
    main.maxParticles = 20;
    var emission = embers.emission;
    emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 7, 11) });
    var shape = embers.shape;
    shape.shapeType = ParticleSystemShapeType.Hemisphere;
    shape.radius = 0.8f;
    shape.rotation = new Vector3(-90f, 0f, 0f);
    var collision = embers.collision;
    collision.enabled = true;
    collision.type = ParticleSystemCollisionType.World;
    collision.bounce = 0.25f;
    collision.dampen = 0.5f;
    var trails = embers.trails;
    trails.enabled = true;
    trails.ratio = 1f;
    trails.lifetime = new ParticleSystem.MinMaxCurve(0.35f);
    trails.minVertexDistance = 0.3f;
    trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
    trails.dieWithParticles = false;
    var renderer = embers.GetComponent<ParticleSystemRenderer>();
    renderer.trailMaterial = fxMat("Fx_Fire");
    var size = embers.sizeOverLifetime;
    size.enabled = true;
    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
    log.Append("explosion ");
}

// Ракета: крупнее пламя, ярче свет, светящийся след за ней
{
    string path = "Assets/Prefabs/Enemy/EnemyRocket.prefab";
    var root = PrefabUtility.LoadPrefabContents(path);
    var flame = root.transform.Find("Flame").GetComponent<ParticleSystem>();
    var fm = flame.main;
    fm.startSize = 0.9f;
    var light = root.GetComponentInChildren<Light>(true);
    light.intensity = 8f;
    light.range = 14f;
    var trail = root.transform.Find("Trail").GetComponent<ParticleSystem>();
    var tm = trail.main;
    tm.startColor = new Color(0.85f, 0.84f, 0.82f, 0.7f);
    var glow = particles(root.transform, "Glow", fxMat("Fx_Fire"));
    var gm = glow.main;
    gm.startLifetime = 0.35f;
    gm.startSpeed = 0f;
    gm.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
    gm.startColor = new Color(1f, 0.65f, 0.3f, 0.9f);
    gm.maxParticles = 200;
    var ge = glow.emission;
    ge.rateOverDistance = 10f;
    var gs = glow.shape;
    gs.enabled = false;
    var gsz = glow.sizeOverLifetime;
    gsz.enabled = true;
    gsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.1f));
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
    log.Append("rocket ");
}
AssetDatabase.SaveAssets();
return "ok: " + log;
