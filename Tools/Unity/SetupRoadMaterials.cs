// Материалы дорог (Assets/Content/Roads): импорт текстур из Tools/Textures/generate_road_textures.py,
// материалы URP Lit и физические материалы покрытий. Запускать после генерации текстур, до BuildRoads.cs.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск обновляет материалы на месте.
string root = "Assets/Content/Roads";
string[] names = { "Road_Asphalt", "Road_AsphaltWorn", "Road_Gravel", "Road_Sidewalk", "Road_Curb", "Road_Paint" };
AssetDatabase.Refresh();
foreach (var n in names) {
    foreach (var suffix in new[] { "", "_Normal" }) {
        string path = root + "/Textures/" + n + suffix + ".png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) continue;
        imp.textureType = suffix == "" ? TextureImporterType.Default : TextureImporterType.NormalMap;
        imp.sRGBTexture = suffix == "";
        imp.alphaSource = suffix == "" ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.anisoLevel = 8; // дорогу видно под острым углом
        imp.maxTextureSize = 2048;
        imp.SaveAndReimport();
    }
}
if (!AssetDatabase.IsValidFolder(root + "/Materials")) AssetDatabase.CreateFolder(root, "Materials");
if (!AssetDatabase.IsValidFolder(root + "/Physics")) AssetDatabase.CreateFolder(root, "Physics");
var lit = Shader.Find("Universal Render Pipeline/Lit");
// Материал с картой цвета и нормалей, гладкость из альфы цвета (как у материалов частного сектора)
System.Func<string, string, float, Material> mat = (name, tex, bump) => {
    string path = root + "/Materials/" + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
    m.shader = lit;
    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/Textures/" + tex + ".png"));
    m.SetColor("_BaseColor", Color.white);
    m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(root + "/Textures/" + tex + "_Normal.png"));
    m.SetFloat("_BumpScale", bump);
    m.EnableKeyword("_NORMALMAP");
    m.SetFloat("_Smoothness", 1f);
    m.SetFloat("_SmoothnessTextureChannel", 1f);
    m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
    m.SetFloat("_Metallic", 0f);
    m.SetFloat("_AlphaClip", 0f);
    m.DisableKeyword("_ALPHATEST_ON");
    m.enableInstancing = true;
    EditorUtility.SetDirty(m);
    return m;
};
mat("Road_Asphalt", "Road_Asphalt", 1f);
mat("Road_AsphaltWorn", "Road_AsphaltWorn", 1.2f);
mat("Road_Gravel", "Road_Gravel", 1f);
mat("Road_Sidewalk", "Road_Sidewalk", 1f);
mat("Road_Curb", "Road_Curb", 1f);
// Разметка: альфа текстуры — остаток краски, порог отсечки задаёт износ. Гладкость постоянная (альфа занята)
System.Action<string, float, float> paint = (name, cutoff, smooth) => {
    var m = mat(name, "Road_Paint", 0.5f);
    m.SetFloat("_SmoothnessTextureChannel", 0f);
    m.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
    m.SetFloat("_Smoothness", smooth);
    m.SetFloat("_AlphaClip", 1f);
    m.SetFloat("_Cutoff", cutoff);
    m.EnableKeyword("_ALPHATEST_ON");
    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
    m.SetOverrideTag("RenderType", "TransparentCutout");
    EditorUtility.SetDirty(m);
};
paint("Road_PaintCity", 0.22f, 0.35f);  // в городе краска почти целая
paint("Road_PaintWorn", 0.6f, 0.25f);   // на трассе от неё остались клочки
// Физические материалы: значения как у прежних покрытий Road Architect (асфальт 1/2, грунт 0,4)
System.Action<string, float, float> phys = (name, dyn, stat) => {
    string path = root + "/Physics/" + name + ".physicMaterial";
    var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
    if (pm == null) { pm = new PhysicsMaterial(name); AssetDatabase.CreateAsset(pm, path); }
    pm.dynamicFriction = dyn; pm.staticFriction = stat; pm.bounciness = 0f;
    pm.frictionCombine = PhysicsMaterialCombine.Average; pm.bounceCombine = PhysicsMaterialCombine.Average;
    EditorUtility.SetDirty(pm);
};
phys("RoadAsphalt", 1f, 2f);
phys("RoadGravel", 0.4f, 0.4f);
AssetDatabase.SaveAssets();
return "ok";
