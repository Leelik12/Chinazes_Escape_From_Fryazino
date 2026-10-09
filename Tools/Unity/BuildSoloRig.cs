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
var clusterCopy = panel(car.Find("DriverCluster"), new Vector2(0f, 0f), new Vector2(24f, 24f), 0.55f);
// Подсказку «удерживайте R» в одиночной игре показывает крупная надпись по центру экрана, а не мелкая на приборке
{
    var copyPrompt = clusterCopy.Find("RecoveryPrompt");
    if (copyPrompt != null) UnityEngine.Object.DestroyImmediate(copyPrompt.gameObject);
    var so = new SerializedObject(clusterCopy.GetComponent<RacingProject.Hud.CockpitDisplay>());
    so.FindProperty("recovery").objectReferenceValue = null;
    so.FindProperty("recoveryRoot").objectReferenceValue = null;
    so.FindProperty("recoveryPrompt").objectReferenceValue = null;
    so.ApplyModifiedPropertiesWithoutUndo();
}
panel(car.Find("TacticalDisplay"), new Vector2(0.5f, 1f), new Vector2(0f, 16f), 0.5f);
var heatSrc = car.GetComponentsInChildren<RacingProject.Hud.CockpitDisplay>(true);
foreach (var cd in heatSrc) if (cd.name == "GunnerHeat") panel(cd.transform, new Vector2(1f, 0f), new Vector2(24f, 24f), 0.6f);

// Прицел: точка, тонкое кольцо и четыре штриха с тёмной обводкой (читаются и на небе, и на снегу).
// Текстура кольца рисуется здесь же: 128×128, сглаженная окружность толщиной 4 пикселя
const string ringPath = "Assets/Content/UI/CrosshairRing.png";
{
    var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
    for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) {
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(64f, 64f));
        float a = Mathf.Clamp01(2.5f - Mathf.Abs(d - 58f));
        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
    }
    System.IO.File.WriteAllBytes(ringPath, tex.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(tex);
    AssetDatabase.ImportAsset(ringPath);
    var ti = (TextureImporter)AssetImporter.GetAtPath(ringPath);
    ti.textureType = TextureImporterType.Sprite; ti.spriteImportMode = SpriteImportMode.Single;
    ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();
}
var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ringPath);
var cross = new GameObject("Crosshair", typeof(RectTransform)).GetComponent<RectTransform>();
cross.gameObject.layer = hud.layer;
cross.SetParent(hud.transform, false);
cross.sizeDelta = new Vector2(64f, 64f);
var tinted = new System.Collections.Generic.List<UnityEngine.UI.Graphic>();
System.Func<string, Vector2, Vector2, Sprite, RectTransform> mark = (mname, pos, size, sprite) => {
    var g = new GameObject(mname, typeof(RectTransform), typeof(UnityEngine.UI.Image));
    g.layer = hud.layer;
    var r = (RectTransform)g.transform;
    r.SetParent(cross, false);
    r.anchoredPosition = pos;
    r.sizeDelta = size;
    var im = g.GetComponent<UnityEngine.UI.Image>();
    im.sprite = sprite;
    im.color = new Color(0.92f, 1f, 0.88f, 0.95f);
    im.raycastTarget = false;
    var outline = g.AddComponent<UnityEngine.UI.Outline>();
    outline.effectColor = new Color(0f, 0f, 0f, 0.55f);
    outline.effectDistance = new Vector2(1f, -1f);
    tinted.Add(im);
    return r;
};
mark("Ring", Vector2.zero, new Vector2(44f, 44f), ringSprite).GetComponent<UnityEngine.UI.Image>().color = new Color(0.92f, 1f, 0.88f, 0.5f);
tinted.RemoveAt(tinted.Count - 1); // кольцо полупрозрачное и не краснеет
mark("Dot", Vector2.zero, new Vector2(4f, 4f), null);
var ticks = new[] {
    mark("Up", new Vector2(0f, 14f), new Vector2(2f, 10f), null),
    mark("Down", new Vector2(0f, -14f), new Vector2(2f, 10f), null),
    mark("Left", new Vector2(-14f, 0f), new Vector2(10f, 2f), null),
    mark("Right", new Vector2(14f, 0f), new Vector2(10f, 2f), null),
};
{
    var ch = cross.gameObject.AddComponent<RacingProject.Hud.SoloCrosshair>();
    var so = new SerializedObject(ch);
    so.FindProperty("gun").objectReferenceValue = gun;
    var t = so.FindProperty("ticks"); t.arraySize = ticks.Length;
    for (int i = 0; i < ticks.Length; i++) t.GetArrayElementAtIndex(i).objectReferenceValue = ticks[i];
    var g = so.FindProperty("tinted"); g.arraySize = tinted.Count;
    for (int i = 0; i < tinted.Count; i++) g.GetArrayElementAtIndex(i).objectReferenceValue = tinted[i];
    so.ApplyModifiedPropertiesWithoutUndo();
}

// Подсказка «удерживайте R» под прицелом: появляется, когда машина перевернулась или застряла
{
    var recovery = car.GetComponent<RacingProject.Car.CarRecovery>();
    var promptRoot = new GameObject("RecoveryPrompt", typeof(RectTransform)).GetComponent<RectTransform>();
    promptRoot.gameObject.layer = hud.layer;
    promptRoot.SetParent(hud.transform, false);
    promptRoot.anchoredPosition = new Vector2(0f, -170f);
    promptRoot.sizeDelta = new Vector2(920f, 120f);
    var back = promptRoot.gameObject.AddComponent<UnityEngine.UI.Image>();
    back.color = new Color(0.03f, 0.05f, 0.03f, 0.8f); back.raycastTarget = false;
    var label = new GameObject("Text", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
    label.gameObject.layer = hud.layer;
    label.rectTransform.SetParent(promptRoot, false);
    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
    label.rectTransform.offsetMin = new Vector2(16f, 8f); label.rectTransform.offsetMax = new Vector2(-16f, -8f);
    label.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Content/BlackOpsOne-Regular.asset");
    label.fontSize = 32f; label.alignment = TMPro.TextAlignmentOptions.Center;
    label.color = new Color(1f, 0.75f, 0.3f); label.raycastTarget = false;
    promptRoot.gameObject.SetActive(false);
    var display = hud.AddComponent<RacingProject.Hud.CockpitDisplay>();
    var so = new SerializedObject(display);
    so.FindProperty("recovery").objectReferenceValue = recovery;
    so.FindProperty("recoveryRoot").objectReferenceValue = promptRoot.gameObject;
    so.FindProperty("recoveryPrompt").objectReferenceValue = label;
    so.ApplyModifiedPropertiesWithoutUndo();
}

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
