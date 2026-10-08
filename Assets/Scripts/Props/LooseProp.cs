using System.Collections.Generic;
using UnityEngine;

namespace RacingProject.Props
{
    // Незакреплённый предмет разрухи (конус, бочка, заграждение): стоит неподвижно, пока его не заденет
    // машина, пуля или взрыв, дальше летит по физике. Физику предметов каждый игрок считает у себя:
    // на игру они не влияют, и мелкие расхождения между игроками незаметны
    [RequireComponent(typeof(Rigidbody))]
    public class LooseProp : MonoBehaviour
    {
        [Tooltip("Сила толчка от попадания пули")]
        [SerializeField] private float bulletImpulse = 6f;

        private static readonly HashSet<Rigidbody> BlastBodies = new HashSet<Rigidbody>();
        private Rigidbody body;

        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        // Спящее тело не считается физикой, пока его не разбудит касание или сила
        private void Start()
        {
            body.Sleep();
        }

        public void PushFromBullet(Vector3 point, Vector3 direction)
        {
            body.AddForceAtPosition(direction.normalized * bulletImpulse, point, ForceMode.Impulse);
        }

        // Толчок от выстрела по этому лучу, если пуля попала в незакреплённый предмет
        public static void PushHit(RaycastHit hit, Vector3 direction)
        {
            if (hit.rigidbody == null) return;
            LooseProp prop = hit.rigidbody.GetComponent<LooseProp>();
            if (prop != null)
                prop.PushFromBullet(hit.point, direction);
        }

        // Взрывная волна раскидывает предметы вокруг. Вызывается у каждого игрока в точке взрыва
        // speed — прирост скорости у самого центра взрыва, м/с
        public static void Blast(Vector3 point, float radius, float speed)
        {
            BlastBodies.Clear();
            foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                Rigidbody rb = collider.attachedRigidbody;
                // У предмета из нескольких коллайдеров толкаем тело один раз
                if (rb == null || rb.isKinematic || !BlastBodies.Add(rb) || rb.GetComponent<LooseProp>() == null) continue;
                rb.AddExplosionForce(speed, point, radius, 1f, ForceMode.VelocityChange);
            }
        }
    }
}
