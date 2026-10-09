using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using RacingProject.Props;

namespace RacingProject.Enemy
{
    // Гранатомёт РПГ на врага (УАЗ). Наводится и стреляет сервер: медленная граната с упреждением
    // по скорости игрока и небольшой ошибкой, от неё можно увернуться. Гранату в полёте все игроки
    // ведут сами по одной и той же прямой, попадание и урон считает сервер, взрыв рассылается всем
    public class EnemyRocketLauncher : NetworkBehaviour
    {
        [Header("Target")]
        public Transform target;
        [Tooltip("Куда целиться относительно начала машины игроков, м вверх")]
        [SerializeField] private float aimHeight = 1.5f;

        [Header("Стрельба")]
        [Tooltip("Урон в центре взрыва; растёт с волнами в EnemyManager")]
        public float damage = 220f;
        [SerializeField] private float splashRadius = 7f;
        [Tooltip("Доля урона на краю радиуса взрыва")]
        [Range(0f, 1f)] [SerializeField] private float edgeDamage = 0.3f;
        [Tooltip("Толчок машины игроков взрывом")]
        [SerializeField] private float explosionPush = 9000f;
        [SerializeField] private float reloadTime = 7f;
        [Tooltip("Пауза перед первым выстрелом после появления, с")]
        [SerializeField] private float firstShotDelay = 4f;
        [SerializeField] private float minRange = 15f;
        [SerializeField] private float maxRange = 140f;
        [SerializeField] private float rocketSpeed = 45f;
        [Tooltip("Доля упреждения по скорости игрока: меньше 1 — граната отстаёт, и от неё можно уйти рывком")]
        [Range(0f, 1.5f)] [SerializeField] private float leadAccuracy = 0.75f;
        [Tooltip("Разброс в градусах")]
        [SerializeField] private float spreadAngle = 1.2f;
        [Tooltip("Стреляет, только когда отклонение от точки прицеливания меньше этого угла")]
        [SerializeField] private float aimTolerance = 4f;
        [SerializeField] private float rotationSpeed = 2.5f;
        [SerializeField] private LayerMask hitLayerMask = ~0;

        [Header("Вид и звук")]
        [SerializeField] private Transform firePoint;
        [Tooltip("Граната на гранатомёте: видна, пока он заряжен")]
        [SerializeField] private GameObject loadedRocket;
        [SerializeField] private EnemyRocket rocketPrefab;
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Выхлоп назад при выстреле")]
        [SerializeField] private ParticleSystem backblast;
        [SerializeField] private AudioSource launchAudio;
        [SerializeField] private AudioClip launchSound;

        private readonly NetworkVariable<Quaternion> networkRotation = new NetworkVariable<Quaternion>(Quaternion.identity);
        private readonly NetworkVariable<bool> loaded = new NetworkVariable<bool>(true);
        private const float SendAngleThreshold = 0.5f;
        private const float SyncLerpSpeed = 10f;

        // Не стрелять, пока стрелок не высунулся из окна (ставит EnemyWindowGunner)
        public bool HoldFire { get; set; }

        private readonly Dictionary<int, EnemyRocket> rockets = new Dictionary<int, EnemyRocket>();
        private readonly HashSet<PlayerHealth> splashTargets = new HashSet<PlayerHealth>();
        private Rigidbody targetBody;
        private float nextFireTime;
        private int rocketCounter;

        private void Start()
        {
            if (target == null)
            {
                GameObject playerCar = GameObject.FindWithTag(Tags.Car);
                if (playerCar != null)
                    target = playerCar.transform;
            }
            if (target != null)
                targetBody = target.GetComponentInParent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            loaded.OnValueChanged += OnLoadedChanged;
            OnLoadedChanged(false, loaded.Value);

            if (IsServer)
            {
                networkRotation.Value = transform.rotation;
                nextFireTime = Time.time + firstShotDelay;
            }
            else
            {
                transform.rotation = networkRotation.Value;
            }
        }

