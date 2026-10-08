// Осевые линии дорог: Environment/RoadNetwork (компонент RoadNetwork). Сейчас это единственный источник трассы:
// по нему ездит ИИ врагов и строит дороги Tools/Unity/BuildRoads.cs.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Скрипт снимал оси со сплайнов Road Architect (GSDSplineC, через отражение) и работает, только пока Road Architect
// есть в проекте: после его удаления оси правятся в RoadNetwork, а этот файл остаётся как запись, откуда они взялись.
// Точки через 2 м, высота — по поверхности старого асфальта (луч вниз), полуширина проезжей части —
// полосы GSDRoad (opt_Lanes × opt_LaneWidth / 2), без обочин.
// Сплайн местами идёт дальше асфальта (через лес): точка берётся, только если под ней меш дороги или перекрёстка,
// поэтому дорога может разбиться на несколько линий (Road2#0, Road2#1, ...).
var env = GameObject.Find("Environment").transform;
// Высоту меряем по старому асфальту: Road Architect на время включаем, новые дороги выключаем
var raSys = env.Find("RoadArchitectSystem1"); var newRoads = env.Find("Roads");
raSys.gameObject.SetActive(true); if (newRoads != null) newRoads.gameObject.SetActive(false);
Physics.SyncTransforms();
var holder = env.Find("RoadNetwork");
if (holder == null) { holder = new GameObject("RoadNetwork").transform; holder.SetParent(env, false); }
var net = holder.GetComponent<RacingProject.Enemy.RoadNetwork>();
if (net == null) net = holder.gameObject.AddComponent<RacingProject.Enemy.RoadNetwork>();
var roads = new System.Collections.Generic.List<RacingProject.Enemy.RoadNetwork.Road>();
var log = new System.Text.StringBuilder();
foreach (Transform roadT in env.Find("RoadArchitectSystem1")) {
    var splineT = roadT.Find("Spline"); if (splineT == null) continue;
    var sp = splineT.GetComponent("GSDSplineC"); var T = sp.GetType();
    float len = (float)T.GetField("distance").GetValue(sp);
    var value = T.GetMethod("GetSplineValue", new[] { typeof(float), typeof(bool) });
    var gsd = roadT.GetComponent("GSDRoad"); var RT = gsd.GetType();
    int lanes = System.Convert.ToInt32(RT.GetField("opt_Lanes").GetValue(gsd)); float laneWidth = System.Convert.ToSingle(RT.GetField("opt_LaneWidth").GetValue(gsd));
    // Высота асфальта под точкой (самое верхнее попадание в меш дороги или перекрёстка), NaN — асфальта нет
    System.Func<Vector3, float> surface = p => {
        float best = float.NaN;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 5f, Vector3.down, 12f)) {
            var n = h.collider.name;
            if ((n.StartsWith("Road") || n.StartsWith("Inter")) && (float.IsNaN(best) || h.point.y > best)) best = h.point.y; }
        return best; };
    var pts = new System.Collections.Generic.List<Vector3>(); int part = 0; int kept = 0;
    System.Action flush = () => {
        if (pts.Count >= 2) {
            var road = new RacingProject.Enemy.RoadNetwork.Road(); road.name = roadT.name + "#" + part; road.halfWidth = lanes * laneWidth / 2f; road.points = pts.ToArray();
            roads.Add(road); part++; kept += pts.Count;
        }
        pts.Clear(); };
    // TranslateDistBasedToParam неточен (точки шли вперёд-назад), поэтому сплайн проходим по параметру мелким шагом,
    // сами считаем длину дуги и берём точки ровно через 2 м
    var dense = new System.Collections.Generic.List<Vector3>(); var arc = new System.Collections.Generic.List<float>();
    int steps = Mathf.CeilToInt(len / 0.25f);
    for (int k = 0; k <= steps; k++) {
        var q = (Vector3)value.Invoke(sp, new object[] { k / (float)steps, false });
        if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z)) continue; // на концах параметра сплайн даёт NaN
        arc.Add(dense.Count == 0 ? 0f : arc[arc.Count - 1] + Vector3.Distance(q, dense[dense.Count - 1])); dense.Add(q);
    }
    int seg = 0;
    for (float d = 0f; d <= arc[arc.Count - 1]; d += 2f) {
        while (seg < arc.Count - 2 && arc[seg + 1] < d) seg++;
        var p = Vector3.Lerp(dense[seg], dense[seg + 1], Mathf.InverseLerp(arc[seg], arc[seg + 1], d));
        float y = surface(p);
        if (float.IsNaN(y)) { flush(); continue; }
        p.y = y;
        pts.Add(p);
    }
    flush();
    log.AppendLine(roadT.name + ": " + part + " parts, " + kept + " points");
}
// Road3 — петля, и после точки, где начинается Road1, оба сплайна идут по одному коридору (расхождение до 0,4 м):
// обрезаем Road3 в этой точке, иначе дорога строилась бы дважды
var r1 = roads.Find(r => r.name == "Road1#0"); var r3 = roads.Find(r => r.name == "Road3#0");
if (r1 != null && r3 != null) {
    int cut = 0; float best = float.MaxValue;
    for (int i = 0; i < r3.points.Length; i++) { float dd = Vector3.Distance(r3.points[i], r1.points[0]); if (dd < best) { best = dd; cut = i; } }
    var trimmed = new Vector3[cut + 1]; System.Array.Copy(r3.points, trimmed, cut + 1); r3.points = trimmed;
    log.AppendLine("Road3 trimmed at " + cut);
}
raSys.gameObject.SetActive(false); if (newRoads != null) newRoads.gameObject.SetActive(true);
if (roads.Count == 0) return "no roads baked, RoadNetwork left as is";
net.SetRoads(roads.ToArray());
EditorUtility.SetDirty(net);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
