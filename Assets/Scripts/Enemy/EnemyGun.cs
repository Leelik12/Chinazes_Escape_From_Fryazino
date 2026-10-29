using UnityEngine;
using Unity.Netcode;
using System.Collections;
using RacingProject.Audio;
using RacingProject.Props;

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
        [Tooltip("Стреляет, только если ствол смотрит на цель точнее этого, градусы")]
        public float aimTolerance = 6f;
        [Tooltip("Упреждение по скорости цели, с: пулемёт поворачивается с запаздыванием и без упреждения бьёт в след")]
        public float leadTime = 0.25f;
        [Tooltip("Точка прицеливания над началом машины игроков, м: середина кузова, а не днище")]
        public float aimHeight = 0.6f;

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
        private Rigidbody targetBody;
        private readonly RaycastHit[] hits = new RaycastHit[16];

        // Для сетевой интерполяции: поворот пулемёта задаёт сервер
        private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(Quaternion.identity);
        private float syncLerpSpeed = 10f;
        // Мелкие повороты не отправляем, чтобы не гонять переменную каждый тик
        private const float SendAngleThreshold = 0.5f;

        private void Start()
        {
            RefreshTarget();
        }

        // Цель ищется заново, если пропала (машину пересоздали, сцена догрузилась позже врага)
        private bool RefreshTarget()
        {
            Transform previous = target;
            if (!PlayerCarTarget.Refresh(ref target)) return false;
            if (target != previous || targetBody == null)
                targetBody = target.GetComponentInParent<Rigidbody>();
            return true;
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
            if (!RefreshTarget()) return;

            // Плавное наведение пулемёта на игрока с упреждением по его скорости
            Vector3 velocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
            Vector3 aimPoint = target.position + target.up * aimHeight + velocity * leadTime;
            Vector3 toAim = aimPoint - transform.position;
            if (toAim.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toAim), rotationSpeed * Time.deltaTime);

            // Стреляет только по цели в пределах дальности, когда ствол уже на неё смотрит и её ничто не закрывает
            if (Time.time < nextFireTime) return;
            float distance = toAim.magnitude;
            if (distance > range || Vector3.Angle(transform.forward, toAim) > aimTolerance) return;
            if (!HasLineOfSight(toAim / distance, distance)) return;

            nextFireTime = Time.time + fireRate;
            Fire();
        }

        // Между стволом и целью нет ничего, кроме самой машины игроков (своя машина не мешает)
        private bool HasLineOfSight(Vector3 direction, float distance)
        {
            if (!FindHit(direction, distance, out RaycastHit hit)) return true;
            return hit.collider.GetComponentInParent<PlayerHealth>() != null;
        }

        // Ближайшее попадание луча из ствола, пропуская коллайдеры своей машины
        private bool FindHit(Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            int count = Physics.RaycastNonAlloc(transform.position, direction, hits, distance, hitLayerMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Transform own = transform.root;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance >= best || hits[i].collider.transform.IsChildOf(own)) continue;
                best = hits[i].distance;
                nearest = hits[i];
            }
            return best < float.MaxValue;
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

            bool hasHit = FindHit(direction, range, out RaycastHit hit);
            if (hasHit)
            {
                PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
                if (player != null)
                {
                    player.RequestDamage((int)damage, transform.position);
                }

                ExplosiveBarrel barrel = hit.collider.GetComponentInParent<ExplosiveBarrel>();
                if (barrel != null)
                    barrel.RequestHit((int)damage);
            }

            // Без попадания трассер летит на дальность выстрела
            Vector3 endPoint = hasHit ? hit.point : transform.position + direction * range;
            ShootRpc(hasHit, endPoint, hit.normal);
        }

        [Tooltip("Ближе этого к слушателю пролетающая пуля свистит, м")]
        [SerializeField] private float whizzRadius = 7f;

        // Свист пули, пролетевшей рядом с игроком: в ближайшей к нему точке траектории. Попадания в упор
        // и выстрелы, прошедшие далеко, молчат
        private void PlayWhizz(Vector3 from, Vector3 to)
        {
            AudioListener listener = ActiveListener.Current;
            if (listener == null) return;
            Vector3 ear = listener.transform.position;
            Vector3 path = to - from;
            float length = path.magnitude;
            if (length < 1f || (ear - from).sqrMagnitude < 100f) return;
            float along = Vector3.Dot(ear - from, path / length);
            if (along <= 0f || along >= length - 1.5f) return;
            Vector3 closest = from + path / length * along;
            float distance = (closest - ear).magnitude;
            if (distance > whizzRadius || distance < 0.3f) return;
            SoundLibrary.Play(SoundLibrary.Sfx.BulletWhizz, closest, 1f - distance / whizzRadius * 0.7f);
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
            PlayWhizz(muzzleFlash != null ? muzzleFlash.transform.position : transform.position, endPoint);

            if (hasHit)
            {
                ShotEffects.SpawnImpact(hitEffectPrefabDust, hitEffectPrefabSparks, endPoint, hitNormal, 0.01f, hitEffectLifetime);
                // След ставим по лучу в точку попадания: в RPC приходит только точка и нормаль
                if (Physics.Raycast(endPoint + hitNormal * 0.05f, -hitNormal, out RaycastHit surface, 0.1f, hitLayerMask, QueryTriggerInteraction.Ignore))
                {
                    ShotEffects.SpawnBulletHole(surface);
                    LooseProp.PushHit(surface, -hitNormal);
                }
            }
        }
    }
}
