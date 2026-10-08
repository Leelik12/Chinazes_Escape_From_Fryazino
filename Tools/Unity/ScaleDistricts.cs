// Увеличение старых домов районов (City1, City2, City3) в 1.5 раза под масштаб мира.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6). Запускать один раз.
// Дома обрабатываются от крупных к мелким: каждый пробует масштаб 1.5 на месте и со сдвигами до 12 м,
// потом 1.25 на месте; если ничего не влезло, остаётся прежним. «Не влезло» — пятно дома (с зазором 5 м на проезд)
// пересекает уже принятые дома или дворы, задевает дорогу или стоит на склоне с перепадом больше 3 м.
// Мелочь из *SmallEnvironment, оказавшаяся под новым пятном дома, удаляется.
var env = GameObject.Find("Environment").transform;
var terrain = Terrain.activeTerrain;
var log = new System.Text.StringBuilder();

// Растр дорог 2 м на клетку
int N = 1000; var road = new bool[N * N];
foreach (var mf in env.Find("RoadArchitectSystem1").GetComponentsInChildren<MeshFilter>()) {
    if (mf.sharedMesh == null || mf.GetComponent<Renderer>() == null) continue;
    var v = mf.sharedMesh.vertices; var tris = mf.sharedMesh.triangles; var tf = mf.transform;
    for (int i = 0; i < tris.Length; i += 3) {
        var a = tf.TransformPoint(v[tris[i]]); var b = tf.TransformPoint(v[tris[i + 1]]); var c = tf.TransformPoint(v[tris[i + 2]]);
        int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, Mathf.Min(b.x, c.x)) / 2)), x1 = Mathf.Min(N - 1, (int)(Mathf.Max(a.x, Mathf.Max(b.x, c.x)) / 2));
        int z0 = Mathf.Max(0, (int)(Mathf.Min(a.z, Mathf.Min(b.z, c.z)) / 2)), z1 = Mathf.Min(N - 1, (int)(Mathf.Max(a.z, Mathf.Max(b.z, c.z)) / 2));
        if (x1 - x0 > 40 || z1 - z0 > 40) continue;
        for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++) road[z * N + x] = true;
    }
}

