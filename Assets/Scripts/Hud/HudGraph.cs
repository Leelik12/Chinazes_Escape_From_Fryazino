using UnityEngine;

namespace RacingProject.Hud
{
    // Столбчатый график загрузки 0–100% для панели HUD: два ряда (всего и доля игры) поверх друг друга,
    // история на capacity замеров, новые справа
    public sealed class HudGraph
    {
        private static Texture2D pixel;

        private readonly float[] total;
        private readonly float[] own;
        private readonly Color totalColor;
        private readonly Color ownColor;
        private int head;
        private int count;

        public HudGraph(int capacity, Color totalColor, Color ownColor)
        {
            total = new float[capacity];
            own = new float[capacity];
            this.totalColor = totalColor;
            this.ownColor = ownColor;
        }

        public void Push(float totalPercent, float ownPercent)
        {
            total[head] = totalPercent;
            own[head] = ownPercent;
            head = (head + 1) % total.Length;
            if (count < total.Length) count++;
        }

        // Рисовать в координатах GUI.matrix панели; label — подпись в левом верхнем углу
        public void Draw(Rect rect, string label, GUIStyle labelStyle)
        {
            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            Color previousColor = GUI.color;
            if (Event.current.type == EventType.Repaint)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.08f);
                GUI.DrawTexture(rect, pixel);

                // Линии 50% и 100%
                GUI.color = new Color(1f, 1f, 1f, 0.15f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), pixel);
                GUI.DrawTexture(new Rect(rect.x, rect.y + rect.height / 2f, rect.width, 1f), pixel);

                float barWidth = rect.width / total.Length;
                for (int i = 0; i < count; i++)
                {
                    int index = (head - count + i + total.Length) % total.Length;
                    float x = rect.xMax - (count - i) * barWidth;
                    DrawBar(x, barWidth, rect, total[index], totalColor);
                    DrawBar(x, barWidth, rect, own[index], ownColor);
                }
            }
            GUI.color = previousColor;
            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, rect.width - 8f, rect.height), label, labelStyle);
        }

        private static void DrawBar(float x, float width, Rect rect, float percent, Color color)
        {
            float height = rect.height * Mathf.Clamp01(percent / 100f);
            if (height <= 0f) return;
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, rect.yMax - height, Mathf.Max(1f, width - 0.5f), height), pixel);
        }
    }
}
