// Перепечка NavMesh для машин врагов (Environment/NavMesh Surface, тип агента Car).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Поверхность собирает физические коллайдеры. Деревья террейна NavMeshSurface не видит, поэтому на время
// запекания под каждое дерево ставится временный BoxCollider, потом они удаляются.
// Дороги Road Architect помечены областью Road (стоимость 1) через NavMeshModifier на RoadArchitectSystem1,
// остальная земля — Walkable (стоимость 3), поэтому враги предпочитают дороги. Машина игроков из запекания исключена.
var surf = GameObject.Find("Environment/NavMesh Surface").GetComponent<Unity.AI.Navigation.NavMeshSurface>();
var terrain = Terrain.activeTerrain; var td = terrain.terrainData;
var tmp = new GameObject("TmpTreeObstacles");
foreach (var ti in td.treeInstances)
{
    var w = Vector3.Scale(ti.position, td.size) + terrain.transform.position;
    var go = new GameObject("t"); go.transform.SetParent(tmp.transform, false);
    go.transform.position = w + Vector3.up * 3f;
    var box = go.AddComponent<BoxCollider>(); box.size = new Vector3(1.4f, 6f, 1.4f) * Mathf.Max(0.6f, ti.widthScale);
}
var data = surf.navMeshData; string path = AssetDatabase.GetAssetPath(data);
var t = System.Diagnostics.Stopwatch.StartNew();
surf.BuildNavMesh();
UnityEngine.Object.DestroyImmediate(tmp);
// Новые данные копируем в существующий ассет, чтобы ссылка в сцене не менялась
var nd = surf.navMeshData;
if (data != null && nd != data) { EditorUtility.CopySerialized(nd, data); surf.navMeshData = data; surf.RemoveData(); surf.AddData(); }
EditorUtility.SetDirty(surf); AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(surf.gameObject.scene);
return "baked in " + t.Elapsed.TotalSeconds.ToString("0.0") + " s, trees " + td.treeInstances.Length + ", asset " + path;
