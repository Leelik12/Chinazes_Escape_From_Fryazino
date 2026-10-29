// Префабы наполнения карты (Assets/Prefabs/LevelProps) из моделей Tools/Blender/level_props.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// 1) Материалы LP_* (Assets/Content/LevelProps/Materials) — копии PS_Rust с текстурами из generate_level_props_textures.py;
// 2) импорт FBX: масштаб 2 (масштаб мира), материалы переназначаются на PS_* и LP_*;
// 3) префаб: корень с моделью внутри (вложенный префаб FBX), коллайдер по типу:
//    Static — MeshCollider по мешу, корень статичный (здания, ориентиры, укрытия);
//    Loose  — BoxCollider по границам, Rigidbody и LooseProp: стоит, пока не заденут (знаки, ящики, брошенные машины),
//             в NavMesh не попадает (NavMeshModifier ignoreFromBuild), как фонари;
//    None   — без коллайдера (лужи, люки, камыш, антенны).
// Мелкие предметы из SMALL кладутся на слой SmallProps (DistanceCulling отсекает их раньше, Tools/Unity/BuildPerformance.cs)
// и не отбрасывают тени.
// Повторный запуск пересоздаёт материалы и префабы; в ONLY можно перечислить модели, чтобы пересоздать только их
// (после этого — BuildBattleEvents и BuildPropLods: они донастраивают префабы).
var ONLY = new System.Collections.Generic.HashSet<string> { };
var log = new System.Text.StringBuilder();
string MD = "Assets/Models/LevelProps/", PF = "Assets/Prefabs/LevelProps/", MAT = "Assets/Content/LevelProps/Materials/", TEX = "Assets/Content/LevelProps/Textures/";
System.IO.Directory.CreateDirectory(PF); System.IO.Directory.CreateDirectory(MAT);
AssetDatabase.Refresh();

// --- 1. Текстуры и материалы ---
var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/PS_Rust.mat");
var lpMats = new System.Collections.Generic.Dictionary<string, Material>();
foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TEX.TrimEnd('/') })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    string tname = System.IO.Path.GetFileNameWithoutExtension(path);
    var ti = (TextureImporter)AssetImporter.GetAtPath(path);
    bool normal = tname.EndsWith("_Normal");
    bool fit = tname.StartsWith("LP_Sign") || tname.StartsWith("LP_Slogan") || tname == "LP_Puddle";
    ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
    ti.sRGBTexture = !normal;
    ti.maxTextureSize = 2048;
    ti.wrapMode = fit ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
    ti.SaveAndReimport();
}
foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TEX.TrimEnd('/') })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    string tname = System.IO.Path.GetFileNameWithoutExtension(path);
    if (tname.EndsWith("_Normal")) continue;
    string mPath = MAT + tname + ".mat";
    var m = AssetDatabase.LoadAssetAtPath<Material>(mPath);
    if (m == null) { m = new Material(template); AssetDatabase.CreateAsset(m, mPath); }
    else m.CopyPropertiesFromMaterial(template);
    m.name = tname;
    m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
    m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TEX + tname + "_Normal.png"));
    m.SetFloat("_Metallic", tname.StartsWith("LP_Paint") || tname == "LP_Burnt" || tname == "LP_Stripes" || tname.StartsWith("LP_Sign") ? 0.4f : 0f);
    EditorUtility.SetDirty(m);
    lpMats[tname] = m;
}
AssetDatabase.SaveAssets();
log.Append("materials ").Append(lpMats.Count).Append("; ");

