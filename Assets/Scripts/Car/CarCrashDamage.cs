using RacingProject.Enemy;
using RacingProject.Management;
using RacingProject.Network;
using RacingProject.Props;
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
        [SerializeField] private float damagePerSpeed = 5f;
        [SerializeField] private int maxDamage = 100;
        [Tooltip("Предметы с Rigidbody легче этого сбиваются без урона, кг")]
        [SerializeField] private float harmlessMass = 300f;
        [Tooltip("Пауза между засчитанными ударами, с")]
        [SerializeField] private float hitCooldown = 0.6f;

        private float nextHitTime;

        // Скорость перед шагом физики: удар её уже погасил, а BreakableProp возвращает машине её долю
        public Vector3 LastVelocity { get; private set; }
        private Rigidbody body;

        private void Awake()
        {
            if (health == null)
                health = GetComponent<PlayerHealth>();
            body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (body != null) LastVelocity = body.linearVelocity;
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
            // Постройка, которая от такого удара разваливается, машину не ранит
            BreakableProp breakable = collision.collider.GetComponentInParent<BreakableProp>();
            if (breakable != null && (breakable.Broken || speed >= breakable.BreakSpeed)) return;

            nextHitTime = Time.time + hitCooldown;
            // Улучшение «усиленный бампер» снижает урон от ударов
            int damage = Mathf.Min(maxDamage, Mathf.RoundToInt((speed - minImpactSpeed) * damagePerSpeed * RunUpgrades.CrashDamageTaken));
            if (damage > 0)
                health.RequestDamage(damage, contact.point);
        }
    }
}
