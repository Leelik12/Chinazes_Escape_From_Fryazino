using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingProject.Desktop
{
    // Камера от третьего лица для одиночной игры: висит позади и выше машины и вращается мышью вокруг неё.
    // Направление обзора задаётся в мировых осях, поэтому прицел не сбивается, когда машина поворачивает;
    // если мышь не трогать и не стрелять, на ходу камера плавно заходит за корму. Если между машиной и камерой
    // стена, камера придвигается к машине. Esc освобождает курсор, клик снова захватывает
    [DefaultExecutionOrder(40)] // раньше DesktopGunnerAim (50), который целится из центра этой камеры
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Высота точки, вокруг которой вращается камера, над началом машины, м")]
        [SerializeField] private float pivotHeight = 3.5f;
        [SerializeField] private float distance = 11f;
        [SerializeField] private float minDistance = 2f;

        [Header("Мышь")]
        [Tooltip("Градусов на пиксель движения мыши")]
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 60f;
        [Tooltip("Наклон камеры вниз при включении, градусы")]
        [SerializeField] private float startPitch = 10f;

        [Header("Возврат за корму")]
        [Tooltip("Через сколько секунд без движения мыши и стрельбы камера заходит за корму")]
        [SerializeField] private float recenterDelay = 2.5f;
        [Tooltip("Скорость возврата, градусы в секунду")]
        [SerializeField] private float recenterSpeed = 90f;
        [Tooltip("Медленнее этого (км/ч) камера не возвращается: стоя можно спокойно осматриваться")]
        [SerializeField] private float recenterMinSpeedKmh = 10f;

        [Header("Препятствия")]
        [SerializeField] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private Rigidbody body;
        private float yaw;
        private float pitch;
        private float idleTime;

        private void OnEnable()
        {
            if (target != null)
            {
                body = target.GetComponentInParent<Rigidbody>();
                yaw = HeadingOf(target);
            }
            pitch = startPitch;
            idleTime = 0f;
            SetCursorLocked(true);
        }

        private void OnDisable()
        {
            SetCursorLocked(false);
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Mouse mouse = Mouse.current;
            bool active = false;
            if (mouse != null && UpdateCursorLock(mouse))
            {
                Vector2 delta = mouse.delta.ReadValue() * sensitivity;
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, minPitch, maxPitch);
                active = delta.sqrMagnitude > 0.0001f || mouse.leftButton.isPressed;
            }

            idleTime = active ? 0f : idleTime + Time.deltaTime;
            float speedKmh = body != null ? body.linearVelocity.magnitude * 3.6f : 0f;
            if (idleTime > recenterDelay && speedKmh > recenterMinSpeedKmh)
                yaw = Mathf.MoveTowardsAngle(yaw, HeadingOf(target), recenterSpeed * Time.deltaTime);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = target.position + Vector3.up * pivotHeight;
            Vector3 back = rotation * Vector3.back;
            transform.SetPositionAndRotation(pivot + back * FreeDistance(pivot, back), rotation);
        }

        // Насколько камера может отойти от точки вращения, не заходя за стену (своя машина не мешает)
        private float FreeDistance(Vector3 pivot, Vector3 direction)
        {
            int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
            float free = distance;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.transform.IsChildOf(target)) continue;
                // Касание в самом начале SphereCast отдаёт без точки: камера внутри препятствия
                if (hits[i].distance <= 0f) continue;
                free = Mathf.Min(free, hits[i].distance);
            }
            return Mathf.Max(minDistance, free);
        }

        private static float HeadingOf(Transform t)
        {
            Vector3 forward = t.forward;
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        // Возвращает true, если курсор захвачен и мышь управляет камерой
        private static bool UpdateCursorLock(Mouse mouse)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(false);
            else if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame)
                SetCursorLocked(true);

            return Cursor.lockState == CursorLockMode.Locked;
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