// --- 2. Типы префабов: имя модели -> (тип, масса) ---
var kinds = new System.Collections.Generic.Dictionary<string, object[]> {
    { "Guardrail", new object[] { "Loose", 250f } }, { "Sign_NoEntry", new object[] { "Loose", 40f } },
    { "Sign_Danger", new object[] { "Loose", 40f } }, { "Sign_City", new object[] { "Loose", 90f } },
    { "Manhole", new object[] { "None", 0f } }, { "Puddle_0", new object[] { "None", 0f } }, { "Puddle_1", new object[] { "None", 0f } },
    { "Zhiguli_Red", new object[] { "Loose", 900f } }, { "Zhiguli_White", new object[] { "Loose", 900f } },
    { "Moskvich_Blue", new object[] { "Loose", 950f } }, { "Moskvich_Yellow", new object[] { "Loose", 950f } },
    { "Zhiguli_Burnt", new object[] { "Loose", 700f } }, { "TireStack", new object[] { "Loose", 60f } },
    { "TireStack_Low", new object[] { "Loose", 30f } }, { "RoadSlab", new object[] { "Static", 0f } },
    { "Billboard_0", new object[] { "Static", 0f } }, { "Billboard_1", new object[] { "Static", 0f } }, { "Billboard_2", new object[] { "Static", 0f } },
    { "TrashPile_0", new object[] { "None", 0f } }, { "TrashPile_1", new object[] { "None", 0f } },
    { "ShellGarage_Green", new object[] { "Static", 0f } }, { "ShellGarage_Blue", new object[] { "Static", 0f } },
    { "RocketSlide", new object[] { "Static", 0f } }, { "Swings", new object[] { "Loose", 150f } },
    { "Clothesline", new object[] { "Loose", 40f } }, { "YardBench", new object[] { "Loose", 60f } },
    { "TrashBin", new object[] { "Loose", 90f } }, { "Sandbox", new object[] { "None", 0f } }, { "TvAntenna", new object[] { "None", 0f } },
    { "FactoryChimney", new object[] { "Static", 0f } }, { "TowerCrane", new object[] { "Static", 0f } },
    { "FuelTank", new object[] { "Static", 0f } }, { "Container_Green", new object[] { "Static", 0f } },
    { "GasCylinder", new object[] { "Loose", 45f } }, { "GasCylinderRack", new object[] { "Static", 0f } },
    { "FuelTruck", new object[] { "Static", 0f } },
    { "Container_Blue", new object[] { "Static", 0f } }, { "Container_Rust", new object[] { "Static", 0f } },
    { "Wagon", new object[] { "Static", 0f } }, { "RailTrack", new object[] { "None", 0f } }, { "PipeRack", new object[] { "Static", 0f } },
    { "FloodlightMast", new object[] { "Static", 0f } }, { "Woodpile", new object[] { "Static", 0f } }, { "Well", new object[] { "Static", 0f } },
    { "Greenhouse", new object[] { "Loose", 200f } }, { "DogHouse", new object[] { "Loose", 40f } }, { "WoodenPole", new object[] { "Static", 0f } },
    { "SandbagWall", new object[] { "Static", 0f } }, { "BurntBTR", new object[] { "Static", 0f } }, { "BurntTruck", new object[] { "Static", 0f } },
    { "Hedgehog", new object[] { "Static", 0f } }, { "BarbedWire", new object[] { "Loose", 60f } }, { "Dugout", new object[] { "Static", 0f } },
    { "WaterTower", new object[] { "Static", 0f } }, { "TvMast", new object[] { "Static", 0f } }, { "PowerChimney", new object[] { "Static", 0f } },
    { "CultureHouse", new object[] { "Static", 0f } }, { "Pier", new object[] { "Static", 0f } },
    { "Boat_Blue", new object[] { "Loose", 120f } }, { "Boat_Green", new object[] { "Loose", 120f } }, { "BridgeRuin", new object[] { "Static", 0f } },
    { "Reeds_0", new object[] { "None", 0f } }, { "Reeds_1", new object[] { "None", 0f } },
    { "Boulder_0", new object[] { "Static", 0f } }, { "Boulder_1", new object[] { "Static", 0f } }, { "Boulder_2", new object[] { "Static", 0f } },
    { "RoadClosed", new object[] { "Loose", 120f } }, { "Ramp", new object[] { "Static", 0f } }, { "WoodRamp", new object[] { "Static", 0f } },
    { "Crate", new object[] { "Loose", 35f } }, { "Barricade", new object[] { "Loose", 90f } }, { "NitroCanister", new object[] { "None", 0f } },
};

