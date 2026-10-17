// Трава и кусты на террейне города (Assets/New Terrain.asset, SovietCity) генератором VegetationGenerator.
// Заменяет прежнюю траву террейна и прежние сгенерированные кусты, деревья не трогает. Траву и кусты ставит
// только там, где на террейне нарисована трава (слой 0), — не на земле и гравии; дороги, дома и заборы
// генератор обходит сам по коллайдерам.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6),
// SovietCity должна быть открыта
Terrain terrain = null;
foreach (var tr in UnityEngine.Object.FindObjectsOfType<Terrain>()) if (tr.gameObject.scene.path == "Assets/Scenes/SovietCity.unity") terrain = tr;
var td = terrain.terrainData;
var alpha = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
var vs = new RacingProject.EditorTools.VegetationGenerator.Settings();
vs.Seed = 23;
// Клетка травы здесь ~2 м (1024 на 2000 м) — пучков на клетку больше, чем на полигоне, чтобы густота была та же
vs.GrassPerCell = 14;
vs.BushesPerHectare = 20f;
vs.KeepOut = w => {
    int ax = Mathf.Clamp(Mathf.RoundToInt((w.x - terrain.transform.position.x) / td.size.x * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1);
    int az = Mathf.Clamp(Mathf.RoundToInt((w.z - terrain.transform.position.z) / td.size.z * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);
    return alpha[az, ax, 0] < 0.5f;
};
var report = RacingProject.EditorTools.VegetationGenerator.Populate(terrain, vs);
AssetDatabase.SaveAssets();
return report;
