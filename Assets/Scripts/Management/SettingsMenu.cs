using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Экран настроек на левом терминале: вкладки «Звук», «Графика», «Экран».
    // Значения берутся из GraphicsQuality, StaticHolder и ViewModeService и сразу применяются
    public class SettingsMenu : MonoBehaviour
    {
        [SerializeField] private MenuManager manager;

        [Header("Вкладки")]
        [SerializeField] private Button[] tabButtons = new Button[0];
        [SerializeField] private GameObject[] tabPages = new GameObject[0];
        [Tooltip("Подложки вкладок: у выбранной ярче")]
        [SerializeField] private Graphic[] tabBackgrounds = new Graphic[0];
        [SerializeField] private Color tabOn = new Color(0.6f, 0.88f, 0.42f, 0.9f);
        [SerializeField] private Color tabOff = new Color(0.6f, 0.88f, 0.42f, 0.06f);
        [Tooltip("Подписи вкладок: у выбранной тёмный текст на яркой плашке, как инверсия на терминале")]
        [SerializeField] private Graphic[] tabLabels = new Graphic[0];
        [SerializeField] private Color labelOn = new Color(0.035f, 0.075f, 0.045f);
        [SerializeField] private Color labelOff = new Color(0.6f, 0.88f, 0.42f);

        [Header("Звук")]
        [SerializeField] private TerminalSelector music;
        [SerializeField] private TerminalSelector engine;
        [SerializeField] private TerminalSelector ambient;

        [Header("Графика")]
        [SerializeField] private TerminalSelector preset;
        [Tooltip("По порядку GraphicsQuality.Option: масштаб, сглаживание, тени, SSAO, постобработка, текстуры, анизотропия, трава, деревья, детализация")]
        [SerializeField] private TerminalSelector[] graphics = new TerminalSelector[0];

        [Header("Экран")]
        [SerializeField] private TerminalSelector viewMode;
        [SerializeField] private TerminalSelector windowMode;
        [SerializeField] private TerminalSelector resolution;
        [SerializeField] private TerminalSelector vSync;
        [SerializeField] private TerminalSelector frameLimit;

        [Header("Подсказка")]
        [SerializeField] private TMP_Text hint;
        [SerializeField, TextArea] private string defaultHint = "Наведите на строку — здесь появится пояснение";

        private const int VolumeSteps = 10;
        private static readonly string[] ViewModes = { "Шлем VR", "Монитор" };

        private int tab;

        private void Awake()
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                int index = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }

            BindVolume(music, StaticHolder.MusikVolume, v => manager.SetVolumeMusik(v));
            BindVolume(engine, StaticHolder.EngineVolume, v => manager.SetVolumeEngine(v));
            BindVolume(ambient, StaticHolder.AmbientVolume, v => manager.SetVolumeAmbient(v));

            if (preset != null) preset.ValueChanged += GraphicsQuality.SetPreset;
            for (int i = 0; i < graphics.Length; i++)
                Bind(graphics[i], (GraphicsQuality.Option)i);
            Bind(windowMode, GraphicsQuality.Option.WindowMode);
            Bind(resolution, GraphicsQuality.Option.Resolution);
            Bind(vSync, GraphicsQuality.Option.VSync);
            Bind(frameLimit, GraphicsQuality.Option.FrameLimit);
            if (viewMode != null) viewMode.ValueChanged += i => ViewModeService.SetPreferDesktop(i == 1);

            SelectTab(0);
        }

        private void OnEnable()
        {
            GraphicsQuality.Changed += Refresh;
            ViewModeService.Changed += OnViewModeChanged;
            TerminalSelector.Hovered += ShowHint;
            Refresh();
            ShowHint(null);
        }

        private void OnDisable()
        {
            GraphicsQuality.Changed -= Refresh;
            ViewModeService.Changed -= OnViewModeChanged;
            TerminalSelector.Hovered -= ShowHint;
        }

        public void SelectTab(int index)
        {
            tab = index;
            for (int i = 0; i < tabPages.Length; i++)
                if (tabPages[i] != null) tabPages[i].SetActive(i == tab);
            for (int i = 0; i < tabBackgrounds.Length; i++)
                if (tabBackgrounds[i] != null) tabBackgrounds[i].color = i == tab ? tabOn : tabOff;
            for (int i = 0; i < tabLabels.Length; i++)
                if (tabLabels[i] != null) tabLabels[i].color = i == tab ? labelOn : labelOff;
            ShowHint(null);
        }

        private void Refresh()
        {
            if (preset != null)
            {
                // Параметры не совпадают ни с одним пресетом: в конце списка появляется «Своё»
                int current = GraphicsQuality.Preset;
                string[] names = GraphicsQuality.PresetNames;
                if (current < 0)
                {
                    names = new string[names.Length + 1];
                    GraphicsQuality.PresetNames.CopyTo(names, 0);
                    names[names.Length - 1] = GraphicsQuality.CustomPresetName;
                    current = names.Length - 1;
                }
                preset.SetOptions(names, current);
            }
            for (int i = 0; i < graphics.Length; i++)
                Show(graphics[i], (GraphicsQuality.Option)i);
            Show(windowMode, GraphicsQuality.Option.WindowMode);
            Show(resolution, GraphicsQuality.Option.Resolution);
            Show(vSync, GraphicsQuality.Option.VSync);
            Show(frameLimit, GraphicsQuality.Option.FrameLimit);

            // В VR окном и частотой кадров управляет шлем
            bool desktop = !ViewModeService.IsVR;
            if (viewMode != null) viewMode.SetOptions(ViewModes, desktop ? 1 : 0);
            SetInteractable(windowMode, desktop);
            SetInteractable(resolution, desktop);
            SetInteractable(vSync, desktop);
            SetInteractable(frameLimit, desktop && GraphicsQuality.Get(GraphicsQuality.Option.VSync) == 0);
        }

        private void OnViewModeChanged(ViewMode mode) => Refresh();

        private void ShowHint(TerminalSelector selector)
        {
            if (hint == null) return;
            hint.text = selector != null && !string.IsNullOrEmpty(selector.Hint) ? selector.Hint : defaultHint;
        }

        private static void Bind(TerminalSelector selector, GraphicsQuality.Option option)
        {
            if (selector != null)
                selector.ValueChanged += i => GraphicsQuality.Set(option, i);
        }

        private static void Show(TerminalSelector selector, GraphicsQuality.Option option)
        {
            if (selector != null)
                selector.SetOptions(GraphicsQuality.OptionLabels(option), GraphicsQuality.Get(option));
        }

        private static void SetInteractable(TerminalSelector selector, bool enabled)
        {
            if (selector != null) selector.SetInteractable(enabled);
        }

        private static void BindVolume(TerminalSelector selector, float volume, System.Action<float> apply)
        {
            if (selector == null) return;
            var labels = new string[VolumeSteps + 1];
            for (int i = 0; i <= VolumeSteps; i++) labels[i] = i * 100 / VolumeSteps + "%";
            selector.SetOptions(labels, Mathf.RoundToInt(volume * VolumeSteps));
            selector.ValueChanged += i => apply((float)i / VolumeSteps);
        }
    }
}
