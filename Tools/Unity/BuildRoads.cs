// Дороги Environment/Roads по осям Environment/RoadNetwork (вместо Road Architect).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
// Перед запуском — SetupRoadMaterials.cs. Повторный запуск строит всё заново; меши лежат под-ассетами
// в Assets/Content/Roads/Meshes/RoadMeshes.asset. После перестройки перепечь NavMesh (BakeNavMesh.cs).
// Проезжая часть 2 × halfWidth (20 м, 4 полосы), высота — по оси RoadNetwork (поверхность прежнего асфальта).
// В городах (прямоугольники cityRects) — ровный асфальт, свежая разметка, бордюр 15 см и тротуар из плитки 2,7 м;
// за городом — разбитый асфальт с рваным краем, стёртая разметка и гравийная обочина 3 м, сходящая к земле.
// Разметка по ГОСТ: двойная сплошная по оси (1.3), прерывистая между полосами 3 м штрих / 9 м разрыв (1.5),
// сплошная по краю (1.2). У перекрёстков разметки нет, обочины и тротуары обрываются у края другой дороги.
// Дорога, которая начинается там, где кончается другая (Road3 → Road1), строится одной линией.
var env = GameObject.Find("Environment").transform;
var net = env.Find("RoadNetwork").GetComponent<RacingProject.Enemy.RoadNetwork>();
var terrain = Terrain.activeTerrain;
string root = "Assets/Content/Roads";
System.Func<string, Material> M = n => AssetDatabase.LoadAssetAtPath<Material>(root + "/Materials/" + n + ".mat");
var pmAsphalt = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(root + "/Physics/RoadAsphalt.physicMaterial");
var pmGravel = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(root + "/Physics/RoadGravel.physicMaterial");
var cityRects = new[] { new Rect(330f, 90f, 500f, 690f), new Rect(1450f, 440f, 450f, 620f), new Rect(420f, 1550f, 160f, 150f) };
System.Func<Vector3, bool> inCity = p => { foreach (var r in cityRects) if (r.Contains(new Vector2(p.x, p.z))) return true; return false; };
const int ChunkRows = 50;            // 100 м дороги на кусок: меньше кусков — меньше вызовов отрисовки, больше — лучше отсечение
const float CurbHeight = 0.15f, CurbWidth = 0.3f, WalkWidth = 2.7f, ShoulderWidth = 3f;
const float PaintLift = 0.02f;

// Старые дороги выключаем, чтобы не перекрывались с новыми

// --- Линии: оси RoadNetwork, продолжающие друг друга дороги склеиваются ---
var lines = new System.Collections.Generic.List<System.Collections.Generic.List<Vector3>>();
var names = new System.Collections.Generic.List<string>(); var halfs = new System.Collections.Generic.List<float>();
foreach (var r in net.Roads) { lines.Add(new System.Collections.Generic.List<Vector3>(r.points)); names.Add(r.name.Split('#')[0]); halfs.Add(r.halfWidth); }
for (bool merged = true; merged; ) {
    merged = false;
    for (int a = 0; a < lines.Count && !merged; a++) for (int b = 0; b < lines.Count && !merged; b++) {
        if (a == b || Vector3.Distance(lines[a][lines[a].Count - 1], lines[b][0]) > 2f) continue;
        lines[a].AddRange(lines[b].GetRange(1, lines[b].Count - 1)); names[a] = names[a] + "_" + names[b];
        lines.RemoveAt(b); names.RemoveAt(b); halfs.RemoveAt(b); merged = true;
    }
}
// Ближайшая точка чужой оси (и своей же дальше 60 м по линии — петля может пересечь сама себя), по горизонтали:
// { расстояние, x, y, z, направление оси x, z, номер линии, номер отрезка }
System.Func<int, int, Vector3, float[]> closestOther = (li, idx, q) => {
    var res = new float[] { 1e9f, 0f, 0f, 0f, 0f, 0f, -1f, -1f };
    for (int k = 0; k < lines.Count; k++) {
        var L = lines[k];
        for (int i = 0; i < L.Count - 1; i++) {
            if (k == li && Mathf.Abs(i - idx) < 30) continue;
            float dx = L[i].x - q.x, dz = L[i].z - q.z; if (dx * dx + dz * dz > 1600f) continue;
            var a = new Vector2(L[i].x, L[i].z); var ab = new Vector2(L[i + 1].x, L[i + 1].z) - a; var p = new Vector2(q.x, q.z);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            float d = (a + ab * t - p).magnitude;
            if (d >= res[0]) continue;
            var c = Vector3.Lerp(L[i], L[i + 1], t); var dir = ab.normalized;
            res = new float[] { d, c.x, c.y, c.z, dir.x, dir.y, k, i };
        }
    }
    return res; };
