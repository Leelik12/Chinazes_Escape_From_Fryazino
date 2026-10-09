// Пулемётная турель на крыше EnemySedan (и через него EnemyBoss) со стрелком в люке (EnemyTurretGunner).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск пересобирает узел Turret, точки хвата на пулемёте, клип позы стрелка и контроллер.
// Кольцо люка и станок со щитами — те же сетки, что у пулемётной позиции игроков (Assets/Models/Turret), в масштабе 1,2
// вместо 2: крыша седана уже. Стрелок — та же модель, что у игроков, ростом как стрелок игроков (масштаб 1,5),
// стоит ногами на заднем сиденье. Координаты — метры мира в осях корня машины, x — вправо, z — вперёд.
var log = new System.Text.StringBuilder();
const string dirPath = "Assets/Content/EnemyCrew";
if (!AssetDatabase.IsValidFolder(dirPath)) AssetDatabase.CreateFolder("Assets/Content", "EnemyCrew");

// Поза стрелка: стоит прямо, руки вперёд к пулемёту (их ставит IK), правая кисть обхватывает рукоять, левая лежит сверху
var clipPath = dirPath + "/EnemyGunner.anim";
var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
clip.ClearCurves();
System.Action<string, float> muscle = (n, v) => clip.SetCurve("", typeof(Animator), n, AnimationCurve.Constant(0f, 1f, v));
// Ноги: у этого аватара прямая нога — «Upper Leg Front-Back» 2,11 (так стоит T-поза модели), а не 0
foreach (var s in new[] { "Left", "Right" }) {
    muscle(s + " Upper Leg Front-Back", 2f); muscle(s + " Lower Leg Stretch", 0.9f);
    muscle(s + " Upper Leg Twist In-Out", 0.12f); muscle(s + " Lower Leg Twist In-Out", -0.14f);
    muscle(s + " Arm Down-Up", -0.2f); muscle(s + " Arm Front-Back", 0.5f); muscle(s + " Forearm Stretch", 0.4f);
    float curl = s == "Right" ? -0.8f : -0.25f;
    foreach (var f in new[] { "Index", "Middle", "Ring", "Little" }) for (int j = 1; j <= 3; j++) muscle(s + "Hand." + f + "." + j + " Stretched", curl);
    for (int j = 1; j <= 3; j++) muscle(s + "Hand.Thumb." + j + " Stretched", -0.3f);
}
EditorUtility.SetDirty(clip);

var ctrlPath = dirPath + "/EnemyGunner.controller";
var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
if (ctrl == null) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
{
    var layers = ctrl.layers;
    var sm = layers[0].stateMachine;
    foreach (var st in sm.states) sm.RemoveState(st.state);
    var standing = sm.AddState("Standing");
    standing.motion = clip;
    sm.defaultState = standing;
    layers[0].iKPass = true;
    ctrl.layers = layers;
    EditorUtility.SetDirty(ctrl);
}

// Тёмный проём люка внутри кольца: крыша под ним не вырезана
var hatchMatPath = dirPath + "/HatchOpening.mat";
var hatchMat = AssetDatabase.LoadAssetAtPath<Material>(hatchMatPath);
if (hatchMat == null) { hatchMat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(hatchMat, hatchMatPath); }
hatchMat.SetColor("_BaseColor", new Color(0.02f, 0.02f, 0.02f));
hatchMat.SetFloat("_Smoothness", 0.05f);
EditorUtility.SetDirty(hatchMat);
AssetDatabase.SaveAssets();

var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Turret/TurretRing.asset");
var mountMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Turret/TurretMount.asset");
var armor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Turret/TurretArmor.mat");
var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/Player/Untitled (1).fbx");
// Внутренний радиус кольца — по вершинам сетки
float ringInner = float.MaxValue;
foreach (var v in ringMesh.vertices) ringInner = Mathf.Min(ringInner, new Vector2(v.x, v.z).magnitude);

// Ось кисти для IK: у левой пальцы −X, ладонь −Y, у правой пальцы +X, ладонь −Y (T-поза тела)
System.Func<Vector3, Vector3, bool, Quaternion> handRot = (fingers, palm, left) => {
    palm = palm.normalized;
    fingers = (fingers - palm * Vector3.Dot(fingers, palm)).normalized;
    return left ? Quaternion.LookRotation(Vector3.Cross(fingers, palm), -palm)
                : Quaternion.LookRotation(Vector3.Cross(fingers, -palm), -palm);
};

const float turretScale = 1.2f;   // масштаб сеток кольца и станка (у игроков 2): кольцо 1,1 м по крыше седана
const float gunnerScale = 1.5f;
const float ringBottom = 2.26f;   // крыша под кольцом 2,25–2,29 м
var ringCenter = new Vector2(0f, -0.59f); // x, z: там, где стоял пулемёт
var mountOffset = new Vector3(0f, 0f, 0.42f);

