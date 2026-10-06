using UnityEngine;
using UnityEngine.InputSystem;
using RacingProject.Management;
using RacingProject.Network;

namespace RacingProject.Hud
{
    // Подсказка по управлению водителя: клавиатура и руль Logitech G29.
    // Привязки взяты из InputController.inputactions, CarNitro и CarControllerSample.
    // Стрелку панель не показывается
    public class DriverControlsHud : MonoBehaviour
    {
        [SerializeField] private Key toggleKey = Key.F1;
        [SerializeField] private bool expanded = true;

        private HudPanel panel;

        private void Awake()
        {
            panel = new HudPanel("УПРАВЛЕНИЕ ВОДИТЕЛЯ", toggleKey, HudCorner.BottomLeft, 0, 600f, expanded);
        }

        private void OnEnable()
        {
            panel.Show();
        }

        private void OnDisable()
        {
            panel.Hide();
        }

        private void Update()
        {
            bool visible = LocalPlayerRole.Current != PlayerRole.Gunner;
            if (visible) panel.Show();
            else panel.Hide();

            if (visible)
                panel.HandleInput();
        }

        private void OnGUI()
        {
            if (LocalPlayerRole.Current == PlayerRole.Gunner) return;
            panel.Draw(BuildText());
        }

        private static string BuildText()
        {
            string look = ViewModeService.IsVR
                ? "Обзор — поворот головы"
                : "Обзор — <b>мышь</b>; <b>Esc</b> — отпустить курсор, <b>ЛКМ</b> — захватить";

            return
                "<color=#9a9a9a>действие — клавиатура · руль G29</color>\n" +
                "Газ / тормоз — <b>W</b> / <b>S</b> · правая / средняя педаль\n" +
                "Руль — <b>A</b> / <b>D</b> · руль\n" +
                "Сцепление — <b>F</b> · левая педаль\n" +
                "Передачи 1–6 — <b>1</b>–<b>6</b> · коробка 1–6\n" +
                "Задняя передача — <b>7</b> · коробка R\n" +
                "Нитро — <b>левый Shift</b> · кнопка Right Bumper\n" +
                "Толчок вперёд / назад, если застрял — крестовина вверх / вниз\n" +
                look + "\n" +
                "<color=#ffd27a>Передача включается при выжатом сцеплении: сначала отпустите F, потом цифру</color>\n" +
                "<color=#9a9a9a>HUD: F1 — управление, F2 — 2DOF, F3 — FutuRift, F4 — система</color>";
        }
    }
}
