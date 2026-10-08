// Экраны меню в стиле военного терминала: главный экран (Menu/Canvases/Main) с лобби и блоком состояния.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Каждый запуск пересобирает содержимое холстов с нуля; сами холсты (размер, положение на экранах пульта,
// рейкастеры VR) не трогаются — их ставит BuildBunker.cs. Текстуры развёртки и виньетки пишутся в Assets/Content/UI/Terminal.
var menu = GameObject.Find("Menu").transform;
GameObjectUtility.RemoveMonoBehavioursWithMissingScript(menu.gameObject); // компонент старой темы меню
var canvases = menu.Find("Canvases");
var room = UnityEngine.Object.FindObjectOfType<RacingProject.Network.RoomController>();
var lobby = room.GetComponent<RacingProject.Network.LanLobby>();
var manager = menu.Find("Manager").GetComponent<RacingProject.Management.MenuManager>();
var log = new System.Text.StringBuilder();

// --- Палитра и шрифты ---
var bg = new Color(0.035f, 0.075f, 0.045f, 1f);
var phosphor = new Color(0.6f, 0.88f, 0.42f);
var dim = new Color(0.38f, 0.58f, 0.33f);
var bodyFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
var titleFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Content/BlackOpsOne-Regular.asset");

// --- Текстуры: строки развёртки (повторяются) и виньетка по краям экрана ---
string UI = "Assets/Content/UI/Terminal/";
if (!AssetDatabase.IsValidFolder("Assets/Content/UI/Terminal")) AssetDatabase.CreateFolder("Assets/Content/UI", "Terminal");
System.Func<string, int, int, System.Func<int, int, Color>, bool, Texture2D> makeTex = (name, w, h, f, repeat) => {
    var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
    for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, f(x, y));
    string path = UI + name + ".png";
    System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(t);
    AssetDatabase.ImportAsset(path);
    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
    ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.mipmapEnabled = false;
    ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; ti.SaveAndReimport();
    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
};
var scanTex = makeTex("Scanlines", 4, 4, (x, y) => new Color(0f, 0f, 0f, y < 2 ? 0.28f : 0f), true);
var vignetteTex = makeTex("Vignette", 256, 256, (x, y) => {
    float dx = (x + 0.5f) / 128f - 1f, dy = (y + 0.5f) / 128f - 1f;
    float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.2f);
    return new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.25f, r)) * 0.75f);
}, false);

