// Экипажи машин врагов: водитель за рулём (EnemyCrewDriver) в EnemySedan (и через него EnemyBoss), EnemyUaz, EnemyUral.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Повторный запуск пересобирает узел Crew в каждом префабе, клип позы и контроллер.
// Модель — та же, что у игроков (Assets/Content/Player/Untitled (1).fbx). Поза сидя — гуманоидный клип EnemySeated.anim
// (мышцы ног, спины и пальцев); руки ставит IK на точки хвата руля. Руль у моделей запечён в кузов: его центр,
// радиус и наклон измерены по вершинам сеток, точки хвата крутятся вокруг этой оси вслед за передними колёсами.
// Координаты ниже — метры мира в осях корня машины (масштаб корня учитывается), x — вправо, z — вперёд.
var log = new System.Text.StringBuilder();
const string dirPath = "Assets/Content/EnemyCrew";
if (!AssetDatabase.IsValidFolder(dirPath)) AssetDatabase.CreateFolder("Assets/Content", "EnemyCrew");

// Поза сидя за рулём: бёдра чуть вверх, ноги вытянуты к педалям, пальцы согнуты под обод.
// У гуманоида таз поворачивается вслед за мышцами ног и спины, поэтому значения подобраны по замерам углов костей
var clipPath = dirPath + "/EnemySeated.anim";
var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
clip.ClearCurves();
System.Action<string, float> muscle = (n, v) => clip.SetCurve("", typeof(Animator), n, AnimationCurve.Constant(0f, 1f, v));
foreach (var s in new[] { "Left", "Right" }) {
    muscle(s + " Upper Leg Front-Back", 0f); muscle(s + " Lower Leg Stretch", 0.55f); muscle(s + " Foot Up-Down", 0.2f);
    muscle(s + " Upper Leg In-Out", -0.15f);
    muscle(s + " Arm Down-Up", -0.1f); muscle(s + " Arm Front-Back", 0.45f); muscle(s + " Forearm Stretch", 0.4f);
    foreach (var f in new[] { "Index", "Middle", "Ring", "Little" }) for (int j = 1; j <= 3; j++) muscle(s + "Hand." + f + "." + j + " Stretched", -0.75f);
    for (int j = 1; j <= 3; j++) muscle(s + "Hand.Thumb." + j + " Stretched", -0.3f);
}
muscle("Spine Front-Back", -0.2f); muscle("Chest Front-Back", -0.1f); muscle("Head Nod Down-Up", -0.05f);
EditorUtility.SetDirty(clip);

// Контроллер: одно состояние с позой, слой с проходом IK
var ctrlPath = dirPath + "/EnemyCrew.controller";
var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath);
if (ctrl == null) ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
{
    var layers = ctrl.layers;
    var sm = layers[0].stateMachine;
    foreach (var st in sm.states) sm.RemoveState(st.state);
    var seated = sm.AddState("Seated");
    seated.motion = clip;
    sm.defaultState = seated;
    layers[0].iKPass = true;
    ctrl.layers = layers;
    EditorUtility.SetDirty(ctrl);
}
AssetDatabase.SaveAssets();

var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/Player/Untitled (1).fbx");
// Таз персонажа над корнем модели в позе EnemySeated при масштабе 1 (измерено в Play Mode)
var hipsOffset = new Vector3(0f, -0.194f, -0.091f);

// Хват: ладонь к ободу (от водителя), пальцы к центру руля и вперёд. Оси кисти в T-позе: у левой пальцы −X, ладонь −Y,
// у правой пальцы +X, ладонь −Y
System.Func<Vector3, Vector3, bool, Quaternion> handRot = (fingers, palm, left) => {
    fingers = (fingers - palm * Vector3.Dot(fingers, palm)).normalized;
    return left ? Quaternion.LookRotation(Vector3.Cross(fingers, palm), -palm)
                : Quaternion.LookRotation(Vector3.Cross(fingers, -palm), -palm);
};

