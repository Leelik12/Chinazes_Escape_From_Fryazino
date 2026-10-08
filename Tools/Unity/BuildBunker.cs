// Бункер-командный пункт для стартового меню (Menu/Bunker).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Модели — Tools/Blender/bunker/build_models.py (Assets/Models/Bunker), префабы — Assets/Prefabs/Bunker,
// материалы BK_* и карта района — Assets/Content/Bunker. Каждый запуск пересобирает Menu/Bunker с нуля.
// Модели построены в общих координатах комнаты: ноль — пол под глазами сидящего оператора, взгляд вдоль +Y Blender.
// После импорта Blender (x, y, z) становится Unity (−x, z, −y), поэтому оператор смотрит в −Z корня.
// Корень ставится под XR Rig Menu и поворачивается так, чтобы −Z смотрел туда же, куда камера меню.
// Холсты Main и Settings ложатся на экраны пульта, Tips — листком на пробковую доску (и выходит из-под Canvases,
// чтобы MenuTheme не перекрашивал его в цвета темы). Бункер — дочерний объект Menu и прячется вместе с меню.
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
var pinMat = mat("BK_Pin", new Color(0.7f, 0.08f, 0.06f), 0.6f, Color.black);

// --- Импорт моделей: масштаб 1 (бункер в человеческом масштабе, не как город), материалы PS_* и BK_* ---
var models = new[] { "Bunker_Room", "Bunker_Door", "Bunker_Console", "Bunker_Lamp", "Bunker_Props" };
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
    sign("УКРЫТИЕ № 17", B(X1 - 0.017f, -2.1f, 1.61f), B(1f, 0f, 0f), new Vector2(0.54f, 0.16f), new Color(0.12f, 0.1f, 0.08f));
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
