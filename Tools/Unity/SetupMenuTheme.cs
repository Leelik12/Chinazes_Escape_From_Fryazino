// Две темы меню (MenuTheme на объекте Menu) и кнопка выбора темы в настройках.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск перенастраивает компонент и кнопку на месте. Кнопка — копия Main/Host без его обработчика.
var menu = GameObject.Find("Menu");
var canv = menu.transform.Find("Canvases");
var theme = menu.GetComponent<RacingProject.Management.MenuTheme>();
if (theme == null) theme = menu.AddComponent<RacingProject.Management.MenuTheme>();

// Кнопка «Стиль меню» справа от переключателей «Играть без VR» и «Низкая графика»
var settings = canv.Find("Settings");
var btn = settings.Find("MenuStyleButton");
if (btn == null) {
    var host = canv.Find("Main/Host").gameObject;
    btn = UnityEngine.Object.Instantiate(host, settings).transform; btn.name = "MenuStyleButton";
    var b = btn.GetComponent<UnityEngine.UI.Button>();
    for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEditor.Events.UnityEventTools.RemovePersistentListener(b.onClick, i);
}
var rt = btn.GetComponent<RectTransform>();
rt.anchoredPosition = new Vector2(190f, -149f); rt.sizeDelta = new Vector2(180f, 52f);
var label = btn.GetComponentInChildren<TMPro.TMP_Text>(); label.fontSize = 16f; label.text = "Стиль меню";

var blackOps = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Content/BlackOpsOne-Regular.asset");
var liberation = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
System.Func<string, Color> hex = s => { Color c; ColorUtility.TryParseHtmlString(s, out c); return c; };

var so = new SerializedObject(theme);
so.FindProperty("canvasesRoot").objectReferenceValue = canv;
so.FindProperty("room").objectReferenceValue = UnityEngine.Object.FindObjectOfType<RacingProject.Network.RoomController>();
so.FindProperty("accentButton").objectReferenceValue = canv.Find("Main/Host").GetComponent<UnityEngine.UI.Button>();
so.FindProperty("styleButton").objectReferenceValue = btn.GetComponent<UnityEngine.UI.Button>();
so.FindProperty("styleButtonLabel").objectReferenceValue = label;
System.Action<string, string, TMPro.TMP_FontAsset, bool, string[]> palette = (prop, name, font, upper, c) => {
    var p = so.FindProperty(prop);
    p.FindPropertyRelative("displayName").stringValue = name;
    p.FindPropertyRelative("font").objectReferenceValue = font;
    p.FindPropertyRelative("upperCase").boolValue = upper;
    var keys = new[] { "panel", "text", "button", "buttonText", "accentButton", "accentButtonText", "track", "fill", "ready", "notReady", "connected", "disconnected" };
    for (int i = 0; i < keys.Length; i++) p.FindPropertyRelative(keys[i]).colorValue = hex(c[i]);
};
// Терминал: тёмно-зелёный экран, люминофорный текст
palette("terminal", "терминал", blackOps, true, new[] { "#1B2116F0", "#97C459", "#27321C", "#97C459", "#3B6D11", "#C0DD97", "#27321C", "#97C459", "#C0DD97", "#EF9F27", "#97C459", "#E24B4A" });
// Панель: графит и оранжевый акцент
palette("panel", "панель", liberation, false, new[] { "#2C2C2AF0", "#F1EFE8", "#444441", "#F1EFE8", "#EF9F27", "#412402", "#444441", "#EF9F27", "#97C459", "#888780", "#97C459", "#E24B4A" });
var texts = so.FindProperty("texts"); texts.arraySize = 1;
var tv = texts.GetArrayElementAtIndex(0);
tv.FindPropertyRelative("target").objectReferenceValue = canv.Find("Main/Title").GetComponent<TMPro.TMP_Text>();
tv.FindPropertyRelative("terminal").stringValue = "> Escape From Fryazino_"; // прописными полное название не влезает в строку
tv.FindPropertyRelative("panel").stringValue = "Chinazes: Escape From Fryazino";
so.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.scene);
return "ok";
