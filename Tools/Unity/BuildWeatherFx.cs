// Дождь на лобовом стекле машины игроков: капли (RainDrops.png и RainDrops_Normal.png из
// Tools/Textures/generate_rain_drops.py), материал капель и резины дворников, WindshieldRain на VolgaCar.
// Нужен «Windshield» из Tools/Unity/BuildDamageAndRpg.cs.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск обновляет материалы и настройки компонента.
string FX = "Assets/Content/Effects/";
AssetDatabase.Refresh();
foreach (var name in new[] { "RainDrops", "RainDrops_Normal" }) {
    var ti = (TextureImporter)AssetImporter.GetAtPath(FX + name + ".png");
    bool normal = name.EndsWith("_Normal");
    ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
    ti.sRGBTexture = !normal;
    ti.alphaIsTransparency = !normal;
    ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 1024; ti.mipmapEnabled = true;
    ti.SaveAndReimport();
}
var lit = Shader.Find("Universal Render Pipeline/Lit");
string dropsPath = FX + "RainDrops.mat";
var drops = AssetDatabase.LoadAssetAtPath<Material>(dropsPath);
if (drops == null) { drops = new Material(lit); AssetDatabase.CreateAsset(drops, dropsPath); }
drops.shader = lit;
drops.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FX + "RainDrops.png"));
drops.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FX + "RainDrops_Normal.png"));
drops.SetFloat("_BumpScale", 1f);
drops.EnableKeyword("_NORMALMAP");
drops.SetColor("_BaseColor", new Color(0.85f, 0.9f, 1f, 0.8f));
// Каждая сторона стекла — свой квад (WindshieldRain), обратные грани не рисуются
drops.SetFloat("_Surface", 1f); drops.SetFloat("_Blend", 0f); drops.SetFloat("_Cull", 2f);
drops.SetFloat("_Smoothness", 0.95f); drops.SetFloat("_Metallic", 0f);
// Ночью в каплях преломляется свет фонарей и фар: слабое свечение по рисунку капель, иначе изнутри их не видно
drops.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FX + "RainDrops.png"));
drops.SetColor("_EmissionColor", new Color(0.16f, 0.17f, 0.2f));
drops.EnableKeyword("_EMISSION");
drops.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
// Как у трещин: обычное альфа-смешивание без сохранения блика, иначе прозрачная часть квада бликует квадратом
drops.SetFloat("_BlendModePreserveSpecular", 0f);
drops.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
drops.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
drops.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
drops.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
drops.SetFloat("_ZWrite", 0f);
drops.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
drops.DisableKeyword("_ALPHAPREMULTIPLY_ON");
drops.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5;
drops.SetShaderPassEnabled("ShadowCaster", false);
EditorUtility.SetDirty(drops);

string rubberPath = FX + "WiperRubber.mat";
var rubber = AssetDatabase.LoadAssetAtPath<Material>(rubberPath);
if (rubber == null) { rubber = new Material(lit); AssetDatabase.CreateAsset(rubber, rubberPath); }
rubber.shader = lit;
rubber.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.045f, 1f));
rubber.SetFloat("_Smoothness", 0.35f); rubber.SetFloat("_Metallic", 0.2f);
EditorUtility.SetDirty(rubber);
AssetDatabase.SaveAssets();

var car = GameObject.Find("VolgaCar");
Transform windshield = null;
foreach (var t in car.GetComponentsInChildren<Transform>(true)) if (t.name == "Windshield") windshield = t;
if (windshield == null) throw new System.Exception("no Windshield, run BuildDamageAndRpg first");
var rain = car.GetComponent<RacingProject.Car.WindshieldRain>();
if (rain == null) rain = car.AddComponent<RacingProject.Car.WindshieldRain>();
var so = new SerializedObject(rain);
so.FindProperty("windshield").objectReferenceValue = windshield;
so.FindProperty("dropsMaterial").objectReferenceValue = drops;
so.FindProperty("wiperMaterial").objectReferenceValue = rubber;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(car.scene);
return "windshield rain ok";