// Пятно дома в его локальных осях (без учёта масштаба корня): центр и полуразмеры по X/Z
System.Func<Transform, Vector4> localFootprint = root => {
    var min = new Vector3(1e9f, 0, 1e9f); var max = new Vector3(-1e9f, 0, -1e9f);
    var inv = Matrix4x4.TRS(root.position, root.rotation, root.lossyScale).inverse;
    foreach (var mf in root.GetComponentsInChildren<MeshFilter>()) {
        if (mf.sharedMesh == null || mf.GetComponent<Renderer>() == null) continue;
        var bb = mf.sharedMesh.bounds;
        for (int i = 0; i < 8; i++) {
            var c = bb.center + Vector3.Scale(bb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var l = inv.MultiplyPoint3x4(mf.transform.TransformPoint(c));
            min = Vector3.Min(min, l); max = Vector3.Max(max, l);
        }
    }
    return new Vector4((min.x + max.x) / 2, (min.z + max.z) / 2, (max.x - min.x) / 2, (max.z - min.z) / 2);
};
// Прямоугольник на земле: центр, полуразмеры, оси
System.Func<Vector3, Quaternion, float, Vector4, Vector3[]> rect = (pos, rot, scale, fp) => {
    var right = rot * Vector3.right; var fwd = rot * Vector3.forward;
    var c = pos + right * fp.x * scale + fwd * fp.y * scale;
    return new[] { c, right * fp.z * scale, fwd * fp.w * scale };
};
System.Func<Vector3[], Vector3[], float, bool> overlap = (r1, r2, margin) => {
    var axes = new[] { r1[1].normalized, r1[2].normalized, r2[1].normalized, r2[2].normalized };
    foreach (var ax in axes) {
        float d = Mathf.Abs(Vector3.Dot(r2[0] - r1[0], ax));
        float e1 = Mathf.Abs(Vector3.Dot(r1[1], ax)) + Mathf.Abs(Vector3.Dot(r1[2], ax));
        float e2 = Mathf.Abs(Vector3.Dot(r2[1], ax)) + Mathf.Abs(Vector3.Dot(r2[2], ax));
        if (d > e1 + e2 + margin) return false;
    }
    return true;
};
System.Func<Vector3[], bool> badGround = r => {
    float lo = 1e9f, hi = -1e9f;
    for (float u = -1f; u <= 1.001f; u += 0.1f) for (float w = -1f; w <= 1.001f; w += 0.1f) {
        var p = r[0] + r[1] * u + r[2] * w;
        int ix = (int)(p.x / 2), iz = (int)(p.z / 2);
        if (ix < 0 || iz < 0 || ix >= N || iz >= N || road[iz * N + ix]) return true;
        float h = terrain.SampleHeight(p); lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
    }
    return hi - lo > 3f;
};

// Уже стоящие дома дворов — неподвижные препятствия
var accepted = new System.Collections.Generic.List<Vector3[]>();
var yardsRoot = env.Find("Yards");
if (yardsRoot != null) foreach (Transform y in yardsRoot) foreach (Transform h in y.Find("Houses"))
    accepted.Add(rect(h.position, h.rotation, h.lossyScale.x, localFootprint(h)));

var items = new System.Collections.Generic.List<Transform>();
foreach (var city in new[] { "City1", "City2", "City3" }) foreach (Transform c in env.Find(city)) if (c.name.StartsWith("rus_build")) items.Add(c);
var fps = new System.Collections.Generic.Dictionary<Transform, Vector4>();
foreach (var t in items) fps[t] = localFootprint(t);
items.Sort((a, b) => (fps[b].z * fps[b].w).CompareTo(fps[a].z * fps[a].w));

float gap = 5f; int full = 0, partial = 0, kept = 0;
var offsets = new System.Collections.Generic.List<Vector2> { Vector2.zero };
foreach (var d in new[] { 6f, 12f }) for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; offsets.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d); }
foreach (var t in items) {
    float s0 = t.localScale.x; var fp = fps[t]; Vector3[] chosen = null; float chosenScale = s0; Vector3 chosenPos = t.position;
    var tries = new System.Collections.Generic.List<object[]>();
    foreach (var o in offsets) tries.Add(new object[] { 1.5f, o });
    tries.Add(new object[] { 1.25f, Vector2.zero });
    foreach (var tr in tries) {
        float s = s0 * (float)tr[0]; var o = (Vector2)tr[1];
        var pos = t.position + new Vector3(o.x, 0, o.y);
        var r = rect(pos, t.rotation, s, fp);
        if (badGround(r)) continue;
        bool hit = false; foreach (var a in accepted) if (overlap(r, a, gap)) { hit = true; break; }
        if (hit) continue;
        chosen = r; chosenScale = s; chosenPos = pos; break;
    }
    if (chosen == null) { kept++; accepted.Add(rect(t.position, t.rotation, s0, fp)); log.AppendLine("kept 1x: " + t.parent.name + "/" + t.name); continue; }
    if (Mathf.Approximately(chosenScale, s0 * 1.5f)) full++; else partial++;
    chosenPos.y = terrain.SampleHeight(chosenPos) + terrain.transform.position.y;
    t.position = chosenPos; t.localScale = Vector3.one * chosenScale; accepted.Add(chosen);
}

// Мелочь под новыми пятнами домов убираем
int propsRemoved = 0;
foreach (var city in new[] { "City1SmallEnvironment", "City2SmallEnvironment" }) {
    var del = new System.Collections.Generic.List<GameObject>();
    foreach (Transform p in env.Find(city)) {
        var pr = new[] { p.position, Vector3.right * 1.5f, Vector3.forward * 1.5f };
        foreach (var a in accepted) if (overlap(pr, a, 0f)) { del.Add(p.gameObject); break; }
    }
    foreach (var g in del) { UnityEngine.Object.DestroyImmediate(g); propsRemoved++; }
}
log.AppendLine("1.5x: " + full + ", 1.25x: " + partial + ", kept: " + kept + ", props removed: " + propsRemoved);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
return log.ToString();
