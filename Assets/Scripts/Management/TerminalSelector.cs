using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Строка настройки меню-терминала: «Подпись ◄ значение ►» и, если задана, шкала из делений.
    // Стрелки листают варианты без зацикливания; при наведении строка сообщает свою подсказку
    public class TerminalSelector : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text value;
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [Tooltip("Деления шкалы (необязательно): горят от первого до выбранного")]
        [SerializeField] private Graphic[] segments = new Graphic[0];
        [SerializeField] private Color segmentOn = new Color(0.6f, 0.88f, 0.42f);
        [SerializeField] private Color segmentOff = new Color(0.6f, 0.88f, 0.42f, 0.15f);
        [Tooltip("Подсказка внизу экрана при наведении")]
        [SerializeField, TextArea] private string hint;

        public event Action<int> ValueChanged;
        public static event Action<TerminalSelector> Hovered;

        public string Hint => hint;
        public int Index { get; private set; }

        private string[] options = new string[0];
        private bool interactable = true;
        private CanvasGroup group;
        private bool initialized;

        private void Awake()
        {
            Initialize();
        }

        // Строки на скрытых вкладках получают значения раньше своего Awake, поэтому стрелки подключаются при первом обращении
        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            group = GetComponent<CanvasGroup>();
            if (previous != null) previous.onClick.AddListener(() => Step(-1));
            if (next != null) next.onClick.AddListener(() => Step(1));
        }

        public void SetOptions(string[] newOptions, int index)
        {
            Initialize();
            options = newOptions ?? new string[0];
            Index = Mathf.Clamp(index, 0, Mathf.Max(0, options.Length - 1));
            Refresh();
        }

        public void SetInteractable(bool enabled)
        {
            Initialize();
            interactable = enabled;
            if (group != null) group.alpha = enabled ? 1f : 0.35f;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered?.Invoke(this);
        }

        private void Step(int direction)
        {
            int target = Mathf.Clamp(Index + direction, 0, options.Length - 1);
            if (!interactable || target == Index) return;
            Index = target;
            Refresh();
            ValueChanged?.Invoke(Index);
        }

        private void Refresh()
        {
            if (value != null)
                value.text = options.Length > 0 ? options[Index] : "—";
            if (previous != null) previous.interactable = interactable && Index > 0;
            if (next != null) next.interactable = interactable && Index < options.Length - 1;
            // Деления делят шкалу поровну: при 11 вариантах и 10 делениях каждое — один шаг
            float filled = options.Length > 1 ? (float)Index / (options.Length - 1) * segments.Length : 0f;
            for (int i = 0; i < segments.Length; i++)
                if (segments[i] != null)
                    segments[i].color = i < Mathf.RoundToInt(filled) ? segmentOn : segmentOff;
        }
    }
}
