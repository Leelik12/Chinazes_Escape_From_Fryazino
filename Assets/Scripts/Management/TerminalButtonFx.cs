using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Кнопка меню-терминала: при наведении перед подписью появляется «>», подсветку даёт ColorTint кнопки.
    // Наведение и нажатие объявляются событиями: на них отвечают звуки и тумблеры пульта бункера
    [RequireComponent(typeof(Button))]
    public class TerminalButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("Знак «>» перед подписью, виден при наведении")]
        [SerializeField] private TMP_Text marker;

        public static event Action<TerminalButtonFx> Hovered;
        public static event Action<TerminalButtonFx> Pressed;

        // Холст, на котором стоит кнопка: по нему пульт выбирает, какой ряд тумблеров щёлкнет
        public Canvas Screen { get; private set; }

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            Screen = GetComponentInParent<Canvas>();
            button.onClick.AddListener(() => Pressed?.Invoke(this));
            SetMarker(false);
        }

        private void OnDisable()
        {
            SetMarker(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!button.interactable) return;
            SetMarker(true);
            Hovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetMarker(false);
        }

        private void SetMarker(bool visible)
        {
            if (marker != null)
                marker.enabled = visible;
        }
    }
}
