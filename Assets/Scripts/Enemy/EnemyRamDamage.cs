using UnityEngine;
using Unity.Netcode;

namespace RacingProject.Enemy
{
    // Урон машине игроков от удара корпусом (Урал-таран). Столкновения считает сервер:
    // физику и врагов, и машины игроков симулирует хост
    public class EnemyRamDamage : NetworkBehaviour
    {
        [Tooltip("Удары со скоростью сближения меньше этой не ранят, м/с")]
        [SerializeField] private float minImpactSpeed = 5f;
        [Tooltip("Урон на каждый м/с сверх порога")]
        [SerializeField] private float damagePerSpeed = 14f;
        [SerializeField] private int maxDamage = 320;
        [Tooltip("Пауза между засчитанными ударами, с: машины после удара ещё трутся друг о друга")]
        [SerializeField] private float hitCooldown = 1.2f;
        [Tooltip("Дополнительный толчок машине игроков на каждый м/с удара, чтобы удар грузовика ощущался")]
        [SerializeField] private float pushPerSpeed = 250f;

        private float nextHitTime;

        private void OnCollisionEnter(Collision collision)
        {
            if (IsSpawned && !IsServer) return;
            if (Time.time < nextHitTime) return;

            PlayerHealth player = collision.collider.GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            nextHitTime = Time.time + hitCooldown;
            int damage = Mathf.Min(maxDamage, Mathf.RoundToInt((speed - minImpactSpeed) * damagePerSpeed));
            if (damage > 0)
                player.RequestDamage(damage);

            Rigidbody playerBody = collision.rigidbody;
            if (playerBody != null && !playerBody.isKinematic)
            {
                // Толкаем от тарана вбок и чуть вверх
                Vector3 away = playerBody.worldCenterOfMass - transform.position;
                away.y = 0f;
                Vector3 direction = (away.normalized + Vector3.up * 0.25f).normalized;
                playerBody.AddForceAtPosition(direction * pushPerSpeed * speed, collision.GetContact(0).point, ForceMode.Impulse);
            }
        }
    }
}
