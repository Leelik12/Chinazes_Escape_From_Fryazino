// Материалы эффектов в Assets/Resources/Fx (их грузят ExplosionFx, EnemySilhouettes, Searchlight, погода и пожары)
// и настройки импорта сгенерированных звуков (Generated/Vehicle, Combat, Weather).
// Запуск: execute_code (MCP for Unity, C# 6) вне Play. Повторный запуск обновляет то же самое
string fx = "Assets/Resources/Fx/";
var log = new System.Text.StringBuilder();
System.Func<string, Shader, Material> make = (name, shader) => {
    string path = fx + name + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
    if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
    else m.shader = shader;
    log.Append(name + " ");
    return m;
};

var shock = make("Fx_Shockwave", Shader.Find("RacingProject/Shockwave"));
shock.SetColor("_Color", new Color(2.4f, 1.5f, 0.7f, 1f));
shock.SetFloat("_Width", 0.1f);

var heat = make("Fx_Heat", Shader.Find("RacingProject/HeatDistortion"));
heat.SetFloat("_Strength", 0.025f);

var sil = make("Fx_Silhouette", Shader.Find("RacingProject/Silhouette"));
sil.SetColor("_Color", new Color(1.5f, 0.28f, 0.15f, 0.32f));
sil.SetFloat("_RimPower", 2f);

var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(fx + "Glow.png");
var glow = make("Fx_GlowNoFog", Shader.Find("RacingProject/GlowNoFog"));
glow.SetTexture("_BaseMap", glowTex);
glow.SetColor("_BaseColor", new Color(1.6f, 0.55f, 0.18f, 1f));

// Частицы URP: Unlit, прозрачные; blend 0 — альфа, 2 — аддитив
var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
System.Action<Material, int, Texture> setupParticle = (m, blend, tex) => {
    m.SetFloat("_Surface", 1f);
    m.SetFloat("_Blend", blend);
    m.SetFloat("_ZWrite", 0f);
    m.SetFloat("_Cull", 0f);
    m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
    m.SetFloat("_DstBlend", blend == 2 ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    m.SetOverrideTag("RenderType", "Transparent");
    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    if (tex != null) m.SetTexture("_BaseMap", tex);
    m.SetColor("_BaseColor", Color.white);
};

var scorch = make("Fx_Scorch", particles);
setupParticle(scorch, 0, AssetDatabase.LoadAssetAtPath<Texture2D>(fx + "Scorch.png"));
// Подпалина рисуется раньше прочей прозрачности, чтобы дым и огонь ложились поверх
scorch.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 50;

var beam = make("Fx_Beam", Shader.Find("RacingProject/LightBeam"));
beam.SetColor("_Color", new Color(1f, 0.96f, 0.86f, 1f));
beam.SetFloat("_EdgePower", 2f);
beam.SetFloat("_NearFade", 8f);

var rain = make("Fx_Rain", particles);
setupParticle(rain, 0, glowTex);
rain.SetColor("_BaseColor", new Color(0.75f, 0.8f, 0.88f, 0.55f));

var dust = make("Fx_Dust", particles);
setupParticle(dust, 0, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Fx/Glow.png"));
var mist = AssetDatabase.LoadAssetAtPath<Material>(fx + "Fx_Mist.mat");
if (mist != null && mist.GetTexture("_BaseMap") != null) dust.SetTexture("_BaseMap", mist.GetTexture("_BaseMap"));

var shard = make("Fx_GlassShard", particles);
setupParticle(shard, 2, glowTex);
shard.SetColor("_BaseColor", new Color(0.8f, 0.9f, 1f, 0.9f));

foreach (var m in new[] { shock, heat, sil, glow, scorch, beam, rain, dust, shard }) EditorUtility.SetDirty(m);

// Звуки: короткие — в память несжатыми (без задержки на распаковку), длинные петли и гром — сжатые потоком из памяти
int clips = 0;
foreach (string folder in new[] { "Assets/Content/Sound/Generated/Vehicle", "Assets/Content/Sound/Generated/Combat", "Assets/Content/Sound/Generated/Weather" }) {
    foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { folder })) {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        var settings = importer.defaultSampleSettings;
        bool longClip = clip.length > 4f;
        settings.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = longClip ? 0.6f : 0.75f;
        settings.preloadAudioData = true;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.loadInBackground = false;
        importer.SaveAndReimport();
        clips++;
    }
}
AssetDatabase.SaveAssets();
return "materials: " + log + "| clips " + clips;
