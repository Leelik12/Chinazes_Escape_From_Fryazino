// События карты (Assets/Scripts/Enemy/MapEvents.cs): компонент на объекте EnemyManager, ссылки на машину игроков,
// рацию, ремкомплекты и ящики выстрелов к РПГ. Нужны ЛЭП (PowerLines) и дороги (RoadNetwork) в сцене.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var manager = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.EnemyManager>();
if (manager == null) throw new System.Exception("no EnemyManager");
var events = manager.GetComponent<RacingProject.Enemy.MapEvents>();
if (events == null) events = manager.gameObject.AddComponent<RacingProject.Enemy.MapEvents>();
var managerSo = new SerializedObject(manager);
var so = new SerializedObject(events);
so.FindProperty("enemyManager").objectReferenceValue = manager;
so.FindProperty("carHealth").objectReferenceValue = managerSo.FindProperty("carHealth").objectReferenceValue;
so.FindProperty("repairKits").objectReferenceValue = managerSo.FindProperty("repairKits").objectReferenceValue;
so.FindProperty("radio").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.RadioChatter>();
so.FindProperty("rocketCrates").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RacingProject.Pickups.RocketCrates>();
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(manager.gameObject.scene);
return "map events: crates " + so.FindProperty("rocketCrates").objectReferenceValue + " kits " + so.FindProperty("repairKits").objectReferenceValue;
