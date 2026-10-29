using System;
using LogitechG29.Sample.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingProject.Management
{
    // Действия водителя на клавиатуре, которые можно переназначить в настройках
    public enum GameAction
    {
        Throttle,
        Brake,
        SteerLeft,
        SteerRight,
        Handbrake,
        Nitro,
        Recover,
        Lights,
        Radio,
    }

    // Клавиши действий: по умолчанию W/S/A/D, пробел — ручник, левый Shift — нитро, R — поставить на колёса, L — фары,
    // M — магнитола (свои треки вместо музыки боя).
    // Выбор игрока хранится в PlayerPrefs. Стрелки работают всегда как запасные клавиши руля, газа и тормоза
    public static class GameKeys
    {
        private const string PrefsPrefix = "Key_";

        private static readonly Key[] Defaults = { Key.W, Key.S, Key.A, Key.D, Key.Space, Key.LeftShift, Key.R, Key.L, Key.M };
        private static readonly Key[] Alternates = { Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.None, Key.None, Key.None, Key.None, Key.None };
        private static readonly string[] Titles = { "Газ", "Тормоз / назад", "Руль влево", "Руль вправо", "Ручник", "Нитро", "Поставить на колёса", "Фары", "Магнитола" };

        private static Key[] current;

        // Газ, тормоз и руль читаются через действия Input System (вместе с педалями и рулём G29):
        // их клавиши меняются переопределением привязок с этими путями
        private static readonly string[] ActionPaths = { "<Keyboard>/w", "<Keyboard>/s", "<Keyboard>/a", "<Keyboard>/d" };

        // Клавиши поменялись (настройки)
        public static event Action Changed;

        public static int Count => Defaults.Length;

        public static string Title(GameAction action) => Titles[(int)action];

        public static Key Get(GameAction action)
        {
            Load();
            return current[(int)action];
        }

        public static Key Default(GameAction action) => Defaults[(int)action];

        // Нажата ли клавиша действия (или её запасная стрелка)
        public static bool IsPressed(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            Key alternate = Alternates[(int)action];
            return Pressed(keyboard, Get(action)) || Pressed(keyboard, alternate);
        }

        public static bool WasPressedThisFrame(GameAction action)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            Key key = Get(action);
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        // Назначает клавишу; если она была у другого действия, они меняются клавишами
        public static void Set(GameAction action, Key key)
        {
            Load();
            int index = (int)action;
            for (int i = 0; i < current.Length; i++)
                if (i != index && current[i] == key)
                    current[i] = current[index];
            current[index] = key;
            Save();
        }

        public static void ResetToDefaults()
        {
            current = (Key[])Defaults.Clone();
            Save();
        }

        // Переносит клавиши газа, тормоза и руля в действия Input System
        public static void ApplyTo(InputControllerReader reader)
        {
            if (reader == null) return;
            for (int i = 0; i < ActionPaths.Length; i++)
                reader.OverrideKeyboardBinding(ActionPaths[i], ControlPath(Get((GameAction)i)));
        }

        // Подсказка с клавишей действия: «<b>W</b>»
        public static string Tag(GameAction action) => "<b>" + KeyName(action) + "</b>";

        // Название клавиши для подсказок: «W», «Пробел», «Левый Shift»
        public static string KeyName(GameAction action) => KeyName(Get(action));

        public static string KeyName(Key key)
        {
            switch (key)
            {
                case Key.Space: return "Пробел";
                case Key.LeftShift: return "Левый Shift";
                case Key.RightShift: return "Правый Shift";
                case Key.LeftCtrl: return "Левый Ctrl";
                case Key.RightCtrl: return "Правый Ctrl";
                case Key.LeftAlt: return "Левый Alt";
                case Key.RightAlt: return "Правый Alt";
                case Key.Tab: return "Tab";
                case Key.Enter: return "Enter";
                case Key.Backspace: return "Backspace";
                case Key.UpArrow: return "↑";
                case Key.DownArrow: return "↓";
                case Key.LeftArrow: return "←";
                case Key.RightArrow: return "→";
                case Key.None: return "—";
            }
            // Буквы и цифры — по физической клавише, а не по раскладке: при русской раскладке W не станет «Ц»
            if (key >= Key.A && key <= Key.Z) return key.ToString();
            if (key >= Key.Digit1 && key <= Key.Digit0) return key == Key.Digit0 ? "0" : ((int)(key - Key.Digit1) + 1).ToString();
            Keyboard keyboard = Keyboard.current;
            string display = keyboard != null ? keyboard[key].displayName : null;
            return string.IsNullOrEmpty(display) ? key.ToString() : display.ToUpperInvariant();
        }

        // Путь клавиши для Input System: "<Keyboard>/w"
        public static string ControlPath(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && key != Key.None) return "<Keyboard>/" + keyboard[key].name;
            return "<Keyboard>/" + key.ToString().ToLowerInvariant();
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].isPressed;
        }

        private static void Load()
        {
            if (current != null) return;
            current = new Key[Defaults.Length];
            for (int i = 0; i < Defaults.Length; i++)
            {
                int stored = PlayerPrefs.GetInt(PrefsPrefix + (GameAction)i, (int)Defaults[i]);
                current[i] = Enum.IsDefined(typeof(Key), stored) ? (Key)stored : Defaults[i];
            }
        }

        private static void Save()
        {
            for (int i = 0; i < current.Length; i++)
                PlayerPrefs.SetInt(PrefsPrefix + (GameAction)i, (int)current[i]);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
