using UnityEngine;
using UnityEngine.Rendering;
using RacingProject.Effects;

namespace RacingProject.Car
{
    // Фары машины игроков: у каждой — свет с маской (cookie), светящееся стекло и слабый луч в воздухе
    // (в дождь луч и ореол стекла заметнее). Сильный удар рядом с фарой заставляет её мигнуть, а если
    // от кузова оторвалось стекло фары — она гаснет до конца раунда. Отрыв деталей приходит из CarDeformation
    // у каждого игрока, поэтому разбитые фары у обоих одинаковые. Пули разбивают фары через CarDamageState,
    // ремкомплект их чинит
    public class CarHeadlights : MonoBehaviour
    {
        [System.Serializable]
        public class Headlight
        {
            public Light light;
            [Tooltip("Ореол стекла фары")]
            public Transform glow;
            [HideInInspector] public float intensity;
            [HideInInspector] public Vector3 glowScale;
            [HideInInspector] public Renderer beam;
            [HideInInspector] public bool broken;
            [HideInInspector] public float flicker;
        }

        [SerializeField] private Headlight[] headlights = new Headlight[0];
        [SerializeField] private CarDeformation deformation;

        [Header("Луч в воздухе")]
        [SerializeField] private Material beamMaterial;
        [SerializeField] private float beamLength = 18f;
        [SerializeField] private float beamRadius = 3.5f;
        [SerializeField, Range(0f, 1f)] private float beamAlpha = 0.18f;
        [Tooltip("Яркость луча в сухую погоду и в дождь")]
        [SerializeField] private float dryBeam = 0.25f;
        [SerializeField] private float wetBeam = 1f;
        [Tooltip("На сколько крупнее ореол стекла в дождь")]
        [SerializeField] private float wetGlowGrow = 0.5f;
        [Tooltip("С какого расстояния от камеры луч начинает проявляться, м (чтобы не слепил водителя и стрелка)")]
        [SerializeField] private float beamNearFade = 5f;

        [Header("Повреждения")]
        [Tooltip("Оторванное стекло ближе этого к фаре разбивает её, м")]
        [SerializeField] private float breakRadius = 1.3f;
        [Tooltip("Удар ближе этого к фаре заставляет её мигнуть, м")]
        [SerializeField] private float flickerRadius = 3f;
        [Tooltip("Скорость удара, с которой фара мигает, м/с")]
        [SerializeField] private float flickerSpeed = 6f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int NearFadeId = Shader.PropertyToID("_NearFade");
        private MaterialPropertyBlock block;
        private Color beamColor = Color.white;

        public int BrokenCount
        {
            get
            {
                int count = 0;
                foreach (Headlight h in headlights) if (h.broken) count++;
                return count;
            }
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (beamMaterial != null && beamMaterial.HasProperty(ColorId)) beamColor = beamMaterial.GetColor(ColorId);
            foreach (Headlight h in headlights)
            {
                if (h.light == null) continue;
                h.intensity = h.light.intensity;
                if (h.glow != null) h.glowScale = h.glow.localScale;
                if (beamMaterial != null) h.beam = CreateBeam(h.light.transform);
            }
        }

        private Renderer CreateBeam(Transform parent)
        {
            var go = new GameObject("Beam", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            // Конус строится в мировых метрах, а машина отмасштабирована
            go.transform.localScale = Vector3.one / Mathf.Max(0.01f, parent.lossyScale.x);
            go.GetComponent<MeshFilter>().sharedMesh = Searchlight.BuildCone(beamLength, beamRadius, beamAlpha);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = beamMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private void OnEnable()
        {
            if (deformation == null) deformation = GetComponent<CarDeformation>();
            if (deformation == null) return;
            deformation.PartsDetached += OnPartsDetached;
            deformation.Impacted += OnImpacted;
        }

        private void OnDisable()
        {
            if (deformation == null) return;
            deformation.PartsDetached -= OnPartsDetached;
            deformation.Impacted -= OnImpacted;
        }

        private void OnPartsDetached(Vector3 point, int count, bool glass)
        {
            if (!glass) return;
            foreach (Headlight h in headlights)
                if (h.light != null && !h.broken && Vector3.Distance(GlassPosition(h), point) < breakRadius)
                {
                    h.broken = true;
                    h.flicker = 0.6f;
                }
        }

        private void OnImpacted(Vector3 point, float speed)
        {
            if (speed < flickerSpeed) return;
            foreach (Headlight h in headlights)
                if (h.light != null && Vector3.Distance(GlassPosition(h), point) < flickerRadius)
                    h.flicker = Mathf.Max(h.flicker, Mathf.Lerp(0.25f, 0.8f, Mathf.InverseLerp(flickerSpeed, 20f, speed)));
        }

        private static Vector3 GlassPosition(Headlight h) => h.glow != null ? h.glow.position : h.light.transform.position;

        // Для CarDamageState: фары по порядку, их стекло в осях машины, разбить пулей и починить ремкомплектом
        public int Count => headlights.Length;

        public Vector3 LocalGlassPosition(int index)
        {
            Headlight h = headlights[index];
            return h.light != null ? transform.InverseTransformPoint(GlassPosition(h)) : Vector3.zero;
        }

        public void Break(int index)
        {
            if (index < 0 || index >= headlights.Length || headlights[index].broken) return;
            headlights[index].broken = true;
            headlights[index].flicker = 0.4f;
        }

        public void RepairAll()
        {
            foreach (Headlight h in headlights)
                h.broken = false;
        }

        private void Update()
        {
            float wet = WheelEffects.Wetness;
            float beam = Mathf.Lerp(dryBeam, wetBeam, wet);
            foreach (Headlight h in headlights)
            {
                if (h.light == null) continue;
                // Мигание — неровное, как у контакта, который то есть, то нет
                float on = h.broken ? 0f : 1f;
                if (h.flicker > 0f)
                {
                    h.flicker -= Time.deltaTime;
                    float noise = Mathf.PerlinNoise(Time.time * 25f, h.intensity);
                    on = noise > 0.45f ? (h.broken ? 0.6f : 1f) : 0.05f;
                }
                h.light.intensity = h.intensity * on;
                h.light.enabled = on > 0.01f;
                if (h.glow != null)
                {
                    h.glow.gameObject.SetActive(on > 0.01f);
                    h.glow.localScale = h.glowScale * (1f + wetGlowGrow * wet);
                }
                if (h.beam != null)
                {
                    h.beam.enabled = on > 0.01f;
                    block.SetColor(ColorId, beamColor * (beam * on));
                    block.SetFloat(NearFadeId, beamNearFade);
                    h.beam.SetPropertyBlock(block);
                }
            }
        }
    }
}
