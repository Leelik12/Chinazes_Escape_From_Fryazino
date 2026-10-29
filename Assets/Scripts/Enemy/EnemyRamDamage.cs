using RacingProject.Management;
using UnityEngine;
using Unity.Netcode;

namespace RacingProject.Enemy
{
    // Урон машине игроков от удара корпусом врага (таран Урала, удар бортом седана и УАЗа). Столкновения считает сервер:
    // физику и врагов, и машины игроков симулирует хост. Удар ранит и самого врага, а его ИИ узнаёт о попадании.
    // Таран игроков: если в удар быстрее влетела машина игроков, враг получает урон от её скорости (в борт — больше,
    // тяжёлый Урал — меньше), его разворачивает и отбрасывает, а сами игроки получают лишь часть обычного урона.
    // Улучшение «Усиленный бампер» усиливает таран
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
        [Tooltip("Доля урона удара, которую получает сам враг (тяжёлый таран почти не страдает)")]
        [SerializeField] private float selfDamageScale = 0.5f;

        [Header("Таран игроков")]
        [Tooltip("Таран засчитывается, если машина игроков влетела в врага хотя бы с такой скоростью, м/с")]
        [SerializeField] private float minRamSpeed = 6f;
        [Tooltip("Урон врагу на каждый м/с тарана сверх порога")]
        [SerializeField] private float ramDamagePerSpeed = 40f;
        [SerializeField] private int maxRamDamage = 1500;
        [Tooltip("Множитель урона при ударе в борт")]
        [SerializeField] private float sideHitBonus = 1.6f;
        [Tooltip("Доля обычного урона, которую получают игроки, когда таранят сами")]
        [SerializeField] private float rammerDamageScale = 0.35f;
        [Tooltip("Скорость разворота врага на каждый м/с тарана, рад/с")]
        [SerializeField] private float spinPerSpeed = 0.12f;
        [Tooltip("Отброс врага по ходу машины игроков, доля скорости тарана")]
        [SerializeField] private float ramPush = 0.25f;

        // Масса машины игроков, при которой урон тарана не меняется: легче враг — урон больше, тяжелее — меньше
        private const float ReferenceMass = 1500f;
        private static RadioChatter radio;

        private float nextHitTime;
        // Урон тарана наносится в следующем Update, а не в обработчике столкновения: гибель врага создаёт остов,
        // а удалять компоненты во время обработки контактов физики Unity не даёт
        private int pendingRamDamage;

        private void Update()
        {
            if (pendingRamDamage <= 0) return;
            int damage = pendingRamDamage;
            pendingRamDamage = 0;
            EnemyHealth health = GetComponent<EnemyHealth>();
            if (health != null)
                health.ApplyServerDamage(damage, NetworkManager != null ? NetworkManager.ServerClientId : 0);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (IsSpawned && !IsServer) return;
            if (Time.time < nextHitTime) return;

            PlayerHealth player = collision.collider.GetComponentInParent<PlayerHealth>();
            if (player == null || player.IsDead) return;

            float speed = collision.relativeVelocity.magnitude;
            if (speed < minImpactSpeed) return;

            nextHitTime = Time.time + hitCooldown;
            Rigidbody playerBody = collision.rigidbody;
            bool rammed = PlayerRammed(playerBody, collision, out float ramSpeed);
            int damage = Mathf.Min(maxDamage, Mathf.RoundToInt((speed - minImpactSpeed) * damagePerSpeed));
            if (rammed)
                damage = Mathf.RoundToInt(damage * rammerDamageScale * RunUpgrades.CrashDamageTaken);
            if (damage > 0)
                player.RequestDamage(damage, transform.position);
            EnemyHealth own = GetComponent<EnemyHealth>();
            if (rammed)
                Ram(playerBody, collision, ramSpeed, own);
            else if (damage > 0)
            {
                int selfDamage = Mathf.RoundToInt(damage * selfDamageScale);
                if (selfDamage > 0 && own != null)
                    pendingRamDamage += selfDamage;
            }
            EnemyCarController driver = GetComponent<EnemyCarController>();
            if (driver != null)
                driver.OnRamHit();
            if (rammed) return;

            if (playerBody != null && !playerBody.isKinematic)
            {
                // Толкаем от тарана вбок и чуть вверх
                Vector3 away = playerBody.worldCenterOfMass - transform.position;
                away.y = 0f;
                Vector3 direction = (away.normalized + Vector3.up * 0.25f).normalized;
                playerBody.AddForceAtPosition(direction * pushPerSpeed * speed, collision.GetContact(0).point, ForceMode.Impulse);
            }
        }

        // Кто кого: таран игроков — если их машина сближалась с врагом заметно быстрее, чем враг с ней
        private bool PlayerRammed(Rigidbody playerBody, Collision collision, out float ramSpeed)
        {
            ramSpeed = 0f;
            Rigidbody own = GetComponent<Rigidbody>();
            if (playerBody == null || own == null) return false;
            Vector3 toEnemy = own.worldCenterOfMass - playerBody.worldCenterOfMass;
            toEnemy.y = 0f;
            if (toEnemy.sqrMagnitude < 0.01f) return false;
            toEnemy.Normalize();
            ramSpeed = Vector3.Dot(playerBody.linearVelocity, toEnemy);
            float enemyClosing = Vector3.Dot(own.linearVelocity, -toEnemy);
            return ramSpeed >= minRamSpeed && ramSpeed > enemyClosing + 1f;
        }

        private void Ram(Rigidbody playerBody, Collision collision, float ramSpeed, EnemyHealth health)
        {
            Rigidbody own = GetComponent<Rigidbody>();
            Vector3 point = collision.GetContact(0).point;
            Vector3 hitDirection = playerBody.linearVelocity;
            hitDirection.y = 0f;
            hitDirection.Normalize();
            // В борт: удар поперёк корпуса врага
            bool side = Mathf.Abs(Vector3.Dot(transform.forward, hitDirection)) < 0.5f;
            float massFactor = Mathf.Clamp(ReferenceMass / own.mass, 0.25f, 1.2f);
            int damage = Mathf.Min(maxRamDamage, Mathf.RoundToInt((ramSpeed - minRamSpeed) * ramDamagePerSpeed
                * (side ? sideHitBonus : 1f) * massFactor * RunUpgrades.RamDamage));
            if (health != null && damage > 0)
                pendingRamDamage += damage;

            // Разворот: удар перед центром масс крутит машину в одну сторону, за ним — в другую
            float turn = Vector3.Cross(point - own.worldCenterOfMass, hitDirection).y;
            own.AddTorque(Vector3.up * Mathf.Sign(turn) * spinPerSpeed * ramSpeed * massFactor, ForceMode.VelocityChange);
            own.AddForce(hitDirection * ramPush * ramSpeed * massFactor, ForceMode.VelocityChange);

            if (radio == null) radio = FindFirstObjectByType<RadioChatter>();
            if (radio != null && damage >= 300) radio.SayCarEvent(RadioChatter.CarEvent.Ram);
        }
    }
}