// --- Помощники разметки: координаты от левого верхнего угла холста (x вправо, y вниз), в единицах холста ---
System.Func<string, Transform, RectTransform> node = (name, parent) => {
    var g = new GameObject(name, typeof(RectTransform)); g.layer = parent.gameObject.layer;
    var r = (RectTransform)g.transform; r.SetParent(parent, false); return r;
};
System.Func<RectTransform, RectTransform> stretch = r => {
    r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero; return r;
};
System.Action<RectTransform, float, float, float, float> place = (r, x, y, w, h) => {
    r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
    r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
};
System.Func<string, Transform, Color, UnityEngine.UI.Image> image = (name, parent, color) => {
    var im = node(name, parent).gameObject.AddComponent<UnityEngine.UI.Image>(); im.color = color; im.raycastTarget = false; return im;
};
System.Func<string, Transform, string, float, Color, TMPro.TextAlignmentOptions, TMPro.TextMeshProUGUI> text = (name, parent, s, size, color, align) => {
    var t = node(name, parent).gameObject.AddComponent<TMPro.TextMeshProUGUI>();
    t.font = bodyFont; t.text = s; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false;
    t.fontStyle = TMPro.FontStyles.UpperCase | TMPro.FontStyles.Bold; t.enableWordWrapping = false; t.overflowMode = TMPro.TextOverflowModes.Overflow;
    t.margin = Vector4.zero; return t;
};
System.Action<Transform, float, float, float> hline = (parent, x, y, w) => { var l = image("Line", parent, dim); place(l.rectTransform, x, y, w, 1.5f); };
// Строка меню: подсветка-плашка (ColorTint), знак «>» при наведении и подпись слева
System.Func<string, Transform, string, float, float, UnityEngine.UI.Button> menuButton = (name, parent, label, w, h) => {
    var r = node(name, parent); r.sizeDelta = new Vector2(w, h);
    var bar = r.gameObject.AddComponent<UnityEngine.UI.Image>(); bar.color = phosphor;
    var b = r.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = bar;
    var cb = b.colors; cb.normalColor = new Color(1f, 1f, 1f, 0f); cb.highlightedColor = new Color(1f, 1f, 1f, 0.2f);
    cb.pressedColor = new Color(1f, 1f, 1f, 0.45f); cb.selectedColor = new Color(1f, 1f, 1f, 0f); cb.disabledColor = new Color(1f, 1f, 1f, 0f);
    cb.colorMultiplier = 1f; cb.fadeDuration = 0.05f; b.colors = cb;
    var nav = b.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.None; b.navigation = nav;
    var le = r.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); le.preferredHeight = h; le.minHeight = h;
    var mk = text("Marker", r, ">", h * 0.55f, phosphor, TMPro.TextAlignmentOptions.MidlineLeft); place(mk.rectTransform, 6f, 0f, 16f, h);
    var lb = text("Text (TMP)", r, label, h * 0.5f, phosphor, TMPro.TextAlignmentOptions.MidlineLeft); place(lb.rectTransform, 24f, 0f, w - 28f, h);
    var fx = r.gameObject.AddComponent<RacingProject.Management.TerminalButtonFx>();
    var so = new SerializedObject(fx); so.FindProperty("marker").objectReferenceValue = mk; so.ApplyModifiedPropertiesWithoutUndo();
    return b;
};
// Основа экрана: фон, содержимое под мерцанием, развёртка и виньетка поверх
System.Func<Transform, RectTransform> screenBase = canvas => {
    var kill = new System.Collections.Generic.List<GameObject>();
    foreach (Transform c in canvas) kill.Add(c.gameObject);
    foreach (var g in kill) UnityEngine.Object.DestroyImmediate(g);
    var back = image("Background", canvas, bg); stretch(back.rectTransform);
    var content = stretch(node("Content", canvas));
    content.gameObject.AddComponent<CanvasGroup>();
    var scan = node("Scanlines", canvas).gameObject.AddComponent<UnityEngine.UI.RawImage>();
    stretch(scan.rectTransform); scan.texture = scanTex; scan.raycastTarget = false;
    float h = ((RectTransform)canvas).sizeDelta.y;
    scan.uvRect = new Rect(0f, 0f, 1f, h / 3f);
    var vig = node("Vignette", canvas).gameObject.AddComponent<UnityEngine.UI.RawImage>();
    stretch(vig.rectTransform); vig.texture = vignetteTex; vig.raycastTarget = false;
    return content;
};
System.Action<Transform, RectTransform, TMPro.TMP_Text, UnityEngine.UI.Graphic, TMPro.TMP_Text[]> screenFx = (canvas, content, clockText, cursorMark, typed) => {
    var fx = canvas.GetComponent<RacingProject.Management.TerminalScreen>();
    if (fx == null) fx = canvas.gameObject.AddComponent<RacingProject.Management.TerminalScreen>();
    var so = new SerializedObject(fx);
    so.FindProperty("scanlines").objectReferenceValue = canvas.Find("Scanlines").GetComponent<UnityEngine.UI.RawImage>();
    so.FindProperty("content").objectReferenceValue = content.GetComponent<CanvasGroup>();
    so.FindProperty("clock").objectReferenceValue = clockText;
    so.FindProperty("cursor").objectReferenceValue = cursorMark;
    var arr = so.FindProperty("typed"); arr.arraySize = typed.Length;
    for (int i = 0; i < typed.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = typed[i];
    so.ApplyModifiedPropertiesWithoutUndo();
};

// --- Главный экран ---
var main = canvases.Find("Main");
float W = ((RectTransform)main).sizeDelta.x, H = ((RectTransform)main).sizeDelta.y, M = 14f;
var mc = screenBase(main);
var head = text("Header", mc, "Укрытие № 17 · терминал связи", 11f, dim, TMPro.TextAlignmentOptions.MidlineLeft); place(head.rectTransform, M, 10f, 300f, 18f);
var clock = text("Clock", mc, "00:00:00", 11f, dim, TMPro.TextAlignmentOptions.MidlineRight); place(clock.rectTransform, W - M - 120f, 10f, 120f, 18f);
hline(mc, M, 31f, W - 2 * M);
var sub = text("Subtitle", mc, "Chinazes:", 13f, dim, TMPro.TextAlignmentOptions.MidlineLeft); place(sub.rectTransform, M, 38f, 300f, 18f);
sub.characterSpacing = 12f;
var title = text("Title", mc, "Escape from Fryazino", 30f, phosphor, TMPro.TextAlignmentOptions.MidlineLeft); place(title.rectTransform, M - 2f, 54f, W - 2 * M, 36f);
title.font = titleFont; title.fontStyle = TMPro.FontStyles.UpperCase;
hline(mc, M, 96f, W - 2 * M);

// Меню лобби: скрытые LanLobby кнопки схлопываются в столбце
var list = node("MenuList", mc); place(list, M, 104f, 262f, 152f);
var vl = list.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.spacing = 1f;
var bHost = menuButton("Host", list, "Создать игру", 262f, 29f);
var bJoin = menuButton("Join", list, "Найти игру", 262f, 29f);
var bPlay = menuButton("Play", list, "Готов", 262f, 29f);
var bLeave = menuButton("Leave", list, "Отключиться", 262f, 29f);
var bExit = menuButton("Exit", list, "Выйти из игры", 262f, 29f);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bHost.onClick, lobby.OnHostPressed);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bJoin.onClick, lobby.OnJoinPressed);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bPlay.onClick, room.OnReadyButtonPressed);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bLeave.onClick, lobby.OnLeavePressed);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bExit.onClick, manager.EndGame);
bPlay.gameObject.SetActive(false); bLeave.gameObject.SetActive(false);

