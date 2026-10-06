using UnityEngine;
using RacingProject.Desktop;
using RacingProject.Management;
using RacingProject.Network;

namespace RacingProject.Turret
{
    // Наведение пулемёта на крыше. Объект пулемёта ходит по кольцу люка вокруг стрелка (рысканье)
    // и наклоняется (тангаж). Наводит только стрелок: в VR — куда указывает правый контроллер,
    // в режиме монитора — в точку под прицелом. Позу пулемёта напарнику передаёт VRGun на том же объекте,
    // поэтому родитель пулемёта должен быть carRoot у VRGun
    [DefaultExecutionOrder(-10)] // раньше VRGun, который стреляет по направлению ствола
    public class TurretGunAim : MonoBehaviour
    {
        [Tooltip("Смещение пулемёта от центра кольца люка в его локальных осях при нулевом повороте")]
        [SerializeField] private Vector3 mountOffset = new Vector3(0f, 0f, 0.45f);

        [Header("Ограничения, градусы")]
        [SerializeField] private float maxYaw = 120f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 45f;
        [Tooltip("Скорость поворота, градусы в секунду")]
        [SerializeField] private float turnSpeed = 240f;

        [Header("Прицеливание")]
        [Tooltip("Правый контроллер стрелка: в VR пулемёт смотрит туда же, куда он")]
        [SerializeField] private Transform aimController;
        [Tooltip("Прицел мышью в режиме монитора")]
        [SerializeField] private DesktopGunnerAim desktopAim;

        private float yaw;
        private float pitch;

        private void Awake()
        {
            ApplyPose();
        }

        private void Update()
        {
            if (LocalPlayerRole.Current != PlayerRole.Gunner) return;

            Vector3 direction;
            if (!TryGetAimDirection(out direction)) return;

            // Направление в осях кольца люка: оно повёрнуто вместе с машиной
            Vector3 local = transform.parent.InverseTransformDirection(direction);
            float targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw);
            float targetPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);

            float step = turnSpeed * Time.deltaTime;
            yaw = Mathf.MoveTowards(yaw, targetYaw, step);
            pitch = Mathf.MoveTowards(pitch, targetPitch, step);
            ApplyPose();
        }

        private bool TryGetAimDirection(out Vector3 direction)
        {
            bool useDesktopAim = !ViewModeService.IsVR && desktopAim != null && desktopAim.isActiveAndEnabled;
            if (useDesktopAim)
            {
                // Из центра кольца: так угол не зависит от текущего положения пулемёта на кольце
                direction = desktopAim.AimPoint - transform.parent.position;
            }
            else if (aimController != null)
            {
                direction = aimController.forward;
            }
            else
            {
                direction = Vector3.zero;
            }
            return direction.sqrMagnitude > 0.0001f;
        }

        private void ApplyPose()
        {
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            transform.localPosition = yawRotation * mountOffset;
            transform.localRotation = yawRotation * Quaternion.Euler(-pitch, 0f, 0f);
        }
    }
}