        public override void OnNetworkDespawn()
        {
            loaded.OnValueChanged -= OnLoadedChanged;
            // Вместе с машиной пропадают и гранаты в полёте: взрыв без гранатомёта уже некому разослать
            foreach (EnemyRocket rocket in rockets.Values)
                if (rocket != null)
                    rocket.Remove();
            rockets.Clear();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                HandleAiming();
                if (Quaternion.Angle(networkRotation.Value, transform.rotation) > SendAngleThreshold)
                    networkRotation.Value = transform.rotation;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation.Value, Time.deltaTime * SyncLerpSpeed);
            }
        }

        private void HandleAiming()
        {
            if (target == null) return;

            Vector3 muzzle = firePoint != null ? firePoint.position : transform.position;
            Vector3 aimPoint = target.position + Vector3.up * aimHeight;
            float distance = Vector3.Distance(muzzle, aimPoint);
            // Упреждение: где будет игрок, когда долетит граната
            if (targetBody != null)
                aimPoint += targetBody.linearVelocity * (distance / rocketSpeed) * leadAccuracy;

            Vector3 aimDirection = (aimPoint - muzzle).normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimDirection), rotationSpeed * Time.deltaTime);

            if (!loaded.Value)
            {
                if (Time.time >= nextFireTime)
                    loaded.Value = true;
                return;
            }

            if (HoldFire || Time.time < nextFireTime || distance < minRange || distance > maxRange) return;
            if (Vector3.Angle(transform.forward, aimDirection) > aimTolerance) return;
            if (!HasLineOfSight(muzzle, aimPoint)) return;

            Fire(muzzle);
        }

        // Между стволом и целью нет ничего, кроме самой машины игроков и своей машины
        private bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (!FindHit(from, delta, 0.1f, out RaycastHit hit))
                return true;
            return hit.collider.GetComponentInParent<PlayerHealth>() != null;
        }

        private void Fire(Vector3 muzzle)
        {
            Vector3 direction = transform.rotation * Quaternion.Euler(
                Random.Range(-spreadAngle, spreadAngle),
                Random.Range(-spreadAngle, spreadAngle),
                0f) * Vector3.forward;

            loaded.Value = false;
            nextFireTime = Time.time + reloadTime;
            FireRpc(++rocketCounter, muzzle, direction);
        }

        [Rpc(SendTo.Everyone)]
        private void FireRpc(int id, Vector3 position, Vector3 direction)
        {
            if (rocketPrefab != null)
            {
                EnemyRocket rocket = Instantiate(rocketPrefab, position, Quaternion.LookRotation(direction));
                rocket.Launch(this, id, direction, rocketSpeed, maxRange / rocketSpeed + 1f);
                rockets[id] = rocket;
            }

            if (backblast != null)
                backblast.Play();
            if (launchAudio != null && launchSound != null)
                launchAudio.PlayOneShot(launchSound);
        }

        // Для гранаты на сервере: первое препятствие на шаге полёта, кроме своей машины
        public bool FindHit(Vector3 from, Vector3 step, float radius, out RaycastHit nearest)
        {
            nearest = default;
            float length = step.magnitude;
            if (length < 0.0001f) return false;

            bool found = false;
            Transform ownRoot = transform.root;
            foreach (RaycastHit hit in Physics.SphereCastAll(from, radius, step / length, length, hitLayerMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(ownRoot)) continue;
                // Касание в самом начале шага SphereCast отдаёт без точки
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        // Только на сервере
        public void RocketHit(EnemyRocket rocket, Vector3 point, Vector3 normal)
        {
            splashTargets.Clear();
            foreach (Collider collider in Physics.OverlapSphere(point, splashRadius, hitLayerMask, QueryTriggerInteraction.Ignore))
            {
                PlayerHealth player = collider.GetComponentInParent<PlayerHealth>();
                if (player != null)
                    splashTargets.Add(player);
            }

            foreach (PlayerHealth player in splashTargets)
            {
                // Расстояние до ближайшего коллайдера машины, а не до её центра: прямое попадание — полный урон
                float closest = splashRadius;
                foreach (Collider collider in player.GetComponentsInChildren<Collider>())
                {
                    if (collider.isTrigger || collider is WheelCollider) continue;
                    closest = Mathf.Min(closest, Vector3.Distance(point, collider.ClosestPoint(point)));
                }
                float falloff = Mathf.Lerp(1f, edgeDamage, closest / splashRadius);
                player.RequestDamage(Mathf.RoundToInt(damage * falloff));

                Rigidbody body = player.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic)
                    body.AddExplosionForce(explosionPush * falloff, point, splashRadius * 1.5f, 0.6f, ForceMode.Impulse);
            }

            if (PropNetwork.Instance != null)
                PropNetwork.Instance.IgniteInRadius(point, splashRadius);

            // Убираем гранату сразу: RPC себе приходит только в следующем кадре, и граната успела бы попасть ещё раз
            rockets.Remove(rocket.Id);
            rocket.Remove();
            ExplodeRpc(rocket.Id, point, normal);
        }

        [Rpc(SendTo.Everyone)]
        private void ExplodeRpc(int id, Vector3 point, Vector3 normal)
        {
            if (rockets.TryGetValue(id, out EnemyRocket rocket))
            {
                rockets.Remove(id);
                if (rocket != null)
                    rocket.Remove();
            }
            LooseProp.Blast(point, splashRadius * 1.5f, 12f);
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, point, normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(normal) : Quaternion.identity);
        }

        private void OnLoadedChanged(bool previous, bool current)
        {
            if (loadedRocket != null)
                loadedRocket.SetActive(current);
        }
    }
}
