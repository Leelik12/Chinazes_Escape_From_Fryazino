// Устойчивость и управляемость машин: CarStabilizer на машину игроков (VolgaCar) и на префабы врагов,
// CarRecovery («удерживайте R») на машину игроков и подсказка о нём на приборке водителя (DriverCluster).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск ничего не дублирует: компоненты добавляются, только если их нет, подсказка пересоздаётся.
// Экранный HUD одиночной игры копирует DriverCluster, поэтому после этого скрипта пересобрать BuildSoloRig.cs
var log = new System.Text.StringBuilder();
var car = GameObject.Find("VolgaCar");
if (car.GetComponent<RacingProject.Car.CarStabilizer>() == null) car.AddComponent<RacingProject.Car.CarStabilizer>();
var recovery = car.GetComponent<RacingProject.Car.CarRecovery>();
if (recovery == null) recovery = car.AddComponent<RacingProject.Car.CarRecovery>();
{
    var controller = new SerializedObject(car.GetComponent<RacingProject.Car.CarControllerSample>());
    var so = new SerializedObject(recovery);
    so.FindProperty("input").objectReferenceValue = controller.FindProperty("inputControllerReader").objectReferenceValue;
    so.ApplyModifiedPropertiesWithoutUndo();
}

foreach (var path in new[] { "Assets/Prefabs/Enemy/EnemyUaz.prefab", "Assets/Prefabs/Enemy/EnemyUral.prefab", "Assets/Resources/EnemySedan.prefab" }) {
    var root = PrefabUtility.LoadPrefabContents(path);
    if (root.GetComponent<RacingProject.Car.CarStabilizer>() == null) root.AddComponent<RacingProject.Car.CarStabilizer>();
    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
    log.AppendLine("стабилизатор: " + path);
}

// Подсказка на приборке водителя: тёмная полоса с текстом у нижнего края экрана, видна, только когда машину можно поставить
var cluster = car.transform.Find("DriverCluster");
var old = cluster.Find("RecoveryPrompt");
if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var font = cluster.Find("Gear").GetComponent<TMPro.TMP_Text>().font;
var prompt = new GameObject("RecoveryPrompt", typeof(RectTransform)).GetComponent<RectTransform>();
prompt.gameObject.layer = cluster.gameObject.layer;
prompt.SetParent(cluster, false);
prompt.anchorMin = new Vector2(0f, 0f); prompt.anchorMax = new Vector2(1f, 0f); prompt.pivot = new Vector2(0.5f, 0f);
prompt.anchoredPosition = new Vector2(0f, 12f); prompt.sizeDelta = new Vector2(-24f, 130f);
var back = prompt.gameObject.AddComponent<UnityEngine.UI.Image>();
back.color = new Color(0.05f, 0.03f, 0.02f, 0.92f); back.raycastTarget = false;
var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
text.gameObject.layer = prompt.gameObject.layer;
text.rectTransform.SetParent(prompt, false);
text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
text.rectTransform.offsetMin = new Vector2(12f, 6f); text.rectTransform.offsetMax = new Vector2(-12f, -6f);
text.font = font; text.fontSize = 34f; text.alignment = TMPro.TextAlignmentOptions.Center;
text.color = new Color(1f, 0.75f, 0.3f); text.raycastTarget = false; text.text = "";
prompt.gameObject.SetActive(false);
{
    var so = new SerializedObject(cluster.GetComponent<RacingProject.Hud.CockpitDisplay>());
    so.FindProperty("recovery").objectReferenceValue = recovery;
    so.FindProperty("recoveryRoot").objectReferenceValue = prompt.gameObject;
    so.FindProperty("recoveryPrompt").objectReferenceValue = text;
    so.ApplyModifiedPropertiesWithoutUndo();
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
log.AppendLine("подсказка: DriverCluster/RecoveryPrompt");
return log.ToString();
