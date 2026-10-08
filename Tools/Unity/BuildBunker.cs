// Бункер-командный пункт для стартового меню (Menu/Bunker).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Модели — Tools/Blender/bunker/build_models.py (Assets/Models/Bunker), префабы — Assets/Prefabs/Bunker,
// материалы BK_* и карта района — Assets/Content/Bunker. Каждый запуск пересобирает Menu/Bunker с нуля.
// Модели построены в общих координатах комнаты: ноль — пол под глазами сидящего оператора, взгляд вдоль +Y Blender.
// После импорта Blender (x, y, z) становится Unity (−x, z, −y), поэтому оператор смотрит в −Z корня.
// Корень ставится под XR Rig Menu и поворачивается так, чтобы −Z смотрел туда же, куда камера меню.
// Холсты Main и Settings ложатся на экраны пульта, Tips — листком на пробковую доску (и выходит из-под Canvases).
// Подвижные детали (створка двери со штурвалом, кремальеры, стрелки, тумблеры, линзы ламп) — Bunker_Moving.fbx;
// приборами пульта управляет BunkerConsole, звуками, тумблерами и разрывами наверху — BunkerLife,
// выходом из бункера при старте раунда — MenuExitSequence.
// Бункер — дочерний объект Menu и прячется вместе с меню.
AssetDatabase.Refresh(); // свежие FBX из Blender: иначе ремап увидит старые имена материалов
var menu = GameObject.Find("Menu").transform;
var canvases = menu.Find("Canvases");
var rig = menu.Find("XR Rig Menu");
var cam = rig.GetComponentInChildren<Camera>(true);
string MD = "Assets/Models/Bunker/", PD = "Assets/Prefabs/Bunker/", CD = "Assets/Content/Bunker/";
var log = new System.Text.StringBuilder();
foreach (var dir in new[] { "Assets/Prefabs/Bunker", "Assets/Content/Bunker", "Assets/Content/Bunker/Materials", "Assets/Content/Bunker/Textures" })
    if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(dir).Replace('\\', '/'), System.IO.Path.GetFileName(dir));

// Размеры из build_models.py (метры Blender)
float X1 = 1.7f, Y1 = 1.6f, SCREEN_Y = 1.0f, SCREEN_Z = 1.2f, SECTION = 55f, MAP = 0.8f;
float mainW = 0.84f, tipsW = 0.9f;
System.Func<float, float, float, Vector3> B = (x, y, z) => new Vector3(-x, z, -y); // точка Blender → локальная Unity

// --- Материалы BK_* ---
// Текстурные: цвет с гладкостью в альфе и карта нормалей из Tools/Textures/generate_bunker_textures.py.
// Остальные — однотонные (резина, стекло, светящиеся экран, колбы и индикаторы)
var lit = Shader.Find("Universal Render Pipeline/Lit");
System.Func<string, Color, float, Color, Material> mat = (name, baseColor, smooth, emission) => {
    string path = CD + "Materials/" + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
    m.shader = lit;
    m.SetColor("_BaseColor", baseColor);
    m.SetFloat("_Smoothness", smooth);
    m.SetFloat("_Metallic", 0f);
    m.SetTexture("_BaseMap", null); m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP");
    m.SetFloat("_SmoothnessTextureChannel", 0f); m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
    if (emission.maxColorComponent > 0f) {
        m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
    } else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
    EditorUtility.SetDirty(m);
    return m;
};
System.Func<string, float, Material> texMat = (name, metallic) => {
    foreach (var suffix in new[] { "", "_Normal" }) {
        var ti = (TextureImporter)AssetImporter.GetAtPath(CD + "Textures/" + name + suffix + ".png");
        ti.textureType = suffix == "" ? TextureImporterType.Default : TextureImporterType.NormalMap;
        ti.sRGBTexture = suffix == "";
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = false;
        ti.mipmapEnabled = true; ti.anisoLevel = 4; ti.maxTextureSize = 1024;
        ti.SaveAndReimport();
    }
    var m = mat(name, Color.white, 1f, Color.black);
    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CD + "Textures/" + name + ".png"));
    m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CD + "Textures/" + name + "_Normal.png"));
    m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
    m.SetFloat("_SmoothnessTextureChannel", 1f); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
    m.SetFloat("_Metallic", metallic);
    return m;
};
var bkMats = new System.Collections.Generic.Dictionary<string, Material>();
foreach (var n in new[] { "BK_Concrete", "BK_Floor", "BK_WallPaint", "BK_Paint", "BK_PaintRed", "BK_PaintCream", "BK_PaintYellow", "BK_Burlap", "BK_Wood" })
    bkMats[n] = texMat(n, 0f);
