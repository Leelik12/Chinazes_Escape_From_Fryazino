using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RacingProject.Audio;
using RacingProject.Car;
using RacingProject.Management;

namespace RacingProject.Effects
{
    // Пост-эффекты по событиям поверх общего Global Volume: нитро (хроматическая аберрация, размытие в движении,
    // лёгкое искажение линзы), малое здоровье (мир блёкнет, виньетка краснеет) и вспышка от близкого взрыва.
    // У каждого эффекта свой временный Volume с более высоким приоритетом, сила — его вес. В VR всё слабее,
    // а размытия и искажения нет вовсе: в шлеме от них укачивает
    public class PostFxEvents : MonoBehaviour
    {
        [SerializeField] private CarNitro nitro;
        [SerializeField] private PlayerHealth health;
        [Tooltip("Ниже этой доли здоровья мир начинает блёкнуть")]
        [SerializeField, Range(0f, 1f)] private float lowHealth = 0.35f;
        [Tooltip("Во сколько раз слабее эффекты в VR")]
        [SerializeField, Range(0f, 1f)] private float vrScale = 0.35f;

        private Volume nitroVolume;
        private Volume healthVolume;
        private Volume flashVolume;
        private VolumeProfile[] profiles;
        private float nitroWeight;
        private float healthWeight;
        private float flash;
        private MotionBlur motionBlur;
        private LensDistortion lens;

        private void Awake()
        {
            VolumeProfile nitroProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var aberration = nitroProfile.Add<ChromaticAberration>(true);
            aberration.intensity.value = 0.65f;
            motionBlur = nitroProfile.Add<MotionBlur>(true);
            motionBlur.intensity.value = 0.45f;
            motionBlur.quality.value = MotionBlurQuality.Medium;
            lens = nitroProfile.Add<LensDistortion>(true);
            lens.intensity.value = -0.18f;
            lens.scale.value = 1f;

            VolumeProfile healthProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colors = healthProfile.Add<ColorAdjustments>(true);
            colors.saturation.value = -65f;
            colors.contrast.value = 12f;
            var vignette = healthProfile.Add<Vignette>(true);
            vignette.color.value = new Color(0.45f, 0f, 0f);
            vignette.intensity.value = 0.45f;
            vignette.smoothness.value = 0.6f;

            VolumeProfile flashProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var flashColors = flashProfile.Add<ColorAdjustments>(true);
            flashColors.postExposure.value = 2.2f;
            flashColors.saturation.value = -40f;
            var flashBloom = flashProfile.Add<Bloom>(true);
            flashBloom.intensity.value = 4f;

            profiles = new[] { nitroProfile, healthProfile, flashProfile };
            nitroVolume = CreateVolume("NitroFx", nitroProfile, 50f);
            healthVolume = CreateVolume("LowHealthFx", healthProfile, 51f);
            flashVolume = CreateVolume("ExplosionFlashFx", flashProfile, 52f);
        }

        private Volume CreateVolume(string name, VolumeProfile profile, float priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
            volume.weight = 0f;
            return volume;
        }

        private void OnEnable() => ListenerEffects.NearExplosion += OnNearExplosion;
        private void OnDisable() => ListenerEffects.NearExplosion -= OnNearExplosion;

        private void OnNearExplosion(float strength)
        {
            flash = Mathf.Max(flash, strength);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool vr = ViewModeService.IsVR;
            float scale = vr ? vrScale : 1f;
            motionBlur.active = !vr;
            lens.active = !vr;

            bool boosting = nitro != null && nitro.Boosting;
            nitroWeight = Mathf.MoveTowards(nitroWeight, boosting ? 1f : 0f, dt * (boosting ? 3f : 1.5f));

            float danger = 0f;
            if (health != null && health.MaxHealth > 0 && !health.IsDead && lowHealth > 0f)
                danger = Mathf.Clamp01(1f - (float)health.CurrentHealth / health.MaxHealth / lowHealth);
            healthWeight = Mathf.MoveTowards(healthWeight, danger, dt * 0.8f);

            flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);

            nitroVolume.weight = nitroWeight * scale;
            healthVolume.weight = healthWeight * scale;
            // Вспышка гаснет быстрее, чем нарастает глухота: короткий удар по глазам
            flashVolume.weight = flash * flash * scale;
        }

        private void OnDestroy()
        {
            if (profiles == null) return;
            foreach (VolumeProfile profile in profiles)
                if (profile != null) Destroy(profile);
        }
    }
}
