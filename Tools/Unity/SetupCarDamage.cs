// Повреждения машин: вмятины (CarDeformation) на машине игроков (VolgaCar в сцене) и на префабах врагов,
// урон машине игроков в авариях (CarCrashDamage) и ссылка на прочность для слабеющего мотора.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск безопасен: компоненты не дублируются, списки сеток переписываются.
// Мнутся только видимые части кузова (без колёс и оружия); моделям без Read/Write он включается в импорте.
var log = new System.Text.StringBuilder();
foreach (var model in new[] { "Assets/Models/UAZ/UAZ.fbx", "Assets/Models/Ural/Ural4320.fbx" }) {
    var mi = (ModelImporter)AssetImporter.GetAtPath(model);
    if (!mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); log.AppendLine("Read/Write: " + model); }
}
// Найти сетки кузова по путям от корня ("" — сам корень)
System.Func<Transform, string[], MeshFilter[]> filters = (root, paths) => {
    var list = new System.Collections.Generic.List<MeshFilter>();
    foreach (var p in paths) {
        var t = p == "" ? root : root.Find(p);
        var mf = t != null ? t.GetComponent<MeshFilter>() : null;
        if (mf == null) throw new System.Exception("Нет сетки " + root.name + "/" + p);
        list.Add(mf);
    }
    return list.ToArray();
};
System.Action<GameObject, string[]> setup = (go, paths) => {
    var d = go.GetComponent<RacingProject.Car.CarDeformation>();
    if (d == null) d = go.AddComponent<RacingProject.Car.CarDeformation>();
    var so = new SerializedObject(d);
    var arr = so.FindProperty("bodies");
    var mfs = filters(go.transform, paths);
    arr.arraySize = mfs.Length;
    for (int i = 0; i < mfs.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = mfs[i];
    // Глубокие вмятины; детали (острова сетки) отрываются по урону, настройки отрыва — значения по умолчанию в CarDeformation
    so.FindProperty("minImpactSpeed").floatValue = 3f;
    so.FindProperty("depthPerSpeed").floatValue = 0.045f;
    so.FindProperty("maxDepth").floatValue = 0.55f;
    so.FindProperty("minRadius").floatValue = 0.7f;
    so.FindProperty("maxRadius").floatValue = 1.8f;
    so.FindProperty("maxTotalDent").floatValue = 0.9f;
    so.ApplyModifiedPropertiesWithoutUndo();
    log.AppendLine(go.name + ": " + mfs.Length + " сеток");
};
System.Action<string, string[]> setupPrefab = (path, paths) => {
    var root = PrefabUtility.LoadPrefabContents(path);
    setup(root, paths);
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
};

var car = GameObject.Find("VolgaCar");
setup(car, new[] { "TahoeBody" });
// Урон машине игроков в жёстких авариях (CarCrashDamage) и прочность для слабеющего мотора (CarControllerSample)
var health = car.GetComponent<RacingProject.PlayerHealth>();
var crash = car.GetComponent<RacingProject.Car.CarCrashDamage>();
if (crash == null) crash = car.AddComponent<RacingProject.Car.CarCrashDamage>();
var cso = new SerializedObject(crash); cso.FindProperty("health").objectReferenceValue = health; cso.ApplyModifiedPropertiesWithoutUndo();
var ctl = new SerializedObject(car.GetComponent<RacingProject.Car.CarControllerSample>());
ctl.FindProperty("health").objectReferenceValue = health; ctl.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(car.scene);
// EnemyBoss — вариант EnemySedan и получает компонент от него
setupPrefab("Assets/Resources/EnemySedan.prefab", new[] { "", "SEDAN", "SEDAN/matte_plastic", "SEDAN/F_LAMPS", "SEDAN/R_LAMPS", "SEDAN/WINDOWS" });
setupPrefab("Assets/Prefabs/Enemy/EnemyUaz.prefab", new[] { "Model/uaz3909_body_low", "Model/backwall", "Model/uaz3909_glass", "Model/uaz3909_lights_high", "Model/uaz3909_reflector_low" });
setupPrefab("Assets/Prefabs/Enemy/EnemyUral.prefab", new[] { "Model" });
// Таран по-настоящему: удар корпусом ранит игроков и самого врага (EnemyRamDamage), седан (и босс) и УАЗ бьют бортом.
// Урал (таран) почти не страдает от своих ударов, лёгкие машины — наравне с игроками
System.Action<string, float, float, int, float, float, bool> ram = (path, minSpeed, perSpeed, maxDmg, push, selfScale, swipe) => {
    var root = PrefabUtility.LoadPrefabContents(path);
    var r = root.GetComponent<RacingProject.Enemy.EnemyRamDamage>();
    if (r == null) r = root.AddComponent<RacingProject.Enemy.EnemyRamDamage>();
    var so = new SerializedObject(r);
    so.FindProperty("minImpactSpeed").floatValue = minSpeed;
    so.FindProperty("damagePerSpeed").floatValue = perSpeed;
    so.FindProperty("maxDamage").intValue = maxDmg;
    so.FindProperty("pushPerSpeed").floatValue = push;
    so.FindProperty("selfDamageScale").floatValue = selfScale;
    so.ApplyModifiedPropertiesWithoutUndo();
    var ai = root.GetComponent<RacingProject.Enemy.EnemyCarController>();
    ai.sideSwipe = swipe;
    ai.ramBackOffTime = 2.5f; // тяжёлому Уралу нужно время, чтобы отъехать для нового разгона
    EditorUtility.SetDirty(ai);
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
    log.AppendLine("Таран: " + path);
};
ram("Assets/Prefabs/Enemy/EnemyUral.prefab", 5f, 14f, 320, 250f, 0.1f, false);
ram("Assets/Resources/EnemySedan.prefab", 3f, 18f, 150, 120f, 0.6f, true);
ram("Assets/Prefabs/Enemy/EnemyUaz.prefab", 3f, 20f, 180, 150f, 0.5f, true);
return log.ToString();
