using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace RacingProject.Enemy
{
    public class EnemyGun : NetworkBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Gun Settings")]
        public float fireRate = 0.3f;
        public float damage = 10f;
        public float range = 50f;
        [Tooltip("Максимальный угол разброса в градусах")]
        public float spreadAngle = 5f;

        [Header("Rotation")]
        public float rotationSpeed = 5f;

        [Header("Muzzle Effects")]
        public ParticleSystem muzzleFlash;
        public Light muzzleLight;
        public float lightDuration = 0.05f;

        [Header("Sound")]
        public AudioSource shotAudio;
        public AudioClip shotSound;

        [Header("Hit Effects")]
        public GameObject hitEffectPrefabDust;
        public GameObject hitEffectPrefabSparks;
        public float hitEffectLifetime = 2f;
        public LayerMask hitLayerMask = ~0;
        [Tooltip("Трассер — каждая такая пуля (0 — без трассеров)")]
        public int tracerEvery = 2;
        [Tooltip("Цвет трассера, HDR: зелёный, чтобы встречный огонь отличался от своего")]
        [ColorUsage(false, true)] public Color tracerColor = new Color(0.8f, 5f, 0.9f);

        private float nextFireTime = 0f;
        private int shotsSinceTracer;

        // Для сетевой интерполяции: поворот пулемёта задаёт сервер
        private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(Quaternion.identity);
        private float syncLerpSpeed = 10f;
        // Мелкие повороты не отправляем, чтобы не гонять переменную каждый тик
        private const float SendAngleThreshold = 0.5f;

        private void Start()
        {
            if (target == null)
            {
                GameObject playerCar = GameObject.FindWithTag(Tags.Car);
                if (playerCar != null)
                    target = playerCar.transform;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                networkRotation.Value = transform.rotation;
            else
                transform.rotation = networkRotation.Value;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                HandleTurretLogic();

                if (Quaternion.Angle(networkRotation.Value, transform.rotation) > SendAngleThreshold)
                    networkRotation.Value = transform.rotation;
            }
            else
            {
                // плавная интерполяция поворота
                transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation.Value, Time.deltaTime * syncLerpSpeed);
            }
        }

        private void HandleTurretLogic()
        {
            if (target == null) return;

            // Плавное наведение пулемета на игрока
            Vector3 targetDir = (target.position - transform.position).normalized;
            Quaternion lookRot = Quaternion.LookRotation(targetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);

            // Стрельба
            if (Time.time >= nextFireTime)
            {
                nextFireTime = Time.time + fireRate;
                Fire();
            }
        }

        // Попадание и урон считает только сервер, остальным рассылается результат для эффектов,
        // иначе каждый клиент бросал свой разброс и наносил урон повторно
        private void Fire()
        {
            // Разброс относительно направления ствола
            Vector3 direction = transform.rotation * Quaternion.Euler(
                Random.Range(-spreadAngle, spreadAngle),
                Random.Range(-spreadAngle, spreadAngle),
                0) * Vector3.forward;

            bool hasHit = Physics.Raycast(transform.position, direction, out RaycastHit hit, range, hitLayerMask, QueryTriggerInteraction.Ignore);
            if (hasHit)
            {
                PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    player.RequestDamage((int)damage);
                }
            }

            // Без попадания трассер летит на дальность выстрела
            Vector3 endPoint = hasHit ? hit.point : transform.position + direction * range;
            ShootRpc(hasHit, endPoint, hit.normal);
        }

        [Rpc(SendTo.Everyone)]
        private void ShootRpc(bool hasHit, Vector3 endPoint, Vector3 hitNormal)
        {
            ShotEffects.PlayMuzzle(this, muzzleFlash, muzzleLight, lightDuration);
            if (shotAudio != null && shotSound != null)
                shotAudio.PlayOneShot(shotSound);

            if (tracerEvery > 0 && ++shotsSinceTracer >= tracerEvery)
            {
                shotsSinceTracer = 0;
                Vector3 muzzle = muzzleFlash != null ? muzzleFlash.transform.position : transform.position;
                ShotEffects.SpawnTracer(muzzle, endPoint, tracerColor);
            }

            if (hasHit)
            {
                ShotEffects.SpawnImpact(hitEffectPrefabDust, hitEffectPrefabSparks, endPoint, hitNormal, 0.01f, hitEffectLifetime);
                // След ставим по лучу в точку попадания: в RPC приходит только точка и нормаль
                if (Physics.Raycast(endPoint + hitNormal * 0.05f, -hitNormal, out RaycastHit surface, 0.1f, hitLayerMask, QueryTriggerInteraction.Ignore))
                    ShotEffects.SpawnBulletHole(surface);
            }
        }
    }
}
