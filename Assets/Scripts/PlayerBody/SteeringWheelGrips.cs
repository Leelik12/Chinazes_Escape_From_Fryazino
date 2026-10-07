using UnityEngine;
using RacingProject.Car;

namespace RacingProject.PlayerBody
{
    // Точки хвата водителя на ободе руля (дочерние объекты руля, по ним VRArmIK ставит кисти).
    // Руль поворачивается до 450°, и если кисти ездят вместе с ним, руки перекрещиваются и локти
    // выворачиваются. Поэтому кисти идут за рулём только до maxGripAngle, дальше обод проскальзывает под ними
    public class SteeringWheelGrips : MonoBehaviour
    {
        [SerializeField] private CarControllerSample car;
        [SerializeField] private Transform leftGrip;
        [SerializeField] private Transform rightGrip;
        [Tooltip("На сколько градусов кисти поворачиваются вместе с рулём")]
        [SerializeField] private float maxGripAngle = 70f;

        private Vector3 leftPosition, rightPosition;
        private Quaternion leftRotation, rightRotation;

        private void Awake()
        {
            if (leftGrip != null) { leftPosition = leftGrip.localPosition; leftRotation = leftGrip.localRotation; }
            if (rightGrip != null) { rightPosition = rightGrip.localPosition; rightRotation = rightGrip.localRotation; }
        }

        // Руль поворачивается в FixedUpdate, а IK читает точки во время анимации, после Update
        private void Update()
        {
            float angle = car != null ? car.SteeringWheelAngle : 0f;
            // Руль повёрнут на -angle вокруг своей оси z; возвращаем точки на разницу с ограниченным углом
            Quaternion back = Quaternion.Euler(0f, 0f, angle - Mathf.Clamp(angle, -maxGripAngle, maxGripAngle));
            Place(leftGrip, back, leftPosition, leftRotation);
            Place(rightGrip, back, rightPosition, rightRotation);
        }

        private static void Place(Transform grip, Quaternion back, Vector3 position, Quaternion rotation)
        {
            if (grip == null) return;
            grip.localPosition = back * position;
            grip.localRotation = back * rotation;
        }
    }
}