bkMats["BK_Steel"] = texMat("BK_Steel", 0.75f);
bkMats["BK_Galv"] = texMat("BK_Galv", 0.6f);
bkMats["BK_Screen"] = mat("BK_Screen", new Color(0.01f, 0.02f, 0.015f), 0.8f, new Color(0.015f, 0.07f, 0.05f));
bkMats["BK_Bulb"] = mat("BK_Bulb", new Color(1f, 0.85f, 0.6f), 0.5f, new Color(1f, 0.72f, 0.4f) * 4f);
bkMats["BK_Rubber"] = mat("BK_Rubber", new Color(0.04f, 0.04f, 0.04f), 0.3f, Color.black);
bkMats["BK_Glass"] = mat("BK_Glass", new Color(0.05f, 0.06f, 0.06f), 0.95f, Color.black);
bkMats["BK_LampRed"] = mat("BK_LampRed", new Color(0.6f, 0.05f, 0.03f), 0.9f, new Color(1f, 0.08f, 0.04f) * 2.5f);
bkMats["BK_LampGreen"] = mat("BK_LampGreen", new Color(0.1f, 0.5f, 0.15f), 0.9f, new Color(0.2f, 1f, 0.3f) * 2f);
bkMats["BK_LampAmber"] = mat("BK_LampAmber", new Color(0.7f, 0.45f, 0.1f), 0.9f, new Color(1f, 0.55f, 0.1f) * 1.6f);
// Дневной свет в проёме выхода наверх за гермодверью
bkMats["BK_Daylight"] = mat("BK_Daylight", new Color(0.85f, 0.9f, 1f), 0f, new Color(0.85f, 0.9f, 1f) * 3f);
var pinMat = mat("BK_Pin", new Color(0.7f, 0.08f, 0.06f), 0.6f, Color.black);

// --- Импорт моделей: масштаб 1 (бункер в человеческом масштабе, не как город), материалы PS_* и BK_* ---
var models = new[] { "Bunker_Room", "Bunker_Door", "Bunker_Console", "Bunker_Lamp", "Bunker_Props", "Bunker_Moving" };
foreach (var name in models) {
    var imp = (ModelImporter)AssetImporter.GetAtPath(MD + name + ".fbx");
    imp.globalScale = 1f; imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
    imp.addCollider = false;
    imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
    foreach (var r in AssetDatabase.LoadAllAssetsAtPath(MD + name + ".fbx")) {
        var m = r as Material; if (m == null) continue;
        Material target;
        if (!bkMats.TryGetValue(m.name, out target))
            target = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/" + m.name + ".mat");
        if (target != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
        else log.AppendLine("no material " + m.name + " in " + name);
    }
    imp.SaveAndReimport();
}

// --- Префабы ---
var prefabs = new System.Collections.Generic.Dictionary<string, GameObject>();
foreach (var name in models) {
    var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MD + name + ".fbx"));
    PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    // Тени отбрасывает всё, кроме самой лампы: иначе абажур и колба запирают её свет
    foreach (var mr in inst.GetComponentsInChildren<MeshRenderer>())
        mr.shadowCastingMode = name == "Bunker_Lamp" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
    if (name == "Bunker_Lamp") {
        // Тёплая лампа под решёткой с лёгким мерцанием и мягкими тенями
        var lg = new GameObject("Light"); lg.transform.SetParent(inst.transform, false);
        lg.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        var light = lg.AddComponent<Light>();
        light.type = LightType.Point; light.color = new Color(1f, 0.74f, 0.45f); light.intensity = 5f; light.range = 8f;
        light.shadows = LightShadows.Soft; light.shadowStrength = 0.9f; light.shadowNearPlane = 0.1f;
        var fl = lg.AddComponent<RacingProject.FlickerLight>();
        var so = new SerializedObject(fl);
        so.FindProperty("depth").floatValue = 0.12f; so.FindProperty("speed").floatValue = 2.5f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    prefabs[name] = PrefabUtility.SaveAsPrefabAsset(inst, PD + name + ".prefab");
    UnityEngine.Object.DestroyImmediate(inst);
}

