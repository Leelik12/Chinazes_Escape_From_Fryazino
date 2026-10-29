// Пожары в городе (Environment/CityFires): в восьми панельках горит по квартире — огонь из окна, копоть на фасаде
// над ним, чёрный дым и мерцающий свет; одна высотка City2 горит на нескольких этажах, её дым и зарево видны
// издалека (слой Landmark). Над City1 и City2 висит медленная дымная пелена. Частицы — копии систем пожара
// на горизонте (Environment/HorizonFires/HorizonFire_0), уменьшенные масштабом трансформа (scalingMode Hierarchy).
// Выбор домов и окон детерминирован (seed), повторный запуск пересобирает группу с нуля.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var log = new System.Text.StringBuilder();
var env = GameObject.Find("Environment").transform;
var old = env.Find("CityFires");
if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
var root = new GameObject("CityFires").transform;
root.SetParent(env, false);
int landmark = LayerMask.NameToLayer("Landmark");
var template = env.Find("HorizonFires/HorizonFire_0");
var scorchMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_Scorch.mat");
var rnd = new System.Random(1945);

// Дома-панельки: прямые потомки групп районов с материалами rus_build_ и выше 12 м
var houses = new System.Collections.Generic.List<Transform>();
foreach (var district in new[] { "City1", "City2", "City3", "Yards" }) {
    var d = env.Find(district);
    if (d == null) continue;
    foreach (Transform h in d) {
        var rs = h.GetComponentsInChildren<MeshRenderer>();
        if (rs.Length == 0) continue;
        bool panel = false;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) { b.Encapsulate(r.bounds); foreach (var m in r.sharedMaterials) if (m != null && m.name.StartsWith("rus_build_")) panel = true; }
        if (panel && b.size.y > 12f && b.size.x < 150f && b.size.z < 150f) houses.Add(h);
    }
}
System.Func<Transform, Bounds> boundsOf = h => {
    var rs = h.GetComponentsInChildren<MeshRenderer>();
    Bounds b = rs[0].bounds;
    foreach (var r in rs) b.Encapsulate(r.bounds);
    return b;
};

// Копия системы частиц шаблона с множителями
System.Func<Transform, string, Vector3, float, Transform> copy = (parent, name, pos, scale) => {
    var src = template.Find(name);
    var go = UnityEngine.Object.Instantiate(src.gameObject, parent);
    go.name = name;
    go.transform.localPosition = pos;
    go.transform.localRotation = Quaternion.identity;
    go.transform.localScale = Vector3.one * scale;
    var ps = go.GetComponent<ParticleSystem>();
    if (ps != null) { var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; }
    return go.transform;
};

// Точка на фасаде: луч снаружи к центру дома на заданной высоте; возвращает false, если попал не в этот дом
System.Func<Transform, Bounds, float, Vector3[]> facade = (h, b, heightFrac) => {
    for (int attempt = 0; attempt < 12; attempt++) {
        float angle = (float)rnd.NextDouble() * 360f;
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        float y = b.min.y + b.size.y * heightFrac;
        Vector3 target = new Vector3(b.center.x + ((float)rnd.NextDouble() - 0.5f) * b.size.x * 0.5f, y, b.center.z + ((float)rnd.NextDouble() - 0.5f) * b.size.z * 0.5f);
        Vector3 from = target + dir * (b.extents.magnitude + 10f);
        RaycastHit hit;
        if (Physics.Raycast(from, -dir, out hit, b.extents.magnitude * 3f, ~0, QueryTriggerInteraction.Ignore) && hit.transform.IsChildOf(h) && Mathf.Abs(hit.normal.y) < 0.3f)
            return new[] { hit.point, hit.normal };
    }
    return null;
};

