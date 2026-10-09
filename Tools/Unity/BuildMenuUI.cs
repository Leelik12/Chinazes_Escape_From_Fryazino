// Экраны меню в стиле военного терминала: главный экран (Menu/Canvases/Main) с лобби и блоком состояния
// и экран настроек (Menu/Canvases/Settings) с вкладками «Звук», «Графика», «Экран».
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

// Меню лобби: скрытые LanLobby кнопки схлопываются в столбце. «Одиночная игра» видна только в режиме монитора
var list = node("MenuList", mc); place(list, M, 104f, 262f, 152f);
var vl = list.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.spacing = 1f;
var bSolo = menuButton("Solo", list, "Одиночная игра", 262f, 29f);
var bHost = menuButton("Host", list, "Создать игру", 262f, 29f);
var bJoin = menuButton("Join", list, "Найти игру", 262f, 29f);
var bPlay = menuButton("Play", list, "Готов", 262f, 29f);
var bLeave = menuButton("Leave", list, "Отключиться", 262f, 29f);
var bExit = menuButton("Exit", list, "Выйти из игры", 262f, 29f);
UnityEditor.Events.UnityEventTools.AddPersistentListener(bSolo.onClick, lobby.OnSoloPressed);
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
    so.FindProperty("soloButton").objectReferenceValue = bSolo.gameObject;
    so.FindProperty("hostButton").objectReferenceValue = bHost.gameObject;
    so.FindProperty("joinButton").objectReferenceValue = bJoin.gameObject;
    so.FindProperty("joinButtonLabel").objectReferenceValue = bJoin.transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>();
    so.FindProperty("readyButton").objectReferenceValue = bPlay.gameObject;
    so.FindProperty("leaveButton").objectReferenceValue = bLeave.gameObject;
    so.FindProperty("statusText").objectReferenceValue = status;
    so.ApplyModifiedPropertiesWithoutUndo();
}

log.AppendLine("main ok " + W + "x" + H);

// --- Экран настроек ---
var set = canvases.Find("Settings");
float SW = ((RectTransform)set).sizeDelta.x, SH = ((RectTransform)set).sizeDelta.y;
var sc = screenBase(set);
var sHead = text("Header", sc, "Укрытие № 17 · настройки терминала", 11f, dim, TMPro.TextAlignmentOptions.MidlineLeft); place(sHead.rectTransform, M, 10f, 320f, 18f);
var sClock = text("Clock", sc, "00:00:00", 11f, dim, TMPro.TextAlignmentOptions.MidlineRight); place(sClock.rectTransform, SW - M - 120f, 10f, 120f, 18f);
hline(sc, M, 31f, SW - 2 * M);

// Вкладки: плашка выбранной ярче (цвет задаёт SettingsMenu), при наведении — «>»
string[] tabNames = { "Звук", "Графика", "Экран" };
var tabButtons = new UnityEngine.UI.Button[3];
var tabBacks = new UnityEngine.UI.Image[3];
var tabPages = new RectTransform[3];
float tabW = 150f;
for (int i = 0; i < 3; i++) {
    var tb = menuButton("Tab" + i, sc, tabNames[i], tabW, 26f);
    place((RectTransform)tb.transform, M + i * (tabW + 6f), 40f, tabW, 26f);
    var back = image("Back", tb.transform, phosphor); stretch(back.rectTransform); back.transform.SetSiblingIndex(0);
    tabButtons[i] = tb; tabBacks[i] = back;
    var page = node("Page" + i, sc); place(page, M, 78f, SW - 2 * M, SH - 78f - 40f);
    tabPages[i] = page;
}
hline(sc, M, 70f, SW - 2 * M);

