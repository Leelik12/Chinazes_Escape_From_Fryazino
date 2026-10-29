// Время суток за забег (Assets/Scripts/Effects/TimeOfDay.cs): компонент на General/Weather рядом с WeatherController,
// ссылки на солнце сцены, EnemyManager и WeatherController. Свет сцены как есть — середина (сумерки).
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var weather = UnityEngine.Object.FindFirstObjectByType<RacingProject.Effects.WeatherController>();
if (weather == null) throw new System.Exception("no WeatherController");
var tod = weather.GetComponent<RacingProject.Effects.TimeOfDay>();
if (tod == null) tod = weather.gameObject.AddComponent<RacingProject.Effects.TimeOfDay>();
var so = new SerializedObject(tod);
so.FindProperty("sun").objectReferenceValue = RenderSettings.sun;
so.FindProperty("enemyManager").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RacingProject.Enemy.EnemyManager>();
so.FindProperty("weather").objectReferenceValue = weather;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(weather.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(weather.gameObject.scene);
return "time of day on " + weather.name;
