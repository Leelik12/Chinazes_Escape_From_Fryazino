// Граница мира: коллайдеры секций забора Environment/BorderFence подгоняются под видимый меш (были на 3 м выше),
// а две дыры в заборе (7 м у (330, 803) и (1207, 146)) закрываются стенкой из блоков ФБС в три ряда — без них
// через дыру можно было доехать до невидимых стен Environment/BordersWorld. Стенки — Environment/BorderFence/Gap_*.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var log = new System.Text.StringBuilder();
var fence = GameObject.Find("Environment/BorderFence").transform;

// --- Коллайдеры по мешу ---
int fitted = 0;
foreach (Transform seg in fence) {
    var box = seg.GetComponent<BoxCollider>();
    var mf = seg.GetComponentInChildren<MeshFilter>();
    if (box == null || mf == null || mf.sharedMesh == null) continue;
    // Границы меша (он может лежать в дочернем объекте) в локальных координатах секции
    Bounds src = mf.sharedMesh.bounds;
    var mb = new Bounds(seg.InverseTransformPoint(mf.transform.TransformPoint(src.min)), Vector3.zero);
    for (int c = 0; c < 8; c++) {
        Vector3 corner = new Vector3((c & 1) == 0 ? src.min.x : src.max.x, (c & 2) == 0 ? src.min.y : src.max.y, (c & 4) == 0 ? src.min.z : src.max.z);
        mb.Encapsulate(seg.InverseTransformPoint(mf.transform.TransformPoint(corner)));
    }
    box.center = mb.center;
    box.size = new Vector3(mb.size.x, mb.size.y, Mathf.Max(mb.size.z, 0.65f));
    fitted++;
}
log.Append("fitted ").Append(fitted).Append("; ");

// --- Дыры ---
for (int i = fence.childCount - 1; i >= 0; i--)
    if (fence.GetChild(i).name.StartsWith("Gap_")) UnityEngine.Object.DestroyImmediate(fence.GetChild(i).gameObject);
var segs = new System.Collections.Generic.List<Vector3[]>();
foreach (Transform seg in fence) {
    var box = seg.GetComponent<BoxCollider>();
    if (box == null) continue;
    segs.Add(new[] { seg.TransformPoint(box.center - Vector3.right * box.size.x / 2), seg.TransformPoint(box.center + Vector3.right * box.size.x / 2) });
}
// Концы секций, у которых нет соседа ближе 2,5 м, попарно — это дыра
var ends = new System.Collections.Generic.List<Vector3>();
for (int i = 0; i < segs.Count; i++) for (int e = 0; e < 2; e++) {
    Vector3 p = segs[i][e]; float best = 1e9f;
    for (int j = 0; j < segs.Count; j++) if (j != i) for (int k = 0; k < 2; k++) {
        Vector3 q = segs[j][k];
        best = Mathf.Min(best, Vector2.Distance(new Vector2(p.x, p.z), new Vector2(q.x, q.z)));
    }
    if (best > 2.5f && best < 20f) ends.Add(p);
}
var blockPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Roadside/ConcreteBlock.prefab");
var probe = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab);
Bounds pb = probe.GetComponentInChildren<Collider>().bounds;
float blockLen = pb.size.x, blockH = pb.size.y;
UnityEngine.Object.DestroyImmediate(probe);
var used = new bool[ends.Count];
int gaps = 0;
for (int i = 0; i < ends.Count; i++) {
    if (used[i]) continue;
    int pair = -1; float best = 1e9f;
    for (int j = 0; j < ends.Count; j++) if (j != i && !used[j]) {
        float d = Vector3.Distance(ends[i], ends[j]);
        if (d < best) { best = d; pair = j; }
    }
    if (pair < 0) continue;
    used[i] = used[pair] = true;
    Vector3 a = ends[i], b = ends[pair];
    Vector3 dir = b - a; dir.y = 0f;
    float len = dir.magnitude + 1.5f;
    var gap = new GameObject("Gap_" + gaps++).transform;
    gap.SetParent(fence, false);
    gap.position = (a + b) * 0.5f;
    gap.rotation = Quaternion.LookRotation(Vector3.Cross(dir.normalized, Vector3.up));
    int count = Mathf.CeilToInt(len / blockLen);
    for (int row = 0; row < 3; row++) {
        // Ряды вперевязку, верхний ряд из двух блоков с промежутком
        float shift = row % 2 == 1 ? blockLen * 0.5f : 0f;
        int n = row % 2 == 1 ? count - 1 : count;
        for (int k = 0; k < n; k++) {
            if (row == 2 && k % 2 == 1) continue;
            float x = -len / 2 + blockLen * (k + 0.5f) + shift;
            Vector3 p = gap.TransformPoint(new Vector3(x, 0f, 0f));
            p.y = Terrain.activeTerrain.SampleHeight(p) + Terrain.activeTerrain.transform.position.y + row * blockH - 0.1f;
            var block = (GameObject)PrefabUtility.InstantiatePrefab(blockPrefab, gap);
            block.transform.position = p;
            block.transform.rotation = gap.rotation * Quaternion.Euler(0f, UnityEngine.Random.Range(-4f, 4f), 0f);
        }
    }
    log.Append(string.Format("gap {0:0.0} m at ({1:0},{2:0}); ", best, gap.position.x, gap.position.z));
}
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(fence.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(fence.gameObject.scene);
log.Append("block ").Append(blockLen.ToString("0.00")).Append('x').Append(blockH.ToString("0.00"));
return log.ToString();