System.Action<Transform, Vector3, Vector3, float, bool> fire = (parent, point, normal, size, big) => {
    var t = new GameObject("Fire").transform;
    t.SetParent(parent, false);
    t.position = point + normal * 0.8f;
    t.rotation = Quaternion.LookRotation(normal);
    copy(t, "Fire", Vector3.zero, 0.22f * size);
    copy(t, "Smoke", Vector3.up * 2f, (big ? 0.45f : 0.28f) * size);
    // Свет: внутри квартиры и снаружи у окна
    var lt = copy(t, "Light", new Vector3(0f, 1.5f, 0.5f), 1f);
    var light = lt.GetComponent<Light>();
    light.range = 22f * size;
    light.intensity = 40f * size;
    light.shadows = LightShadows.None;
    // Копоть над окном
    if (scorchMat != null) {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
        q.name = "Soot";
        q.transform.SetParent(t, false);
        q.transform.position = point + normal * 0.06f + Vector3.up * 2.6f * size;
        q.transform.rotation = Quaternion.LookRotation(-normal);
        q.transform.localScale = new Vector3(4.5f * size, 7f * size, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = scorchMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
};

// Обычные пожары в квартирах
int fires = 0;
var picked = new System.Collections.Generic.HashSet<int>();
Transform tallest = null; float tallestH = 0f;
foreach (var h in houses) { var b = boundsOf(h); if (h.parent.name == "City2" && b.size.y > tallestH) { tallestH = b.size.y; tallest = h; } }
for (int i = 0; i < 40 && fires < 8 && houses.Count > 0; i++) {
    int k = rnd.Next(houses.Count);
    if (picked.Contains(k) || houses[k] == tallest) continue;
    picked.Add(k);
    var b = boundsOf(houses[k]);
    var hit = facade(houses[k], b, 0.25f + (float)rnd.NextDouble() * 0.5f);
    if (hit == null) continue;
    var group = new GameObject("ApartmentFire_" + fires).transform;
    group.SetParent(root, false);
    fire(group, hit[0], hit[1], 1f, false);
    fires++;
}
log.Append("apartment fires ").Append(fires).Append("; ");

// Горящая высотка
if (tallest != null) {
    var b = boundsOf(tallest);
    var group = new GameObject("BurningTower").transform;
    group.SetParent(root, false);
    int floors = 0;
    foreach (float f in new[] { 0.45f, 0.6f, 0.72f, 0.85f }) {
        var hit = facade(tallest, b, f);
        if (hit == null) continue;
        fire(group, hit[0], hit[1], 1.4f, true);
        floors++;
    }
    // Столб дыма и зарево над крышей — видны из любой точки карты
    var top = new GameObject("Column").transform;
    top.SetParent(group, false);
    top.position = new Vector3(b.center.x, b.max.y, b.center.z);
    copy(top, "Smoke", Vector3.zero, 0.9f);
    copy(top, "Glow", Vector3.up * 6f, 0.35f);
    foreach (var t in group.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = landmark;
    log.Append("tower ").Append(tallest.name).Append(" floors ").Append(floors).Append(" h ").Append(b.size.y.ToString("0")).Append("; ");
}

// Дымная пелена над кварталами: крупные медленные клубы на высоте 40–60 м
foreach (var c in new[] { new Vector3(688f, 0f, 420f), new Vector3(1662f, 0f, 760f) }) {
    var haze = new GameObject("Haze").transform;
    haze.SetParent(root, false);
    float ground = Terrain.activeTerrain.SampleHeight(c);
    haze.position = new Vector3(c.x, ground + 45f, c.z);
    var s = copy(haze, "Smoke", Vector3.zero, 1f);
    var ps = s.GetComponent<ParticleSystem>();
    var main = ps.main;
    main.startLifetime = 40f;
    main.startColor = new Color(0.12f, 0.11f, 0.1f, 0.25f);
    var shape = ps.shape;
    shape.enabled = true;
    shape.shapeType = ParticleSystemShapeType.Box;
    shape.scale = new Vector3(220f, 10f, 300f);
    var em = ps.emission;
    em.rateOverTime = 1.2f;
    foreach (var t in haze.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = landmark;
}

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(env.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(env.gameObject.scene);
log.Append("panel houses ").Append(houses.Count);
return log.ToString();
