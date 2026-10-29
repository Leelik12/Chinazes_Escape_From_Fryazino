// LOD для своих моделей (Assets/Models/LevelProps, PrivateSector, Roadside): Blender-экспорт кладёт рядом с <имя>.fbx
// прореженный <имя>_LOD1.fbx (Tools/Blender/private_sector/build_models.py, make_lod). Здесь:
// 1) импорт LOD1 с теми же настройками, что у основной модели (масштаб, переназначение материалов);
// 2) в каждом префабе Assets/Prefabs с такой моделью рядом с её рендерером появляется дочерний LOD1, а на корне
//    префаба — LODGroup: вблизи полная модель, дальше LOD1. Мелочь и так отсекает DistanceCulling, поэтому LOD1
//    не пропадает раньше него (кроме совсем крошечных на экране, CULL);
// 3) если префаб взрывается (ExplosiveBarrel), LOD1 тоже обугливается.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск обновляет LOD1 и LODGroup, не дублируя их; после BuildLevelProps (пересоздаёт префабы) — запустить снова.
float LOD0_HEIGHT = 0.12f; // до какой доли высоты экрана рисуется полная модель
float CULL = 0.002f;
var log = new System.Text.StringBuilder();
var lods = new System.Collections.Generic.Dictionary<string, string>(); // имя меша -> путь LOD1.fbx
foreach (var dir in new[] { "Assets/Models/LevelProps/", "Assets/Models/PrivateSector/", "Assets/Models/Roadside/" }) {
    foreach (var file in System.IO.Directory.GetFiles(dir, "*_LOD1.fbx")) {
        string lodPath = file.Replace('\\', '/');
        string name = System.IO.Path.GetFileNameWithoutExtension(lodPath);
        name = name.Substring(0, name.Length - 5);
        var src = (ModelImporter)AssetImporter.GetAtPath(dir + name + ".fbx");
        var imp = (ModelImporter)AssetImporter.GetAtPath(lodPath);
        if (src == null || imp == null) { log.Append("skip ").Append(name).Append("; "); continue; }
        imp.globalScale = src.globalScale; imp.useFileScale = src.useFileScale;
        imp.importCameras = false; imp.importLights = false; imp.importAnimation = false; imp.addCollider = false;
        imp.isReadable = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
        foreach (var kv in src.GetExternalObjectMap()) imp.AddRemap(kv.Key, kv.Value);
        imp.SaveAndReimport();
        lods[name] = lodPath;
    }
}

int prefabs = 0;
foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" })) {
    string path = AssetDatabase.GUIDToAssetPath(guid);
    var root = PrefabUtility.LoadPrefabContents(path);
    bool changed = false;
    var lod0 = new System.Collections.Generic.List<Renderer>();
    var lod1 = new System.Collections.Generic.List<Renderer>();
    foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true)) {
        var mf = mr.GetComponent<MeshFilter>();
        string lodPath;
        if (mf == null || mf.sharedMesh == null || mr.name == "LOD1" || !lods.TryGetValue(mf.sharedMesh.name, out lodPath)) continue;
        // Модель из вложенного префаба получает LOD в своём префабе
        var nested = PrefabUtility.GetNearestPrefabInstanceRoot(mr.gameObject);
        if (nested != null && AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(nested)).EndsWith(".prefab")) continue;
        Mesh lodMesh = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(lodPath)) if (o is Mesh) lodMesh = (Mesh)o;
        var t = mr.transform.Find("LOD1");
        if (t == null) {
            t = new GameObject("LOD1").transform;
            t.SetParent(mr.transform, false);
            t.gameObject.AddComponent<MeshFilter>();
            t.gameObject.AddComponent<MeshRenderer>();
        }
        t.gameObject.layer = mr.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(mr.gameObject));
        t.GetComponent<MeshFilter>().sharedMesh = lodMesh;
        var lr = t.GetComponent<MeshRenderer>();
        // Материалы по именам: если прореживание убрало подсетку, порядок у LOD1 другой
        var lodSrcMats = AssetDatabase.LoadAssetAtPath<GameObject>(lodPath).GetComponentInChildren<MeshRenderer>().sharedMaterials;
        var mats = new Material[lodSrcMats.Length];
        for (int i = 0; i < mats.Length; i++) {
            mats[i] = lodSrcMats[i];
            foreach (var m in mr.sharedMaterials) if (m != null && lodSrcMats[i] != null && m.name == lodSrcMats[i].name) mats[i] = m;
        }
        lr.sharedMaterials = mats;
        lr.shadowCastingMode = mr.shadowCastingMode;
        lr.receiveShadows = mr.receiveShadows;
        lr.enabled = mr.enabled;
        lod0.Add(mr); lod1.Add(lr);
        // Взрывающийся предмет обугливает и дальний вид
        foreach (var eb in root.GetComponentsInChildren<RacingProject.Props.ExplosiveBarrel>(true)) {
            var so = new SerializedObject(eb);
            var arr = so.FindProperty("charRenderers");
            bool has = false, hasBase = false;
            for (int i = 0; i < arr.arraySize; i++) {
                var r = arr.GetArrayElementAtIndex(i).objectReferenceValue;
                if (r == lr) has = true; if (r == mr) hasBase = true;
            }
            if (hasBase && !has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = lr; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
    if (lod0.Count > 0) {
        var group = root.GetComponent<LODGroup>();
        if (group == null) group = root.AddComponent<LODGroup>();
        group.fadeMode = LODFadeMode.None;
        group.SetLODs(new[] { new LOD(LOD0_HEIGHT, lod0.ToArray()), new LOD(CULL, lod1.ToArray()) });
        group.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        prefabs++;
        log.Append(root.name).Append(' ');
    }
    PrefabUtility.UnloadPrefabContents(root);
}
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return "lod models " + lods.Count + ", prefabs " + prefabs + ": " + log;
