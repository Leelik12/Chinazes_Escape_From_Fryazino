// Точки засад (Environment/AmbushPoints) для EnemyManager.ambushPoints.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Каждый запуск пересоздаёт точки. Кандидаты — дворы, проезд гаражей, улицы частного сектора, промзона, кварталы City2/City3.
// Точка ставится на NavMesh (агент Car) рядом с кандидатом (перебор смещений до 36 м) и принимается, только если от неё
// есть полный путь до ближайшей точки дороги (по осям Environment/RoadNetwork); разворачивается вдоль этого пути.
// Запускать после запекания NavMesh.
var env = GameObject.Find("Environment").transform;
var log = new System.Text.StringBuilder();
var old = env.Find("AmbushPoints"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var root = new GameObject("AmbushPoints").transform; root.SetParent(env, false);
int carAgent = -1372625422;
var filter = new UnityEngine.AI.NavMeshQueryFilter(); filter.agentTypeID = carAgent; filter.areaMask = UnityEngine.AI.NavMesh.AllAreas;
var roadFilter = filter; roadFilter.areaMask = 1 << UnityEngine.AI.NavMesh.GetAreaFromName("Road");
var candidates = new[] {
    new Vector3(523, 62, 640), new Vector3(525, 62, 503), new Vector3(505, 62, 355), new Vector3(500, 62, 210),
    new Vector3(854, 62, 375), new Vector3(854, 62, 455),
    new Vector3(1066, 21, 1312), new Vector3(1066, 21, 1364), new Vector3(1300, 30, 1470), new Vector3(1420, 35, 1560),
    new Vector3(1075, 68, 230), new Vector3(1035, 68, 250),
    new Vector3(1700, 30, 700), new Vector3(1600, 30, 600), new Vector3(1750, 30, 880), new Vector3(495, 36, 1625) };
// Точки осей дорог через 10 м (оси хранят точки через 2 м), лежащие на NavMesh в области Road
var roadPts = new System.Collections.Generic.List<Vector3>();
foreach (var road in env.Find("RoadNetwork").GetComponent<RacingProject.Enemy.RoadNetwork>().Roads) {
    for (int i = 0; i < road.points.Length; i += 5) {
        UnityEngine.AI.NavMeshHit rh; if (UnityEngine.AI.NavMesh.SamplePosition(road.points[i], out rh, 8f, roadFilter)) roadPts.Add(rh.position);
    }
}
var path = new UnityEngine.AI.NavMeshPath();
int n = 0;
foreach (var c in candidates) {
    bool done = false;
    for (int ring = 0; ring <= 3 && !done; ring++) for (int k = 0; k < (ring == 0 ? 1 : 8) && !done; k++) {
        var q = c + Quaternion.Euler(0, k * 45f, 0) * Vector3.forward * (ring * 12f);
        UnityEngine.AI.NavMeshHit hit;
        if (!UnityEngine.AI.NavMesh.SamplePosition(q, out hit, 10f, filter)) continue;
        var road = Vector3.zero; float best = float.MaxValue;
        foreach (var r in roadPts) { float dd = (r - hit.position).sqrMagnitude; if (dd < best) { best = dd; road = r; } }
        if (!UnityEngine.AI.NavMesh.CalculatePath(hit.position, road, filter, path) || path.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;
        // Смотрим вдоль первого отрезка пути к дороге: так машина сразу едет к выезду, а не в стену
        var look = path.corners.Length > 1 ? path.corners[1] - path.corners[0] : road - hit.position; look.y = 0;
        var p = new GameObject("Ambush_" + n.ToString("00")).transform; p.SetParent(root, false);
        p.SetPositionAndRotation(hit.position + Vector3.up * 0.5f, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
        log.AppendLine(p.name + " " + hit.position.ToString("F0") + " road " + Mathf.Sqrt(best).ToString("0") + " m");
        n++; done = true;
    }
    if (!done) log.AppendLine("skipped " + c);
}
var em = UnityEngine.Object.FindObjectOfType<RacingProject.Enemy.EnemyManager>();
var so = new SerializedObject(em); var arr = so.FindProperty("ambushPoints");
arr.arraySize = root.childCount;
for (int i = 0; i < root.childCount; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = root.GetChild(i);
so.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
