using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RacingProject.Enemy;

namespace RacingProject.Management
{
    // Экран гибели: при уничтожении машины перед камерой игрока затемняется экран с итогом раунда
    // и рекордом. Табличка крепится к активной камере, поэтому видна и в VR, и на мониторе.
    // Сцену перезагружает RoomController, вместе с ней исчезает и табличка
    public class DeathScreen : MonoBehaviour
    {
        [SerializeField] private PlayerHealth carHealth;
        [SerializeField] private EnemyManager enemyManager;
        [Tooltip("Шрифт с кириллицей")]
        [SerializeField] private TMP_FontAsset font;

        [Header("Вид")]
        [Tooltip("Расстояние от камеры, м")]
        [SerializeField] private float distance = 0.6f;
        [SerializeField] private float fadeDuration = 0.8f;
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color recordColor = new Color(1f, 0.8f, 0.2f);

        // Размер холста в условных пикселях и их размер в метрах на расстоянии distance
        private const float CanvasHeight = 1000f;
        private const float PixelsPerMeter = 1000f;
        // ZTest Always: табличку не перекрывают ни салон, ни руль
        private const int ZTestAlways = 8;
        private const int OverlayQueue = 4000;

        private bool shown;

        private void OnEnable()
        {
            if (carHealth != null)
                carHealth.OnDeath += Show;
        }

        private void OnDisable()
        {
            if (carHealth != null)
                carHealth.OnDeath -= Show;
        }

        private void Show()
        {
            if (shown) return;
            shown = true;

            Camera cam = Camera.main;
            if (cam == null) return;

            int kills = enemyManager != null ? enemyManager.Kills : 0;
            int wave = enemyManager != null ? enemyManager.Wave : 0;
            bool newRecord = GameRecords.SubmitKills(kills);

            CanvasGroup group = BuildOverlay(cam, wave, kills, newRecord);
            StartCoroutine(FadeIn(group));
        }

        private CanvasGroup BuildOverlay(Camera cam, int wave, int kills, bool newRecord)
        {
            var root = new GameObject("DeathScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, distance);
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one / PixelsPerMeter;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = short.MaxValue;

            // Фон с запасом шире поля зрения: в VR оно больше, чем на мониторе
            float viewHeight = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * PixelsPerMeter;
            float size = Mathf.Max(viewHeight, CanvasHeight) * 4f;
            ((RectTransform)root.transform).sizeDelta = new Vector2(size, size);

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            Stretch((RectTransform)background.transform);
            var image = background.GetComponent<Image>();
            image.color = backgroundColor;
            image.material = MakeOverlayMaterial(Canvas.GetDefaultCanvasMaterial());

            string record = newRecord
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(recordColor)}>Новый рекорд!</color>"
                : $"Рекорд: {GameRecords.BestKills}";
            AddText(root.transform, "Машина уничтожена", 90f, new Vector2(0f, 120f));
            AddText(root.transform, $"Волна {wave}    Убито: {kills}\n{record}", 56f, new Vector2(0f, -60f));

            var group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            return group;
        }

        private void AddText(Transform parent, string content, float fontSize, Vector2 position)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(1400f, 300f);
            rect.anchoredPosition = position;

            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSharedMaterial = MakeOverlayMaterial(text.fontSharedMaterial);
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
        }

        private static Material MakeOverlayMaterial(Material source)
        {
            var material = new Material(source);
            material.SetInt("unity_GUIZTestMode", ZTestAlways);
            material.renderQueue = OverlayQueue;
            return material;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private IEnumerator FadeIn(CanvasGroup group)
        {
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                group.alpha = t / fadeDuration;
                yield return null;
            }
            group.alpha = 1f;
        }
    }
}
