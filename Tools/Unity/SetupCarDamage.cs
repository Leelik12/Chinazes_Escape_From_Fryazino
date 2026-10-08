// Повреждения машин: вмятины (CarDeformation) на машине игроков (VolgaCar в сцене) и на префабах врагов.
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
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
// EnemyBoss — вариант EnemySedan и получает компонент от него
setupPrefab("Assets/Resources/EnemySedan.prefab", new[] { "", "SEDAN", "SEDAN/matte_plastic", "SEDAN/F_LAMPS", "SEDAN/R_LAMPS", "SEDAN/WINDOWS" });
setupPrefab("Assets/Prefabs/Enemy/EnemyUaz.prefab", new[] { "Model/uaz3909_body_low", "Model/backwall", "Model/uaz3909_glass", "Model/uaz3909_lights_high", "Model/uaz3909_reflector_low" });
setupPrefab("Assets/Prefabs/Enemy/EnemyUral.prefab", new[] { "Model" });
return log.ToString();
