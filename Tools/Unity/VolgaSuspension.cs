// Подвеска, шины и центр масс машины игрока VolgaCar (SovietCity) — настройка под реальную физику вместо костылей стабилизатора.
// Машина в сцене увеличена в 2 раза: колесо радиусом 0,76 м, база 5,7 м, колея 3,2 м, масса 1500 кг, поэтому значения ниже
// заданы в локальных единицах и пересчитаны под этот масштаб. Пружины спереди и сзади разные под нагрузку на оси (~1,45 Гц),
// посадка (центр колеса в покое) та же, что была. Шины: пик сцепления ~1,1–1,25 (раньше 2–3), машина скользит раньше, чем
// опрокидывается. Центр масс ~1,05 м над землёй (раньше был под асфальтом). Проверка — Tools/Unity/HandlingTest.cs.
// Запуск: execute_code (MCP for Unity, C# 6) вне Play, затем сохранить сцену
var car = UnityEngine.Object.FindFirstObjectByType<RacingProject.Car.CarControllerSample>(FindObjectsInactive.Include);
var log = new System.Text.StringBuilder();
foreach (var w in car.GetComponentsInChildren<WheelCollider>(true)) {
    bool front = w.transform.localPosition.z > 0f;
    Undo.RecordObject(w.transform, "Volga suspension"); Undo.RecordObject(w, "Volga suspension");
    var lp = w.transform.localPosition; lp.y = -0.18f; w.transform.localPosition = lp;
    w.suspensionDistance = 0.2f;
    var s = w.suspensionSpring; s.spring = front ? 20000f : 29000f; s.damper = 2000f; s.targetPosition = 0.25f; w.suspensionSpring = s;
    w.forceAppPointDistance = 0.03f;
    var f = w.forwardFriction; f.extremumSlip = 0.4f; f.extremumValue = 1.25f; f.asymptoteSlip = 0.8f; f.asymptoteValue = 0.9f; f.stiffness = 1f; w.forwardFriction = f;
    var sf = w.sidewaysFriction; sf.extremumSlip = 0.25f; sf.extremumValue = 1.1f; sf.asymptoteSlip = 0.6f; sf.asymptoteValue = 0.8f; sf.stiffness = 1f; w.sidewaysFriction = sf;
    EditorUtility.SetDirty(w); EditorUtility.SetDirty(w.transform);
    log.Append(w.name + " ");
}
var so = new SerializedObject(car);
so.FindProperty("centerOfMass").vector3Value = new Vector3(0f, -0.15f, 0f);
so.FindProperty("handbrakeSideGrip").floatValue = 0.4f;
so.ApplyModifiedProperties();
var stab = new SerializedObject(car.GetComponent<RacingProject.Car.CarStabilizer>());
stab.FindProperty("antiRoll").floatValue = 0.15f;
stab.FindProperty("downforceAt100").floatValue = 1f;
stab.FindProperty("rollDamping").floatValue = 0.3f;
stab.FindProperty("airLeveling").floatValue = 2f;
stab.FindProperty("airAngularDamping").floatValue = 1.5f;
stab.ApplyModifiedProperties();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.gameObject.scene);
return "ok: " + log;