// --- Карта района: ортографический снимок местности сверху, тонированный под старую бумагу, с дорогами ---
var terrain = Terrain.activeTerrain; var tsize = terrain.terrainData.size; var tpos = terrain.transform.position;
var mapPath = CD + "Textures/DistrictMap.png";
{
    int res = 1024;
    var camGo = new GameObject("MapCam"); var mc = camGo.AddComponent<Camera>();
    float side = Mathf.Max(tsize.x, tsize.z);
    mc.orthographic = true; mc.orthographicSize = side / 2f; mc.aspect = 1f;
    mc.transform.position = tpos + new Vector3(tsize.x / 2f, tsize.y + 500f, tsize.z / 2f);
    mc.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    mc.nearClipPlane = 1f; mc.farClipPlane = tsize.y + 1000f;
    mc.clearFlags = CameraClearFlags.SolidColor; mc.backgroundColor = Color.gray;
    var rt = new RenderTexture(res, res, 24); mc.targetTexture = rt;
    bool menuActive = menu.gameObject.activeSelf; menu.gameObject.SetActive(false);
    mc.Render();
    menu.gameObject.SetActive(menuActive);
    RenderTexture.active = rt;
    var tex = new Texture2D(res, res, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, res, res), 0, 0);
    RenderTexture.active = null; mc.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(camGo);
    var px = tex.GetPixels();
    var paper = new Color(0.84f, 0.78f, 0.62f); var ink = new Color(0.25f, 0.22f, 0.16f);
    for (int i = 0; i < px.Length; i++) {
        float l = Mathf.Clamp01(px[i].grayscale * 1.6f);
        // Квантование яркости даёт «топографические» пятна вместо фотографии
        l = Mathf.Round(l * 5f) / 5f;
        px[i] = Color.Lerp(ink, paper, 0.35f + 0.65f * l);
    }
    // Сетка километровых квадратов
    float pxPerM = res / side;
    for (int i = 0; i < res; i++)
        for (int j = 0; j < res; j++)
            if (Mathf.Repeat(i / pxPerM, 250f) < 1.2f / pxPerM * 2f || Mathf.Repeat(j / pxPerM, 250f) < 1.2f / pxPerM * 2f)
                px[j * res + i] = Color.Lerp(px[j * res + i], ink, 0.35f);
    // Дороги из RoadNetwork красным карандашом
    var net = GameObject.Find("Environment").transform.Find("RoadNetwork").GetComponent<RacingProject.Enemy.RoadNetwork>();
    var red = new Color(0.6f, 0.12f, 0.08f);
    float cx0 = tpos.x + tsize.x / 2f - side / 2f, cz0 = tpos.z + tsize.z / 2f - side / 2f;
    foreach (var road in net.Roads)
        foreach (var p in road.points) {
            int u = Mathf.RoundToInt((p.x - cx0) * pxPerM), v = Mathf.RoundToInt((p.z - cz0) * pxPerM);
            for (int du = -3; du <= 3; du++) for (int dv = -3; dv <= 3; dv++) {
                int a = u + du, bb = v + dv;
                if (a >= 0 && a < res && bb >= 0 && bb < res && du * du + dv * dv <= 9) px[bb * res + a] = red;
            }
        }
    tex.SetPixels(px); tex.Apply();
    System.IO.File.WriteAllBytes(mapPath, tex.EncodeToPNG());
    UnityEngine.Object.DestroyImmediate(tex);
    AssetDatabase.ImportAsset(mapPath);
}
var mapMat = mat("BK_Map", Color.white, 0.1f, Color.black);
mapMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath));

// --- Сцена: корень Menu/Bunker ---
var tips = canvases.Find("Tips");
var oldRoot = menu.Find("Bunker");
if (oldRoot != null) {
    if (tips == null) { tips = oldRoot.Find("Tips"); tips.SetParent(menu, true); }
    UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
}
var oldPins = new System.Collections.Generic.List<GameObject>();
foreach (Transform t in tips) if (t.name == "Pin") oldPins.Add(t.gameObject);
foreach (var pg in oldPins) UnityEngine.Object.DestroyImmediate(pg);
var root = new GameObject("Bunker").transform;
root.SetParent(menu, false);
// Пол — уровень XR Rig Menu, начало — под камерой; −Z корня смотрит туда же, куда камера
root.position = new Vector3(cam.transform.position.x, rig.position.y, cam.transform.position.z);
root.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y + 180f, 0f);
foreach (var name in models) {
    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[name], root);
    go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity;
}
// Лампы под сводом: над оператором (LAMP_POS из build_models.py) и вторая в глубине у ящиков
{
    float X0 = -2.3f, half = (X1 - X0) / 2f, rise = 0.75f, r = (half * half + rise * rise) / (2f * rise), cxv = (X0 + X1) / 2f;
    float vz = 2.2f + rise - r + Mathf.Sqrt(r * r - (-0.3f - cxv) * (-0.3f - cxv));
    root.Find("Bunker_Lamp").localPosition = B(-0.3f, -0.7f, vz);
    var lamp2 = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["Bunker_Lamp"], root);
    lamp2.transform.localPosition = B(-0.3f, -2.6f, vz);
    var l2 = lamp2.GetComponentInChildren<Light>();
    l2.intensity = 3f; l2.shadows = LightShadows.None; // вторая лампа без теней: экономим на точечных тенях
}
System.Func<string, Vector3, Light> addLight = (lname, pos) => {
    var g = new GameObject(lname); g.transform.SetParent(root, false); g.transform.localPosition = pos;
    return g.AddComponent<Light>();
};
// Настольная лампа: прожектор в абажуре (точка и наклон абажура как в desk_lamp() build_models.py)
{
    float a = 32f * Mathf.Deg2Rad, dz = 0.76f + 0.035f;
    var c = new Vector3(-Mathf.Sin(a) * 1.2f, Mathf.Cos(a) * 1.2f, dz);
    var j3 = c + new Vector3(0f, 0.1f, 0.35f) + new Vector3(Mathf.Sin(a) * 0.05f, -Mathf.Cos(a) * 0.32f, 0.02f);
    var dir = new Vector3(0f, -0.35f, -1f).normalized;
    var p = j3 + dir * 0.04f;
    var l = addLight("DeskLampLight", B(p.x, p.y, p.z));
    l.transform.localRotation = Quaternion.LookRotation(B(dir.x, dir.y, dir.z), Vector3.up);
    l.type = LightType.Spot; l.spotAngle = 95f; l.innerSpotAngle = 50f; l.range = 2.5f; l.intensity = 2.2f;
    l.color = new Color(1f, 0.82f, 0.6f); l.shadows = LightShadows.Soft; l.shadowNearPlane = 0.05f;
}
// Настенный светильник у двери (BULKHEAD) — тусклый, без теней
{
    var l = addLight("BulkheadLight", B(-2.3f + 0.13f, -1.25f, 2.05f));
    l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.55f); l.intensity = 1.2f; l.range = 3f; l.shadows = LightShadows.None;
}

