// Фары машины игроков (VolgaCar/Fara): машина в масштабе 2, фары в 2 м над асфальтом, поэтому свет яркий (9000: он гаснет с квадратом расстояния, а маска приглушает низ)
// и наклонён на 6° вниз — пятно маски ложится на дорогу в 12–20 м. Свет выносится на 0,1 м впереди кузова (раньше стоял внутри бампера
// и кузов резал его собственной тенью), конус уже, с маской Assets/Content/Car/HeadlightCookie.png
// (python Tools/Textures/generate_headlight_cookie.py), тень — жёсткая и только у левой фары.
// На машину ставится CarHeadlights: ореолы стёкол, лучи в воздухе, мигание от ударов и разбитые фары.
// Не компилируется Unity (лежит вне Assets): текст выполняется в редакторе через execute_code (MCP for Unity, C# 6).
var car = GameObject.Find("VolgaCar");
var fara = car.transform.Find("Fara");
var fx = car.transform.Find("CarLightsFx");

string cookiePath = "Assets/Content/Car/HeadlightCookie.png";
AssetDatabase.ImportAsset(cookiePath);
var importer = (TextureImporter)AssetImporter.GetAtPath(cookiePath);
importer.textureType = TextureImporterType.Cookie;
importer.textureShape = TextureImporterShape.Texture2D;
importer.wrapMode = TextureWrapMode.Clamp;
importer.alphaSource = TextureImporterAlphaSource.FromGrayScale;
importer.mipmapEnabled = false;
importer.SaveAndReimport();
var cookie = AssetDatabase.LoadAssetAtPath<Texture2D>(cookiePath);

var lights = new Light[] { fara.Find("Spot Light (1)").GetComponent<Light>(), fara.Find("Spot Light").GetComponent<Light>() };
var glows = new Transform[] { fx.Find("HeadGlowL"), fx.Find("HeadGlowR") };
for (int i = 0; i < 2; i++) {
    var l = lights[i];
    // Позиция в координатах машины: на высоте и по бокам стёкол, на 0,1 м впереди переднего края (z 2,52)
    Vector3 glass = glows[i].localPosition;
    l.transform.position = car.transform.TransformPoint(new Vector3(glass.x, glass.y, 2.62f));
    l.transform.rotation = car.transform.rotation * Quaternion.Euler(6f, glass.x < 0 ? -1.5f : 1.5f, 0f);
    l.type = LightType.Spot;
    l.spotAngle = 74f;
    l.innerSpotAngle = 60f;
    l.intensity = 9000f;
    l.range = 90f;
    l.color = new Color(1f, 0.93f, 0.82f);
    l.cookie = cookie;
    l.shadows = i == 0 ? LightShadows.Hard : LightShadows.None;
    l.shadowStrength = 0.85f;
    l.shadowNearPlane = 0.5f;
    l.shadowBias = 0.05f;
    l.shadowNormalBias = 0.4f;
}

var hl = car.GetComponent<RacingProject.Car.CarHeadlights>();
if (hl == null) hl = car.AddComponent<RacingProject.Car.CarHeadlights>();
var so = new SerializedObject(hl);
var arr = so.FindProperty("headlights");
arr.arraySize = 2;
for (int i = 0; i < 2; i++) {
    var e = arr.GetArrayElementAtIndex(i);
    e.FindPropertyRelative("light").objectReferenceValue = lights[i];
    e.FindPropertyRelative("glow").objectReferenceValue = glows[i];
}
so.FindProperty("deformation").objectReferenceValue = car.GetComponent<RacingProject.Car.CarDeformation>();
so.FindProperty("beamMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Fx/Fx_Beam.mat");
so.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(car.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(car.scene);
return "headlights: " + lights[0].transform.localPosition + " / " + lights[1].transform.localPosition + ", cookie " + (cookie != null);
