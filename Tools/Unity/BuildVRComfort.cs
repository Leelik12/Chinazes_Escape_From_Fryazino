// VR-комфорт и переподключение (SovietCity):
// - на обоих VR-ригах машины (стрелок, водитель) VRComfortRig (поворот плавный/рывками, затемнение краёв) и VRPauseMenu;
//   затемнение — полусфера TunnelingVignette из XRI Starter Assets на камере шлема, её контроллер XRI убран;
// - строки «Поворот в VR» и «Затемнение в VR» на вкладке «Экран» настроек (копии строки «Отладочная информация»);
// - кнопка «Переподключиться» в меню лобби (копия «Найти игру»).
// Запуск: execute_code (MCP for Unity, C# 6) вне Play; сцена сохраняется. Повторный запуск пересобирает добавленное
var car = UnityEngine.Object.FindFirstObjectByType<RacingProject.Car.CarControllerSample>(FindObjectsInactive.Include).transform;
var vignettePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.0.8/Starter Assets/TunnelingVignette/TunnelingVignette.prefab");
if (vignettePrefab == null) return "no vignette prefab";
var log = new System.Text.StringBuilder();

foreach (var origin in UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
    if (!origin.transform.IsChildOf(car)) continue;
    var cam = origin.Camera;
    var old = cam.transform.Find("TunnelingVignette"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    var vignette = (GameObject)PrefabUtility.InstantiatePrefab(vignettePrefab, cam.transform);
    PrefabUtility.UnpackPrefabInstance(vignette, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    vignette.name = "TunnelingVignette";
    vignette.transform.localPosition = Vector3.zero; vignette.transform.localRotation = Quaternion.identity;
    foreach (var mb in vignette.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(mb);
    var renderer = vignette.GetComponentInChildren<Renderer>(true);
    renderer.enabled = false;

    var rig = origin.GetComponent<RacingProject.Management.VRComfortRig>();
    if (rig == null) rig = origin.gameObject.AddComponent<RacingProject.Management.VRComfortRig>();
    var turn = origin.transform.Find("Camera Offset/Turn");
    var so = new SerializedObject(rig);
    so.FindProperty("smoothTurn").objectReferenceValue = turn != null ? turn.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.ContinuousTurnProvider>() : null;
    so.FindProperty("snapTurn").objectReferenceValue = turn != null ? turn.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>() : null;
    so.FindProperty("vignette").objectReferenceValue = renderer;
    so.FindProperty("vehicle").objectReferenceValue = car;
    so.ApplyModifiedPropertiesWithoutUndo();

    var pause = origin.GetComponent<RacingProject.Management.VRPauseMenu>();
    if (pause == null) pause = origin.gameObject.AddComponent<RacingProject.Management.VRPauseMenu>();
    var pso = new SerializedObject(pause);
    pso.FindProperty("head").objectReferenceValue = cam;
    pso.ApplyModifiedPropertiesWithoutUndo();
    log.Append(origin.name + " turn " + (turn != null) + "; ");
}

// Настройки: две строки под «Отладочной информацией»
var menu = UnityEngine.Object.FindFirstObjectByType<RacingProject.Management.SettingsMenu>(FindObjectsInactive.Include);
var page = menu.transform.Find("Content/Page2");
var template = page.Find("DebugInfo");
string[] names = { "VRTurn", "VRVignette" };
string[] labels = { "Поворот в VR", "Затемнение в VR" };
string[] hints = {
    "Поворот стиком в шлеме: плавный или рывками по 45°. Рывками меньше укачивает",
    "Края обзора темнеют, когда машину резко разгоняет, тормозит или разворачивает. Снижает укачивание",
};
string[] fields = { "vrTurn", "vrVignette" };
var mso = new SerializedObject(menu);
for (int i = 0; i < 2; i++) {
    var old = page.Find(names[i]); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    var row = UnityEngine.Object.Instantiate(template.gameObject, page); row.name = names[i];
    var r = (RectTransform)row.transform; var tr = (RectTransform)template;
    r.anchoredPosition = tr.anchoredPosition + new Vector2(0f, -28f * (i + 1));
    row.transform.Find("Label").GetComponent<TMPro.TMP_Text>().text = labels[i];
    var sel = row.GetComponent<RacingProject.Management.TerminalSelector>();
    var sso = new SerializedObject(sel); sso.FindProperty("hint").stringValue = hints[i]; sso.ApplyModifiedPropertiesWithoutUndo();
    mso.FindProperty(fields[i]).objectReferenceValue = sel;
}
mso.ApplyModifiedPropertiesWithoutUndo();

// Лобби: «Переподключиться» сразу под «Найти игру», скрыта до обрыва связи
var lobby = UnityEngine.Object.FindFirstObjectByType<RacingProject.Network.LanLobby>(FindObjectsInactive.Include);
var lso = new SerializedObject(lobby);
var join = (GameObject)lso.FindProperty("joinButton").objectReferenceValue;
var oldReconnect = join.transform.parent.Find("Reconnect"); if (oldReconnect != null) UnityEngine.Object.DestroyImmediate(oldReconnect.gameObject);
var reconnect = UnityEngine.Object.Instantiate(join, join.transform.parent); reconnect.name = "Reconnect";
reconnect.transform.SetSiblingIndex(join.transform.GetSiblingIndex() + 1);
reconnect.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>().text = "Переподключиться";
var button = reconnect.GetComponent<UnityEngine.UI.Button>();
while (button.onClick.GetPersistentEventCount() > 0) UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, 0);
UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, lobby.OnReconnectPressed);
reconnect.SetActive(false);
lso.FindProperty("reconnectButton").objectReferenceValue = reconnect;
lso.ApplyModifiedPropertiesWithoutUndo();

var scene = car.gameObject.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "ok: " + log;
