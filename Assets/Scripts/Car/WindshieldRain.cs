using RacingProject.Effects;
using UnityEngine;

namespace RacingProject.Car
{
    // Дождь на лобовом стекле: капли копятся слоем поверх стекла (две половины — у каждого дворника своя),
    // дворники ходят, пока идёт дождь (в грозу чаще), и стирают свою половину, проходя её середину.
    // Чисто косметика: у каждого игрока своя, погоду берёт из WeatherController.Current
    public class WindshieldRain : MonoBehaviour
    {
        [Tooltip("Лобовое стекло: квадрат 1×1 в его осях — всё стекло (Windshield, его же используют трещины)")]
        [SerializeField] private Transform windshield;
        [SerializeField] private Material dropsMaterial;
        [SerializeField] private Material wiperMaterial;
        [Tooltip("За сколько секунд стекло заливает каплями в дождь и в грозу")]
        [SerializeField] private float fillTime = 3f;
        [SerializeField] private float stormFillTime = 1.2f;
        [Tooltip("Ход дворника туда и обратно, с; в грозу — быстрее")]
        [SerializeField] private float wipePeriod = 1.6f;
        [SerializeField] private float stormWipePeriod = 1.1f;
        [Tooltip("Пауза между ходами в обычный дождь, с")]
        [SerializeField] private float wipePause = 1.2f;
        [Tooltip("Размах дворника, градусы")]
        [SerializeField] private float sweep = 95f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");

        // Половины стекла; у каждой два квада — снаружи и изнутри (освещение Lit с обратной стороны тёмное)
        private readonly Renderer[,] halves = new Renderer[2, 2];
        private readonly float[] cover = new float[2];
        private readonly Transform[] wipers = new Transform[2];
        private readonly float[] lastAngle = new float[2];
        private MaterialPropertyBlock block;
        private float phase = 1f;
        private float pause;

        private void Start()
        {
            if (windshield == null || dropsMaterial == null) { enabled = false; return; }
            block = new MaterialPropertyBlock();
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            for (int i = 0; i < 2; i++)
            {
                for (int side = 0; side < 2; side++)
                {
                    var go = new GameObject("RainDrops " + i + (side == 0 ? " Out" : " In"), typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(windshield, false);
                    // Чуть меньше прямоугольника стекла: у реального стекла скруглены углы и низ уходит под капот
                    go.transform.localPosition = new Vector3(i == 0 ? -0.24f : 0.24f, 0.03f, 0.012f);
                    go.transform.localRotation = Quaternion.Euler(0f, side == 0 ? 180f : 0f, 0f);
                    go.transform.localScale = new Vector3(side == 0 ? 0.48f : -0.48f, 0.9f, 1f);
                    go.GetComponent<MeshFilter>().sharedMesh = quad;
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = dropsMaterial;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.enabled = false;
                    halves[i, side] = renderer;
                }
                wipers[i] = CreateWiper(i);
            }
        }

        // Плоскость стекла в осях машины: нормаль, «вправо» (к правому борту) и «вверх» по стеклу
        private Vector3 glassNormal, glassRight, glassUp;
        private float bladeLength;

        // Дворник: тёмная щётка от оси у нижнего края стекла; в покое лежит вправо вдоль края
        private Transform CreateWiper(int index)
        {
            Transform car = windshield.parent;
            if (index == 0)
            {
                glassNormal = car.InverseTransformDirection(windshield.forward).normalized;
                glassUp = Vector3.ProjectOnPlane(Vector3.up, glassNormal).normalized;
                glassRight = Vector3.Cross(glassUp, glassNormal).normalized;
                if (glassRight.x < 0f) glassRight = -glassRight;
            }
            Vector3 size = windshield.lossyScale;
            Vector3 carScale = car.lossyScale;
            // Размеры стекла в осях машины (масштаб машины учитывается отдельно)
            float width = size.x / Mathf.Max(0.01f, carScale.x);
            float height = size.y / Mathf.Max(0.01f, carScale.y);
            bladeLength = Mathf.Min(width * 0.42f, height * 0.85f);
            Vector3 bottom = car.InverseTransformPoint(windshield.position) - glassUp * height * 0.44f + glassNormal * 0.015f;
            var pivot = new GameObject("Wiper " + index).transform;
            pivot.SetParent(car, false);
            pivot.localPosition = bottom + glassRight * width * (index == 0 ? -0.38f : 0.04f);
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(blade.GetComponent<Collider>());
            blade.name = "Blade";
            blade.transform.SetParent(pivot, false);
            blade.transform.localPosition = new Vector3(0f, bladeLength * 0.5f, 0f);
            blade.transform.localScale = new Vector3(0.012f, bladeLength, 0.012f);
            var renderer = blade.GetComponent<MeshRenderer>();
            if (wiperMaterial != null) renderer.sharedMaterial = wiperMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Aim(pivot, RestAngle);
            return pivot;
        }

        // Щётка под углом angle от нижнего края стекла (0° — вправо вдоль края, 90° — вверх)
        private void Aim(Transform pivot, float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector3 direction = glassRight * Mathf.Cos(rad) + glassUp * Mathf.Sin(rad);
            pivot.localRotation = Quaternion.LookRotation(glassNormal, direction);
        }

        // В покое щётка чуть приподнята над краем
        private const float RestAngle = 6f;

        private void Update()
        {
            int weather = WeatherController.Current;
            float dt = Time.deltaTime;
            float fill = weather == 2 ? stormFillTime : fillTime;

            // Ход дворников: полный цикл туда-обратно, в обычный дождь — с паузой
            bool running = weather > 0 || phase < 1f;
            if (running)
            {
                if (pause > 0f) pause -= dt;
                else
                {
                    phase += dt / (weather == 2 ? stormWipePeriod : wipePeriod);
                    if (phase >= 1f)
                    {
                        phase = weather > 0 ? 0f : 1f;
                        if (weather == 1) pause = wipePause;
                    }
                }
            }
            float swing = Mathf.Sin(Mathf.Clamp01(phase) * Mathf.PI); // 0 → 1 → 0
            for (int i = 0; i < 2; i++)
            {
                float angle = RestAngle + swing * sweep;
                if (wipers[i] != null) Aim(wipers[i], angle);
                // Щётка прошла середину своей половины — половина стёрта
                float mid = RestAngle + sweep * 0.5f;
                if ((lastAngle[i] - mid) * (angle - mid) < 0f) cover[i] = 0.05f;
                lastAngle[i] = angle;

                cover[i] = weather > 0 ? Mathf.Min(1f, cover[i] + dt / fill) : Mathf.Max(0f, cover[i] - dt / 20f);
                bool visible = cover[i] > 0.01f;
                Color color = dropsMaterial.GetColor(BaseColorId);
                color.a *= cover[i];
                block.SetColor(BaseColorId, color);
                // Половины берут разные места плитки, чтобы рисунок не повторялся
                block.SetVector(BaseMapSt, new Vector4(1.2f, 1.5f, i * 0.37f, i * 0.21f));
                for (int side = 0; side < 2; side++)
                {
                    Renderer half = halves[i, side];
                    if (half == null) continue;
                    half.enabled = visible;
                    if (visible) half.SetPropertyBlock(block);
                }
            }
        }
    }
}
