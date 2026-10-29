using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RacingProject.Management
{
    // Красная виньетка по краям экрана: вспыхивает при уроне машине и пульсирует, когда прочность
    // на исходе. Работает через свой глобальный Volume поверх основного профиля постобработки.
    // Если известно, откуда стреляли, центр виньетки уходит в другую сторону: краснеет край со стороны
    // попадания (сзади — нижний край). Так сторона урона видна и на мониторе, и в шлеме VR
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
        [Tooltip("Насколько сдвигается центр виньетки от стороны попадания, доля экрана")]
        [SerializeField, Range(0f, 0.5f)] private float directionShift = 0.3f;
        [Tooltip("Сколько секунд вспышка помнит сторону попадания")]
        [SerializeField] private float directionMemory = 1.2f;

        // Выше приоритета основного Volume, чтобы перекрыть его виньетку
        private const float VolumePriority = 100f;

        private Volume volume;
        private VolumeProfile profile;
        private float flash;
        private Vignette vignette;
        private Vector3 hitSource;
        private float hitTime = float.NegativeInfinity;
        private Vector2 center = new Vector2(0.5f, 0.5f);

        private void Awake()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
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
            {
                carHealth.OnDamageTaken += OnDamageTaken;
                carHealth.DamageFrom += OnDamageFrom;
            }
        }

        private void OnDisable()
        {
            if (carHealth != null)
            {
                carHealth.OnDamageTaken -= OnDamageTaken;
                carHealth.DamageFrom -= OnDamageFrom;
            }
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

        private void OnDamageFrom(Vector3 source)
        {
            hitSource = source;
            hitTime = Time.time;
        }

        private void Update()
        {
            flash = Mathf.MoveTowards(flash, 0f, fadeSpeed * Time.deltaTime);
            UpdateCenter();

            float pulse = 0f;
            if (carHealth != null && !carHealth.IsDead && carHealth.MaxHealth > 0
                && (float)carHealth.CurrentHealth / carHealth.MaxHealth < lowHealthBelow)
            {
                pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * 6f);
            }

            volume.weight = Mathf.Max(flash, pulse);
        }

        // Сторона попадания относительно взгляда камеры: слева — краснеет левый край, сзади — нижний
        private void UpdateCenter()
        {
            Vector2 target = new Vector2(0.5f, 0.5f);
            Camera cam = Camera.main;
            if (cam != null && Time.time - hitTime < directionMemory)
            {
                Vector3 local = cam.transform.InverseTransformDirection(hitSource - cam.transform.position);
                local.y = 0f;
                if (local.sqrMagnitude > 0.01f)
                {
                    local.Normalize();
                    // Центр уходит от стороны попадания: попадание слева — центр вправо
                    target.x -= local.x * directionShift;
                    target.y += (local.z < 0f ? -local.z : -local.z * 0.5f) * directionShift;
                }
            }
            center = Vector2.Lerp(center, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            vignette.center.Override(center);
        }
    }
}
