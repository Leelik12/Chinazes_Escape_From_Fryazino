// Осевые линии дорог для ИИ врагов: Environment/RoadNetwork (компонент RoadNetwork).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Точки берутся через 5 м по сплайнам Road Architect (GSDSplineC, через отражение). Полуширина проезжей части —
// полосы GSDRoad (opt_Lanes × opt_LaneWidth / 2), без обочин. Перезапускать после правки дорог.
// Сплайн местами идёт дальше асфальта (через лес): точка берётся, только если под ней меш дороги или перекрёстка,
// поэтому дорога может разбиться на несколько линий (Road2#0, Road2#1, ...).
var env = GameObject.Find("Environment").transform;
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
    var toParam = T.GetMethod("TranslateDistBasedToParam"); var value = T.GetMethod("GetSplineValue", new[] { typeof(float), typeof(bool) });
    var gsd = roadT.GetComponent("GSDRoad"); var RT = gsd.GetType();
    int lanes = System.Convert.ToInt32(RT.GetField("opt_Lanes").GetValue(gsd)); float laneWidth = System.Convert.ToSingle(RT.GetField("opt_LaneWidth").GetValue(gsd));
    System.Func<Vector3, bool> onMesh = p => {
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 5f, Vector3.down, 12f)) {
            var n = h.collider.name; if (n.StartsWith("Road") || n.StartsWith("Inter") || n.StartsWith("SCut")) return true; }
        return false; };
    var pts = new System.Collections.Generic.List<Vector3>(); int part = 0; int kept = 0;
    System.Action flush = () => {
        if (pts.Count >= 2) {
            var road = new RacingProject.Enemy.RoadNetwork.Road(); road.name = roadT.name + "#" + part; road.halfWidth = lanes * laneWidth / 2f; road.points = pts.ToArray();
            roads.Add(road); part++; kept += pts.Count;
        }
        pts.Clear(); };
    for (float d = 0f; d <= len; d += 5f) {
        var p = (Vector3)value.Invoke(sp, new object[] { (float)toParam.Invoke(sp, new object[] { Mathf.Min(d, len) }), false });
        if (onMesh(p)) pts.Add(p); else flush();
    }
    flush();
    log.AppendLine(roadT.name + ": " + part + " parts, " + kept + " points");
}
net.SetRoads(roads.ToArray());
EditorUtility.SetDirty(net);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
