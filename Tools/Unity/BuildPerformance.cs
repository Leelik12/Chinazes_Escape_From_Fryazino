// Производительность сцены: на General ставится DistanceCulling (объекты слоя Default дальше 300 м не рисуются —
// за ночным туманом их и так не видно), а зарева пожаров на горизонте (Environment/HorizonFires) и ориентиры
// (Environment/Landmarks) переносятся на слой Landmark, который не отсекается. Мелкие пропсы наполнения карты
// (слой SmallProps, назначается в префабах Tools/Unity/BuildLevelProps.cs) отсекаются дальше 150 м.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
int landmark = LayerMask.NameToLayer("Landmark");
if (landmark < 0) throw new System.Exception("layer Landmark missing (ProjectSettings/TagManager)");
var general = GameObject.Find("General");
var culling = general.GetComponent<RacingProject.Effects.DistanceCulling>();
if (culling == null) culling = general.AddComponent<RacingProject.Effects.DistanceCulling>();
var so = new SerializedObject(culling);
so.FindProperty("cullDistance").floatValue = 300f;
so.FindProperty("cullLayers").intValue = 1 << 0;
int small = LayerMask.NameToLayer("SmallProps");
if (small < 0) throw new System.Exception("layer SmallProps missing (ProjectSettings/TagManager)");
so.FindProperty("smallDistance").floatValue = 150f;
so.FindProperty("smallLayers").intValue = 1 << small;
so.ApplyModifiedPropertiesWithoutUndo();
int moved = 0;
foreach (var path in new[] { "Environment/HorizonFires", "Environment/Landmarks" }) {
    var root = GameObject.Find(path);
    if (root == null) continue;
    foreach (var t in root.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = landmark; moved++; }
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(general.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(general.scene);
return "culling on, landmark objects " + moved;