// Холст на экран: центр экрана в Blender на угле deg пульта, холст на 5 мм перед утопленным стеклом
System.Action<Transform, float, float> onScreen = (canvas, deg, width) => {
    float a = deg * Mathf.Deg2Rad, d = SCREEN_Y + 0.04f;
    canvas.SetParent(canvases, true);
    canvas.position = root.TransformPoint(B(-Mathf.Sin(a) * d, Mathf.Cos(a) * d, SCREEN_Z));
    var fwd = root.TransformDirection(B(-Mathf.Sin(a), Mathf.Cos(a), 0f));
    canvas.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    float s = width / ((RectTransform)canvas).sizeDelta.x;
    canvas.localScale = new Vector3(s, s, s);
};
onScreen(canvases.Find("Main"), 0f, mainW);
onScreen(canvases.Find("Settings"), SECTION, mainW);
// Подсветка от экранов
foreach (var deg in new[] { 0f, SECTION }) {
    float a = deg * Mathf.Deg2Rad;
    var g = new GameObject("ScreenGlow"); g.transform.SetParent(root, false);
    g.transform.localPosition = B(-Mathf.Sin(a) * 0.75f, Mathf.Cos(a) * 0.75f, SCREEN_Z);
    var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.55f, 0.85f, 0.75f);
    l.intensity = 0.6f; l.range = 2.2f; l.shadows = LightShadows.None;
}

