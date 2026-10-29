// Вкладка «Клавиши» в терминале настроек (SovietCity, Canvas «Settings»): четвёртая кнопка вкладки и страница
// со строками всех действий GameKeys и кнопкой сброса. Строки сделаны из строки «ViewMode»: стрелки убраны, значение — кнопка.
// Запуск: execute_code (MCP for Unity, C# 6) вне Play, затем сохранить сцену. Повторный запуск пересобирает вкладку
var menu = UnityEngine.Object.FindFirstObjectByType<RacingProject.Management.SettingsMenu>(FindObjectsInactive.Include);
var content = menu.transform.Find("Content");
var oldTab = content.Find("Tab3"); if (oldTab != null) UnityEngine.Object.DestroyImmediate(oldTab.gameObject);
var oldPage = content.Find("Page3"); if (oldPage != null) UnityEngine.Object.DestroyImmediate(oldPage.gameObject);

// Четыре вкладки по 146 px вместо трёх по 150
var tabs = new Transform[4];
for (int i = 0; i < 3; i++) tabs[i] = content.Find("Tab" + i);
var tab3 = UnityEngine.Object.Instantiate(tabs[2].gameObject, content); tab3.name = "Tab3"; tabs[3] = tab3.transform;
tab3.transform.SetSiblingIndex(content.Find("Page2").GetSiblingIndex() + 1);
for (int i = 0; i < 4; i++) {
    var r = (RectTransform)tabs[i];
    r.anchoredPosition = new Vector2(14f + i * 151.5f, r.anchoredPosition.y);
    r.sizeDelta = new Vector2(146f, r.sizeDelta.y);
}
tab3.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>().text = "Клавиши";

// Страница: копия страницы «Экран» без строк
var page2 = content.Find("Page2");
var page = UnityEngine.Object.Instantiate(page2.gameObject, content); page.name = "Page3";
page.transform.SetSiblingIndex(tab3.transform.GetSiblingIndex() + 1);
var template = page.transform.Find("ViewMode").gameObject;
template.SetActive(false);
foreach (Transform ch in page.transform) if (ch.gameObject != template) ch.gameObject.SetActive(false);
var removeList = new System.Collections.Generic.List<GameObject>();
foreach (Transform ch in page.transform) if (ch.gameObject != template) removeList.Add(ch.gameObject);
foreach (var g in removeList) UnityEngine.Object.DestroyImmediate(g);

var hint = content.Find("Hint").GetComponent<TMPro.TMP_Text>();
var actions = (RacingProject.Management.GameAction[])System.Enum.GetValues(typeof(RacingProject.Management.GameAction));
string[] hints = {
    "Газ. Стрелка вверх работает всегда",
    "Тормоз; стоя на месте — задний ход. Стрелка вниз работает всегда",
    "Руль влево. Стрелка влево работает всегда",
    "Руль вправо. Стрелка вправо работает всегда",
    "Ручник: блокирует задние колёса, зад уходит в занос — разворот на месте",
    "Нитро: короткий мощный разгон",
    "Удерживайте, чтобы поставить перевёрнутую или застрявшую машину на колёса",
    "Фары и мигалка на крыше",
    "Магнитола: свои треки вместо музыки боя и обратно",
};
var pageComp = page.AddComponent<RacingProject.Management.KeyBindingsPage>();
var so = new SerializedObject(pageComp);
var rowsProp = so.FindProperty("rows");
rowsProp.arraySize = actions.Length;
// Девять и больше строк — шаг плотнее, чтобы кнопка сброса не наезжала на подсказку
float RowStep = actions.Length > 8 ? 24f : 26f;
for (int i = 0; i <= actions.Length; i++) {
    bool reset = i == actions.Length;
    var row = UnityEngine.Object.Instantiate(template, page.transform);
    row.SetActive(true);
    row.name = reset ? "Reset" : "Key" + actions[i];
    var rr = (RectTransform)row.transform;
    rr.anchoredPosition = new Vector2(0f, -(i * RowStep + (reset ? 8f : 0f)));
    var prev = row.transform.Find("Prev").gameObject; var next = row.transform.Find("Next").gameObject;
    var value = row.transform.Find("Value").GetComponent<TMPro.TMP_Text>();
    var label = row.transform.Find("Label").GetComponent<TMPro.TMP_Text>();
    // Кнопка на месте значения с эффектами терминала (звук и подсветка, как у стрелок)
    var vr = (RectTransform)value.transform;
    vr.anchoredPosition = new Vector2(190f, vr.anchoredPosition.y);
    vr.sizeDelta = new Vector2(220f, vr.sizeDelta.y);
    value.raycastTarget = true;
    var button = value.gameObject.AddComponent<UnityEngine.UI.Button>();
    button.targetGraphic = value;
    UnityEditorInternal.ComponentUtility.CopyComponent(prev.GetComponent<RacingProject.Management.TerminalButtonFx>());
    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(value.gameObject);
    UnityEngine.Object.DestroyImmediate(prev); UnityEngine.Object.DestroyImmediate(next);
    var selector = row.GetComponent<RacingProject.Management.TerminalSelector>();
    var sso = new SerializedObject(selector);
    sso.FindProperty("previous").objectReferenceValue = null;
    sso.FindProperty("next").objectReferenceValue = null;
    sso.FindProperty("hint").stringValue = reset ? "Вернуть клавиши по умолчанию: WASD, пробел, левый Shift, R, L" : hints[i];
    sso.ApplyModifiedPropertiesWithoutUndo();
    if (reset) {
        label.text = "Сбросить клавиши";
        value.text = "[ сбросить ]";
        so.FindProperty("resetButton").objectReferenceValue = button;
    } else {
        label.text = RacingProject.Management.GameKeys.Title(actions[i]);
        value.text = "[ " + RacingProject.Management.GameKeys.KeyName(RacingProject.Management.GameKeys.Default(actions[i])) + " ]";
        var e = rowsProp.GetArrayElementAtIndex(i);
        e.FindPropertyRelative("action").enumValueIndex = i;
        e.FindPropertyRelative("label").objectReferenceValue = label;
        e.FindPropertyRelative("value").objectReferenceValue = value;
        e.FindPropertyRelative("button").objectReferenceValue = button;
    }
}
UnityEngine.Object.DestroyImmediate(template);
so.FindProperty("hint").objectReferenceValue = hint;
so.ApplyModifiedPropertiesWithoutUndo();
page.SetActive(false);

// Вкладка подключается к меню настроек
var mso = new SerializedObject(menu);
System.Action<string, UnityEngine.Object> append = (name, obj) => {
    var p = mso.FindProperty(name);
    if (p.arraySize < 4) p.arraySize = 4;
    p.GetArrayElementAtIndex(3).objectReferenceValue = obj;
};
append("tabButtons", tab3.GetComponent<UnityEngine.UI.Button>());
append("tabPages", page);
append("tabBackgrounds", tab3.transform.Find("Back").GetComponent<UnityEngine.UI.Graphic>());
append("tabLabels", tab3.transform.Find("Text (TMP)").GetComponent<UnityEngine.UI.Graphic>());
mso.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
return "ok: " + page.transform.childCount + " rows";
