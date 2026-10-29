﻿using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using RacingProject.Audio;
using RacingProject.Enemy;
using RacingProject.Management;
using RacingProject.Network;

namespace RacingProject.Hud
{
    // Надписи волн в стиле терминала меню (зелёный люминофор, заголовки BlackOpsOne): «ВОЛНА N» в начале, итог
    // зачистки с бонусом и выбор одной из трёх карточек улучшений. Текст печатается побуквенно, карточки выезжают
    // по очереди и подсвечиваются под курсором. На мониторе — поверх экрана, карточку выбирают клавишами 1–3
    // (стрелок и одиночная игра; у водителя в паре на них передачи) или мышью. В VR — панель в мире перед глазами,
    // карточку выбирают лучом контроллера; если не выбрать, она выберется сама по истечении времени (EnemyManager)
    public class WaveHud : MonoBehaviour
    {
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip waveStartSound;
        [SerializeField] private AudioClip waveClearSound;
        [SerializeField] private AudioClip cardPickSound;
        [Tooltip("Сколько держится надпись волны, с")]
        [SerializeField] private float bannerTime = 3f;
        [SerializeField] private TMP_FontAsset titleFont;
        [SerializeField] private TMP_FontAsset textFont;

        [Header("VR")]
        [Tooltip("Расстояние панели от глаз и её масштаб (метров на единицу холста)")]
        [SerializeField] private float vrDistance = 1.5f;
        [SerializeField] private float vrScale = 0.0011f;

        // Цвета терминала меню
        internal static readonly Color Phosphor = new Color32(0x99, 0xE0, 0x6B, 0xFF);
        internal static readonly Color PhosphorDim = new Color32(0x61, 0x94, 0x54, 0xFF);
        internal static readonly Color Panel = new Color32(0x09, 0x13, 0x0B, 0xE0);
        internal static readonly Color PanelHover = new Color32(0x1C, 0x33, 0x16, 0xF0);
        private static readonly Color TextLight = new Color32(0xEB, 0xFF, 0xE0, 0xF2);

        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float CardWidth = 340f;
        private const float CardHeight = 200f;
        private const float CardSpacing = 34f;
        // Скорость печати, букв в секунду
        private const float TypeSpeed = 38f;

        private readonly List<RunUpgrades.Card> cards = new List<RunUpgrades.Card>();
        private bool choiceSent;
        private float bannerStart;
        private float bannerUntil;
        private float chosenUntil;
        private float cardsStart;

        private Canvas canvas;
        private CanvasGroup root;
        private RectTransform rootRect;
        private TextMeshProUGUI bannerTitle;
        private TextMeshProUGUI bannerSubtitle;
        private CanvasGroup bannerGroup;
        private TextMeshProUGUI chosenLabel;
        private TextMeshProUGUI hintLabel;
        private RectTransform cardRow;
        private readonly List<WaveHudCard> views = new List<WaveHudCard>();
        private bool vrMode;
        private Camera vrHead;

        // Рация (RadioChatter): строка сообщения сверху слева
        private CanvasGroup radioGroup;
        private TextMeshProUGUI radioFrom;
        private TextMeshProUGUI radioText;
        private float radioStart;
        private float radioUntil;

        private void Awake()
        {
            BuildUi();
        }

        private void OnEnable()
        {
            if (enemyManager == null) return;
            enemyManager.WaveStarted += OnWaveStarted;
            enemyManager.WaveCleared += OnWaveCleared;
            enemyManager.CardsOffered += OnCardsOffered;
            enemyManager.CardChosen += OnCardChosen;
            RadioChatter.Message += OnRadio;
        }

        private void OnDisable()
        {
            if (enemyManager == null) return;
            enemyManager.WaveStarted -= OnWaveStarted;
            enemyManager.WaveCleared -= OnWaveCleared;
            enemyManager.CardsOffered -= OnCardsOffered;
            enemyManager.CardChosen -= OnCardChosen;
            RadioChatter.Message -= OnRadio;
        }

