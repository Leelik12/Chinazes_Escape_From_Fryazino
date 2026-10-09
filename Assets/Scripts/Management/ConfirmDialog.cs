using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Окно подтверждения «вы точно хотите…?» с кнопками «Да» и «Нет»: защищает от случайного клика по выходу.
    // Окно — этот объект; пока оно открыто, под ним ничего не нажимается (фон перехватывает клики)
    public class ConfirmDialog : MonoBehaviour
    {
        [SerializeField] private TMP_Text question;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action onYes;

        public bool IsOpen => gameObject.activeSelf;

        private void Awake()
        {
            yesButton.onClick.AddListener(Confirm);
            noButton.onClick.AddListener(Close);
        }

        public void Show(string text, Action confirmed)
        {
            if (question != null) question.text = text;
            onYes = confirmed;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            onYes = null;
            gameObject.SetActive(false);
        }

        private void Confirm()
        {
            Action action = onYes;
            Close();
            action?.Invoke();
        }
    }
}
