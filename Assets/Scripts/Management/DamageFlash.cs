using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RacingProject.Management
{
    // Красная виньетка по краям экрана: вспыхивает при уроне машине и пульсирует, когда прочность
    // на исходе. Работает через свой глобальный Volume поверх основного профиля постобработки
    public class DamageFlash : MonoBehaviour
    {
        [SerializeField] private PlayerHealth carHealth;
        [SerializeField] private Color color = new Color(0.8f, 0f, 0f);
        [SerializeField, Range(0f, 1f)] private float intensity = 0.5f;
        [Tooltip("Урон, дающий полную вспышку")]
        [SerializeField] private float fullFlashDamage = 60f;
        [Tooltip("Скорость угасания вспышки, доля в секунду")]
        [SerializeField] private float fadeSpeed = 2.5f;
        [Tooltip("Ниже этой доли прочности виньетка пульсирует")]
        [SerializeField, Range(0f, 1f)] private float lowHealthBelow = 0.25f;

        // Выше приоритета основного Volume, чтобы перекрыть его виньетку
        private const float VolumePriority = 100f;

        private Volume volume;
        private VolumeProfile profile;
        private float flash;

        private void Awake()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.color.Override(color);
            vignette.intensity.Override(intensity);
            vignette.smoothness.Override(0.6f);

            var go = new GameObject("DamageFlashVolume");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = VolumePriority;
            volume.sharedProfile = profile;
            volume.weight = 0f;
        }

        private void OnEnable()
        {
            if (carHealth != null)
                carHealth.OnDamageTaken += OnDamageTaken;
        }

        private void OnDisable()
        {
            if (carHealth != null)
                carHealth.OnDamageTaken -= OnDamageTaken;
        }

        private void OnDestroy()
        {
            if (profile != null)
                Destroy(profile);
        }

        private void OnDamageTaken(int damage)
        {
            flash = Mathf.Min(1f, flash + damage / fullFlashDamage);
        }

        private void Update()
        {
            flash = Mathf.MoveTowards(flash, 0f, fadeSpeed * Time.deltaTime);

            float pulse = 0f;
            if (carHealth != null && !carHealth.IsDead && carHealth.MaxHealth > 0
                && (float)carHealth.CurrentHealth / carHealth.MaxHealth < lowHealthBelow)
            {
                pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * 6f);
            }

            volume.weight = Mathf.Max(flash, pulse);
        }
    }
}
