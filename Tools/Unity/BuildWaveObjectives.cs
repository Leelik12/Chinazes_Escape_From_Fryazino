// Задания волн (Assets/Scripts/Enemy/WaveObjectives.cs): компонент на объекте EnemyManager (там уже NetworkObject),
// ссылки на машину игроков, рацию и ремкомплекты; материал маяка — URP Unlit, прозрачный, без теней.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск обновляет материал и ссылки, настройки компонента не трогает.
string FX = "Assets/Content/Effects/";
string matPath = FX + "ObjectiveBeacon.mat";
var unlit = Shader.Find("Universal Render Pipeline/Unlit");
var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
if (mat == null) { mat = new Material(unlit); AssetDatabase.CreateAsset(mat, matPath); }
mat.shader = unlit;
mat.SetColor("_BaseColor", new Color(1f, 0.8f, 0.25f, 0.35f));
mat.SetFloat("_Surface", 1f); mat.SetFloat("_Blend", 2f); mat.SetFloat("_Cull", 0f);
mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
mat.SetFloat("_ZWrite", 0f);
mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
mat.SetShaderPassEnabled("ShadowCaster", false);
EditorUtility.SetDirty(mat);
AssetDatabase.SaveAssets();

var manager = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.EnemyManager>();
if (manager == null) throw new System.Exception("no EnemyManager");
var objectives = manager.GetComponent<RacingProject.Enemy.WaveObjectives>();
if (objectives == null) objectives = manager.gameObject.AddComponent<RacingProject.Enemy.WaveObjectives>();
var managerSo = new SerializedObject(manager);
var so = new SerializedObject(objectives);
so.FindProperty("enemyManager").objectReferenceValue = manager;
so.FindProperty("carHealth").objectReferenceValue = managerSo.FindProperty("carHealth").objectReferenceValue;
so.FindProperty("repairKits").objectReferenceValue = managerSo.FindProperty("repairKits").objectReferenceValue;
so.FindProperty("radio").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.RadioChatter>();
so.FindProperty("beaconMaterial").objectReferenceValue = mat;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(manager.gameObject.scene);
return "objectives on " + manager.name + " car " + so.FindProperty("carHealth").objectReferenceValue + " radio " + so.FindProperty("radio").objectReferenceValue;