// Трафаретные надписи на табличках (шрифт BlackOpsOne): «ВЫХОД» на гермодвери, «УКРЫТИЕ № 17» на правой стене
{
    var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Content/BlackOpsOne-Regular.asset");
    System.Action<string, Vector3, Vector3, Vector2, Color> sign = (text, pos, look, size, color) => {
        var g = new GameObject("Sign_" + text.Split(' ')[0]); g.transform.SetParent(root, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = Quaternion.LookRotation(look, Vector3.up);
        var t = g.AddComponent<TMPro.TextMeshPro>();
        t.font = font; t.text = text; t.color = color;
        t.alignment = TMPro.TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = 0.05f; t.fontSizeMax = 3f;
        t.rectTransform.sizeDelta = size;
    };
    sign("ВЫХОД", B(-2.3f + 0.212f, -2.1f, 1.75f), B(-1f, 0f, 0f), new Vector2(0.42f, 0.11f), new Color(0.55f, 0.08f, 0.05f));
    root.Find("Sign_ВЫХОД").SetParent(root.Find("Bunker_Moving/DoorLeaf"), true); // табличка на створке и открывается с ней
    sign("УКРЫТИЕ № 17", B(X1 - 0.017f, -2.1f, 1.61f), B(1f, 0f, 0f), new Vector2(0.54f, 0.16f), new Color(0.12f, 0.1f, 0.08f));
}

// Приборы пульта: панель перед экраном — strip_frame(deg) из build_models.py (x вдоль, y вверх по скату, z — нормаль)
var moving = root.Find("Bunker_Moving");
{
    float slope = Mathf.Atan2(0.1f, 0.33f), DESK_Z = 0.76f;
    System.Func<float, Vector3, Vector3> panelDirB = (deg, v) => {
        var r = new Vector3(v.x, v.y * Mathf.Cos(slope) - v.z * Mathf.Sin(slope), v.y * Mathf.Sin(slope) + v.z * Mathf.Cos(slope));
        float d = deg * Mathf.Deg2Rad;
        return new Vector3(r.x * Mathf.Cos(d) - r.y * Mathf.Sin(d), r.x * Mathf.Sin(d) + r.y * Mathf.Cos(d), r.z);
    };
    System.Func<float, float, float, float, Vector3> panelPt = (deg, x, y, z) => {
        var o = panelDirB(deg, new Vector3(x, y, z)); float d = deg * Mathf.Deg2Rad;
        var p = o + new Vector3(-0.6f * Mathf.Sin(d), 0.6f * Mathf.Cos(d), DESK_Z + 0.035f);
        return B(p.x, p.y, p.z);
    };
    System.Func<float, Vector3, Vector3> panelDir = (deg, v) => { var d = panelDirB(deg, v); return moving.InverseTransformDirection(root.TransformDirection(B(d.x, d.y, d.z))); };
    var cons = root.gameObject.AddComponent<RacingProject.Management.BunkerConsole>();
    var so = new SerializedObject(cons);
    var roomCtrl = UnityEngine.Object.FindObjectOfType<RacingProject.Network.RoomController>();
    so.FindProperty("room").objectReferenceValue = roomCtrl;
    so.FindProperty("lobby").objectReferenceValue = roomCtrl.GetComponent<RacingProject.Network.LanLobby>();
    var lamps = so.FindProperty("lamps"); lamps.arraySize = 5;
    for (int i = 0; i < 5; i++) lamps.GetArrayElementAtIndex(i).objectReferenceValue = moving.Find("Lens_" + i).GetComponent<MeshRenderer>();
    foreach (var g in new[] { new { prop = "signal", part = "Needle_Signal", deg = 0f }, new { prop = "ready", part = "Needle_Ready", deg = 0f }, new { prop = "volts", part = "Needle_Volts", deg = SECTION } }) {
        var gp = so.FindProperty(g.prop);
        gp.FindPropertyRelative("needle").objectReferenceValue = moving.Find(g.part);
        gp.FindPropertyRelative("normal").vector3Value = panelDir(g.deg, Vector3.forward).normalized;
        gp.FindPropertyRelative("right").vector3Value = panelDir(g.deg, Vector3.right).normalized;
        gp.FindPropertyRelative("up").vector3Value = panelDir(g.deg, Vector3.up).normalized;
    }
    so.ApplyModifiedPropertiesWithoutUndo();
    foreach (Transform part in moving)
        if (part.name.StartsWith("Lens_") || part.name.StartsWith("Needle_") || part.name.StartsWith("Toggle_"))
            part.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

    // Надписи на лицевой пластине и шкалах: тёмная краска по кремовому, как гравировка
    var labelFont = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    var labels = new GameObject("PanelLabels").transform; labels.SetParent(root, false);
    System.Action<string, float, float, float, float, float, float> label = (text, deg, x, y, z, w, h) => {
        var g = new GameObject("Label_" + text); g.transform.SetParent(labels, false);
        g.transform.localPosition = panelPt(deg, x, y, z);
        var n = panelDirB(deg, Vector3.forward); var u = panelDirB(deg, Vector3.up);
        g.transform.localRotation = Quaternion.LookRotation(-B(n.x, n.y, n.z), B(u.x, u.y, u.z));
        var t = g.AddComponent<TMPro.TextMeshPro>();
        t.font = labelFont; t.text = text; t.color = new Color(0.13f, 0.11f, 0.09f);
        t.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase; t.alignment = TMPro.TextAlignmentOptions.Center;
        t.enableWordWrapping = false; t.enableAutoSizing = true; t.fontSizeMin = 0.01f; t.fontSizeMax = 0.2f;
        t.rectTransform.sizeDelta = new Vector2(w, h);
        g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    };
    string[] lampNames = { "Связь", "Водитель", "Поиск", "Нет связи", "Стрелок" };
    for (int i = 0; i < 5; i++) label(lampNames[i], 0f, -0.16f + i * 0.08f, 0.18f, 0.0035f, 0.076f, 0.014f);
    label("Сигнал", 0f, -0.33f, 0.174f, 0.0168f, 0.05f, 0.008f);
    label("Экипаж", 0f, 0.33f, 0.174f, 0.0168f, 0.05f, 0.008f);
    label("Сеть, В", SECTION, -0.3f, 0.163f, 0.0168f, 0.06f, 0.009f);
    // Цифры шкалы готовности: 0, 1 и 2 из двух на нуле, середине и конце шкалы
    for (int k = 0; k < 3; k++) {
        float a = (210f - 120f * k) * Mathf.Deg2Rad;
        label(k.ToString(), 0f, 0.33f + Mathf.Cos(a) * 0.029f, 0.2f + Mathf.Sin(a) * 0.029f, 0.0168f, 0.012f, 0.008f);
    }

    // Жизнь бункера: фоновые звуки, щелчки терминала и тумблеров, разрывы наверху (BunkerLife)
    var life = root.gameObject.AddComponent<RacingProject.Management.BunkerLife>();
    var lso = new SerializedObject(life);
    var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Content/Sound/AmbientMixer.mixer");
    lso.FindProperty("output").objectReferenceValue = mixer.FindMatchingGroups("Master")[0];
    System.Func<string, Vector3, Transform> marker = (mname, pos) => {
        var g = new GameObject(mname).transform; g.SetParent(root, false); g.localPosition = pos; return g;
    };
    // Фильтровентиляционная установка у задней стены и радиостанция на правой секции пульта
    lso.FindProperty("vent").objectReferenceValue = marker("VentSound", B(-0.45f, -3.1f, 1.2f));
    {
        float d = -SECTION * Mathf.Deg2Rad;
        lso.FindProperty("radio").objectReferenceValue = marker("RadioSound", B(-1.1f * Mathf.Sin(d), 1.1f * Mathf.Cos(d), 0.97f));
    }
    var hanging = new System.Collections.Generic.List<Transform>();
    foreach (Transform c in root) if (c.name == "Bunker_Lamp") hanging.Add(c);
    var hl = lso.FindProperty("hangingLamps"); hl.arraySize = hanging.Count;
    for (int i = 0; i < hanging.Count; i++) hl.GetArrayElementAtIndex(i).objectReferenceValue = hanging[i];
    var dipList = new System.Collections.Generic.List<Light>();
    foreach (var h in hanging) dipList.Add(h.GetComponentInChildren<Light>());
    dipList.Add(root.Find("DeskLampLight").GetComponent<Light>());
    dipList.Add(root.Find("BulkheadLight").GetComponent<Light>());
    var dl = lso.FindProperty("dipLights"); dl.arraySize = dipList.Count;
    for (int i = 0; i < dipList.Count; i++) dl.GetArrayElementAtIndex(i).objectReferenceValue = dipList[i];
    var rows = lso.FindProperty("toggleRows"); rows.arraySize = 2;
    for (int r = 0; r < 2; r++) {
        var row = rows.GetArrayElementAtIndex(r);
        float deg = r == 0 ? 0f : SECTION;
        string prefix = r == 0 ? "Toggle_Main_" : "Toggle_Settings_";
        row.FindPropertyRelative("screen").objectReferenceValue = canvases.Find(r == 0 ? "Main" : "Settings").GetComponent<Canvas>();
        var parts = new System.Collections.Generic.List<Transform>();
        foreach (Transform part in moving) if (part.name.StartsWith(prefix)) parts.Add(part);
        var tg = row.FindPropertyRelative("toggles"); tg.arraySize = parts.Count;
        for (int i = 0; i < parts.Count; i++) tg.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        row.FindPropertyRelative("axis").vector3Value = panelDir(deg, Vector3.right).normalized;
        row.FindPropertyRelative("up").vector3Value = panelDir(deg, Vector3.up).normalized;
        row.FindPropertyRelative("throwAngle").floatValue = 2f * Mathf.Atan2(0.012f, 0.03f) * Mathf.Rad2Deg; // 2 × TOGGLE_TILT
    }

    // Частицы: пылинки в лучах настольной и подвесной ламп (светятся, складываясь со светом) и пыль со свода
    var dotPath = CD + "Textures/DustMote.png";
    {
        var dot = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) {
            float rr = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f)) / 16f;
            dot.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - rr), 2f)));
        }
        System.IO.File.WriteAllBytes(dotPath, dot.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(dot);
        AssetDatabase.ImportAsset(dotPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(dotPath);
        ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
    }
    var partShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
    System.Func<string, bool, Material> partMat = (pname, additive) => {
        string path = CD + "Materials/" + pname + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(partShader); AssetDatabase.CreateAsset(m, path); }
        m.shader = partShader;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(dotPath)); m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        m.SetFloat("_ZWrite", 0f); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    };
    var moteMat = partMat("BK_DustMote", true);
    var fallMat = partMat("BK_DustFall", false);
    System.Func<string, Transform, Material, ParticleSystem> particles = (pname, parent, pm) => {
        var g = new GameObject(pname); g.transform.SetParent(parent, false);
        var ps = g.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var pr = g.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = pm;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        return ps;
    };
    System.Action<ParticleSystem, float, float, float, float, float> motes = (ps, angle, length, radius, rate, alpha) => {
        var main = ps.main; main.loop = true; main.prewarm = true; main.duration = 10f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.003f, 0.015f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.0025f, 0.006f); main.maxParticles = 250;
        main.startColor = new Color(1f, 0.86f, 0.65f, alpha);
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.ConeVolume; sh.angle = angle; sh.radius = radius; sh.length = length;
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.015f; nz.frequency = 0.4f; nz.scrollSpeed = 0.05f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;
        ps.Play();
    };
    motes(particles("DustMotes", root.Find("DeskLampLight"), moteMat), 38f, 0.75f, 0.03f, 18f, 0.55f);
    {
        var lampMotes = particles("DustMotes", hanging[0].GetComponentInChildren<Light>().transform, moteMat);
        lampMotes.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // конус вниз из-под решётки
        motes(lampMotes, 40f, 1.5f, 0.08f, 22f, 0.35f);
    }
    // Пыль со свода: только выпускается из BunkerLife при разрыве
    System.Func<string, ParticleSystem> ceilingDust = pname => {
        var ps = particles(pname, root, fallMat);
        ps.transform.localPosition = B(-0.3f, -0.9f, 2.75f);
        var main = ps.main; main.loop = true; main.playOnAwake = true; main.duration = 5f;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(3f, 0.05f, 3.8f);
        return ps;
    };
    {
        var crumbs = ceilingDust("DustFall");
        var main = crumbs.main; main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f); main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.012f); main.gravityModifier = 0.25f; main.maxParticles = 400;
        main.startColor = new Color(0.55f, 0.5f, 0.42f, 0.8f);
        var cloud = ceilingDust("DustCloud");
        var cm = cloud.main; cm.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f); cm.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        cm.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f); cm.gravityModifier = 0.03f; cm.maxParticles = 40;
        cm.startColor = new Color(0.5f, 0.46f, 0.4f, 0.07f);
        var ccol = cloud.colorOverLifetime; ccol.enabled = true;
        var cg = new Gradient(); cg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        ccol.color = cg;
        var csz = cloud.sizeOverLifetime; csz.enabled = true; csz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.4f));
        crumbs.Play(); cloud.Play();
        lso.FindProperty("dust").objectReferenceValue = crumbs;
        lso.FindProperty("dustCloud").objectReferenceValue = cloud;
    }
    lso.ApplyModifiedPropertiesWithoutUndo();

    // Выход из бункера при старте раунда (MenuExitSequence): оси и знаки поворотов подбираются здесь по геометрии,
    // поэтому не зависят от того, как FBX повернул детали при импорте
    var exit = root.gameObject.AddComponent<RacingProject.Management.MenuExitSequence>();
    // При старте раунда RoomController прячет весь Menu: с ним выключаются бункер, его звуки и BunkerAmbience
    // (возвращает солнце), а MenuExitSequence находится среди дочерних объектов
    { var rso = new SerializedObject(roomCtrl); rso.FindProperty("Menu").objectReferenceValue = menu.gameObject; rso.ApplyModifiedPropertiesWithoutUndo(); }
    var xso = new SerializedObject(exit);
    xso.FindProperty("lobby").objectReferenceValue = roomCtrl.GetComponent<RacingProject.Network.LanLobby>();
    xso.FindProperty("output").objectReferenceValue = mixer.FindMatchingGroups("Master")[0];
    var inward = root.TransformDirection(B(1f, 0f, 0f)); // нормаль стены с дверью — внутрь комнаты
    System.Func<Transform, Vector3> centerOf = part => part.TransformPoint(part.GetComponent<MeshFilter>().sharedMesh.bounds.center);
    // Знак угла, при котором точка детали уходит по направлению goal
    System.Func<Transform, Vector3, float, Vector3, float> signFor = (part, axisLocal, angle, goal) => {
        var rest = part.localRotation; var c0 = centerOf(part);
        part.localRotation = Quaternion.AngleAxis(angle, axisLocal) * rest; var c1 = centerOf(part);
        part.localRotation = rest;
        return Vector3.Dot(c1 - c0, goal) >= 0f ? angle : -angle;
    };
    var leafT = moving.Find("DoorLeaf");
    {
        var p = xso.FindProperty("leaf");
        var axis = leafT.parent.InverseTransformDirection(Vector3.up);
        p.FindPropertyRelative("transform").objectReferenceValue = leafT;
        p.FindPropertyRelative("axis").vector3Value = axis;
        p.FindPropertyRelative("angle").floatValue = signFor(leafT, axis, 80f, inward); // створка открывается в комнату
    }
    {
        var wheelT = leafT.Find("DoorWheel");
        var p = xso.FindProperty("wheel");
        p.FindPropertyRelative("transform").objectReferenceValue = wheelT;
        p.FindPropertyRelative("axis").vector3Value = wheelT.parent.InverseTransformDirection(inward);
        p.FindPropertyRelative("angle").floatValue = -720f; // два оборота против часовой, если смотреть из комнаты
    }
    var dogsProp = xso.FindProperty("dogs"); dogsProp.arraySize = 3;
    for (int i = 0; i < 3; i++) {
        var dogT = moving.Find("DoorDog_" + i);
        var p = dogsProp.GetArrayElementAtIndex(i);
        var axis = dogT.parent.InverseTransformDirection(inward);
        p.FindPropertyRelative("transform").objectReferenceValue = dogT;
        p.FindPropertyRelative("axis").vector3Value = axis;
        p.FindPropertyRelative("angle").floatValue = signFor(dogT, axis, 90f, Vector3.up); // рычаг откидывается вверх
    }
    {
        // Красная лампа тревоги над дверью и дневной свет в коридоре за ней (без теней: до открытия выключен)
        var alarm = addLight("AlarmLight", B(-2.3f + 0.25f, -2.1f, 2.35f));
        alarm.type = LightType.Point; alarm.color = new Color(1f, 0.12f, 0.06f); alarm.range = 4.5f; alarm.shadows = LightShadows.None;
        alarm.enabled = false;
        var day = addLight("DaylightLight", B(-2.3f - 0.8f, -2.1f, 1.5f));
        day.type = LightType.Point; day.color = new Color(0.85f, 0.9f, 1f); day.range = 4f; day.shadows = LightShadows.None;
        day.enabled = false;
        xso.FindProperty("alarm").objectReferenceValue = alarm;
        xso.FindProperty("daylight").objectReferenceValue = day;
    }
    xso.ApplyModifiedPropertiesWithoutUndo();
}