System.Func<int, int, Vector3, float> distOther = (li, idx, q) => closestOther(li, idx, q)[0];
// Перекрёстки, где обе дороги идут дальше: { центр, правая сторона первой дороги, правая сторона второй, (полуширины, город) }
var crossings = new System.Collections.Generic.List<Vector3[]>();

// --- Сборка мешей ---
var meshDir = root + "/Meshes"; if (!AssetDatabase.IsValidFolder(meshDir)) AssetDatabase.CreateFolder(root, "Meshes");
string meshPath = meshDir + "/RoadMeshes.asset";
var container = AssetDatabase.LoadMainAssetAtPath(meshPath);
if (container == null) { container = new Mesh { name = "RoadMeshes" }; AssetDatabase.CreateAsset(container, meshPath); }
foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(meshPath)) if (sub != container) AssetDatabase.RemoveObjectFromAsset(sub);
var oldRoot = env.Find("Roads"); if (oldRoot != null) UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);
var rootGo = new GameObject("Roads"); rootGo.transform.SetParent(env, false); rootGo.transform.position = Vector3.zero; rootGo.isStatic = true;
var roadMod = rootGo.AddComponent<Unity.AI.Navigation.NavMeshModifier>(); roadMod.overrideArea = true; roadMod.area = UnityEngine.AI.NavMesh.GetAreaFromName("Road");
int meshCount = 0, vertCount = 0;
// Буфер одного меша: вершины, UV, треугольники
var V = new System.Collections.Generic.List<Vector3>(); var UV = new System.Collections.Generic.List<Vector2>(); var T = new System.Collections.Generic.List<int>();
// Полоса: rows рядов по cols точек профиля, четырёхугольники между соседними рядами и точками; flip — зеркальный профиль (левая сторона)
System.Action<Vector3[,], Vector2[,], bool> strip = (pts, uvs, flip) => {
    int rows = pts.GetLength(0), cols = pts.GetLength(1), b0 = V.Count;
    for (int i = 0; i < rows; i++) for (int j = 0; j < cols; j++) { V.Add(pts[i, j]); UV.Add(uvs[i, j]); }
    for (int i = 0; i < rows - 1; i++) for (int j = 0; j < cols - 1; j++) {
        int v00 = b0 + i * cols + j, v01 = v00 + 1, v10 = v00 + cols, v11 = v10 + 1;
        if (!flip) { T.Add(v00); T.Add(v10); T.Add(v11); T.Add(v00); T.Add(v11); T.Add(v01); }
        else { T.Add(v00); T.Add(v11); T.Add(v10); T.Add(v00); T.Add(v01); T.Add(v11); }
    }
};
// Готовый меш из буфера — в дочерний объект с материалом и (если нужно) коллайдером
System.Func<Transform, string, Material, PhysicsMaterial, Transform> flush = (parent, name, mat, pm) => {
    if (T.Count == 0) { V.Clear(); UV.Clear(); return null; }
    var mesh = new Mesh { name = parent.name + "_" + name };
    mesh.SetVertices(V); mesh.SetUVs(0, UV); mesh.SetTriangles(T, 0);
    mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
    AssetDatabase.AddObjectToAsset(mesh, container);
    var go = new GameObject(name); go.transform.SetParent(parent, false); go.isStatic = true;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    if (pm != null) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.sharedMaterial = pm; }
    meshCount++; vertCount += V.Count;
    V.Clear(); UV.Clear(); T.Clear();
    return go.transform; };
