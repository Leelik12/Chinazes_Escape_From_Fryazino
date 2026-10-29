using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
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

        [Header("Звук шин")]
        [Tooltip("Визг при проскальзывании (петля)")]
        [SerializeField] private AudioClip skidClip;
        [Tooltip("Шорох качения по асфальту, гравию и траве/грунту (петли)")]
        [SerializeField] private AudioClip roadClip;
        [SerializeField] private AudioClip gravelClip;
        [SerializeField] private AudioClip grassClip;
        [SerializeField] private AudioMixerGroup audioOutput;
        [SerializeField, Range(0f, 1f)] private float skidVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float rollVolume = 0.45f;
        [Tooltip("Скорость, на которой шорох качения звучит в полную силу, м/с")]
        [SerializeField] private float rollFullSpeed = 25f;

        [Header("Пыль")]
        [Tooltip("Шаблон пыли из-под колёс на гравии и траве: копируется на каждое колесо")]
        [SerializeField] private ParticleSystem dustTemplate;
        [SerializeField] private float maxDustRate = 30f;
        [Tooltip("Скорость, на которой пыль летит в полную силу, м/с")]
        [SerializeField] private float dustFullSpeed = 20f;

        // Покрытие под колесом: от него зависят шорох и пыль
        public enum Surface { None, Asphalt, Gravel, Grass }

        private byte[] slip;
        // Сглаженная сила проскальзывания: у хоста она скачет от кадра к кадру, а по сети приходит рывками
        private float[] shown;
        private ParticleSystem[] smoke;
        private TrailRenderer[] skids;
        private Vector3[] lastPositions;
        private ParticleSystem[] dust;
        private AudioSource skidSource;
        private AudioSource roadSource;
        private AudioSource gravelSource;
        private AudioSource grassSource;
        // Доли колёс на каждом покрытии и средняя скорость колёс за кадр: из них громкость петель
        private float roadShare, gravelShare, grassShare, wheelSpeed, maxSlip;

        // Сырость дороги 0–1 (дождь): пыль сменяется брызгами, их задаёт погода
        public static float Wetness { get; set; }

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

            dust = new ParticleSystem[wheels.Length];
            for (int i = 0; i < wheels.Length; i++)
            {
                if (dustTemplate == null || wheels[i] == null) continue;
                dust[i] = Instantiate(dustTemplate, dustTemplate.transform.parent);
                dust[i].name = "WheelDust " + wheels[i].name;
                dust[i].gameObject.SetActive(true);
            }
            if (dustTemplate != null)
                dustTemplate.gameObject.SetActive(false);

            skidSource = CreateLoop("TyreSkid", skidClip);
            roadSource = CreateLoop("TyreRoad", roadClip);
            gravelSource = CreateLoop("TyreGravel", gravelClip);
            grassSource = CreateLoop("TyreGrass", grassClip);
        }

        private AudioSource CreateLoop(string name, AudioClip clip)
        {
            if (clip == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 0.6f;
            source.minDistance = 5f;
            source.maxDistance = 90f;
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = audioOutput;
            source.time = Random.Range(0f, clip.length);
            source.Play();
            return source;
        }

        // Покрытие по коллайдеру: террейн — трава и грунт, дорожный материал с «Gravel» — гравий, остальное — асфальт
        public static Surface SurfaceOf(Collider collider)
        {
            if (collider == null) return Surface.None;
            if (collider is TerrainCollider) return Surface.Grass;
            PhysicsMaterial material = collider.sharedMaterial;
            if (material != null && material.name.Contains("Gravel")) return Surface.Gravel;
            return Surface.Asphalt;
        }

        private void Update()
        {
            if (LocalPlayerRole.SimulatesPhysics)
                MeasureSlip();

            int road = 0, gravel = 0, grass = 0, counted = 0;
            float speedSum = 0f, slipMax = 0f;
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

                Surface surface = grounded ? SurfaceOf(ground.collider) : Surface.None;
                counted++;
                speedSum += grounded ? speed : 0f;
                slipMax = Mathf.Max(slipMax, surface == Surface.Asphalt ? amount : amount * 0.35f);
                if (surface == Surface.Asphalt) road++;
                else if (surface == Surface.Gravel) gravel++;
                else if (surface == Surface.Grass) grass++;
                UpdateDust(i, surface, grounded ? ground : default, speed, amount);

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

            if (counted > 0)
            {
                roadShare = road / (float)counted;
                gravelShare = gravel / (float)counted;
                grassShare = grass / (float)counted;
                wheelSpeed = speedSum / counted;
            }
            maxSlip = slipMax;
            UpdateAudio();
        }

        // Пыль на гравии и траве, по скорости и пробуксовке; на мокрой дороге — брызги из-под всех колёс
        private void UpdateDust(int i, Surface surface, RaycastHit ground, float speed, float amount)
        {
            if (dust[i] == null) return;
            float loose = surface == Surface.Gravel ? 1f : surface == Surface.Grass ? 0.6f : 0f;
            float wet = surface == Surface.Asphalt || surface == Surface.Gravel ? Wetness : 0f;
            float strength = Mathf.Max(loose * (1f - Wetness * 0.8f), wet) * Mathf.Clamp01(speed / dustFullSpeed + amount);
            if (surface != Surface.None) dust[i].transform.position = ground.point + ground.normal * 0.1f;
            var emission = dust[i].emission;
            emission.rateOverTime = speed < minSpeed ? 0f : strength * maxDustRate;
            // Сухая пыль бурая, брызги серо-прозрачные
            var main = dust[i].main;
            main.startColor = Color.Lerp(new Color(0.55f, 0.48f, 0.38f, 0.35f), new Color(0.75f, 0.8f, 0.85f, 0.3f), Wetness);
        }

        // Громкость петель: шорох — по скорости и доле колёс на покрытии, визг — по сильнейшему проскальзыванию
        private void UpdateAudio()
        {
            float roll = Mathf.Clamp01(wheelSpeed / rollFullSpeed);
            float rollCurve = Mathf.Sqrt(roll);
            float rollPitch = Mathf.Lerp(0.75f, 1.25f, roll);
            SetLoop(roadSource, rollVolume * rollCurve * roadShare * Mathf.Lerp(1f, 1.3f, Wetness), rollPitch);
            SetLoop(gravelSource, rollVolume * 1.2f * rollCurve * gravelShare, rollPitch);
            SetLoop(grassSource, rollVolume * rollCurve * grassShare, rollPitch);
            // На мокром визг тише и ниже
            float skid = maxSlip * (1f - Wetness * 0.6f);
            SetLoop(skidSource, skidVolume * Mathf.SmoothStep(0f, 1f, skid), Mathf.Lerp(0.9f, 1.08f, skid) * (1f - Wetness * 0.08f));
        }

        private static void SetLoop(AudioSource source, float volume, float pitch)
        {
            if (source == null) return;
            source.volume = Mathf.MoveTowards(source.volume, volume, Time.deltaTime * 3f);
            source.pitch = pitch;
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