        private void OnRadio(RadioChatter.Speaker speaker, string text)
        {
            radioFrom.text = speaker == RadioChatter.Speaker.Hq ? "рация · штаб" : "рация · перехват";
            radioFrom.color = speaker == RadioChatter.Speaker.Hq ? PhosphorDim : new Color32(0xE0, 0x9A, 0x5A, 0xFF);
            radioText.text = speaker == RadioChatter.Speaker.Hq ? text : $"<i>{text}</i>";
            radioText.maxVisibleCharacters = 0;
            radioStart = Time.time;
            radioUntil = Time.time + 2.5f + text.Length / 16f;
            PlaceVrPanel(true);
        }

        private void OnWaveStarted(int wave)
        {
            ShowBanner($"ВОЛНА {wave}", wave > 1 ? "враги на подходе" : "продержитесь как можно дольше");
            Play(waveStartSound);
        }

        private void OnWaveCleared(int wave, float seconds, int bonus)
        {
            string time = $"зачищено за {Mathf.RoundToInt(seconds)} с";
            ShowBanner($"ВОЛНА {wave} ЗАЧИЩЕНА", bonus > 0 ? $"{time} · бонус за скорость +{bonus}" : time);
            Play(waveClearSound);
        }

        private void OnCardsOffered(List<RunUpgrades.Card> offered)
        {
            cards.Clear();
            cards.AddRange(offered);
            choiceSent = false;
            cardsStart = Time.time;
            RebuildCards();
            if (cards.Count > 0) PlaceVrPanel(true);
        }

        private void OnCardChosen(RunUpgrades.Card card)
        {
            chosenLabel.text = $"улучшение: {RunUpgrades.Title(card)} (ур. {RunUpgrades.Level(card)})";
            chosenLabel.maxVisibleCharacters = 0;
            chosenUntil = Time.time + 2.5f;
            Play(cardPickSound);
        }

        private void ShowBanner(string title, string subtitle)
        {
            bannerTitle.text = title;
            bannerSubtitle.text = subtitle;
            bannerTitle.maxVisibleCharacters = 0;
            bannerSubtitle.maxVisibleCharacters = 0;
            bannerStart = Time.time;
            bannerUntil = Time.time + bannerTime;
            PlaceVrPanel(true);
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
                audioSource.PlayOneShot(clip);
        }

        private void Update()
        {
            UpdateMode();
            HandleKeys();

            // Надпись волны: печатается, держится и гаснет
            float now = Time.time;
            bool banner = now < bannerUntil;
            bannerGroup.alpha = banner ? Mathf.Clamp01((bannerUntil - now) / 0.6f) : 0f;
            if (banner)
            {
                int typed = Mathf.FloorToInt((now - bannerStart) * TypeSpeed);
                bannerTitle.maxVisibleCharacters = typed;
                bannerSubtitle.maxVisibleCharacters = Mathf.Max(0, typed - bannerTitle.text.Length);
            }

            bool showCards = cards.Count > 0;
            cardRow.gameObject.SetActive(showCards);
            hintLabel.gameObject.SetActive(showCards);
            if (showCards) UpdateCards(now);

            bool chosen = !showCards && now < chosenUntil;
            chosenLabel.gameObject.SetActive(chosen);
            if (chosen)
                chosenLabel.maxVisibleCharacters = Mathf.FloorToInt((now - (chosenUntil - 2.5f)) * TypeSpeed * 1.5f);

            bool radio = now < radioUntil;
            radioGroup.alpha = radio ? Mathf.Clamp01((radioUntil - now) / 0.5f) : 0f;
            if (radio)
                radioText.maxVisibleCharacters = Mathf.FloorToInt((now - radioStart) * TypeSpeed * 1.3f);

            // В VR стрелку в паре раньше надписи не показывались; теперь панель видна обоим, но только во время раунда
            bool visible = LocalPlayerRole.Current != PlayerRole.None && (banner || showCards || chosen || radio);
            root.alpha = visible ? 1f : 0f;
            root.blocksRaycasts = visible && showCards && !choiceSent;
            if (vrMode && visible) PlaceVrPanel(false);
        }

        // Клавиши 1–3 — тем, у кого на них нет передач
        private void HandleKeys()
        {
            if (cards.Count == 0 || choiceSent || Keyboard.current == null) return;
            if (LocalPlayerRole.Current == PlayerRole.Driver) return;
            Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3 };
            for (int i = 0; i < cards.Count && i < keys.Length; i++)
                if (Keyboard.current[keys[i]].wasPressedThisFrame)
                    Choose(i);
        }