var path = "Assets/Resources/EnemySedan.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var rs = root.transform.localScale.x;
var old = root.transform.Find("Turret");
if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

// Начало узла — центр кольца на высоте оси пулемёта; низ кольца на 0,245 ниже (в единицах сетки)
var turret = new GameObject("Turret").transform;
turret.SetParent(root.transform, false);
turret.localPosition = new Vector3(ringCenter.x, ringBottom + 0.245f * turretScale, ringCenter.y) / rs;
turret.localScale = Vector3.one * (turretScale / rs);

System.Func<string, Transform, Mesh, Material, Transform> part = (pname, parent, mesh, mat) => {
    var t = new GameObject(pname).transform;
    t.SetParent(parent, false);
    t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
    var r = t.gameObject.AddComponent<MeshRenderer>();
    r.sharedMaterial = mat;
    return t;
};
part("TurretRing", turret, ringMesh, armor);
var hatch = part("HatchOpening", turret, Resources.GetBuiltinResource<Mesh>("Cylinder.fbx"), hatchMat);
hatch.localPosition = new Vector3(0f, -0.2f, 0f); // над выпуклостью крыши в центре
hatch.localScale = new Vector3(ringInner * 2.02f, 0.004f, ringInner * 2.02f);
hatch.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
var mount = part("TurretMount", turret, mountMesh, armor);

// Пулемёт — на вилке станка; дальше его каждый кадр ставит EnemyTurretGunner
var gun = root.transform.Find("Machine");
gun.localPosition = turret.localPosition + mountOffset * (turretScale / rs);
gun.localRotation = Quaternion.identity;
foreach (var gname in new[] { "GunnerGripLeft", "GunnerGripRight" }) {
    var g = gun.Find(gname);
    if (g != null) UnityEngine.Object.DestroyImmediate(g.gameObject);
}
// Точки — запястья (цель IK), в осях пулемёта; ладонь от точки хвата смещена на длину кисти против пальцев.
// Правая кисть обхватывает пистолетную рукоять сбоку (ладонь влево, пальцы вперёд и чуть вниз),
// левая лежит сверху на коробке (ладонь вниз, пальцы вправо и вперёд)
float wristBack = 0.09f * gunnerScale / (gun.lossyScale.x);
System.Func<string, Vector3, Vector3, Vector3, bool, Transform> gunGrip = (gname, at, fingers, palm, left) => {
    var g = new GameObject(gname).transform;
    g.SetParent(gun, false);
    g.localPosition = at - fingers.normalized * wristBack;
    g.localRotation = handRot(fingers, palm, left);
    return g;
};
var rightGrip = gunGrip("GunnerGripRight", new Vector3(0.05f, 0.05f, -0.19f), new Vector3(0f, -0.3f, 1f), new Vector3(-1f, 0f, 0f), false);
var leftGrip = gunGrip("GunnerGripLeft", new Vector3(-0.01f, 0.30f, -0.30f), new Vector3(0.7f, 0f, 0.7f), new Vector3(0f, -1f, 0f), true);

// Стрелок в центре кольца чуть позади оси, ноги на заднем сиденье: плечи над рукоятью, глаза выше щита
var gunner = (GameObject)PrefabUtility.InstantiatePrefab(model, mount);
gunner.name = "Gunner";
gunner.transform.localScale = Vector3.one * (gunnerScale / turretScale);
// Клип без положения корня ставит таз на начало модели: таз на 0,21 м ниже оси пулемёта, плечи над рукоятью
gunner.transform.localPosition = new Vector3(0f, -0.21f / turretScale, -0.1f);
gunner.transform.localRotation = Quaternion.identity;
var an = gunner.GetComponent<Animator>();
an.runtimeAnimatorController = ctrl;
an.applyRootMotion = false;
an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
foreach (var smr in gunner.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
    smr.gameObject.SetActive(true);
    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
}
var tg = gunner.AddComponent<RacingProject.Enemy.EnemyTurretGunner>();
var so = new SerializedObject(tg);
so.FindProperty("gun").objectReferenceValue = gun;
so.FindProperty("turret").objectReferenceValue = turret;
so.FindProperty("mount").objectReferenceValue = mount;
so.FindProperty("mountOffset").vector3Value = mountOffset;
so.FindProperty("leftGrip").objectReferenceValue = leftGrip;
so.FindProperty("rightGrip").objectReferenceValue = rightGrip;
so.ApplyModifiedPropertiesWithoutUndo();

PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
log.AppendLine("Турель: " + path + ", внутренний радиус кольца " + ringInner.ToString("F3"));
return log.ToString();