// Тротуары и обочины для NavMesh — обычная земля (дороже дороги), чтобы враги не ездили по ним
System.Action<Transform> walkable = t => { if (t == null) return; var m = t.gameObject.AddComponent<Unity.AI.Navigation.NavMeshModifier>(); m.overrideArea = true; m.area = 0; };

var log = new System.Text.StringBuilder();
for (int li = 0; li < lines.Count; li++) {
    var L = lines[li]; int n = L.Count; float half = halfs[li];
    var lineGo = new GameObject(names[li]); lineGo.transform.SetParent(rootGo.transform, false); lineGo.isStatic = true;
    var right = new Vector3[n]; var s = new float[n]; var city = new bool[n]; var inter = new bool[n];
    var sideOk = new bool[n, 2]; var edge = new float[n, 2];
    for (int i = 0; i < n; i++) {
        var f = L[Mathf.Min(i + 1, n - 1)] - L[Mathf.Max(i - 1, 0)]; f.y = 0f;
        right[i] = Vector3.Cross(Vector3.up, f.normalized);
        s[i] = i == 0 ? 0f : s[i - 1] + Vector3.Distance(L[i], L[i - 1]);
        city[i] = inCity(L[i]);
        inter[i] = distOther(li, i, L[i]) < half + 4f;
        for (int side = 0; side < 2; side++) {
            float sg = side == 0 ? -1f : 1f;
            sideOk[i, side] = distOther(li, i, L[i] + right[i] * sg * (half + 1.5f)) > half + 3.5f;
            // Рваный край асфальта за городом: плавный шум вдоль дороги, ±0,35 м
            edge[i, side] = city[i] ? half : half + 0.7f * (Mathf.PerlinNoise(s[i] * 0.12f, side * 17.3f + li * 5.1f) - 0.5f);
        }
    }
    var edgePos = new Vector3[n, 2];
    for (int i = 0; i < n; i++) { edgePos[i, 0] = L[i] - right[i] * edge[i, 0]; edgePos[i, 1] = L[i] + right[i] * edge[i, 1]; }
    // Дорога упирается в другую (Т-образный перекрёсток): край торца продлевается до оси той дороги,
    // иначе при подходе под углом угол полотна торчал бы за её дальний край
    for (int end = 0; end < 2; end++) {
        int i0 = end == 0 ? 0 : n - 1, i1 = end == 0 ? 1 : n - 2;
        var co = closestOther(li, i0, L[i0]); if (co[0] > 3f) continue;
        var cB = new Vector2(co[1], co[3]); var dB = new Vector2(co[4], co[5]);
        var dA3 = L[i0] - L[i1]; var dA = new Vector2(dA3.x, dA3.z).normalized;
        float den = dA.x * dB.y - dA.y * dB.x; if (Mathf.Abs(den) < 0.2f) continue;
        for (int side = 0; side < 2; side++) {
            var e0 = new Vector2(edgePos[i1, side].x, edgePos[i1, side].z);
            var w = cB - e0; float t = (w.x * dB.y - w.y * dB.x) / den;
            var hit = e0 + dA * Mathf.Clamp(t, 0f, 30f); edgePos[i0, side] = new Vector3(hit.x, L[i0].y, hit.y);
        }
        log.AppendLine(names[li] + (end == 0 ? " start" : " end") + " cut along " + names[(int)co[6]]);
    }
    // Перекрёстки с продолжением обеих дорог (для угловых тротуаров): ближайшая точка чужой оси рядом, локальный минимум
    for (int i = 8; i < n - 8; i++) {
        var co = closestOther(li, i, L[i]); if (co[0] > 1.5f) continue;
        int k = (int)co[6], j = (int)co[7];
        if (j < 8 || j > lines[k].Count - 9 || k < li || (k == li && j < i)) continue;
        if (distOther(li, i - 1, L[i - 1]) < co[0] || distOther(li, i + 1, L[i + 1]) < co[0]) continue;
        var rB = Vector3.Cross(Vector3.up, new Vector3(co[4], 0f, co[5]));
        crossings.Add(new[] { L[i], right[i], rB, new Vector3(half, halfs[k], city[i] ? 1f : 0f) });
    }
    int chunk = 0;
    for (int c0 = 0; c0 < n - 1; chunk++) {
        int c1 = c0 + 1; bool cityChunk = city[c0];
        while (c1 < n - 1 && c1 - c0 < ChunkRows && city[c1] == cityChunk) c1++;
        int rows = c1 - c0 + 1;
        var chunkGo = new GameObject((cityChunk ? "City_" : "Rural_") + chunk.ToString("00")); chunkGo.transform.SetParent(lineGo.transform, false); chunkGo.isStatic = true;
        var ct = chunkGo.transform;
        // Асфальт: UV по миру, чтобы на перекрёстках наложенные полосы совпадали текстурой
        float tile = cityChunk ? 8f : 16f;
        var ap = new Vector3[rows, 2]; var au = new Vector2[rows, 2];
        for (int r = 0; r < rows; r++) { int i = c0 + r;
            ap[r, 0] = edgePos[i, 0]; ap[r, 1] = edgePos[i, 1];
            for (int j = 0; j < 2; j++) au[r, j] = new Vector2(ap[r, j].x / tile, ap[r, j].z / tile); }
        strip(ap, au, false);
        flush(ct, "Asphalt", M(cityChunk ? "Road_Asphalt" : "Road_AsphaltWorn"), pmAsphalt);
        // Разметка: (смещение от оси, ширина, штрих, разрыв); штрих 0 — сплошная
        var marks = new[] { new Vector4(-0.12f, 0.12f, 0, 0), new Vector4(0.12f, 0.12f, 0, 0), new Vector4(-half * 0.5f, 0.12f, 3f, 9f), new Vector4(half * 0.5f, 0.12f, 3f, 9f),
                            new Vector4(-(half - 0.35f), 0.15f, 0, 0), new Vector4(half - 0.35f, 0.15f, 0, 0) };
        foreach (var mk in marks) {
            for (int i = c0; i < c1; i++) {
                if (inter[i] || inter[i + 1]) continue;
                float sa = s[i], sb = s[i + 1], u = sa;
                while (u < sb - 0.01f) {
                    float end = sb; bool on = true;
                    if (mk.z > 0f) { float period = mk.z + mk.w, phase = u % period; on = phase < mk.z; end = Mathf.Min(sb, u + (on ? mk.z - phase : period - phase)); }
                    if (on) {
                        var q = new Vector3[2, 2]; var qu = new Vector2[2, 2];
                        for (int r = 0; r < 2; r++) {
                            float t = ((r == 0 ? u : end) - sa) / Mathf.Max(sb - sa, 1e-4f);
                            var c = Vector3.Lerp(L[i], L[i + 1], t) + Vector3.up * PaintLift; var rt = Vector3.Lerp(right[i], right[i + 1], t).normalized;
                            q[r, 0] = c + rt * (mk.x - mk.y * 0.5f); q[r, 1] = c + rt * (mk.x + mk.y * 0.5f);
                            for (int j = 0; j < 2; j++) qu[r, j] = new Vector2(q[r, j].x / 4f, q[r, j].z / 4f);
                        }
                        strip(q, qu, false);
                    }
                    u = end;
                }
            }
        }
        flush(ct, "Paint", M(cityChunk ? "Road_PaintCity" : "Road_PaintWorn"), null);
        // Края: бордюр и тротуар в городе, гравийная обочина за городом; ряды, где край упирается в другую дорогу, пропускаются
        for (int side = 0; side < 2; side++) {
            float sg = side == 0 ? -1f : 1f;
            bool flip = side == 0;
            for (int part = 0; part < (cityChunk ? 2 : 1); part++) {
                for (int i = c0; i < c1; i++) {
                    if (!sideOk[i, side] || !sideOk[i + 1, side]) continue;
                    Vector3[,] q; Vector2[,] qu;
                    if (cityChunk && part == 0) {
                        // Бордюр: лицевая грань и верх, UV: вдоль — метры / 2 (шов камня через 1 м), поперёк — по профилю
                        q = new Vector3[2, 3]; qu = new Vector2[2, 3];
                        for (int r = 0; r < 2; r++) { int k = i + r; var c = L[k]; var rt = right[k] * sg; float e = edge[k, side];
                            q[r, 0] = c + rt * e; q[r, 1] = c + rt * e + Vector3.up * CurbHeight; q[r, 2] = c + rt * (e + CurbWidth) + Vector3.up * CurbHeight;
                            qu[r, 0] = new Vector2(s[k] / 2f, 0f); qu[r, 1] = new Vector2(s[k] / 2f, 0.33f); qu[r, 2] = new Vector2(s[k] / 2f, 1f); }
                    } else if (cityChunk) {
                        // Тротуар: верх с лёгким уклоном к дороге и юбка вниз по внешнему краю; плитка вдоль дороги
                        q = new Vector3[2, 3]; qu = new Vector2[2, 3];
                        for (int r = 0; r < 2; r++) { int k = i + r; var c = L[k]; var rt = right[k] * sg; float e = edge[k, side] + CurbWidth;
                            q[r, 0] = c + rt * e + Vector3.up * CurbHeight; q[r, 1] = c + rt * (e + WalkWidth) + Vector3.up * (CurbHeight + 0.03f); q[r, 2] = c + rt * (e + WalkWidth) + Vector3.up * -0.6f;
                            qu[r, 0] = new Vector2(0f, s[k] / 4f); qu[r, 1] = new Vector2(WalkWidth / 4f, s[k] / 4f); qu[r, 2] = new Vector2(WalkWidth / 4f + 0.19f, s[k] / 4f); }
                    } else {
                        // Обочина: от края асфальта чуть ниже него, к внешнему краю сходит к земле (не выше +0,25 и не ниже −0,6 м от дороги)
                        q = new Vector3[2, 4]; qu = new Vector2[2, 4];
                        for (int r = 0; r < 2; r++) { int k = i + r; var c = L[k]; var rt = right[k] * sg; float e = edge[k, side];
                            var outer = c + rt * (e + ShoulderWidth);
                            float ground = terrain.SampleHeight(outer) + terrain.transform.position.y;
                            float dy = Mathf.Clamp(ground + 0.03f - c.y, -0.6f, 0.25f);
                            q[r, 0] = c + rt * e + Vector3.up * -0.02f; q[r, 1] = c + rt * (e + ShoulderWidth * 0.5f) + Vector3.up * Mathf.Min(-0.05f, dy * 0.5f);
                            q[r, 2] = outer + Vector3.up * dy; q[r, 3] = outer + rt * 0.2f + Vector3.up * (dy - 0.6f);
                            for (int j = 0; j < 4; j++) qu[r, j] = new Vector2(q[r, j].x / 4f, q[r, j].z / 4f); }
                    }
                    strip(q, qu, flip);
                }
                string nm = !cityChunk ? "Shoulder" : (part == 0 ? "Curb" : "Sidewalk");
                if (side == 0) nm += "L"; else nm += "R";
                var t2 = flush(ct, nm, M(!cityChunk ? "Road_Gravel" : (part == 0 ? "Road_Curb" : "Road_Sidewalk")), !cityChunk ? pmGravel : pmAsphalt);
                walkable(t2);
            }
        }
        c0 = c1;
    }
    log.AppendLine(names[li] + ": " + n + " points, " + chunk + " chunks, " + s[n - 1].ToString("0") + " m");
}
// Четырёхугольник с нормалью в сторону want (порядок обхода подбирается сам)
System.Action<Vector3[], Vector2[], Vector3> quad = (q4, u4, want) => {
    int b0 = V.Count; for (int k = 0; k < 4; k++) { V.Add(q4[k]); UV.Add(u4[k]); }
    if (Vector3.Dot(Vector3.Cross(q4[1] - q4[0], q4[2] - q4[0]), want) > 0f) { T.Add(b0); T.Add(b0 + 1); T.Add(b0 + 2); T.Add(b0); T.Add(b0 + 2); T.Add(b0 + 3); }
    else { T.Add(b0); T.Add(b0 + 2); T.Add(b0 + 1); T.Add(b0); T.Add(b0 + 3); T.Add(b0 + 2); }
};
// Углы городских перекрёстков: тротуары двух дорог обрываются, не доходя друг до друга, — угол закрывается
// квадратом тротуара с бордюром вдоль обеих проезжих частей. Точка задаётся смещениями от осей обеих дорог
var crossRoot = new GameObject("Crossings"); crossRoot.transform.SetParent(rootGo.transform, false); crossRoot.isStatic = true;
int crossCount = 0;
foreach (var cr in crossings) {
    if (cr[3].z < 0.5f) continue;
    var c = cr[0]; var rA = new Vector2(cr[1].x, cr[1].z); var rB = new Vector2(cr[2].x, cr[2].z); float hA = cr[3].x, hB = cr[3].y;
    float det = rA.x * rB.y - rA.y * rB.x; if (Mathf.Abs(det) < 0.2f) continue;
    float W = CurbWidth + WalkWidth + 0.5f;
    System.Func<float, float, float, Vector3> X = (oa, ob, up) => new Vector3(c.x + (oa * rB.y - ob * rA.y) / det, c.y + up, c.z + (rA.x * ob - rB.x * oa) / det);
    var cgo = new GameObject("Crossing_" + crossCount++); cgo.transform.SetParent(crossRoot.transform, false); cgo.isStatic = true;
    float top = CurbHeight + 0.01f; // на сантиметр выше тротуаров, чтобы не мерцать там, где они заходят под угол
    foreach (float sA in new[] { -1f, 1f }) foreach (float sB in new[] { -1f, 1f }) {
        var q4 = new[] { X(sA * hA, sB * hB, top), X(sA * hA, sB * (hB + W), top), X(sA * (hA + W), sB * (hB + W), top), X(sA * (hA + W), sB * hB, top) };
        quad(q4, System.Array.ConvertAll(q4, p => new Vector2(p.x / 4f, p.z / 4f)), Vector3.up);
    }
    walkable(flush(cgo.transform, "Corners", M("Road_Sidewalk"), pmAsphalt));
    foreach (float sA in new[] { -1f, 1f }) foreach (float sB in new[] { -1f, 1f }) {
        // Грань вдоль края первой дороги смотрит к её оси, вдоль края второй — к оси второй
        var a0 = X(sA * hA, sB * hB, -0.05f); var a1 = X(sA * hA, sB * (hB + W), -0.05f);
        var b1 = X(sA * (hA + W), sB * hB, -0.05f);
        var lift = Vector3.up * (top + 0.05f);
        var uvA = new[] { new Vector2(0f, 0f), new Vector2(W / 2f, 0f), new Vector2(W / 2f, 0.33f), new Vector2(0f, 0.33f) };
        quad(new[] { a0, a1, a1 + lift, a0 + lift }, uvA, -sA * cr[1]);
        quad(new[] { a0, b1, b1 + lift, a0 + lift }, uvA, -sB * cr[2]);
    }
    flush(cgo.transform, "CornerCurbs", M("Road_Curb"), pmAsphalt);
}
log.AppendLine(crossings.Count + " crossings, " + crossCount + " with city corners");
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
log.AppendLine(meshCount + " meshes, " + vertCount + " vertices");
return log.ToString();
