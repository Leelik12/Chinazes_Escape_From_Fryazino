using RacingProject.Enemy;
using RacingProject.Network;
using UnityEngine;

namespace RacingProject.Car
{
    // Урон машине игроков в жёстких авариях: столбы, стены, дома, тяжёлые предметы. Считает тот, кто симулирует
    // физику (хост), урон расходится через PlayerHealth. Удары врагов считает их EnemyRamDamage, здесь они пропускаются
    public class CarCrashDamage : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [Tooltip("Удары со скоростью сближения (по нормали) меньше этой не ранят, м/с")]
        [SerializeField] private float minImpactSpeed = 9f;
        [Tooltip("Урон на каждый м/с сверх порога")]
        [SerializeField] private float damagePerSpeed = 10f;
        [SerializeField] private int maxDamage = 200;
        [Tooltip("Предметы с Rigidbody легче этого сбиваются без урона, кг")]
        [SerializeField] private float harmlessMass = 300f;
        [Tooltip("Пауза между засчитанными ударами, с")]
        [SerializeField] private float hitCooldown = 0.6f;

        private float nextHitTime;

        private void Awake()
        {
            if (health == null)
                health = GetComponent<PlayerHealth>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!LocalPlayerRole.SimulatesPhysics || health == null || health.IsDead) return;
            if (Time.time < nextHitTime || collision.contactCount == 0) return;
            if (collision.collider.GetComponentInParent<EnemyRamDamage>() != null) return;

            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic && other.mass < harmlessMass) return;

            ContactPoint contact = collision.GetContact(0);
            // Касание днищем (приземление, бордюр) не считается
            if (Mathf.Abs(Vector3.Dot(contact.normal, transform.up)) > 0.75f) return;
            float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            if (speed < minImpactSpeed) return;

            nextHitTime = Time.time + hitCooldown;
            int damage = Mathf.Min(maxDamage, Mathf.RoundToInt((speed - minImpactSpeed) * damagePerSpeed));
            if (damage > 0)
                health.RequestDamage(damage);
        }
    }
}
