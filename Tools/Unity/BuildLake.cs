// Озеро (объект water в SovietCity): вода из сплошного куба, по которому можно было ехать, становится
// триггером от поверхности до дна с WaterVolume (машина вязнет и тонет, вытащить — R), получает шейдер воды
// RacingProject/Water и брызги; невидимые стены water/WaterBorder (BorderWater 0–14) удаляются.
// Дно озера закрыто для NavMesh объёмом Lake_NavBlock, чтобы враги не строили путь по дну, а «поставить на колёса»
// не ставило машину под воду. После запуска — BakeNavMesh.cs.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var log = new System.Text.StringBuilder();
var water = GameObject.Find("water");
if (water == null) throw new System.Exception("water not found");

// --- Материал ---
string matPath = "Assets/Content/Water/Lake_Water.mat";
var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
if (mat == null) {
    System.IO.Directory.CreateDirectory("Assets/Content/Water");
    mat = new Material(Shader.Find("RacingProject/Water"));
    AssetDatabase.CreateAsset(mat, matPath);
}
mat.shader = Shader.Find("RacingProject/Water");
EditorUtility.SetDirty(mat);
var renderer = water.GetComponent<MeshRenderer>();
renderer.sharedMaterial = mat;
renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
renderer.receiveShadows = true;

// --- Триггер от поверхности до дна (масштаб объекта по Y = 1, поверхность — верх куба) ---
var box = water.GetComponent<BoxCollider>();
float top = 0.5f;
float bottom = -water.transform.position.y - 0.5f;
box.isTrigger = true;
box.center = new Vector3(0f, (top + bottom) * 0.5f, 0f);
box.size = new Vector3(1f, top - bottom, 1f);

// --- Невидимые стены ---
var border = water.transform.Find("WaterBorder");
if (border != null) { UnityEngine.Object.DestroyImmediate(border.gameObject); log.Append("WaterBorder removed; "); }

// --- Брызги ---
var splashT = water.transform.Find("Splash");
if (splashT == null) { splashT = new GameObject("Splash").transform; splashT.SetParent(water.transform, false); }
var ps = splashT.GetComponent<ParticleSystem>();
if (ps == null) ps = splashT.gameObject.AddComponent<ParticleSystem>();
ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
var main = ps.main;
main.simulationSpace = ParticleSystemSimulationSpace.World;
main.playOnAwake = false;
main.loop = false;
main.duration = 1f;
main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.2f);
main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 13f);
main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.6f);
main.startColor = new Color(0.75f, 0.8f, 0.82f, 0.7f);
main.gravityModifier = 1.4f;
main.maxParticles = 400;
var emission = ps.emission;
emission.rateOverTime = 0f;
var shape = ps.shape;
shape.shapeType = ParticleSystemShapeType.Cone;
shape.angle = 28f;
shape.radius = 1.6f;
shape.rotation = new Vector3(-90f, 0f, 0f);
var sol = ps.sizeOverLifetime;
sol.enabled = true;
sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
var col = ps.colorOverLifetime;
col.enabled = true;
var g = new Gradient();
g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
    new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
col.color = g;
var pr = splashT.GetComponent<ParticleSystemRenderer>();
pr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_Dust.mat");
pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
pr.receiveShadows = false;

// --- WaterVolume ---
var volume = water.GetComponent<RacingProject.Props.WaterVolume>();
if (volume == null) volume = water.AddComponent<RacingProject.Props.WaterVolume>();
var vso = new SerializedObject(volume);
vso.FindProperty("splash").objectReferenceValue = ps;
vso.ApplyModifiedPropertiesWithoutUndo();

// --- Дно закрыто для NavMesh ---
var env = GameObject.Find("Environment").transform;
var blockT = env.Find("Lake_NavBlock");
if (blockT == null) { blockT = new GameObject("Lake_NavBlock").transform; blockT.SetParent(env, false); }
Bounds wb = renderer.bounds;
blockT.position = new Vector3(wb.center.x, wb.max.y * 0.5f - 1f, wb.center.z);
blockT.rotation = Quaternion.identity;
var block = blockT.GetComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
if (block == null) block = blockT.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifierVolume>();
block.size = new Vector3(wb.size.x, wb.max.y + 3f, wb.size.z);
block.center = Vector3.zero;
block.area = UnityEngine.AI.NavMesh.GetAreaFromName("Not Walkable");

AssetDatabase.SaveAssets();
var scene = water.scene;
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
log.Append("collider ").Append(box.bounds.min.y.ToString("0.0")).Append("..").Append(box.bounds.max.y.ToString("0.0"));
return log.ToString();