// Строка настройки: подпись, «◄ значение ►» и, если нужно, шкала из делений
string arrowL = bodyFont.HasCharacter('◄', true) ? "◄" : "<", arrowR = bodyFont.HasCharacter('►', true) ? "►" : ">";
System.Func<string, Transform, string, float, UnityEngine.UI.Button> arrow = (name, parent, glyph, x) => {
    var t = text(name, parent, glyph, 12f, phosphor, TMPro.TextAlignmentOptions.Center); place(t.rectTransform, x, 0f, 22f, 24f);
    t.raycastTarget = true;
    var b = t.gameObject.AddComponent<UnityEngine.UI.Button>(); b.targetGraphic = t;
    var cb = b.colors; cb.normalColor = new Color(1f, 1f, 1f, 0.8f); cb.highlightedColor = Color.white; cb.pressedColor = new Color(1f, 1f, 1f, 0.5f);
    cb.selectedColor = new Color(1f, 1f, 1f, 0.8f); cb.disabledColor = new Color(1f, 1f, 1f, 0.15f); cb.fadeDuration = 0.05f; b.colors = cb;
    var nav = b.navigation; nav.mode = UnityEngine.UI.Navigation.Mode.None; b.navigation = nav;
    t.gameObject.AddComponent<RacingProject.Management.TerminalButtonFx>();
    return b;
};
System.Func<string, Transform, string, float, float, float, float, float, int, string, RacingProject.Management.TerminalSelector> selector =
    (name, parent, label, x, y, w, labelW, valueW, segCount, hintText) => {
    var r = node(name, parent); place(r, x, y, w, 24f);
    var hit = r.gameObject.AddComponent<UnityEngine.UI.Image>(); hit.color = new Color(0f, 0f, 0f, 0f); // ловит наведение для подсказки
    r.gameObject.AddComponent<CanvasGroup>();
    var lb = text("Label", r, label, 11f, dim, TMPro.TextAlignmentOptions.MidlineLeft); place(lb.rectTransform, 4f, 0f, labelW, 24f);
    var prev = arrow("Prev", r, arrowL, labelW);
    var val = text("Value", r, "—", 11f, phosphor, TMPro.TextAlignmentOptions.Center); place(val.rectTransform, labelW + 22f, 0f, valueW, 24f);
    var nxt = arrow("Next", r, arrowR, labelW + 22f + valueW);
    var segs = new UnityEngine.UI.Image[segCount];
    float sx = labelW + 22f + valueW + 22f + 12f, sw = segCount > 0 ? (w - sx - 4f) / segCount : 0f;
    for (int i = 0; i < segCount; i++) { segs[i] = image("Segment" + i, r, phosphor); place(segs[i].rectTransform, sx + i * sw, 7f, sw - 3f, 10f); }
    var low = image("Underline", r, new Color(dim.r, dim.g, dim.b, 0.35f)); place(low.rectTransform, 4f, 23f, w - 8f, 1f);
    var ts = r.gameObject.AddComponent<RacingProject.Management.TerminalSelector>();
    var so = new SerializedObject(ts);
    so.FindProperty("label").objectReferenceValue = lb; so.FindProperty("value").objectReferenceValue = val;
    so.FindProperty("previous").objectReferenceValue = prev; so.FindProperty("next").objectReferenceValue = nxt;
    var arr = so.FindProperty("segments"); arr.arraySize = segCount;
    for (int i = 0; i < segCount; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = segs[i];
    so.FindProperty("segmentOn").colorValue = phosphor; so.FindProperty("segmentOff").colorValue = new Color(phosphor.r, phosphor.g, phosphor.b, 0.15f);
    so.FindProperty("hint").stringValue = hintText;
    so.ApplyModifiedPropertiesWithoutUndo();
    return ts;
};
float PW = SW - 2 * M, row = 28f;

// Звук
var pa = tabPages[0];
var sMusic = selector("Music", pa, "Музыка", 0f, 0f, PW, 190f, 70f, 10, "Громкость музыки в меню и в заезде");
var sEngine = selector("Engine", pa, "Двигатели", 0f, row, PW, 190f, 70f, 10, "Громкость моторов, шин и ударов");
var sAmbient = selector("Ambient", pa, "Окружение", 0f, row * 2, PW, 190f, 70f, 10, "Громкость фоновых звуков: ветер, бункер, стрельба вдали");

// Графика: пресет во всю ширину и два столбца параметров
var pg = tabPages[1];
var sPreset = selector("Preset", pg, "Пресет качества", 0f, 0f, PW, 190f, 120f, 0, "Набор всех параметров графики сразу. Ручная правка любого параметра даёт пресет «Своё»");
float colW = (PW - 16f) / 2f;
string[,] gfx = {
    { "RenderScale", "Масштаб рендера", "Разрешение, в котором рисуется кадр. Меньше — быстрее, но картинка мягче" },
    { "AntiAliasing", "Сглаживание", "MSAA убирает «лесенку» на краях. 4x и 8x заметно нагружают видеокарту" },
    { "Shadows", "Тени", "Разрешение и дальность теней. Самый тяжёлый параметр после масштаба" },
    { "Ssao", "Затенение SSAO", "Мягкое затенение в углах и щелях: объём сцены ценой нескольких FPS" },
    { "PostProcessing", "Постобработка", "Цветокоррекция, свечение и виньетка камеры" },
    { "Textures", "Текстуры", "Чёткость текстур. Ниже — меньше видеопамяти" },
    { "Anisotropic", "Анизотропия", "Чёткость дороги и земли под острым углом" },
    { "Grass", "Трава", "Дальность и густота травы на земле" },
    { "Trees", "Деревья", "С какого расстояния деревья становятся плоскими картинками" },
    { "Detail", "Детализация", "Дальность, на которой модели переключаются на упрощённые" },
};
var sGfx = new RacingProject.Management.TerminalSelector[10];
for (int i = 0; i < 10; i++) {
    int column = i / 5, rowIndex = i % 5;
    sGfx[i] = selector(gfx[i, 0], pg, gfx[i, 1], column * (colW + 16f), 36f + rowIndex * row, colW, 134f, 108f, 0, gfx[i, 2]);
}

// Экран
var pd = tabPages[2];
var sView = selector("ViewMode", pd, "Режим игры", 0f, 0f, PW, 190f, 150f, 0, "Шлем VR или монитор с мышью. Без подключённого шлема остаётся монитор");
var sWindow = selector("WindowMode", pd, "Окно", 0f, row, PW, 190f, 150f, 0, "Полный экран без рамки, окно или монопольный режим. В VR не действует");
var sRes = selector("Resolution", pd, "Разрешение", 0f, row * 2, PW, 190f, 150f, 0, "Разрешение окна игры на мониторе");
var sVSync = selector("VSync", pd, "Вертикальная синхронизация", 0f, row * 3, PW, 190f, 150f, 0, "Убирает разрывы кадра; частота кадров равна частоте монитора");
var sLimit = selector("FrameLimit", pd, "Ограничение кадров", 0f, row * 4, PW, 190f, 150f, 0, "Верхний предел FPS при выключенной синхронизации");

// Подвал: приглашение с курсором и подсказка к строке под указателем
hline(sc, M, SH - 34f, SW - 2 * M);
var sPrompt = text("Prompt", sc, ">", 12f, phosphor, TMPro.TextAlignmentOptions.MidlineLeft); place(sPrompt.rectTransform, M, SH - 28f, 14f, 18f);
var sCursor = image("Cursor", sc, phosphor); place(sCursor.rectTransform, M + 13f, SH - 25f, 8f, 12f);
var sHint = text("Hint", sc, "", 10f, dim, TMPro.TextAlignmentOptions.MidlineLeft); place(sHint.rectTransform, M + 28f, SH - 28f, SW - 2 * M - 28f, 18f);
screenFx(set, sc, sClock, sCursor, new TMPro.TMP_Text[] { sHint });

var sm = set.GetComponent<RacingProject.Management.SettingsMenu>();
if (sm == null) sm = set.gameObject.AddComponent<RacingProject.Management.SettingsMenu>();
{
    var so = new SerializedObject(sm);
    so.FindProperty("manager").objectReferenceValue = manager;
    var tb = so.FindProperty("tabButtons"); var tp = so.FindProperty("tabPages"); var tg = so.FindProperty("tabBackgrounds"); var tl = so.FindProperty("tabLabels");
    tb.arraySize = 3; tp.arraySize = 3; tg.arraySize = 3; tl.arraySize = 3;
    for (int i = 0; i < 3; i++) {
        tb.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];
        tp.GetArrayElementAtIndex(i).objectReferenceValue = tabPages[i].gameObject;
        tg.GetArrayElementAtIndex(i).objectReferenceValue = tabBacks[i];
        tl.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i].transform.Find("Text (TMP)").GetComponent<TMPro.TMP_Text>();
    }
    so.FindProperty("tabOn").colorValue = new Color(phosphor.r, phosphor.g, phosphor.b, 0.9f);
    so.FindProperty("labelOn").colorValue = bg; so.FindProperty("labelOff").colorValue = phosphor;
    so.FindProperty("tabOff").colorValue = new Color(phosphor.r, phosphor.g, phosphor.b, 0.06f);
    so.FindProperty("music").objectReferenceValue = sMusic; so.FindProperty("engine").objectReferenceValue = sEngine; so.FindProperty("ambient").objectReferenceValue = sAmbient;
    so.FindProperty("preset").objectReferenceValue = sPreset;
    var ga = so.FindProperty("graphics"); ga.arraySize = 10;
    for (int i = 0; i < 10; i++) ga.GetArrayElementAtIndex(i).objectReferenceValue = sGfx[i];
    so.FindProperty("viewMode").objectReferenceValue = sView; so.FindProperty("windowMode").objectReferenceValue = sWindow;
    so.FindProperty("resolution").objectReferenceValue = sRes; so.FindProperty("vSync").objectReferenceValue = sVSync; so.FindProperty("frameLimit").objectReferenceValue = sLimit;
    so.FindProperty("hint").objectReferenceValue = sHint;
    so.ApplyModifiedPropertiesWithoutUndo();
}
// В редакторе видна первая вкладка
for (int i = 0; i < 3; i++) tabPages[i].gameObject.SetActive(i == 0);
log.AppendLine("settings ok " + SW + "x" + SH + " arrows " + arrowL + arrowR);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
return log.ToString();
