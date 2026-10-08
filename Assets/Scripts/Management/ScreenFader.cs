using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Затемнение «в чёрное» перед текущей камерой (Camera.main): холст в мировых координатах у самого объектива,
    // поверх всего (ZTest Always), поэтому работает и в VR, и на мониторе. Холст не дочерний камере, а повторяет
    // её позу каждый кадр: камера меню выключается вместе с меню, а затемнение должно дожить до рига игрока
    // и сняться уже в машине
    public class ScreenFader : MonoBehaviour
    {
        private const float Distance = 0.06f;
        private const int OverlayQueue = 5000;
        private const int ZTestAlways = 8;

        private static ScreenFader instance;

        private Canvas canvas;
        private CanvasGroup group;
        private float from, to, start, duration;

        public static float Alpha => instance != null ? instance.group.alpha : 0f;

        // Плавно довести затемнение до alpha (0 — прозрачно, 1 — чёрный экран) за seconds
        public static void FadeTo(float alpha, float seconds)
        {
            if (instance == null)
                instance = new GameObject("ScreenFader").AddComponent<ScreenFader>();
            instance.from = instance.group.alpha;
            instance.to = alpha;
            instance.start = Time.unscaledTime;
            instance.duration = Mathf.Max(0.01f, seconds);
            instance.enabled = true;
        }

        private void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = short.MaxValue;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            // Квадрат 1×1 м на 6 см от камеры закрывает любое поле зрения, в том числе шлема
            var rect = (RectTransform)transform;
            rect.sizeDelta = new Vector2(1f, 1f);

            var black = new GameObject("Black", typeof(RectTransform), typeof(Image));
            black.transform.SetParent(transform, false);
            var blackRect = (RectTransform)black.transform;
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = Vector2.zero;
            blackRect.offsetMax = Vector2.zero;
            var image = black.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            var material = new Material(Canvas.GetDefaultCanvasMaterial());
            material.SetInt("unity_GUIZTestMode", ZTestAlways);
            material.renderQueue = OverlayQueue;
            image.material = material;
        }

        private void OnEnable()
        {
            Application.onBeforeRender += FollowCamera;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= FollowCamera;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // Шлем обновляет позу камеры прямо перед кадром, поэтому холст подтягивается и здесь
        private void FollowCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform t = cam.transform;
            transform.SetPositionAndRotation(t.position + t.forward * Mathf.Max(Distance, cam.nearClipPlane * 1.5f), t.rotation);
        }

        private void LateUpdate()
        {
            FollowCamera();
            float k = Mathf.Clamp01((Time.unscaledTime - start) / duration);
            group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
            canvas.enabled = group.alpha > 0.001f;
            if (k >= 1f && to <= 0f)
                enabled = false;
        }
    }
}
