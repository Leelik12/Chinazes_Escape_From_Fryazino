using System.Collections.Generic;
using UnityEngine;
using RacingProject.Audio;
using RacingProject.Enemy;
using RacingProject.Management;
using RacingProject.Network;

namespace RacingProject.Hud
{
    // Стрелки у края экрана на врагов, которых не видно (сзади, сбоку, за кадром). Только на мониторе: в VR
    // игрок просто поворачивает голову. Чем ближе враг, тем крупнее и ярче стрелка; босс — оранжевый.
    // Ещё одна стрелка — на точку задания волны (WaveObjectives), цветом её маяка
    public class OffscreenEnemyIndicators : MonoBehaviour
    {
        [SerializeField] private float maxDistance = 180f;
        [SerializeField] private float edgeMargin = 46f;
        [SerializeField] private float nearSize = 34f;
        [SerializeField] private float farSize = 18f;
        [SerializeField] private Color color = new Color(1f, 0.25f, 0.18f, 0.9f);
        [SerializeField] private Color bossColor = new Color(1f, 0.6f, 0.1f, 0.95f);

        private struct Arrow
        {
            public Vector2 position;
            public float angle;
            public float size;
            public Color color;
        }

        private readonly List<Arrow> arrows = new List<Arrow>();
        private readonly List<EnemyCarController> enemies = new List<EnemyCarController>();
        private float nextScan;
        private Texture2D arrowTexture;

        private void Awake()
        {
            arrowTexture = BuildArrow(64);
        }

        private void OnDestroy()
        {
            if (arrowTexture != null) Destroy(arrowTexture);
        }

        // Треугольник с мягким краем, остриём вверх
        private static Texture2D BuildArrow(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size;
                    // Ширина треугольника растёт от острия (v = 1) к основанию (v = 0)
                    float half = (1f - v) * 0.9f;
                    float inside = Mathf.Min(half - Mathf.Abs(u), v - 0.05f, 0.95f - v);
                    float a = Mathf.Clamp01(inside * size * 0.5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            texture.Apply();
            return texture;
        }

        private void Update()
        {
            arrows.Clear();
            if (ViewModeService.IsVR || LocalPlayerRole.Current == PlayerRole.None || DeathReplay.Active) return;
            AudioListener listener = ActiveListener.Current;
            Camera cam = listener != null ? listener.GetComponent<Camera>() : null;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.5f;
                enemies.Clear();
                enemies.AddRange(FindObjectsByType<EnemyCarController>(FindObjectsSortMode.None));
            }

            foreach (EnemyCarController enemy in enemies)
            {
                if (enemy == null) continue;
                EnemyHealth health = enemy.GetComponent<EnemyHealth>();
                if (health != null && health.HealthFraction <= 0f) continue;
                Vector3 target = enemy.transform.position + Vector3.up;
                float distance = Vector3.Distance(cam.transform.position, target);
                if (distance > maxDistance) continue;
                bool boss = enemy.name.Contains("Boss");
                float near = 1f - distance / maxDistance;
                Color c = boss ? bossColor : color;
                c.a *= Mathf.Lerp(0.45f, 1f, near);
                AddArrow(cam, target, c, Mathf.Lerp(farSize, nearSize, near) * (boss ? 1.3f : 1f));
            }

            // Точка задания волны (WaveObjectives) — на любом расстоянии, цветом её маяка
            WaveObjectives objectives = WaveObjectives.Instance;
            if (objectives != null && objectives.IsSpawned && objectives.Current != WaveObjectives.Kind.None)
            {
                Color c = objectives.BeaconColor;
                c.a = 0.95f;
                AddArrow(cam, objectives.Point + Vector3.up * 2f, c, nearSize * 1.15f);
            }
        }

        private void AddArrow(Camera cam, Vector3 target, Color c, float size)
        {
            Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
            Vector2 extent = center - Vector2.one * edgeMargin;
            Vector3 viewport = cam.WorldToViewportPoint(target);
            bool onScreen = viewport.z > 0f && viewport.x > 0f && viewport.x < 1f && viewport.y > 0f && viewport.y < 1f;
            if (onScreen) return;

            // Направление на цель в плоскости экрана; сзади камеры экранная точка зеркальна
            Vector3 local = cam.transform.InverseTransformPoint(target);
            Vector2 dir = new Vector2(local.x, local.y);
            if (local.z < 0f && dir.sqrMagnitude < 0.01f) dir = Vector2.down;
            dir.Normalize();
            float scale = Mathf.Min(extent.x / Mathf.Max(1e-4f, Mathf.Abs(dir.x)), extent.y / Mathf.Max(1e-4f, Mathf.Abs(dir.y)));
            Vector2 screen = center + dir * scale;
            arrows.Add(new Arrow
            {
                // IMGUI считает y сверху
                position = new Vector2(screen.x, Screen.height - screen.y),
                angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg,
                size = size,
                color = c,
            });
        }

        private void OnGUI()
        {
            if (arrows.Count == 0 || arrowTexture == null) return;
            Matrix4x4 saved = GUI.matrix;
            Color savedColor = GUI.color;
            foreach (Arrow arrow in arrows)
            {
                GUI.matrix = saved;
                GUIUtility.RotateAroundPivot(arrow.angle, arrow.position);
                GUI.color = arrow.color;
                GUI.DrawTexture(new Rect(arrow.position.x - arrow.size * 0.5f, arrow.position.y - arrow.size * 0.5f, arrow.size, arrow.size), arrowTexture);
            }
            GUI.matrix = saved;
            GUI.color = savedColor;
        }
    }
}