        internal bool ChoiceSent => choiceSent;

        internal void Choose(int index)
        {
            if (choiceSent || index < 0 || index >= cards.Count || enemyManager == null) return;
            choiceSent = true;
            if (index < views.Count) views[index].Picked = true;
            enemyManager.ChooseCard(cards[index]);
        }

        private void UpdateCards(float now)
        {
            bool keys = LocalPlayerRole.Current != PlayerRole.Driver && !vrMode;
            float left = enemyManager != null ? Mathf.Max(0f, enemyManager.CardDeadline - now) : 0f;
            string how = vrMode ? "выберите улучшение лучом контроллера"
                : keys ? "выберите улучшение: клавиши 1–3 или мышь" : "выберите улучшение мышью";
            hintLabel.text = choiceSent ? "ждём подтверждения…" : $"{how} · через {Mathf.CeilToInt(left)} с выберется само";

            for (int i = 0; i < views.Count; i++)
            {
                // Карточки выезжают снизу по очереди
                float t = Mathf.Clamp01((now - cardsStart - i * 0.12f) / 0.35f);
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                views[i].Animate(ease, choiceSent, now);
            }
        }

        // --- Холст ---

        private void UpdateMode()
        {
            bool vr = ViewModeService.IsVR;
            Camera head = vr ? CurrentHead() : null;
            if (vr == vrMode && head == vrHead) return;
            vrMode = vr && head != null;
            vrHead = head;
            if (vrMode)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = head;
                // Панель едет вместе с ригом в машине: родитель — смещение камеры XR Origin
                canvas.transform.SetParent(head.transform.parent, false);
                rootRect.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
                canvas.transform.localScale = Vector3.one * vrScale;
                PlaceVrPanel(true);
            }
            else
            {
                canvas.transform.SetParent(transform, false);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.transform.localScale = Vector3.one;
            }
        }

        private static Camera CurrentHead()
        {
            AudioListener listener = ActiveListener.Current;
            Camera cam = listener != null ? listener.GetComponent<Camera>() : null;
            return cam != null && cam.transform.parent != null ? cam : null;
        }

        // Панель перед взглядом; если голова отвернулась далеко, панель плавно догоняет взгляд
        private void PlaceVrPanel(bool snap)
        {
            if (!vrMode || vrHead == null) return;
            Transform panel = canvas.transform;
            Transform space = panel.parent;
            Vector3 forward = Vector3.ProjectOnPlane(vrHead.transform.forward, space.up);
            if (forward.sqrMagnitude < 1e-4f) forward = space.forward;
            forward.Normalize();
            Vector3 target = vrHead.transform.position + forward * vrDistance - space.up * 0.1f;
            Quaternion rotation = Quaternion.LookRotation(forward, space.up);
            float angle = Vector3.Angle(forward, Vector3.ProjectOnPlane(panel.position - vrHead.transform.position, space.up));
            if (snap || angle > 35f)
                panel.SetPositionAndRotation(target, rotation);
            else
            {
                // Держим расстояние до головы, даже если игрок наклонился
                float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
                panel.SetPositionAndRotation(Vector3.Lerp(panel.position, target, k * 0.3f), Quaternion.Slerp(panel.rotation, rotation, k * 0.3f));
            }
        }

        private void BuildUi()
        {
            var go = new GameObject("WaveHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(TrackedDeviceGraphicRaycaster), typeof(CanvasGroup));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 1f;
            root = go.GetComponent<CanvasGroup>();
            root.alpha = 0f;
            root.interactable = true;
            rootRect = (RectTransform)go.transform;

            var bannerGo = new GameObject("Banner", typeof(RectTransform), typeof(CanvasGroup));
            bannerGo.transform.SetParent(go.transform, false);
            bannerGroup = bannerGo.GetComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            Stretch((RectTransform)bannerGo.transform);
            bannerTitle = CreateText(bannerGo.transform, "Title", titleFont, 68f, Phosphor, new Vector2(0f, 300f), new Vector2(1600f, 90f), false);
            bannerSubtitle = CreateText(bannerGo.transform, "Subtitle", textFont, 26f, PhosphorDim, new Vector2(0f, 235f), new Vector2(1600f, 40f), true);
            chosenLabel = CreateText(go.transform, "Chosen", textFont, 26f, Phosphor, new Vector2(0f, 150f), new Vector2(1600f, 40f), true);
            hintLabel = CreateText(go.transform, "Hint", textFont, 22f, PhosphorDim, new Vector2(0f, 175f), new Vector2(1600f, 34f), true);

            // Рация: панель сверху слева, подпись «рация · штаб» и текст в две строки
            var radioGo = new GameObject("Radio", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            radioGo.transform.SetParent(go.transform, false);
            var radioRect = (RectTransform)radioGo.transform;
            radioRect.anchoredPosition = new Vector2(-500f, 420f);
            radioRect.sizeDelta = new Vector2(820f, 112f);
            radioGo.GetComponent<Image>().color = Panel;
            radioGroup = radioGo.GetComponent<CanvasGroup>();
            radioGroup.alpha = 0f;
            radioGroup.blocksRaycasts = false;
            radioFrom = CreateText(radioGo.transform, "From", textFont, 17f, PhosphorDim, new Vector2(0f, 38f), new Vector2(780f, 24f), true);
            radioFrom.alignment = TextAlignmentOptions.Left;
            radioText = CreateText(radioGo.transform, "Text", textFont, 23f, TextLight, new Vector2(0f, -12f), new Vector2(780f, 66f), false);
            radioText.alignment = TextAlignmentOptions.TopLeft;
            radioText.textWrappingMode = TextWrappingModes.Normal;

            var row = new GameObject("Cards", typeof(RectTransform));
            row.transform.SetParent(go.transform, false);
            cardRow = (RectTransform)row.transform;
            cardRow.anchoredPosition = new Vector2(0f, 20f);
            cardRow.sizeDelta = new Vector2(3f * CardWidth + 2f * CardSpacing, CardHeight);
        }

        private void RebuildCards()
        {
            foreach (WaveHudCard view in views)
                if (view != null) Destroy(view.gameObject);
            views.Clear();
            float total = cards.Count * CardWidth + (cards.Count - 1) * CardSpacing;
            for (int i = 0; i < cards.Count; i++)
            {
                RunUpgrades.Card card = cards[i];
                int level = RunUpgrades.Level(card);
                var go = new GameObject("Card " + (i + 1), typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup), typeof(WaveHudCard));
                go.transform.SetParent(cardRow, false);
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(CardWidth, CardHeight);
                float x = -total / 2f + CardWidth / 2f + i * (CardWidth + CardSpacing);
                var image = go.GetComponent<Image>();
                image.color = Panel;
                var outline = go.GetComponent<Outline>();
                outline.effectColor = PhosphorDim;
                outline.effectDistance = new Vector2(2f, -2f);

                var title = CreateText(go.transform, "Title", titleFont, 25f, Phosphor, new Vector2(0f, 54f), new Vector2(CardWidth - 32f, 70f), false);
                title.text = $"{i + 1}. {RunUpgrades.Title(card)}";
                title.alignment = TextAlignmentOptions.TopLeft;
                // Длинное название ужимается, чтобы уложиться в две строки над описанием
                title.enableAutoSizing = true;
                title.fontSizeMin = 18f;
                title.fontSizeMax = 25f;
                title.textWrappingMode = TextWrappingModes.Normal;
                var description = CreateText(go.transform, "Description", textFont, 19f, TextLight, new Vector2(0f, -30f), new Vector2(CardWidth - 32f, 70f), false);
                description.text = RunUpgrades.Description(card);
                description.alignment = TextAlignmentOptions.TopLeft;
                description.textWrappingMode = TextWrappingModes.Normal;
                var levelText = CreateText(go.transform, "Level", textFont, 17f, PhosphorDim, new Vector2(0f, -78f), new Vector2(CardWidth - 32f, 26f), true);
                levelText.text = $"уровень {level} → {level + 1} из {RunUpgrades.MaxLevel}";
                levelText.alignment = TextAlignmentOptions.BottomLeft;

                var view = go.GetComponent<WaveHudCard>();
                int index = i;
                view.Setup(this, index, image, outline, x);
                views.Add(view);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font, float size, Color color,
            Vector2 position, Vector2 box, bool upper)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            if (upper) text.fontStyle = FontStyles.UpperCase;
            return text;
        }
    }
}