// Блок состояния справа: рамка, три строки «параметр — значение» и сообщение лобби
float px = M + 262f + 18f, pw = W - M - px;
var box = node("StatusBox", mc); place(box, px, 104f, pw, 150f);
foreach (var e in new[] { new Vector4(0f, 0f, pw, 1.5f), new Vector4(0f, 148.5f, pw, 1.5f), new Vector4(0f, 0f, 1.5f, 150f), new Vector4(pw - 1.5f, 0f, 1.5f, 150f) }) {
    var l = image("Frame", box, dim); place(l.rectTransform, e.x, e.y, e.z, e.w);
}
var boxTitle = text("Title", box, " Состояние ", 10f, dim, TMPro.TextAlignmentOptions.MidlineLeft); place(boxTitle.rectTransform, 10f, -7f, 90f, 14f);
var boxTitleBg = image("TitleBack", box, bg); place(boxTitleBg.rectTransform, 10f, -7f, 82f, 14f); boxTitleBg.transform.SetSiblingIndex(boxTitle.transform.GetSiblingIndex());
System.Func<string, float, TMPro.TextMeshProUGUI> statusRow = (label, y) => {
    var l = text(label + "Label", box, label, 12f, dim, TMPro.TextAlignmentOptions.MidlineLeft); place(l.rectTransform, 10f, y, 110f, 20f);
    var v = text(label + "Value", box, "—", 12f, dim, TMPro.TextAlignmentOptions.MidlineRight); place(v.rectTransform, 110f, y, pw - 120f, 20f);
    return v;
};
var vLink = statusRow("Связь", 10f);
var vDriver = statusRow("Водитель", 30f);
var vGunner = statusRow("Стрелок", 50f);
hline(box, 10f, 74f, pw - 20f);
var status = text("Status", box, "", 11f, phosphor, TMPro.TextAlignmentOptions.TopLeft); place(status.rectTransform, 10f, 80f, pw - 20f, 64f);
status.enableWordWrapping = true; status.overflowMode = TMPro.TextOverflowModes.Truncate;
var lines = box.gameObject.AddComponent<RacingProject.Management.LobbyStatusLines>();
{
    var so = new SerializedObject(lines);
    so.FindProperty("room").objectReferenceValue = room; so.FindProperty("lobby").objectReferenceValue = lobby;
    so.FindProperty("link").objectReferenceValue = vLink; so.FindProperty("driver").objectReferenceValue = vDriver; so.FindProperty("gunner").objectReferenceValue = vGunner;
    so.ApplyModifiedPropertiesWithoutUndo();
}

// Подвал: приглашение с мигающим курсором и подсказка, где что искать
hline(mc, M, H - 34f, W - 2 * M);
var prompt = text("Prompt", mc, ">", 12f, phosphor, TMPro.TextAlignmentOptions.MidlineLeft); place(prompt.rectTransform, M, H - 28f, 14f, 18f);
var cursor = image("Cursor", mc, phosphor); place(cursor.rectTransform, M + 13f, H - 25f, 8f, 12f);
var hint = text("Hint", mc, "Настройки — левый экран · памятка — на стене справа", 10f, dim, TMPro.TextAlignmentOptions.MidlineRight);
place(hint.rectTransform, M + 40f, H - 28f, W - 2 * M - 40f, 18f);
screenFx(main, mc, clock, cursor, new TMPro.TMP_Text[] { status });

// Ссылки лобби на новые кнопки и строку сообщений
{
    var so = new SerializedObject(lobby);
    so.FindProperty("hostButton").objectReferenceValue = bHost.gameObject;
    so.FindProperty("joinButton").objectReferenceValue = bJoin.gameObject;
    so.FindProperty("joinButtonLabel").objectReferenceValue = bJoin.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>();
    so.FindProperty("readyButton").objectReferenceValue = bPlay.gameObject;
    so.FindProperty("leaveButton").objectReferenceValue = bLeave.gameObject;
    so.FindProperty("statusText").objectReferenceValue = status;
    so.ApplyModifiedPropertiesWithoutUndo();
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
log.AppendLine("main ok " + W + "x" + H);
return log.ToString();
