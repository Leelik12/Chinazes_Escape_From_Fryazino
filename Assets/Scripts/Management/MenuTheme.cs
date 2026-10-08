using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RacingProject.Network;

namespace RacingProject.Management
{
    // Оформление меню: «военный терминал» или «тёмная панель». Выбор — кнопкой в настройках, хранится в PlayerPrefs.
    // Тема перекрашивает элементы меню на месте (панели, кнопки, слайдеры, переключатели, тексты), разметку не меняет
    public class MenuTheme : MonoBehaviour
    {
        public enum Style { Terminal, Panel }

        [Serializable]
        public class Palette
        {
            [Tooltip("Название темы на кнопке выбора")]
            public string displayName;
            public TMP_FontAsset font;
            [Tooltip("Все тексты прописными (терминал)")]
            public bool upperCase;
            public Color panel = Color.black;
            public Color text = Color.white;
            public Color button = Color.gray;
            public Color buttonText = Color.white;
            [Tooltip("Главная кнопка (accentButton)")]
            public Color accentButton = Color.yellow;
            public Color accentButtonText = Color.black;
            public Color track = Color.gray;
            public Color fill = Color.white;
            [Tooltip("Цвета индикаторов готовности и связи, их задаёт RoomController")]
            public Color ready = Color.green;
            public Color notReady = Color.red;
            public Color connected = Color.green;
            public Color disconnected = Color.red;
        }

        [Serializable]
        public class TextVariant
        {
            public TMP_Text target;
            [TextArea] public string terminal;
            [TextArea] public string panel;
        }

        [SerializeField] private Transform canvasesRoot;
        [SerializeField] private RoomController room;
        [SerializeField] private Button accentButton;
        [SerializeField] private Button styleButton;
        [SerializeField] private TMP_Text styleButtonLabel;
        [SerializeField] private Palette terminal = new Palette();
        [SerializeField] private Palette panel = new Palette();
        [Tooltip("Тексты, которые в темах звучат по-разному (заголовок, подписи готовности)")]
        [SerializeField] private TextVariant[] texts = new TextVariant[0];

        private const string PrefKey = "MenuTheme";

        public static Style Current
        {
            get => (Style)PlayerPrefs.GetInt(PrefKey, (int)Style.Panel);
            private set
            {
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (styleButton != null)
                styleButton.onClick.AddListener(Next);
            Apply(Current);
        }

        public void Next()
        {
            Current = Current == Style.Terminal ? Style.Panel : Style.Terminal;
            Apply(Current);
        }

        public void Apply(Style style)
        {
            Palette p = style == Style.Terminal ? terminal : panel;
            if (canvasesRoot == null) return;

            foreach (TextVariant v in texts)
                if (v.target != null)
                    v.target.text = style == Style.Terminal ? v.terminal : v.panel;
            if (styleButtonLabel != null)
                styleButtonLabel.text = "Стиль меню: " + p.displayName;

            foreach (Image image in canvasesRoot.GetComponentsInChildren<Image>(true))
                if (image.name == "Panel")
                    image.color = p.panel;

            foreach (TMP_Text label in canvasesRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                if (p.font != null)
                    label.font = p.font;
                label.fontStyle = p.upperCase ? label.fontStyle | FontStyles.UpperCase : label.fontStyle & ~FontStyles.UpperCase;
                label.color = p.text;
            }

            foreach (Button button in canvasesRoot.GetComponentsInChildren<Button>(true))
            {
                bool accent = button == accentButton;
                if (button.image != null)
                    button.image.color = accent ? p.accentButton : p.button;
                SetTint(button);
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                    label.color = accent ? p.accentButtonText : p.buttonText;
            }

            foreach (Slider slider in canvasesRoot.GetComponentsInChildren<Slider>(true))
            {
                foreach (Image image in slider.GetComponentsInChildren<Image>(true))
                    image.color = image.name == "Background" ? p.track : image.name == "Fill" ? p.fill : p.text;
                SetTint(slider);
            }

            foreach (Toggle toggle in canvasesRoot.GetComponentsInChildren<Toggle>(true))
            {
                if (toggle.targetGraphic != null)
                    toggle.targetGraphic.color = p.track;
                if (toggle.graphic != null)
                    toggle.graphic.color = p.fill;
                SetTint(toggle);
            }

            if (room != null)
            {
                room.readyColor = p.ready;
                room.notReadyColor = p.notReady;
                room.connectedColor = p.connected;
                room.disconnectedColor = p.disconnected;
            }
        }

        // Цвет теперь задаёт сама картинка, ColorTint только слегка затемняет её при наведении и нажатии
        private static void SetTint(Selectable selectable)
        {
            ColorBlock colors = selectable.colors;
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            selectable.colors = colors;
        }
    }
}
