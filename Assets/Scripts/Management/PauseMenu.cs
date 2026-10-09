using UnityEngine;
using UnityEngine.InputSystem;
using RacingProject.Network;

namespace RacingProject.Management
{
    // Меню паузы в режиме монитора: Esc во время раунда открывает и закрывает его. В одиночной игре время
    // и звук останавливаются, в сетевой игра идёт дальше (напарник продолжает). «В меню» и «Выйти из игры»
    // спрашивают подтверждение. Пока меню открыто, камера и пулемёт не слушают мышь (IsOpen)
    public class PauseMenu : MonoBehaviour
    {
        [Tooltip("Экран паузы (холст), выключен, пока игра идёт")]
        [SerializeField] private GameObject screen;
        [Tooltip("Строка под заголовком: в сетевой игре — что игра не останавливается")]
        [SerializeField] private TMPro.TMP_Text subtitle;
        [SerializeField] private ConfirmDialog confirm;
        [SerializeField] private LanLobby lobby;

        public static bool IsOpen { get; private set; }

        private void Awake()
        {
            SetOpen(false);
        }

        private void OnDestroy()
        {
            // Сцена перезагружается (выход в меню, перезапуск раунда): время и звук должны идти
            if (IsOpen)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
            IsOpen = false;
        }

        private void Update()
        {
            bool inRound = LocalPlayerRole.Current != PlayerRole.None && !ViewModeService.IsVR;
            if (!inRound)
            {
                if (IsOpen) SetOpen(false);
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

            if (confirm != null && confirm.IsOpen)
                confirm.Close();
            else
                SetOpen(!IsOpen);
        }

        public void Resume()
        {
            SetOpen(false);
        }

        public void ToMenu()
        {
            confirm.Show(LocalPlayerRole.IsSolo
                    ? "Выйти в меню? Текущий раунд будет потерян."
                    : "Выйти в меню? Раунд закончится и у напарника.",
                () =>
                {
                    SetOpen(false);
                    if (lobby != null) lobby.OnLeavePressed();
                });
        }

        public void Quit()
        {
            confirm.Show("Выйти из игры?", Application.Quit);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (screen != null) screen.SetActive(open);
            if (!open && confirm != null) confirm.Close();

            bool solo = LocalPlayerRole.IsSolo;
            if (subtitle != null)
                subtitle.text = solo ? "Игра остановлена" : "Сетевая игра не останавливается";
            Time.timeScale = open && solo ? 0f : 1f;
            AudioListener.pause = open && solo;

            // Курсор нужен меню; после паузы мышь снова управляет камерой
            if (LocalPlayerRole.Current != PlayerRole.None && !ViewModeService.IsVR)
            {
                Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = open;
            }
        }
    }
}
