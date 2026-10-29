// Разрушаемые постройки-мелочь: в префабы добавляется BreakableProp (Assets/Scripts/Props/BreakableProp.cs) с порогом
// скорости удара, массой обломков и исходными мешами (статичный рендерер в игре склеен с соседями, куски режутся
// из исходного меша, поэтому у моделей включается Read/Write).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск обновляет настройки; после BuildLevelProps / BuildPrivateSector (пересоздают префабы) — запустить снова.
// Имя префаба: { порог удара м/с, доля скорости машины после пролома, масса обломков кг, размер куска м, есть стёкла (1/0) }
var props = new System.Collections.Generic.Dictionary<string, float[]> {
    { "Assets/Prefabs/Roadside/Kiosk.prefab",            new[] { 9f, 0.7f, 600f, 1.6f, 1f } },
    { "Assets/Prefabs/Roadside/Kiosk_Yellow.prefab",     new[] { 9f, 0.7f, 600f, 1.6f, 1f } },
    { "Assets/Prefabs/Roadside/BusStop.prefab",          new[] { 8f, 0.8f, 400f, 1.8f, 1f } },
    { "Assets/Prefabs/Roadside/GuardBooth.prefab",       new[] { 8f, 0.75f, 400f, 1.4f, 1f } },
    { "Assets/Prefabs/PrivateSector/Outhouse.prefab",    new[] { 5f, 0.9f, 150f, 1.2f, 0f } },
    { "Assets/Prefabs/PrivateSector/Shed.prefab",        new[] { 11f, 0.6f, 700f, 1.8f, 0f } },
    { "Assets/Prefabs/PrivateSector/Gate.prefab",        new[] { 6f, 0.85f, 250f, 1.5f, 0f } },
    { "Assets/Prefabs/LevelProps/WoodenPole.prefab",     new[] { 7f, 0.85f, 250f, 2.5f, 0f } },
    { "Assets/Prefabs/LevelProps/Billboard_0.prefab",    new[] { 11f, 0.7f, 600f, 3f, 0f } },
    { "Assets/Prefabs/LevelProps/Billboard_1.prefab",    new[] { 11f, 0.7f, 600f, 3f, 0f } },
    { "Assets/Prefabs/LevelProps/Billboard_2.prefab",    new[] { 11f, 0.7f, 600f, 3f, 0f } },
    { "Assets/Prefabs/LevelProps/Woodpile.prefab",       new[] { 7f, 0.8f, 400f, 0.9f, 0f } },
};
var log = new System.Text.StringBuilder();
foreach (var kv in props) {
    var root = PrefabUtility.LoadPrefabContents(kv.Key);
    var parts = new System.Collections.Generic.List<MeshFilter>();
    var meshes = new System.Collections.Generic.List<Mesh>();
    foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) {
        if (mf.sharedMesh == null || mf.name == "LOD1" || mf.name == "ShadowProxy") continue;
        if (mf.GetComponent<MeshRenderer>() == null) continue;
        string meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
        var imp = AssetImporter.GetAtPath(meshPath) as ModelImporter;
        if (imp != null && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
        parts.Add(mf); meshes.Add(mf.sharedMesh);
    }
    // Удары по коллайдерам дочерних объектов доходят до корня через BreakableContact
    foreach (var col in root.GetComponentsInChildren<Collider>(true))
        if (col.gameObject != root && col.GetComponent<RacingProject.Props.BreakableContact>() == null)
            col.gameObject.AddComponent<RacingProject.Props.BreakableContact>();
    var bp = root.GetComponent<RacingProject.Props.BreakableProp>();
    if (bp == null) bp = root.AddComponent<RacingProject.Props.BreakableProp>();
    var so = new SerializedObject(bp);
    var v = kv.Value;
    so.FindProperty("breakSpeed").floatValue = v[0];
    so.FindProperty("keepSpeed").floatValue = v[1];
    so.FindProperty("debrisMass").floatValue = v[2];
    so.FindProperty("chunkSize").floatValue = v[3];
    so.FindProperty("glass").boolValue = v[4] > 0f;
    var pp = so.FindProperty("parts"); pp.arraySize = parts.Count;
    var mp = so.FindProperty("sourceMeshes"); mp.arraySize = meshes.Count;
    for (int i = 0; i < parts.Count; i++) { pp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i]; mp.GetArrayElementAtIndex(i).objectReferenceValue = meshes[i]; }
    so.ApplyModifiedPropertiesWithoutUndo();
    PrefabUtility.SaveAsPrefabAsset(root, kv.Key);
    PrefabUtility.UnloadPrefabContents(root);
    log.Append(System.IO.Path.GetFileNameWithoutExtension(kv.Key)).Append(" parts=").Append(parts.Count).Append("; ");
}
int placed = UnityEngine.Object.FindObjectsByType<RacingProject.Props.BreakableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return log + " in scene " + placed;
