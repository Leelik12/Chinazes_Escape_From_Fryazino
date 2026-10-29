// Повтор перед гибелью (SovietCity): DeathReplay на объекте экрана гибели (DeathScreen), с той же машиной и шрифтом.
// Запуск: execute_code (MCP for Unity, C# 6) вне Play. Повторный запуск только обновляет ссылки
var screen = UnityEngine.Object.FindFirstObjectByType<RacingProject.Management.DeathScreen>(FindObjectsInactive.Include);
var replay = screen.GetComponent<RacingProject.Management.DeathReplay>();
if (replay == null) replay = screen.gameObject.AddComponent<RacingProject.Management.DeathReplay>();
var src = new SerializedObject(screen);
var so = new SerializedObject(replay);
so.FindProperty("carHealth").objectReferenceValue = src.FindProperty("carHealth").objectReferenceValue;
so.FindProperty("font").objectReferenceValue = src.FindProperty("font").objectReferenceValue;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(screen.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(screen.gameObject.scene);
return "death replay on " + screen.name + ", car " + (so.FindProperty("carHealth").objectReferenceValue != null);
