// Стенд управляемости машины игрока: ровная асфальтовая площадка далеко за картой и автопилот с клавиатуры.
// Замеры: разгон 0–60 и 0–100, тормозной путь со 100 км/ч, крен и боковое ускорение в повороте на 60 км/ч,
// слалом на 80 км/ч, разворот ручником на 50 км/ч, наезд на кочку на 70 км/ч. Результат — в SessionState "handling".
// Запуск: Play, «Одиночная игра», затем выполнить текст через execute_code (MCP for Unity, C# 6); пока идёт стенд,
// машину не берёт урон (прочность восстанавливается каждый кадр)
var car = UnityEngine.Object.FindFirstObjectByType<RacingProject.Car.CarControllerSample>();
var rb = car.GetComponent<Rigidbody>();
var hp = car.GetComponent<RacingProject.PlayerHealth>();
var kb = UnityEngine.InputSystem.Keyboard.current;
var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Content/Roads/Physics/RoadAsphalt.physicMaterial");
var basePos = new Vector3(6000f, 0f, 6000f);
// Враги на время стенда убираются: они догоняют машину за картой, падают в пустоту, и поиск пути по навмешу съедает кадр
foreach (var manager in UnityEngine.Object.FindObjectsByType<RacingProject.Enemy.EnemyManager>(FindObjectsSortMode.None)) manager.enabled = false;
foreach (var enemy in UnityEngine.Object.FindObjectsByType<RacingProject.Enemy.EnemyCarController>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(enemy.gameObject);
var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
ground.name = "HandlingTestGround";
ground.transform.position = basePos + new Vector3(0f, -0.5f, 0f);
ground.transform.localScale = new Vector3(600f, 1f, 2400f);
ground.GetComponent<Collider>().sharedMaterial = pm;
// Кочка поперёк дороги: высота 0,15 м, ширина 1 м
var bump = GameObject.CreatePrimitive(PrimitiveType.Cube);
bump.name = "HandlingTestBump";
bump.transform.position = basePos + new Vector3(200f, 0.075f, 300f);
bump.transform.localScale = new Vector3(20f, 0.15f, 1f);
bump.GetComponent<Collider>().sharedMaterial = pm;
Physics.SyncTransforms();

var log = new System.Text.StringBuilder();
System.Action<UnityEngine.InputSystem.Key[]> keys = k => UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(k));
var W = UnityEngine.InputSystem.Key.W; var S = UnityEngine.InputSystem.Key.S; var A = UnityEngine.InputSystem.Key.A;
var D = UnityEngine.InputSystem.Key.D; var Sp = UnityEngine.InputSystem.Key.Space;
System.Action<Vector3, float> place = (offset, yaw) => {
    var p = basePos + offset + Vector3.up * 1.2f; var r = Quaternion.Euler(0f, yaw, 0f);
    rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
    rb.position = p; rb.rotation = r; rb.transform.SetPositionAndRotation(p, r);
};
// Разгон стенда: машину сразу толкаем до нужной скорости, чтобы не ждать
System.Action<float> launch = kmh => { rb.linearVelocity = rb.transform.forward * kmh / 3.6f; };
System.Func<float> kmhF = () => Vector3.Dot(rb.linearVelocity, rb.transform.forward) * 3.6f;
System.Func<float> roll = () => { float z = rb.transform.eulerAngles.z; return Mathf.Abs(z > 180f ? z - 360f : z); };
System.Func<float> pitch = () => { float x = rb.transform.eulerAngles.x; return Mathf.Abs(x > 180f ? x - 360f : x); };

