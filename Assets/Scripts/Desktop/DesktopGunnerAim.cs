using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingProject.Desktop
{
    // Прицеливание стрелка в режиме монитора. Объекты контроллеров остаются, но их позу задаёт
    // этот компонент вместо трекинга: за контроллерами следуют сетевые прокси рук, по ним IK ставит
    // руки тела, а в правой руке закреплён пистолет. Поэтому напарник видит, куда целится стрелок.
    // Точка прицела — то, что под центром экрана; стрельба идёт из ствола в эту точку
    [DefaultExecutionOrder(50)] // раньше NetworkedTransformFollower (100), который копирует позу контроллеров в прокси
    public class DesktopGunnerAim : MonoBehaviour
    {
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform rightController;
        [SerializeField] private Transform leftController;
        [Tooltip("Точка вылета пули на пистолете (firePoint у VRGun)")]
        [SerializeField] private Transform muzzle;

        [Header("Позы рук относительно камеры (в единицах рига)")]
        [SerializeField] private Vector3 rightHandOffset = new Vector3(0.15f, -0.15f, 0.35f);
        [SerializeField] private Vector3 leftHandOffset = new Vector3(-0.15f, -0.35f, 0.15f);
        [SerializeField] private Vector3 leftHandEuler = Vector3.zero;

        [Header("Прицел")]
        [SerializeField] private float aimDistance = 150f;
        [SerializeField] private LayerMask aimMask = ~0;

        public Vector3 AimPoint { get; private set; }
        public bool FirePressed => Mouse.current != null && Mouse.current.leftButton.isPressed;

        private void LateUpdate()
        {
            if (aimCamera == null) return;

            Transform cam = aimCamera.transform;
            AimPoint = Physics.Raycast(cam.position, cam.forward, out RaycastHit hit, aimDistance, aimMask, QueryTriggerInteraction.Ignore)
                ? hit.point
                : cam.position + cam.forward * aimDistance;

            if (rightController != null)
            {
                rightController.position = cam.TransformPoint(rightHandOffset);
                AimMuzzle(cam);
            }

            if (leftController != null)
            {
                leftController.position = cam.TransformPoint(leftHandOffset);
                leftController.rotation = cam.rotation * Quaternion.Euler(leftHandEuler);
            }
        }

        // Связь «контроллер -> ствол» проходит через прокси, IK и кость руки, поэтому заранее неизвестна.
        // Ствол отражает позу контроллера с прошлого кадра: доворачиваем контроллер на ошибку ствола,
        // и поза сходится за кадр, пока рука следует за контроллером
        private void AimMuzzle(Transform cam)
        {
            if (muzzle == null) return;

            Vector3 direction = AimPoint - muzzle.position;
            if (direction.sqrMagnitude < 0.0001f) return;

            Quaternion desired = Quaternion.LookRotation(direction, cam.up);
            Quaternion correction = desired * Quaternion.Inverse(muzzle.rotation);
            rightController.rotation = correction * rightController.rotation;
        }
    }
}
