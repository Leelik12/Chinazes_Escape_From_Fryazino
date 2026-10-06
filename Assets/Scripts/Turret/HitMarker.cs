using UnityEngine;

namespace RacingProject.Turret
{
    // Отметка попадания по врагу: крестик в точке попадания, повёрнутый к камере, с постоянным
    // видимым размером. Коротко «выстреливает» и гаснет; звук — AudioSource на этом же объекте.
    // Видит только стрелок: создаёт её его выстрел
    public class HitMarker : MonoBehaviour
    {
        [SerializeField] private Renderer markerRenderer;
        [SerializeField] private float lifetime = 0.3f;
        [Tooltip("Видимый размер, градусы")]
        [SerializeField] private float angularSize = 3f;
        [Tooltip("Во сколько раз крупнее в первый момент")]
        [SerializeField] private float popScale = 1.6f;
        [Tooltip("Сдвиг к камере, чтобы крестик не утонул в корпусе врага, м")]
        [SerializeField] private float towardCamera = 0.6f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock block;
        private Color baseColor = Color.white;
        private float startTime;

        private void Start()
        {
            startTime = Time.time;
            block = new MaterialPropertyBlock();
            if (markerRenderer != null && markerRenderer.sharedMaterial != null && markerRenderer.sharedMaterial.HasProperty(ColorId))
                baseColor = markerRenderer.sharedMaterial.GetColor(ColorId);

            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCamera = cam.transform.position - transform.position;
                transform.position += toCamera.normalized * Mathf.Min(towardCamera, toCamera.magnitude * 0.5f);
            }
            Destroy(gameObject, lifetime);
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float t = Mathf.Clamp01((Time.time - startTime) / lifetime);
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            float size = 2f * distance * Mathf.Tan(angularSize * 0.5f * Mathf.Deg2Rad);

            transform.rotation = cam.transform.rotation;
            transform.localScale = Vector3.one * size * Mathf.Lerp(popScale, 1f, Mathf.Clamp01(t * 4f));

            if (markerRenderer != null)
            {
                Color color = baseColor;
                color.a *= 1f - Mathf.Clamp01((t - 0.4f) / 0.6f);
                block.SetColor(ColorId, color);
                markerRenderer.SetPropertyBlock(block);
            }
        }
    }
}
