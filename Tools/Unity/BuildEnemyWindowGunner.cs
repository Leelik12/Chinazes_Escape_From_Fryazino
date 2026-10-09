// Гранатомётчик EnemyUaz: стрелок в салоне высовывается в боковое окно с гранатомётом на плече (EnemyWindowGunner).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск пересобирает стрелка, точки хвата на гранатомёте, клип позы и контроллер.
// Стойка на крыше (LauncherMount) убирается: гранатомёт теперь в руках. Стрелок — та же модель, что у игроков,
// в масштабе водителя УАЗа (1,7). Боковые окна салона: z от −0,91 до 0,50, подоконник 2,46, верх 3,19, борт x ±1,52,
// пол салона 0,93 (замерено по сеткам). Координаты — метры в осях корня машины, x — вправо, z — вперёд.
var log = new System.Text.StringBuilder();
const string dirPath = "Assets/Content/EnemyCrew";
if (!AssetDatabase.IsValidFolder(dirPath)) AssetDatabase.CreateFolder("Assets/Content", "EnemyCrew");

// Поза: стоит, бёдра согнуты под наклон корпуса к окну (корень наклоняет EnemyWindowGunner), колени чуть согнуты.
// У этого аватара прямая нога — «Upper Leg Front-Back» 2,11, а не 0
var clipPath = dirPath + "/EnemyWindowGunner.anim";
var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
clip.ClearCurves();
System.Action<string, float> muscle = (n, v) => clip.SetCurve("", typeof(Animator), n, AnimationCurve.Constant(0f, 1f, v));
foreach (var s in new[] { "Left", "Right" }) {
    muscle(s + " Upper Leg Front-Back", 1.3f); muscle(s + " Lower Leg Stretch", 0.85f);
    muscle(s + " Upper Leg Twist In-Out", 0.12f); muscle(s + " Lower Leg Twist In-Out", -0.14f);
    muscle(s + " Arm Down-Up", -0.2f); muscle(s + " Arm Front-Back", 0.5f); muscle(s + " Forearm Stretch", 0.4f);
    float curl = s == "Right" ? -0.8f : -0.6f;
    foreach (var f in new[] { "Index", "Middle", "Ring", "Little" }) for (int j = 1; j <= 3; j++) muscle(s + "Hand." + f + "." + j + " Stretched", curl);
    for (int j = 1; j <= 3; j++) muscle(s + "Hand.Thumb." + j + " Stretched", -0.3f);
}
EditorUtility.SetDirty(clip);

var ctrlPath = dirPath + "/EnemyWindowGunner.controller";
var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
if (ctrl == null) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
{
    var layers = ctrl.layers;
    var sm = layers[0].stateMachine;
    foreach (var st in sm.states) sm.RemoveState(st.state);
    var leaning = sm.AddState("Leaning");
    leaning.motion = clip;
    sm.defaultState = leaning;
    layers[0].iKPass = true;
    ctrl.layers = layers;
    EditorUtility.SetDirty(ctrl);
}
AssetDatabase.SaveAssets();

var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/Player/Untitled (1).fbx");
System.Func<Vector3, Vector3, bool, Quaternion> handRot = (fingers, palm, left) => {
    palm = palm.normalized;
    fingers = (fingers - palm * Vector3.Dot(fingers, palm)).normalized;
    return left ? Quaternion.LookRotation(Vector3.Cross(fingers, palm), -palm)
                : Quaternion.LookRotation(Vector3.Cross(fingers, -palm), -palm);
};
const float gunnerScale = 1.7f;

var path = "Assets/Prefabs/Enemy/EnemyUaz.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var mountPost = root.transform.Find("LauncherMount");
if (mountPost != null) UnityEngine.Object.DestroyImmediate(mountPost.gameObject);
var old = root.transform.Find("WindowGunner");
if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

var launcherT = root.transform.Find("RocketLauncher");
var launcher = launcherT.GetComponent<RacingProject.Enemy.EnemyRocketLauncher>();
foreach (var gname in new[] { "GunnerGripLeft", "GunnerGripRight" }) {
    var g = launcherT.Find(gname);
    if (g != null) UnityEngine.Object.DestroyImmediate(g.gameObject);
}
// Точки — запястья (цель IK) в осях гранатомёта; ладонь от точки хвата смещена на длину кисти против пальцев.
// Правая кисть на пистолетной рукояти со спуском (ладонь влево, пальцы вперёд и чуть вниз),
// левая снизу под трубой впереди (ладонь вверх, пальцы обхватывают трубу вправо)
float wristBack = 0.09f * gunnerScale;
System.Func<string, Vector3, Vector3, Vector3, bool, Transform> grip = (gname, at, fingers, palm, left) => {
    var g = new GameObject(gname).transform;
    g.SetParent(launcherT, false);
    g.localPosition = at - fingers.normalized * wristBack;
    g.localRotation = handRot(fingers, palm, left);
    return g;
};
var rightGrip = grip("GunnerGripRight", new Vector3(0.04f, -0.13f, 0.45f), new Vector3(0f, -0.3f, 1f), new Vector3(-1f, 0f, 0f), false);
var leftGrip = grip("GunnerGripLeft", new Vector3(0f, -0.09f, 0.62f), new Vector3(1f, 0f, 0.3f), new Vector3(0f, 1f, 0f), true);

var gunner = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
gunner.name = "WindowGunner";
gunner.transform.localScale = Vector3.one * gunnerScale;
gunner.transform.localPosition = new Vector3(0.55f, 2.45f, -0.2f);
gunner.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
var an = gunner.GetComponent<Animator>();
an.runtimeAnimatorController = ctrl;
an.applyRootMotion = false;
an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
foreach (var smr in gunner.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
    smr.gameObject.SetActive(true);
    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
}
var wg = gunner.AddComponent<RacingProject.Enemy.EnemyWindowGunner>();
var so = new SerializedObject(wg);
so.FindProperty("launcher").objectReferenceValue = launcher;
so.FindProperty("car").objectReferenceValue = root.transform;
so.FindProperty("leftGrip").objectReferenceValue = leftGrip;
so.FindProperty("rightGrip").objectReferenceValue = rightGrip;
so.ApplyModifiedPropertiesWithoutUndo();

PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
log.AppendLine("Гранатомётчик: " + path);
return log.ToString();
