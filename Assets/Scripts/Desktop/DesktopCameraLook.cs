using UnityEngine;
using UnityEngine.InputSystem;
using RacingProject.Management;

namespace RacingProject.Desktop
{
    // Обзор мышью в режиме монитора. Поворот задаётся локально относительно родителя,
    // поэтому камера внутри машины поворачивается вместе с ней
    public class DesktopCameraLook : MonoBehaviour
    {
        [Tooltip("Градусов на пиксель движения мыши")]
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;
        [Tooltip("Ограничение поворота влево-вправо от начального направления, 0 — без ограничения")]
        [SerializeField] private float maxYaw = 0f;

        [Tooltip("Поворачивать только с зажатой правой кнопкой (курсор свободен для кликов по меню). " +
                 "Иначе курсор захватывается и мышь всегда поворачивает камеру")]
        [SerializeField] private bool requireRightButton = true;

        [Tooltip("Куда смотреть при старте; если не задано — текущий поворот")]
        [SerializeField] private Transform initialLookTarget;

        private float baseYaw;
        private float yaw;
        private float pitch;

        private void OnEnable()
        {
            if (initialLookTarget != null && transform.parent != null)
            {
                Vector3 localDirection = transform.parent.InverseTransformDirection(initialLookTarget.position - transform.position);
                transform.localRotation = Quaternion.LookRotation(localDirection);
            }

            Vector3 euler = transform.localEulerAngles;
            yaw = 0f;
            pitch = NormalizeAngle(euler.x);
            baseYaw = euler.y;

            SetCursorLocked(!requireRightButton);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (requireRightButton)
            {
                if (!mouse.rightButton.isPressed) return;
            }
            else if (!UpdateCursorLock(mouse))
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * sensitivity;
            yaw += delta.x;
            if (maxYaw > 0f)
                yaw = Mathf.Clamp(yaw, -maxYaw, maxYaw);
            pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);

            transform.localRotation = Quaternion.Euler(pitch, baseYaw + yaw, 0f);
        }

        // Курсор освобождает меню паузы (Esc) или переключение окна, клик снова захватывает.
        // Возвращает true, если курсор захвачен и мышь управляет камерой
        private static bool UpdateCursorLock(Mouse mouse)
        {
            // Esc обрабатывает меню паузы: оно и освобождает курсор
            if (PauseMenu.IsOpen) return false;
            if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);

            return Cursor.lockState == CursorLockMode.Locked;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
