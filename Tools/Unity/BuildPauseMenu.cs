// Меню паузы (PauseMenu в корне сцены) в стиле терминала бункера: экранный холст поверх игры с кнопками
// «Продолжить», «В меню», «Выйти из игры» и окно подтверждения (ConfirmDialog) для двух последних.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск пересобирает меню паузы с нуля
var log = new System.Text.StringBuilder();
var room = UnityEngine.Object.FindObjectOfType<RacingProject.Network.RoomController>();
var lobby = room.GetComponent<RacingProject.Network.LanLobby>();

var bg = new Color(0.035f, 0.075f, 0.045f, 1f);
var phosphor = new Color(0.6f, 0.88f, 0.42f);
var dim = new Color(0.38f, 0.58f, 0.33f);
var bodyFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
var titleFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Content/BlackOpsOne-Regular.asset");
var scanTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/UI/Terminal/Scanlines.png");

var oldRoot = GameObject.Find("PauseMenu");
if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot);
var root = new GameObject("PauseMenu");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, room.gameObject.scene);
int uiLayer = LayerMask.NameToLayer("UI");

// --- Помощники разметки: координаты от центра родителя, в пикселях эталонного экрана 1920×1080 ---
System.Func<string, Transform, RectTransform> node = (name, parent) => {
    var g = new GameObject(name, typeof(RectTransform)); g.layer = uiLayer;
    var r = (RectTransform)g.transform; r.SetParent(parent, false); return r;
};
System.Func<RectTransform, RectTransform> stretch = r => {
    r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; return r;
};
System.Action<RectTransform, float, float, float, float> place = (r, x, y, w, h) => {
    r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
    r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
};
System.Func<string, Transform, Color, UnityEngine.UI.Image> image = (name, parent, color) => {
    var im = node(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>(); im.color = color; im.raycastTarget = false; return im;
};
System.Func<string, Transform, string, float, Color, TMPro.TextMeshProUGUI> text = (name, parent, s, size, color) => {
    var t = node(name, parent).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
    t.font = bodyFont; t.text = s; t.fontSize = size; t.color = color; t.alignment = TMPro.TextAlignmentOptions.Center;
    t.raycastTarget = false; t.fontStyle = TMPro.FontStyles.UpperCase | TMPro.FontStyles.Bold; t.enableWordWrapping = false;
    return t;
};
System.Action<Transform, float, float> frame = (parent, w, h) => {
    foreach (var e in new[] { new Vector4(0f, h / 2f, w, 3f), new Vector4(0f, -h / 2f, w, 3f), new Vector4(-w / 2f, 0f, 3f, h), new Vector4(w / 2f, 0f, 3f, h) }) {
        var l = image("Frame", parent, phosphor); place(l.rectTransform, e.x, e.y, e.z, e.w);
    }
};
// Кнопка: подсвечивается при наведении, слева знак «>»
System.Func<string, Transform, string, float, float, float, UnityEngine.UI.Button> button = (name, parent, label, y, w, x) => {
    var r = node(name, parent); place(r, x, y, w, 64f);
    var bar = r.gameObject.AddComponent<UnityEngine.UI.Image>(); bar.color = phosphor;
    var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = bar;
    var cb = b.colors; cb.normalColor = new Color(1f, 1f, 1f, 0.06f); cb.highlightedColor = new Color(1f, 1f, 1f, 0.25f);
    cb.pressedColor = new Color(1f, 1f, 1f, 0.45f); cb.selectedColor = new Color(1f, 1f, 1f, 0.06f); cb.fadeDuration = 0.05f; b.colors = cb;
    var nav = b.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.None; b.navigation = nav;
    var lb = text("Text (TMP)", r, label, 30f, phosphor); stretch(lb.rectTransform);
    return b;
};

// --- Холст ---
var screenGo = node("PauseScreen", root.transform).gameObject;
var canvas = screenGo.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.sortingOrder = 100;
var scaler = screenGo.AddComponent<UnityEngine.UI.CanvasScaler>();
scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
scaler.referenceResolution = new Vector2(1920f, 1080f);
scaler.matchWidthOrHeight = 0.5f;
screenGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();

// Затемнение игры: ловит клики, чтобы они не уходили в мир
var shade = image("Shade", screenGo.transform, new Color(0f, 0f, 0f, 0.6f)); stretch(shade.rectTransform); shade.raycastTarget = true;

float pw = 560f, ph = 470f;
var panel = node("Panel", screenGo.transform); place(panel, 0f, 0f, pw, ph);
var panelBack = panel.gameObject.AddComponent<UnityEngine.UI.Image>(); panelBack.color = new Color(bg.r, bg.g, bg.b, 0.97f);
frame(panel, pw, ph);
var title = text("Title", panel, "Пауза", 64f, phosphor); place(title.rectTransform, 0f, 165f, pw - 40f, 80f);
title.font = titleFont; title.fontStyle = TMPro.FontStyles.UpperCase;
var subtitle = text("Subtitle", panel, "Игра остановлена", 20f, dim); place(subtitle.rectTransform, 0f, 112f, pw - 40f, 30f);
var bResume = button("Resume", panel, "Продолжить", 30f, 440f, 0f);
var bMenu = button("ToMenu", panel, "В меню", -50f, 440f, 0f);
var bQuit = button("Quit", panel, "Выйти из игры", -130f, 440f, 0f);
var hint = text("Hint", panel, "Esc — продолжить", 18f, dim); place(hint.rectTransform, 0f, -200f, pw - 40f, 26f);
var scan = node("Scanlines", panel).gameObject.AddComponent<UnityEngine.UI.RawImage>();
stretch(scan.rectTransform); scan.texture = scanTex; scan.raycastTarget = false; scan.uvRect = new Rect(0f, 0f, 1f, ph / 4f);

// Окно подтверждения поверх панели
var confirmRoot = node("Confirm", screenGo.transform); stretch(confirmRoot);
var confirmShade = confirmRoot.gameObject.AddComponent<UnityEngine.UI.Image>(); confirmShade.color = new Color(0f, 0f, 0f, 0.55f);
float cw = 700f, ch = 260f;
var box = node("Box", confirmRoot); place(box, 0f, 0f, cw, ch);
var boxBack = box.gameObject.AddComponent<UnityEngine.UI.Image>(); boxBack.color = bg;
frame(box, cw, ch);
var question = text("Question", box, "Выйти?", 28f, phosphor); place(question.rectTransform, 0f, 55f, cw - 60f, 100f);
question.enableWordWrapping = true;
var bYes = button("Yes", box, "Да", -70f, 280f, -160f);
var bNo = button("No", box, "Нет", -70f, 280f, 160f);
var dialog = confirmRoot.gameObject.AddComponent<RacingProject.Management.ConfirmDialog>();
{
    var so = new SerializedObject(dialog);
    so.FindProperty("question").objectReferenceValue = question;
    so.FindProperty("yesButton").objectReferenceValue = bYes;
    so.FindProperty("noButton").objectReferenceValue = bNo;
    so.ApplyModifiedPropertiesWithoutUndo();
}
confirmRoot.gameObject.SetActive(false);

var pause = root.AddComponent<RacingProject.Management.PauseMenu>();
{
    var so = new SerializedObject(pause);
    so.FindProperty("screen").objectReferenceValue = screenGo;
    so.FindProperty("subtitle").objectReferenceValue = subtitle;
    so.FindProperty("confirm").objectReferenceValue = dialog;
    so.FindProperty("lobby").objectReferenceValue = lobby;
    so.ApplyModifiedPropertiesWithoutUndo();
}
UnityEditor.Events.UnityEventTools.AddPersistentListener(bResume.onClick, pause.Resume);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bMenu.onClick, pause.ToMenu);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bQuit.onClick, pause.Quit);
screenGo.SetActive(false);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
log.AppendLine("PauseMenu ok");
return log.ToString();
