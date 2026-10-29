// Вкладка «Гараж» в терминале настроек (SovietCity, Canvas «Settings»): пятая кнопка вкладки и страница GaragePage —
// строка талонов и по строке на улучшение Garage (название, деления уровня, кнопка с ценой). Строки — копии строк
// вкладки «Клавиши» (Tools/Unity/BuildKeysTab.cs, её нужно собрать раньше). Талоны и уровни — PlayerPrefs (Garage).
// Ещё сюда же: GarageLoadout на объекте EnemyManager — рассылает уровни гаража хоста на забег.
// Запуск: execute_code (MCP for Unity, C# 6) вне Play. Повторный запуск пересобирает вкладку
var menu = UnityEngine.Object.FindFirstObjectByType<RacingProject.Management.SettingsMenu>(FindObjectsInactive.Include);
var content = menu.transform.Find("Content");
var oldTab = content.Find("Tab4"); if (oldTab != null) UnityEngine.Object.DestroyImmediate(oldTab.gameObject);
var oldPage = content.Find("Page4"); if (oldPage != null) UnityEngine.Object.DestroyImmediate(oldPage.gameObject);

// Пять вкладок по 116 px
var tabs = new Transform[5];
for (int i = 0; i < 4; i++) tabs[i] = content.Find("Tab" + i);
var tab4 = UnityEngine.Object.Instantiate(tabs[3].gameObject, content); tab4.name = "Tab4"; tabs[4] = tab4.transform;
tab4.transform.SetSiblingIndex(content.Find("Page3").GetSiblingIndex() + 1);
for (int i = 0; i < 5; i++) {
    var r = (RectTransform)tabs[i];
    r.anchoredPosition = new Vector2(14f + i * 121.1f, r.anchoredPosition.y);
    r.sizeDelta = new Vector2(116f, r.sizeDelta.y);
}
tab4.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>().text = "Гараж";

// Страница: копия «Клавиш», первые строки становятся строками гаража, остальные удаляются
var page = UnityEngine.Object.Instantiate(content.Find("Page3").gameObject, content); page.name = "Page4";
page.transform.SetSiblingIndex(tab4.transform.GetSiblingIndex() + 1);
UnityEngine.Object.DestroyImmediate(page.GetComponent<RacingProject.Management.KeyBindingsPage>());
int perks = RacingProject.Management.Garage.PerkCount;
var keep = new System.Collections.Generic.List<Transform>();
var drop = new System.Collections.Generic.List<GameObject>();
foreach (Transform ch in page.transform) { if (keep.Count < perks + 1 && ch.name.StartsWith("Key")) keep.Add(ch); else drop.Add(ch.gameObject); }
foreach (var g in drop) UnityEngine.Object.DestroyImmediate(g);
if (keep.Count < perks + 1) throw new System.Exception("мало строк на вкладке «Клавиши»");

var hint = content.Find("Hint").GetComponent<TMPro.TMP_Text>();
var pageComp = page.AddComponent<RacingProject.Management.GaragePage>();
var so = new SerializedObject(pageComp);
var rowsProp = so.FindProperty("rows");
rowsProp.arraySize = perks;
for (int i = 0; i <= perks; i++) {
    bool coins = i == 0;
    var row = keep[i];
    row.name = coins ? "Coins" : "Perk" + (RacingProject.Management.Garage.Perk)(i - 1);
    ((RectTransform)row).anchoredPosition = new Vector2(0f, -(i * 30f + (coins ? 0f : 8f)));
    var label = row.Find("Label").GetComponent<TMPro.TMP_Text>();
    var value = row.Find("Value").GetComponent<TMPro.TMP_Text>();
    var lr = (RectTransform)label.transform; lr.sizeDelta = new Vector2(330f, lr.sizeDelta.y);
    var vr = (RectTransform)value.transform; vr.anchoredPosition = new Vector2(380f, vr.anchoredPosition.y); vr.sizeDelta = new Vector2(210f, vr.sizeDelta.y);
    var selector = row.GetComponent<RacingProject.Management.TerminalSelector>();
    var sso = new SerializedObject(selector);
    var button = value.GetComponent<UnityEngine.UI.Button>();
    if (coins) {
        label.text = "Талоны";
        value.text = "0 т.";
        // Талоны — просто число, не кнопка
        UnityEngine.Object.DestroyImmediate(value.GetComponent<RacingProject.Management.TerminalButtonFx>());
        UnityEngine.Object.DestroyImmediate(button);
        value.raycastTarget = false;
        sso.FindProperty("hint").stringValue = "Талоны дают за каждый забег: очки и пройденные волны. В кооперативе действует гараж хоста (водителя)";
        so.FindProperty("coins").objectReferenceValue = value;
    } else {
        var perk = (RacingProject.Management.Garage.Perk)(i - 1);
        label.text = RacingProject.Management.Garage.Title(perk);
        sso.FindProperty("hint").stringValue = RacingProject.Management.Garage.Description(perk);
        var e = rowsProp.GetArrayElementAtIndex(i - 1);
        e.FindPropertyRelative("perk").enumValueIndex = i - 1;
        e.FindPropertyRelative("label").objectReferenceValue = label;
        e.FindPropertyRelative("value").objectReferenceValue = value;
        e.FindPropertyRelative("button").objectReferenceValue = button;
    }
    sso.ApplyModifiedPropertiesWithoutUndo();
}
so.FindProperty("hint").objectReferenceValue = hint;
so.ApplyModifiedPropertiesWithoutUndo();
page.SetActive(false);

var mso = new SerializedObject(menu);
System.Action<string, UnityEngine.Object> append = (name, obj) => {
    var p = mso.FindProperty(name);
    if (p.arraySize < 5) p.arraySize = 5;
    p.GetArrayElementAtIndex(4).objectReferenceValue = obj;
};
append("tabButtons", tab4.GetComponent<UnityEngine.UI.Button>());
append("tabPages", page);
append("tabBackgrounds", tab4.transform.Find("Back").GetComponent<UnityEngine.UI.Graphic>());
append("tabLabels", tab4.transform.Find("Text (TMP)").GetComponent<UnityEngine.UI.Graphic>());
mso.ApplyModifiedPropertiesWithoutUndo();

// Уровни гаража хоста на забег
var manager = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.EnemyManager>();
if (manager.GetComponent<RacingProject.Management.GarageLoadout>() == null) manager.gameObject.AddComponent<RacingProject.Management.GarageLoadout>();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(menu.gameObject.scene);
return "garage tab ok: " + page.transform.childCount + " rows";
