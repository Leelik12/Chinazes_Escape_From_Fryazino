// Кнопка «Продолжить: волна N» в главном меню (SovietCity, Menu/.../MenuList): копия кнопки «Одиночная игра»
// над ней, вызывает LanLobby.OnContinuePressed; LanLobby показывает её, только если есть сохранение забега (RunSave).
// Запуск: execute_code (MCP for Unity, C# 6) вне Play. Повторный запуск пересобирает кнопку
var lobby = UnityEngine.Object.FindFirstObjectByType<RacingProject.Network.LanLobby>(FindObjectsInactive.Include);
var so = new SerializedObject(lobby);
var solo = (GameObject)so.FindProperty("soloButton").objectReferenceValue;
var list = solo.transform.parent;
var old = list.Find("Continue"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var go = UnityEngine.Object.Instantiate(solo, list); go.name = "Continue";
go.transform.SetSiblingIndex(solo.transform.GetSiblingIndex());
var button = go.GetComponent<UnityEngine.UI.Button>();
UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, new UnityEngine.Events.UnityAction(lobby.OnContinuePressed));
var label = go.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>();
label.text = "Продолжить";
go.SetActive(false);
// Шесть строк меню должны уместиться между линиями (163 px): строки по 25 px вместо 29
foreach (Transform row in list) {
    var le = row.GetComponent<UnityEngine.UI.LayoutElement>();
    if (le != null) { le.minHeight = 25f; le.preferredHeight = 25f; }
}
var listRect = (RectTransform)list; listRect.sizeDelta = new Vector2(listRect.sizeDelta.x, 6f * 26f);
so.FindProperty("continueButton").objectReferenceValue = go;
so.FindProperty("continueButtonLabel").objectReferenceValue = label;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(lobby.gameObject.scene);
return "continue button ok, persistent " + button.onClick.GetPersistentEventCount() + " " + button.onClick.GetPersistentMethodName(0);