int test = 0, phase = 0; float t0 = 0f, mark = 0f, maxRoll = 0f, maxLat = 0f, maxPitch = 0f, yaw0 = 0f;
float t60 = -1f, t100 = -1f; Vector3 p0 = Vector3.zero, lastVel = Vector3.zero; bool flipped = false;
float tStart = Time.time;
EditorApplication.CallbackFunction cb = null;
cb = () => {
    if (!EditorApplication.isPlaying) { EditorApplication.update -= cb; return; }
    hp.Heal(hp.MaxHealth);
    foreach (var enemy in UnityEngine.Object.FindObjectsByType<RacingProject.Enemy.EnemyCarController>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(enemy.gameObject);
    float now = Time.time, st = now - t0;
    float v = kmhF();
    if (rb.transform.up.y < 0.3f) flipped = true;
    switch (test) {
        case 0: // разгон 0–100
            if (phase == 0) { place(new Vector3(-200f, 0f, -1100f), 0f); t0 = now; phase = 1; t60 = -1f; t100 = -1f; }
            else if (st > 0.6f) {
                keys(new[] { W });
                if (t60 < 0f && v >= 60f) t60 = st - 0.6f;
                if (t100 < 0f && v >= 100f) t100 = st - 0.6f;
                if (st > 16f || t100 > 0f) {
                    log.AppendLine("разгон: 0-60 " + t60.ToString("0.0") + " с, 0-100 " + (t100 > 0f ? t100.ToString("0.0") + " с" : "не набрал (" + v.ToString("0") + " км/ч)"));
                    test = 1; phase = 0;
                }
            }
            break;
        case 1: // торможение со 100
            if (phase == 0) { place(new Vector3(-100f, 0f, -1100f), 0f); t0 = now; phase = 1; }
            else if (phase == 1 && st > 0.6f) { launch(100f); keys(new[] { W }); phase = 2; mark = now; }
            else if (phase == 2 && now - mark > 0.3f) { p0 = rb.position; keys(new[] { S }); phase = 3; mark = now; yaw0 = rb.rotation.eulerAngles.y; maxPitch = 0f; }
            else if (phase == 3) {
                keys(new[] { S }); maxPitch = Mathf.Max(maxPitch, pitch());
                if (rb.linearVelocity.magnitude * 3.6f < 2f || now - mark > 8f) {
                    float dyaw = Mathf.Abs(Mathf.DeltaAngle(yaw0, rb.rotation.eulerAngles.y));
                    log.AppendLine("торможение 100→0: " + Vector3.Distance(p0, rb.position).ToString("0.0") + " м за " + (now - mark).ToString("0.0") + " с, увод " + dyaw.ToString("0") + "°, клевок " + maxPitch.ToString("0.0") + "°");
                    test = 2; phase = 0;
                }
            }
            break;
        case 2: // поворот на 60
            if (phase == 0) { place(new Vector3(0f, 0f, -1100f), 0f); t0 = now; phase = 1; maxRoll = 0f; maxLat = 0f; flipped = false; }
            else if (phase == 1 && st > 0.6f) { launch(60f); lastVel = rb.linearVelocity; phase = 2; mark = now; }
            else if (phase == 2) {
                keys(v < 60f ? new[] { W, D } : new[] { D });
                float dt = Time.deltaTime;
                if (dt > 0f && now - mark > 0.5f) {
                    Vector3 acc = (rb.linearVelocity - lastVel) / dt;
                    maxLat = Mathf.Max(maxLat, Mathf.Abs(Vector3.Dot(acc, rb.transform.right)));
                }
                lastVel = rb.linearVelocity;
                maxRoll = Mathf.Max(maxRoll, roll());
                if (now - mark > 5f) {
                    log.AppendLine("поворот 60 км/ч: крен до " + maxRoll.ToString("0.0") + "°, боковое до " + (maxLat / 9.81f).ToString("0.00") + " g, скорость в конце " + v.ToString("0") + (flipped ? ", ПЕРЕВЕРНУЛАСЬ" : ""));
                    test = 3; phase = 0;
                }
            }
            break;
        case 3: // слалом на 80
            if (phase == 0) { place(new Vector3(100f, 0f, -1100f), 0f); t0 = now; phase = 1; maxRoll = 0f; flipped = false; }
            else if (phase == 1 && st > 0.6f) { launch(80f); phase = 2; mark = now; }
            else if (phase == 2) {
                bool left = ((int)((now - mark) / 0.9f)) % 2 == 0;
                keys(v < 80f ? new[] { W, left ? A : D } : new[] { left ? A : D });
                maxRoll = Mathf.Max(maxRoll, roll());
                if (now - mark > 7f) {
                    float drift = Mathf.Abs(Vector3.Dot(rb.linearVelocity.normalized, rb.transform.right));
                    log.AppendLine("слалом 80 км/ч: крен до " + maxRoll.ToString("0.0") + "°, в конце скорость " + v.ToString("0") + ", снос " + drift.ToString("0.00") + (flipped ? ", ПЕРЕВЕРНУЛАСЬ" : ""));
                    test = 4; phase = 0;
                }
            }
            break;
        case 4: // ручник на 50: разворот
            if (phase == 0) { place(new Vector3(200f, 0f, -1100f), 0f); t0 = now; phase = 1; flipped = false; maxRoll = 0f; }
            else if (phase == 1 && st > 0.6f) { launch(50f); keys(new[] { W }); phase = 2; mark = now; }
            else if (phase == 2 && now - mark > 0.4f) { yaw0 = rb.rotation.eulerAngles.y; phase = 3; mark = now; }
            else if (phase == 3) {
                keys(now - mark < 1.2f ? new[] { Sp, D } : new UnityEngine.InputSystem.Key[0]);
                maxRoll = Mathf.Max(maxRoll, roll());
                if (now - mark > 3f) {
                    log.AppendLine("ручник 50 км/ч: разворот на " + Mathf.Abs(Mathf.DeltaAngle(yaw0, rb.rotation.eulerAngles.y)).ToString("0") + "°, крен до " + maxRoll.ToString("0.0") + "°, скорость " + (rb.linearVelocity.magnitude * 3.6f).ToString("0") + (flipped ? ", ПЕРЕВЕРНУЛАСЬ" : ""));
                    test = 5; phase = 0;
                }
            }
            break;
        case 5: // кочка на 70
            if (phase == 0) { place(new Vector3(200f, 0f, 220f), 0f); t0 = now; phase = 1; maxPitch = 0f; flipped = false; }
            else if (phase == 1 && st > 0.6f) { launch(70f); phase = 2; mark = now; }
            else if (phase == 2) {
                keys(new[] { W }); maxPitch = Mathf.Max(maxPitch, pitch());
                if (now - mark > 4f) {
                    log.AppendLine("кочка 15 см на 70 км/ч: тангаж до " + maxPitch.ToString("0.0") + "°" + (flipped ? ", ПЕРЕВЕРНУЛАСЬ" : ""));
                    test = 6;
                }
            }
            break;
        case 6:
            keys(new UnityEngine.InputSystem.Key[0]);
            UnityEngine.Object.Destroy(ground); UnityEngine.Object.Destroy(bump);
            SessionState.SetString("handling", log.ToString() + "DONE");
            EditorApplication.update -= cb;
            break;
    }
    if (now - tStart > 120f) { SessionState.SetString("handling", log.ToString() + "TIMEOUT"); EditorApplication.update -= cb; }
};
SessionState.SetString("handling", "running");
EditorApplication.update += cb;
return "run";