// --- 3. Импорт и префабы ---
// Слой SmallProps (9) создаётся здесь же, если его нет
var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
var layersProp = tagManager.FindProperty("layers");
if (LayerMask.NameToLayer("SmallProps") < 0) {
    if (layersProp.GetArrayElementAtIndex(9).stringValue.Length > 0) throw new System.Exception("layer 9 is taken");
    layersProp.GetArrayElementAtIndex(9).stringValue = "SmallProps";
    tagManager.ApplyModifiedPropertiesWithoutUndo();
}
int smallLayer = LayerMask.NameToLayer("SmallProps");
var SMALL = new System.Collections.Generic.HashSet<string> { "Sign_NoEntry", "Sign_Danger", "Manhole", "Puddle_0", "Puddle_1", "TireStack",
    "TireStack_Low", "TrashPile_0", "TrashPile_1", "Swings", "Clothesline", "YardBench", "TrashBin", "Sandbox", "TvAntenna", "Woodpile", "Well",
    "DogHouse", "GasCylinder", "BarbedWire", "Reeds_0", "Reeds_1", "Boulder_1", "Crate", "Barricade", "Hedgehog", "Greenhouse", "Boat_Blue", "Boat_Green" };
int made = 0;
foreach (var kv in kinds) {
    string name = kv.Key; string kind = (string)kv.Value[0]; float mass = (float)kv.Value[1];
    if (ONLY.Count > 0 && !ONLY.Contains(name)) continue;
    string fbx = MD + name + ".fbx";
    var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
    if (imp == null) { log.Append("no model ").Append(name).Append("; "); continue; }
    imp.globalScale = 2f; imp.useFileScale = true; imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
    imp.addCollider = false; imp.isReadable = kind == "Static";
    imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
    foreach (var r in AssetDatabase.LoadAllAssetsAtPath(fbx)) {
        var m = r as Material; if (m == null) continue;
        Material target;
        if (!lpMats.TryGetValue(m.name, out target))
            target = AssetDatabase.LoadAssetAtPath<Material>("Assets/Content/PrivateSector/Materials/" + m.name + ".mat");
        if (target != null) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), target);
        else log.Append("no material ").Append(m.name).Append(" in ").Append(name).Append("; ");
    }
    imp.SaveAndReimport();

    var root = new GameObject(name);
    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), root.transform);
    model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
    var mf = model.GetComponentInChildren<MeshFilter>();
    var mr = model.GetComponentInChildren<MeshRenderer>();
    // Плоские и мелкие предметы не отбрасывают тень
    if (kind == "None" && name != "TvAntenna") mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    if (kind == "Static") {
        var mc = mf.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mf.sharedMesh;
        GameObjectUtility.SetStaticEditorFlags(root, (StaticEditorFlags)~0);
        GameObjectUtility.SetStaticEditorFlags(model, (StaticEditorFlags)~0);
    } else if (kind == "Loose") {
        Bounds b = mr.bounds;
        var bc = root.AddComponent<BoxCollider>();
        bc.center = root.transform.InverseTransformPoint(b.center);
        bc.size = b.size;
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        root.AddComponent<RacingProject.Props.LooseProp>();
        var nm = root.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
        nm.ignoreFromBuild = true;
    } else {
        GameObjectUtility.SetStaticEditorFlags(root, (StaticEditorFlags)~0 & ~StaticEditorFlags.NavigationStatic);
        GameObjectUtility.SetStaticEditorFlags(model, (StaticEditorFlags)~0 & ~StaticEditorFlags.NavigationStatic);
    }
    // Мелочь: свой слой и без теней (тень удваивает вызовы отрисовки, а от мелкого предмета её почти не видно)
    if (SMALL.Contains(name)) {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = smallLayer;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    PrefabUtility.SaveAsPrefabAsset(root, PF + name + ".prefab");
    UnityEngine.Object.DestroyImmediate(root);
    made++;
}
// Модели собаки (Assets/Scripts/Effects/StrayDogs.cs собирает её сам): только импорт, без префабов
foreach (var name in new[] { "StrayDog_Body", "StrayDog_Leg" }) {
    if (ONLY.Count > 0 && !ONLY.Contains(name)) continue;
    var imp = (ModelImporter)AssetImporter.GetAtPath(MD + name + ".fbx");
    if (imp == null) { log.Append("no model ").Append(name).Append("; "); continue; }
    imp.globalScale = 2f; imp.useFileScale = true; imp.importCameras = false; imp.importLights = false; imp.importAnimation = false;
    imp.addCollider = false; imp.isReadable = false;
    imp.SaveAndReimport();
}
AssetDatabase.SaveAssets();
log.Append("prefabs ").Append(made);
return log.ToString();
