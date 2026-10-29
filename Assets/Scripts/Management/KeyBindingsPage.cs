using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Вкладка «Клавиши» меню-терминала: щелчок по клавише действия ждёт новую клавишу (Esc — отмена).
    // Занятая клавиша меняется местами с прежней; выбор сохраняет GameKeys
    public class KeyBindingsPage : MonoBehaviour
    {
        [System.Serializable]
        public class Row
        {
            public GameAction action;
            public TMP_Text label;
            public TMP_Text value;
            public Button button;
        }

        [SerializeField] private Row[] rows = new Row[0];
        [SerializeField] private Button resetButton;
        [Tooltip("Строка подсказки внизу терминала: пока ждём клавишу, в ней инструкция")]
        [SerializeField] private TMP_Text hint;

        // Esc закрывает меню, F1–F4 — панели HUD, цифры 1–7 и F — ручная коробка и сцепление,
        // стрелки всегда дублируют руль, газ и тормоз
        private static readonly Key[] Reserved =
        {
            Key.Escape, Key.F1, Key.F2, Key.F3, Key.F4, Key.F, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            Key.Digit5, Key.Digit6, Key.Digit7, Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow,
        };

        private int listening = -1;
        private int listenFrame;
        private string message;

        private void Awake()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                int index = i;
                if (rows[i].button != null) rows[i].button.onClick.AddListener(() => Listen(index));
            }
            if (resetButton != null)
                resetButton.onClick.AddListener(() =>
                {
                    GameKeys.ResetToDefaults();
                    message = "Клавиши сброшены";
                    Refresh();
                });
        }

        private void OnEnable()
        {
            GameKeys.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            GameKeys.Changed -= Refresh;
            StopListening();
        }

        private void Listen(int index)
        {
            listening = index;
            listenFrame = Time.frameCount;
            message = null;
            Refresh();
        }

        private void StopListening()
        {
            listening = -1;
        }

        private void Update()
        {
            if (listening < 0) return;
            Keyboard keyboard = Keyboard.current;
            // Щелчок, начавший ожидание, клавишей не считается
            if (keyboard == null || Time.frameCount == listenFrame) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                StopListening();
                message = "Отменено";
                Refresh();
                return;
            }
            foreach (var control in keyboard.allKeys)
            {
                if (control == null || !control.wasPressedThisFrame) continue;
                Key key = control.keyCode;
                if (System.Array.IndexOf(Reserved, key) >= 0)
                {
                    message = "Клавиша " + GameKeys.KeyName(key) + " занята игрой — выберите другую";
                    Refresh();
                    return;
                }
                GameAction action = rows[listening].action;
                StopListening();
                message = GameKeys.Title(action) + " — " + GameKeys.KeyName(key);
                GameKeys.Set(action, key);
                Refresh();
                return;
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                Row row = rows[i];
                if (row.label != null) row.label.text = GameKeys.Title(row.action);
                if (row.value != null)
                    row.value.text = i == listening ? "[ нажмите клавишу ]" : "[ " + GameKeys.KeyName(row.action) + " ]";
            }
            if (hint == null) return;
            if (listening >= 0)
                hint.text = (message != null ? message + ". " : "") + "Нажмите новую клавишу для «" + GameKeys.Title(rows[listening].action) + "», Esc — отмена";
            else if (message != null)
                hint.text = message;
        }
    }
}