// Листок с советами на пробковой доске правой стены
tips.SetParent(root, true);
tips.localPosition = B(X1 - 0.03f, 0.35f, 1.5f);
tips.localRotation = Quaternion.LookRotation(B(1f, 0f, 0f), Vector3.up);
{
    float s = tipsW / ((RectTransform)tips).sizeDelta.x;
    tips.localScale = new Vector3(s, s, s);
    var panel = tips.Find("Panel").GetComponent<UnityEngine.UI.Image>();
    panel.color = new Color(0.9f, 0.87f, 0.78f);
    foreach (var t in tips.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.color = new Color(0.12f, 0.1f, 0.09f); t.fontStyle &= ~TMPro.FontStyles.UpperCase; }
    var size = ((RectTransform)tips).sizeDelta;
    foreach (var c in new[] { new Vector2(-1, 1), new Vector2(1, 1), new Vector2(-1, -1), new Vector2(1, -1) }) {
        var pin = GameObject.CreatePrimitive(PrimitiveType.Sphere); pin.name = "Pin";
        UnityEngine.Object.DestroyImmediate(pin.GetComponent<Collider>());
        pin.transform.SetParent(tips, false);
        pin.transform.localPosition = new Vector3(c.x * (size.x / 2f - 18f), c.y * (size.y / 2f - 18f), -4f);
        pin.transform.localScale = Vector3.one * 14f;
        pin.GetComponent<MeshRenderer>().sharedMaterial = pinMat;
    }
}

