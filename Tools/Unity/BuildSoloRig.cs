// Риг одиночной игры (SoloRig в корне сцены, выключен): камера от третьего лица с прицелом и экранный HUD.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск пересобирает риг. HUD — копии экранов из машины (CockpitDisplay): приборка водителя,
// табло волны и счёта, шкала перегрева пулемёта. Копии ссылаются на те же источники (машина, здоровье, пулемёт),
// поэтому показывают то же, что экраны в салоне. Включает риг RoomController (поле soloRig) в одиночной игре
var log = new System.Text.StringBuilder();
var room = UnityEngine.Object.FindObjectOfType<RacingProject.Network.RoomController>();
var car = room.car.transform;
var gun = car.Find("RoofTurret").GetComponentInChildren<RacingProject.Turret.VRGun>(true);
var turretAim = gun.GetComponent<RacingProject.Turret.TurretGunAim>();
// Камеру настраиваем как у водителя: те же дальность, фон и постобработка
var driverCam = room.driverRig.GetComponentInChildren<Camera>(true);

var old = GameObject.Find("SoloRig");
if (old == null) foreach (var t in Resources.FindObjectsOfTypeAll<Transform>()) if (t.name == "SoloRig" && t.parent == null && t.gameObject.scene.IsValid()) old = t.gameObject;
if (old != null) UnityEngine.Object.DestroyImmediate(old);
var rig = new GameObject("SoloRig");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rig, car.gameObject.scene);

// --- Камера ---
var camGo = new GameObject("ThirdPersonCamera");
camGo.tag = "MainCamera";
camGo.transform.SetParent(rig.transform, false);
var cam = camGo.AddComponent<Camera>();
cam.CopyFrom(driverCam);
cam.targetTexture = null;
cam.fieldOfView = 60f;
cam.nearClipPlane = 0.1f;
var srcData = driverCam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
var camData = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
if (srcData != null) {
    camData.renderPostProcessing = srcData.renderPostProcessing;
    camData.antialiasing = srcData.antialiasing;
    camData.renderShadows = srcData.renderShadows;
}
camGo.AddComponent<AudioListener>();

// Машина 6,9 м в высоту вместе с турелью: точка вращения над крышей, камера достаточно далеко, чтобы видеть всю машину
var tpc = camGo.AddComponent<RacingProject.Desktop.ThirdPersonCamera>();
{
    var so = new SerializedObject(tpc);
    so.FindProperty("target").objectReferenceValue = car;
    so.FindProperty("pivotHeight").floatValue = 5.5f;
    so.FindProperty("distance").floatValue = 15f;
    so.ApplyModifiedPropertiesWithoutUndo();
}
var aim = camGo.AddComponent<RacingProject.Desktop.DesktopGunnerAim>();
{
    var so = new SerializedObject(aim);
    so.FindProperty("aimCamera").objectReferenceValue = cam;
    so.FindProperty("ignoreRoot").objectReferenceValue = car;
    so.FindProperty("aimDistance").floatValue = 250f;
    so.ApplyModifiedPropertiesWithoutUndo();
}

// --- Экранный HUD ---
var hud = new GameObject("SoloHud", typeof(RectTransform));
hud.layer = LayerMask.NameToLayer("UI");
hud.transform.SetParent(rig.transform, false);
var canvas = hud.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.sortingOrder = 10;
var scaler = hud.AddComponent<UnityEngine.UI.CanvasScaler>();
scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1920f, 1080f);
scaler.matchWidthOrHeight = 0.5f;

// Копия экрана из машины в угол экрана: anchor — угол (0..1), margin — отступ от него в пикселях эталона
System.Func<Transform, Vector2, Vector2, float, RectTransform> panel = (src, anchor, margin, scale) => {
    var copy = (GameObject)UnityEngine.Object.Instantiate(src.gameObject, hud.transform, false);
    copy.name = src.name;
    copy.SetActive(true);
    foreach (var t in copy.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = hud.layer;
    var ray = copy.GetComponent<UnityEngine.UI.GraphicRaycaster>();
    if (ray != null) UnityEngine.Object.DestroyImmediate(ray);
    var r = (RectTransform)copy.transform;
    r.anchorMin = r.anchorMax = r.pivot = anchor;
    r.localRotation = Quaternion.identity;
    r.localScale = Vector3.one * scale;
    r.anchoredPosition = new Vector2(anchor.x < 0.5f ? margin.x : anchor.x > 0.5f ? -margin.x : 0f,
                                     anchor.y < 0.5f ? margin.y : -margin.y);
    return r;
};
panel(car.Find("DriverCluster"), new Vector2(0f, 0f), new Vector2(24f, 24f), 0.55f);
panel(car.Find("TacticalDisplay"), new Vector2(0.5f, 1f), new Vector2(0f, 16f), 0.5f);
var heatSrc = car.GetComponentsInChildren<RacingProject.Hud.CockpitDisplay>(true);
foreach (var cd in heatSrc) if (cd.name == "GunnerHeat") panel(cd.transform, new Vector2(1f, 0f), new Vector2(24f, 24f), 0.6f);

// Прицел: точка и четыре штриха вокруг неё
var cross = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
cross.gameObject.layer = hud.layer;
cross.SetParent(hud.transform, false);
cross.sizeDelta = new Vector2(40f, 40f);
System.Action<string, Vector2, Vector2> mark = (mname, pos, size) => {
    var g = new GameObject(mname, typeof(RectTransform), typeof(UnityEngine.UI.Image));
    g.layer = hud.layer;
    var r = (RectTransform)g.transform;
    r.SetParent(cross, false);
    r.anchoredPosition = pos;
    r.sizeDelta = size;
    var im = g.GetComponent<UnityEngine.UI.Image>();
    im.color = new Color(1f, 1f, 1f, 0.85f);
    im.raycastTarget = false;
};
mark("Dot", Vector2.zero, new Vector2(4f, 4f));
mark("Up", new Vector2(0f, 14f), new Vector2(2f, 10f));
mark("Down", new Vector2(0f, -14f), new Vector2(2f, 10f));
mark("Left", new Vector2(-14f, 0f), new Vector2(10f, 2f));
mark("Right", new Vector2(14f, 0f), new Vector2(10f, 2f));

var solo = rig.AddComponent<RacingProject.Desktop.SoloRig>();
{
    var so = new SerializedObject(solo);
    so.FindProperty("aim").objectReferenceValue = aim;
    so.FindProperty("turretAim").objectReferenceValue = turretAim;
    so.FindProperty("gun").objectReferenceValue = gun;
    so.ApplyModifiedPropertiesWithoutUndo();
}
rig.SetActive(false);

{
    var so = new SerializedObject(room);
    so.FindProperty("soloRig").objectReferenceValue = rig;
    so.ApplyModifiedPropertiesWithoutUndo();
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);
log.AppendLine("SoloRig: пулемёт " + gun.name + ", камера по образцу " + driverCam.name);
return log.ToString();
