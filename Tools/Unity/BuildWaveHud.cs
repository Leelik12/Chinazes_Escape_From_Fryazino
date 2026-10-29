// HUD волн и связи для передышки (SovietCity): WaveHud с 2D-звуком на General/Hud, ремкомплекты для выпадения
// из врагов в EnemyManager, звук отметки убийства на обоих пулемётах (VRGun).
// Запуск: execute_code (MCP for Unity, C# 6) вне Play; сцена сохраняется. Повторный запуск ничего не дублирует
var manager = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.EnemyManager>(FindObjectsInactive.Include);
var hudRoot = UnityEngine.Object.FindFirstObjectByType<RacingProject.Hud.DriverControlsHud>(FindObjectsInactive.Include).gameObject;
var kits = manager.GetComponent<RacingProject.Pickups.RepairKits>();
string dir = "Assets/Content/Sound/Generated/";
var waveStart = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "WaveStart.wav");
var waveClear = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "WaveClear.wav");
var cardPick = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "CardPick.wav");
var kill = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "KillConfirm.wav");
if (waveStart == null || waveClear == null || cardPick == null || kill == null) return "missing clips";

var hud = hudRoot.GetComponent<RacingProject.Hud.WaveHud>();
if (hud == null) hud = hudRoot.AddComponent<RacingProject.Hud.WaveHud>();
var audio = hudRoot.GetComponent<AudioSource>();
if (audio == null) audio = hudRoot.AddComponent<AudioSource>();
audio.playOnAwake = false;
audio.spatialBlend = 0f;
audio.volume = 0.8f;

var so = new SerializedObject(hud);
so.FindProperty("enemyManager").objectReferenceValue = manager;
so.FindProperty("audioSource").objectReferenceValue = audio;
so.FindProperty("waveStartSound").objectReferenceValue = waveStart;
so.FindProperty("waveClearSound").objectReferenceValue = waveClear;
so.FindProperty("cardPickSound").objectReferenceValue = cardPick;
so.ApplyModifiedPropertiesWithoutUndo();

var mso = new SerializedObject(manager);
mso.FindProperty("repairKits").objectReferenceValue = kits;
mso.ApplyModifiedPropertiesWithoutUndo();

int guns = 0;
foreach (var gun in UnityEngine.Object.FindObjectsByType<RacingProject.Turret.VRGun>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
    var gso = new SerializedObject(gun);
    gso.FindProperty("killSound").objectReferenceValue = kill;
    gso.ApplyModifiedPropertiesWithoutUndo();
    guns++;
}

var scene = manager.gameObject.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "ok: guns " + guns + ", kits " + (kits != null);
