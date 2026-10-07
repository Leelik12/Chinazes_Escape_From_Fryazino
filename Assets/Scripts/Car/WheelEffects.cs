using System.Collections.Generic;
using UnityEngine;
using RacingProject.Network;

namespace RacingProject.Car
{
    // Дым из-под колёс и следы шин при пробуксовке и заносе. Проскальзывание колёс знает только хост
    // (у него физика), напарнику оно приходит по сети байтом на колесо; точку касания с дорогой
    // каждый игрок находит лучом вниз от колеса
    public class WheelEffects : RoleSyncedBehaviour
    {
        [SerializeField] private WheelCollider[] wheels;
        [Tooltip("Шаблон дыма: копируется на каждое колесо")]
        [SerializeField] private ParticleSystem smokeTemplate;
        [SerializeField] private Material skidMaterial;

        [Header("Проскальзывание")]
        [Tooltip("Продольное проскальзывание, с которого начинается эффект (буксование, блокировка)")]
        [SerializeField] private float forwardSlipStart = 0.45f;
        [Tooltip("Боковое проскальзывание, с которого начинается эффект (занос)")]
        [SerializeField] private float sidewaysSlipStart = 0.25f;
        [Tooltip("Проскальзывание сверх порога, при котором эффект на полную")]
        [SerializeField] private float slipRange = 0.6f;

        [Header("Дым")]
        [SerializeField] private float maxSmokeRate = 45f;
        [Tooltip("Медленнее этого колесо не дымит и не оставляет следов, м/с: на месте проскальзывание шумит")]
        [SerializeField] private float minSpeed = 1.5f;

        [Header("Следы")]
        [Tooltip("Следы появляются от этой силы проскальзывания (0–1)")]
        [SerializeField, Range(0f, 1f)] private float skidStart = 0.25f;
        [SerializeField] private float skidWidth = 0.5f;
        [SerializeField] private float skidLifetime = 25f;
        [SerializeField] private LayerMask groundMask = ~0;

        private byte[] slip;
        // Сглаженная сила проскальзывания: у хоста она скачет от кадра к кадру, а по сети приходит рывками
        private float[] shown;
        private ParticleSystem[] smoke;
        private TrailRenderer[] skids;
        private Vector3[] lastPositions;

        private void Awake()
        {
            slip = new byte[wheels.Length];
            shown = new float[wheels.Length];
            smoke = new ParticleSystem[wheels.Length];
            skids = new TrailRenderer[wheels.Length];
            lastPositions = new Vector3[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                if (smokeTemplate == null || wheels[i] == null) continue;
                smoke[i] = Instantiate(smokeTemplate, smokeTemplate.transform.parent);
                smoke[i].name = "WheelSmoke " + wheels[i].name;
                smoke[i].gameObject.SetActive(true);
            }
            if (smokeTemplate != null)
                smokeTemplate.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (LocalPlayerRole.SimulatesPhysics)
                MeasureSlip();

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                shown[i] = Mathf.MoveTowards(shown[i], slip[i] / 255f, Time.deltaTime * 4f);
                float amount = shown[i];
                bool grounded = Physics.Raycast(wheels[i].transform.position, -transform.up, out RaycastHit ground,
                    wheels[i].radius * transform.lossyScale.y + wheels[i].suspensionDistance * transform.lossyScale.y + 0.3f,
                    groundMask, QueryTriggerInteraction.Ignore) && !ground.collider.transform.IsChildOf(transform);
                // Скорость колеса по его перемещению: одинаково у хоста и у напарника
                Vector3 position = wheels[i].transform.position;
                float speed = Time.deltaTime > 0f ? (position - lastPositions[i]).magnitude / Time.deltaTime : 0f;
                lastPositions[i] = position;
                if (!grounded || speed < minSpeed) amount = 0f;

                if (smoke[i] != null)
                {
                    if (grounded) smoke[i].transform.position = ground.point + ground.normal * 0.15f;
                    var emission = smoke[i].emission;
                    emission.rateOverTime = amount * maxSmokeRate;
                }
                // Гистерезис: след тянется, пока проскальзывание не упадёт вдвое ниже порога, иначе
                // на границе он рвался бы на сотни коротких кусков
                bool skidding = amount >= (skids[i] != null ? skidStart * 0.5f : skidStart);
                UpdateSkid(i, skidding, ground);
            }
        }

        // Хост: сила проскальзывания 0–1 по каждому колесу
        private void MeasureSlip()
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                float amount = 0f;
                if (wheels[i] != null && wheels[i].GetGroundHit(out WheelHit hit))
                {
                    float forward = Mathf.Abs(hit.forwardSlip) - forwardSlipStart;
                    float sideways = Mathf.Abs(hit.sidewaysSlip) - sidewaysSlipStart;
                    amount = Mathf.Clamp01(Mathf.Max(forward, sideways) / slipRange);
                }
                slip[i] = (byte)Mathf.RoundToInt(amount * 255f);
            }
        }

        // Каждый след — отдельный TrailRenderer: если выключать и снова включать один и тот же,
        // новая полоса соединялась бы со старой через всю дорогу
        private void UpdateSkid(int i, bool active, RaycastHit ground)
        {
            if (active)
            {
                if (skids[i] == null)
                    skids[i] = CreateSkid();
                skids[i].transform.SetPositionAndRotation(ground.point + ground.normal * 0.02f, Quaternion.LookRotation(ground.normal));
            }
            else if (skids[i] != null)
            {
                skids[i].emitting = false;
                // Пустой след (одна точка) убираем сразу
                Destroy(skids[i].gameObject, skids[i].positionCount < 2 ? 0f : skidLifetime);
                skids[i] = null;
            }
        }

        private TrailRenderer CreateSkid()
        {
            var go = new GameObject("SkidMark");
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = skidMaterial;
            trail.time = skidLifetime;
            trail.minVertexDistance = 0.3f;
            trail.widthMultiplier = skidWidth;
            // Плоско на дороге: TransformZ кладёт полосу перпендикулярно оси Z объекта, а Z смотрит вдоль нормали
            trail.alignment = LineAlignment.TransformZ;
            trail.textureMode = LineTextureMode.Tile;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            return trail;
        }

        public override void Serialize(SyncStream stream)
        {
            for (int i = 0; i < slip.Length; i++)
                stream.Serialize(ref slip[i]);
        }

        private void OnDestroy()
        {
            // Последние следы остаются на дороге и исчезают сами
            if (skids == null) return;
            foreach (TrailRenderer skid in skids)
                if (skid != null)
                {
                    skid.emitting = false;
                    Destroy(skid.gameObject, skidLifetime);
                }
        }
    }
}