// Карта района над главным экраном
{
    var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "DistrictMap";
    UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
    q.transform.SetParent(root, false);
    q.transform.localPosition = B(0f, Y1 - 0.01f, 2.08f);
    q.transform.localRotation = Quaternion.LookRotation(B(0f, 1f, 0f), Vector3.up);
    q.transform.localScale = new Vector3(MAP, MAP, 1f);
    q.GetComponent<MeshRenderer>().sharedMaterial = mapMat;
    q.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
}

// Освещение и камера меню: мир за стенами не виден, дальняя плоскость 30 м
Light sun = RenderSettings.sun;
if (sun == null) foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional && l.isActiveAndEnabled) { sun = l; break; }
var amb = root.gameObject.AddComponent<RacingProject.Management.BunkerAmbience>();
var aso = new SerializedObject(amb); aso.FindProperty("sun").objectReferenceValue = sun; aso.ApplyModifiedPropertiesWithoutUndo();
cam.farClipPlane = 30f;

// Запечённый пробник отражений комнаты (металл, стекло экранов). Печётся при освещении «как в игре»:
// BunkerAmbience в редакторе не работает, поэтому солнце и общий свет на время запекания приглушаются вручную
{
    var pg = new GameObject("ReflectionProbe"); pg.transform.SetParent(root, false);
    pg.transform.localPosition = B(-0.3f, -0.9f, 1.4f);
    var probe = pg.AddComponent<ReflectionProbe>();
    probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked;
    probe.size = new Vector3(4.1f, 3.1f, 5.1f); probe.boxProjection = true; probe.resolution = 256;
    probe.nearClipPlane = 0.05f; probe.farClipPlane = 10f; probe.importance = 2;
    var ambSaved = RenderSettings.ambientLight; var reflSaved = RenderSettings.reflectionIntensity;
    bool sunSaved = sun != null && sun.enabled;
    RenderSettings.ambientLight = new Color(0.1f, 0.105f, 0.1f); RenderSettings.reflectionIntensity = 0.15f;
    if (sun != null) sun.enabled = false;
    string probePath = CD + "Textures/BunkerReflection.exr";
    Lightmapping.BakeReflectionProbe(probe, probePath);
    RenderSettings.ambientLight = ambSaved; RenderSettings.reflectionIntensity = reflSaved;
    if (sun != null) sun.enabled = sunSaved;
    // Свой кубмап вместо данных запекания сцены: пробник не зависит от общего запекания освещения
    AssetDatabase.ImportAsset(probePath);
    probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Custom;
    probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>(probePath);
    log.AppendLine("probe " + (probe.customBakedTexture != null));
}
log.AppendLine("root " + root.position + " yaw " + root.eulerAngles.y + " sun " + (sun ? sun.name : "none"));

AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
return log.ToString();