// scale — масштаб персонажа, hips — куда сесть тазом, lean — наклон корпуса вперёд (вся модель поворачивается вокруг таза),
// руль — центр обода, радиус и нормаль плоскости к водителю
System.Func<string, float, Vector3, float, Vector3, float, Vector3, bool> build = (path, scale, hips, lean, wheelCenter, wheelRadius, wheelNormal) => {
    var root = PrefabUtility.LoadPrefabContents(path);
    var old = root.transform.Find("Crew");
    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    var crew = new GameObject("Crew").transform;
    crew.SetParent(root.transform, false);
    // Узел в метрах мира, без масштаба корня
    var rs = root.transform.localScale;
    crew.localScale = new Vector3(1f / rs.x, 1f / rs.y, 1f / rs.z);

    var n = wheelNormal.normalized; // к водителю
    var pivot = new GameObject("WheelPivot").transform;
    pivot.SetParent(crew, false);
    pivot.localPosition = wheelCenter;
    pivot.localRotation = Quaternion.LookRotation(n, Vector3.up);
    // Хват на «без десяти два»; +X оси руля — влево от водителя
    float a = 60f * Mathf.Deg2Rad;
    System.Func<string, float, bool, Transform> grip = (gname, side, left) => {
        var g = new GameObject(gname).transform;
        g.SetParent(pivot, false);
        var local = new Vector3(side * Mathf.Sin(a) * wheelRadius, Mathf.Cos(a) * wheelRadius, 0.025f * scale);
        g.localPosition = local;
        var toCenter = pivot.TransformDirection(-new Vector3(local.x, local.y, 0f)).normalized;
        var palm = -pivot.forward;
        var fingers = toCenter * 0.6f + crew.forward * 0.8f;
        g.rotation = handRot(fingers, palm, left);
        return g;
    };
    var leftGrip = grip("LeftGrip", 1f, true);
    var rightGrip = grip("RightGrip", -1f, false);

    var driver = (GameObject)PrefabUtility.InstantiatePrefab(model, crew);
    driver.name = "Driver";
    driver.transform.localScale = Vector3.one * scale;
    driver.transform.localRotation = Quaternion.Euler(lean, 0f, 0f);
    driver.transform.localPosition = hips - driver.transform.localRotation * (hipsOffset * scale);
    var an = driver.GetComponent<Animator>();
    an.runtimeAnimatorController = ctrl;
    an.applyRootMotion = false;
    an.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
    foreach (var smr in driver.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
        smr.gameObject.SetActive(true);
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    var cd = driver.AddComponent<RacingProject.Enemy.EnemyCrewDriver>();
    var so = new SerializedObject(cd);
    so.FindProperty("car").objectReferenceValue = root.GetComponent<RacingProject.Enemy.EnemyCarController>();
    so.FindProperty("wheelPivot").objectReferenceValue = pivot;
    so.FindProperty("leftGrip").objectReferenceValue = leftGrip;
    so.FindProperty("rightGrip").objectReferenceValue = rightGrip;
    so.ApplyModifiedPropertiesWithoutUndo();

    PrefabUtility.SaveAsPrefabAsset(root, path);
    PrefabUtility.UnloadPrefabContents(root);
    log.AppendLine("Водитель: " + path);
    return true;
};

// Таз ставится так, чтобы плечо было в 0,93 длины руки от хвата (руки почти вытянуты), макушка — под крышей.
// Седан (босс — его вариант, крупнее в 1,45 раза вместе с экипажем): руль наклонён на 27° от вертикали, крыша низкая
build("Assets/Resources/EnemySedan.prefab", 1.5f, new Vector3(-0.69f, 0.88f, 0.33f), 10f,
    new Vector3(-0.69f, 1.33f, 1.05f), 0.25f, new Vector3(0f, 0.454f, -0.891f));
// УАЗ: плоский «автобусный» руль, наклон около 45°
build("Assets/Prefabs/Enemy/EnemyUaz.prefab", 1.7f, new Vector3(-0.94f, 2.04f, 1.97f), 17f,
    new Vector3(-0.94f, 2.45f, 2.72f), 0.36f, new Vector3(0f, 0.751f, -0.658f));
// Урал: руль наклонён на 46°, кабина высокая — водитель крупнее, чтобы был виден над дверью
build("Assets/Prefabs/Enemy/EnemyUral.prefab", 2.1f, new Vector3(-0.92f, 3.1f, 2.77f), 12f,
    new Vector3(-0.916f, 3.913f, 3.749f), 0.45f, new Vector3(0f, 0.722f, -0.692f));
return log.ToString();
