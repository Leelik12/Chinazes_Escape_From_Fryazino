using System.Collections.Generic;
using UnityEngine;

namespace RacingProject.Car
{
    // Устойчивость машины на WheelCollider (и игроков, и врагов): стабилизаторы поперечной устойчивости
    // на каждой оси, прижим на скорости и выравнивание в полёте. Без этого машина валится в поворотах,
    // раскачивается и от кочки взлетает, кувыркаясь. Колёса находит сама и разбивает на оси по положению вдоль машины.
    // Выравнивание в полёте выключается, пока корпус чего-то касается: лежащую на крыше машину ставит CarRecovery.
    // Работает только там, где считается физика (у клиента машина кинематическая)
    [RequireComponent(typeof(Rigidbody))]
    public class CarStabilizer : MonoBehaviour
    {
        private class Axle
        {
            public WheelCollider left;
            public WheelCollider right;
        }

        [Tooltip("Жёсткость стабилизатора в долях жёсткости пружины подвески: 0 — нет, 1 — как пружина")]
        [SerializeField, Range(0f, 2f)] private float antiRoll = 0.9f;
        [Tooltip("Прижим: ускорение вниз на 100 км/ч, м/с²; растёт с квадратом скорости")]
        [SerializeField] private float downforceAt100 = 4f;
        [Tooltip("В полёте: гашение вращения по крену и тангажу, 1/с")]
        [SerializeField] private float airAngularDamping = 2.5f;
        [Tooltip("В полёте: насколько сильно машину доворачивает колёсами вниз, 1/с²")]
        [SerializeField] private float airLeveling = 4f;
        [Tooltip("На земле: гашение вращения по крену, 1/с — машина не раскачивается с борта на борт")]
        [SerializeField] private float rollDamping = 1.5f;

        private readonly List<Axle> axles = new List<Axle>();
        private Rigidbody body;
        private float lastContactTime = -1f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            FindAxles();
        }

        // Колёса с одинаковым положением вдоль машины — одна ось; слева и справа — по знаку x
        private void FindAxles()
        {
            var byZ = new List<List<WheelCollider>>();
            foreach (WheelCollider wheel in GetComponentsInChildren<WheelCollider>(true))
            {
                float z = transform.InverseTransformPoint(wheel.transform.position).z;
                List<WheelCollider> group = byZ.Find(g => Mathf.Abs(transform.InverseTransformPoint(g[0].transform.position).z - z) < 0.3f);
                if (group == null) byZ.Add(group = new List<WheelCollider>());
                group.Add(wheel);
            }
            foreach (List<WheelCollider> group in byZ)
            {
                if (group.Count != 2) continue;
                bool firstLeft = transform.InverseTransformPoint(group[0].transform.position).x < 0f;
                axles.Add(new Axle { left = firstLeft ? group[0] : group[1], right = firstLeft ? group[1] : group[0] });
            }
        }

        private void FixedUpdate()
        {
            if (body.isKinematic) return;

            int grounded = 0;
            foreach (Axle axle in axles)
                grounded += ApplyAntiRoll(axle);

            Vector3 velocity = body.linearVelocity;
            float speed = velocity.magnitude;
            Vector3 localAngular = transform.InverseTransformDirection(body.angularVelocity);

            if (grounded > 0)
            {
                // Прижим вдоль оси машины к дороге
                float k = speed / 27.78f;
                body.AddForce(-transform.up * downforceAt100 * k * k, ForceMode.Acceleration);
                // Гашение раскачки по крену (вращение вокруг продольной оси)
                body.AddRelativeTorque(new Vector3(0f, 0f, -localAngular.z * rollDamping), ForceMode.Acceleration);
            }
            else if (Time.fixedTime - lastContactTime > 0.1f)
            {
                // В полёте: гасим кувырки и доворачиваем колёсами вниз
                body.AddRelativeTorque(new Vector3(-localAngular.x, 0f, -localAngular.z) * airAngularDamping, ForceMode.Acceleration);
                Vector3 level = Vector3.Cross(transform.up, Vector3.up);
                body.AddTorque(level * airLeveling, ForceMode.Acceleration);
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            lastContactTime = Time.fixedTime;
        }

        // Возвращает число колёс оси на земле
        private int ApplyAntiRoll(Axle axle)
        {
            bool groundedLeft = axle.left.GetGroundHit(out WheelHit hitLeft);
            bool groundedRight = axle.right.GetGroundHit(out WheelHit hitRight);
            float travelLeft = groundedLeft ? Travel(axle.left, hitLeft) : 1f;
            float travelRight = groundedRight ? Travel(axle.right, hitRight) : 1f;

            // Сила, выравнивающая сжатие колёс оси: пружина × ход подвески, в долях
            float scale = axle.left.transform.lossyScale.y;
            float force = (travelLeft - travelRight) * axle.left.suspensionSpring.spring
                          * axle.left.suspensionDistance * scale * antiRoll;
            if (groundedLeft)
                body.AddForceAtPosition(axle.left.transform.up * -force, axle.left.transform.position);
            if (groundedRight)
                body.AddForceAtPosition(axle.right.transform.up * force, axle.right.transform.position);
            return (groundedLeft ? 1 : 0) + (groundedRight ? 1 : 0);
        }

        // Ход подвески: 0 — сжата полностью, 1 — разжата
        private static float Travel(WheelCollider wheel, WheelHit hit)
        {
            float scale = wheel.transform.lossyScale.y;
            float extension = Vector3.Dot(wheel.transform.position - hit.point, wheel.transform.up) - wheel.radius * scale;
            return Mathf.Clamp01(extension / Mathf.Max(0.001f, wheel.suspensionDistance * scale));
        }
    }
}
