using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingProject.Hud
{
    // Угол экрана, к которому прижата панель
    public enum HudCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    // Отладочная IMGUI-панель: полупрозрачный фон, заголовок с клавишей и текст.
    // Клавиша сворачивает панель до одной строки заголовка. Панели одного угла
    // встают друг под другом (или над, у нижних углов) в порядке order.
    // Размеры заданы для экрана высотой 1080 пикселей и масштабируются под текущий
    public sealed class HudPanel
    {
        private const float ReferenceHeight = 1080f;
        private const float Margin = 16f;
        private const float Spacing = 8f;
        private const float Padding = 10f;
        private const int FontSize = 18;

        private static readonly List<HudPanel> Active = new List<HudPanel>();
        private static Texture2D background;
        private static GUIStyle textStyle;

        private readonly string title;
        private readonly Key toggleKey;
        private readonly HudCorner corner;
        private readonly int order;
        private readonly float width;

        // Высота с прошлой отрисовки: по ней соседние панели считают своё смещение
        private float height;

        public bool Expanded { get; private set; }

        public HudPanel(string title, Key toggleKey, HudCorner corner, int order, float width, bool expanded)
        {
            this.title = title;
            this.toggleKey = toggleKey;
            this.corner = corner;
            this.order = order;
            this.width = width;
            Expanded = expanded;
        }

        // Вызывать из OnEnable/OnDisable владельца, чтобы соседи учитывали панель в раскладке
        public void Show()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        public void Hide()
        {
            Active.Remove(this);
            height = 0f;
        }

        // Вызывать из Update владельца
        public void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
                Expanded = !Expanded;
        }

        // Стиль текста панелей, чтобы дополнительный блок подписывал элементы так же
        public static GUIStyle TextStyle
        {
            get
            {
                EnsureStyles();
                return textStyle;
            }
        }

        // Вызывать из OnGUI владельца; body — текст с rich text, строки через \n.
        // В развёрнутой панели под текстом выделяется место extraHeight, в нём рисует drawExtra
        // (координаты — в пикселях экрана высотой 1080)
        public void Draw(string body, float extraHeight = 0f, System.Action<Rect> drawExtra = null)
        {
            EnsureStyles();

            float scale = Screen.height / ReferenceHeight;
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            string header = Expanded
                ? $"<b>{title}</b>  <color=#9a9a9a>[{toggleKey}] свернуть</color>"
                : $"<b>{title}</b>  <color=#9a9a9a>[{toggleKey}] развернуть</color>";
            string text = Expanded && !string.IsNullOrEmpty(body) ? header + "\n" + body : header;

            GUIContent content = new GUIContent(text);
            float textWidth = width - Padding * 2f;
            float textHeight = textStyle.CalcHeight(content, textWidth);
            bool hasExtra = Expanded && drawExtra != null && extraHeight > 0f;
            height = textHeight + Padding * 2f + (hasExtra ? Spacing + extraHeight : 0f);

            float screenWidth = Screen.width / scale;
            float offset = Margin + OffsetBefore();
            float x = corner == HudCorner.TopLeft || corner == HudCorner.BottomLeft ? Margin : screenWidth - Margin - width;
            float y = corner == HudCorner.TopLeft || corner == HudCorner.TopRight ? offset : ReferenceHeight - offset - height;

            Rect rect = new Rect(x, y, width, height);
            GUI.DrawTexture(rect, background);
            GUI.Label(new Rect(x + Padding, y + Padding, textWidth, textHeight), content, textStyle);
            if (hasExtra)
                drawExtra(new Rect(x + Padding, y + Padding + textHeight + Spacing, textWidth, extraHeight));

            GUI.matrix = previousMatrix;
        }

        // Суммарная высота панелей того же угла, стоящих раньше этой
        private float OffsetBefore()
        {
            float offset = 0f;
            foreach (HudPanel panel in Active)
            {
                if (panel == this || panel.corner != corner || panel.height <= 0f) continue;
                if (panel.order < order || (panel.order == order && Active.IndexOf(panel) < Active.IndexOf(this)))
                    offset += panel.height + Spacing;
            }
            return offset;
        }

        private static void EnsureStyles()
        {
            if (background == null)
            {
                background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
                background.Apply();
            }

            if (textStyle == null)
            {
                textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = FontSize,
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0)
                };
                textStyle.normal.textColor = Color.white;
            }
        }
    }
}
