// Достижения (SovietCity): компонент Achievements на объекте EnemyManager и вкладка «Награды» в терминале настроек
// (Canvas «Settings») — шестая кнопка вкладки и страница AchievementsPage: строка «получено N из M» и по строке
// на достижение. Строки — копии строк вкладки «Клавиши» (Tools/Unity/BuildKeysTab.cs), вкладка «Гараж»
// (Tools/Unity/BuildGarageTab.cs) должна быть собрана раньше: шесть вкладок ужимаются до 96 px.
// Запуск: execute_code (MCP for Unity, C# 6) вне Play. Повторный запуск пересобирает вкладку
var menu = UnityEngine.Object.FindFirstObjectByType<RacingProject.Management.SettingsMenu>(FindObjectsInactive.Include);
var content = menu.transform.Find("Content");
var oldTab = content.Find("Tab5"); if (oldTab != null) UnityEngine.Object.DestroyImmediate(oldTab.gameObject);
var oldPage = content.Find("Page5"); if (oldPage != null) UnityEngine.Object.DestroyImmediate(oldPage.gameObject);

// Шесть вкладок по 96 px
var tabs = new Transform[6];
for (int i = 0; i < 5; i++) tabs[i] = content.Find("Tab" + i);
var tab5 = UnityEngine.Object.Instantiate(tabs[4].gameObject, content); tab5.name = "Tab5"; tabs[5] = tab5.transform;
tab5.transform.SetSiblingIndex(content.Find("Page4").GetSiblingIndex() + 1);
for (int i = 0; i < 6; i++) {
    var r = (RectTransform)tabs[i];
    r.anchoredPosition = new Vector2(14f + i * 100.9f, r.anchoredPosition.y);
    r.sizeDelta = new Vector2(96f, r.sizeDelta.y);
    // Подпись — по центру узкой вкладки (раньше стояла с отступом 24 px и вылезала за край)
    var label = (RectTransform)tabs[i].Find("Text (TMP)");
    label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one; label.pivot = new Vector2(0.5f, 0.5f);
    label.offsetMin = Vector2.zero; label.offsetMax = Vector2.zero;
    label.GetComponent<TMPro.TMP_Text>().alignment = TMPro.TextAlignmentOptions.Center;
}
tab5.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>().text = "Награды";

// Страница: копия «Клавиш», строки клавиш становятся строками достижений, кнопки значений — просто текстом
var page = UnityEngine.Object.Instantiate(content.Find("Page3").gameObject, content); page.name = "Page5";
page.transform.SetSiblingIndex(tab5.transform.GetSiblingIndex() + 1);
UnityEngine.Object.DestroyImmediate(page.GetComponent<RacingProject.Management.KeyBindingsPage>());
int count = RacingProject.Management.Achievements.Count;
Transform sample = null;
var drop = new System.Collections.Generic.List<GameObject>();
foreach (Transform ch in page.transform) { if (sample == null && ch.name.StartsWith("Key")) sample = ch; else drop.Add(ch.gameObject); }
foreach (var g in drop) UnityEngine.Object.DestroyImmediate(g);
var rowsList = new System.Collections.Generic.List<Transform> { sample };
while (rowsList.Count < count + 1) rowsList.Add(UnityEngine.Object.Instantiate(sample.gameObject, page.transform).transform);

var pageComp = page.AddComponent<RacingProject.Management.AchievementsPage>();
var so = new SerializedObject(pageComp);
var rowsProp = so.FindProperty("rows");
rowsProp.arraySize = count;
for (int i = 0; i <= count; i++) {
    bool total = i == 0;
    var row = rowsList[i];
    var id = (RacingProject.Management.Achievements.Id)(Mathf.Max(0, i - 1));
    row.name = total ? "Total" : "Ach" + id;
    ((RectTransform)row).anchoredPosition = new Vector2(0f, -(i * 21.5f + (total ? 0f : 6f)));
    var label = row.Find("Label").GetComponent<TMPro.TMP_Text>();
    var value = row.Find("Value").GetComponent<TMPro.TMP_Text>();
    var lr = (RectTransform)label.transform; lr.sizeDelta = new Vector2(380f, lr.sizeDelta.y);
    var vr = (RectTransform)value.transform; vr.anchoredPosition = new Vector2(420f, vr.anchoredPosition.y); vr.sizeDelta = new Vector2(170f, vr.sizeDelta.y);
    UnityEngine.Object.DestroyImmediate(value.GetComponent<RacingProject.Management.TerminalButtonFx>());
    UnityEngine.Object.DestroyImmediate(value.GetComponent<UnityEngine.UI.Button>());
    value.raycastTarget = false;
    var sso = new SerializedObject(row.GetComponent<RacingProject.Management.TerminalSelector>());
    if (total) {
        label.text = "Получено";
        value.text = "0 из " + count;
        sso.FindProperty("hint").stringValue = "Достижения хранятся на этом компьютере; в кооперативе их получают оба игрока";
        so.FindProperty("total").objectReferenceValue = value;
    } else {
        label.text = RacingProject.Management.Achievements.Title(id);
        value.text = "—";
        sso.FindProperty("hint").stringValue = RacingProject.Management.Achievements.Description(id);
        var e = rowsProp.GetArrayElementAtIndex(i - 1);
        e.FindPropertyRelative("id").enumValueIndex = i - 1;
        e.FindPropertyRelative("label").objectReferenceValue = label;
        e.FindPropertyRelative("value").objectReferenceValue = value;
    }
    sso.ApplyModifiedPropertiesWithoutUndo();
}
so.ApplyModifiedPropertiesWithoutUndo();
page.SetActive(false);

var mso = new SerializedObject(menu);
System.Action<string, UnityEngine.Object> append = (name, obj) => {
    var p = mso.FindProperty(name);
    if (p.arraySize < 6) p.arraySize = 6;
    p.GetArrayElementAtIndex(5).objectReferenceValue = obj;
};
append("tabButtons", tab5.GetComponent<UnityEngine.UI.Button>());
append("tabPages", page);
append("tabBackgrounds", tab5.transform.Find("Back").GetComponent<UnityEngine.UI.Graphic>());
append("tabLabels", tab5.transform.Find("Text (TMP)").GetComponent<UnityEngine.UI.Graphic>());
mso.ApplyModifiedPropertiesWithoutUndo();

// Отслеживание достижений: рядом с EnemyManager, прочность машины — та же, что у него
var manager = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.EnemyManager>();
var ach = manager.GetComponent<RacingProject.Management.Achievements>();
if (ach == null) ach = manager.gameObject.AddComponent<RacingProject.Management.Achievements>();
var aso = new SerializedObject(ach);
aso.FindProperty("enemyManager").objectReferenceValue = manager;
aso.FindProperty("carHealth").objectReferenceValue = new SerializedObject(manager).FindProperty("carHealth").objectReferenceValue;
aso.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(menu.gameObject.scene);
return "achievements tab ok: " + page.transform.childCount + " rows";
