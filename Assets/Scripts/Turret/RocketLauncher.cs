using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using RacingProject.Enemy;
using RacingProject.Management;
using RacingProject.Network;
using RacingProject.Props;

namespace RacingProject.Turret
{
    // РПГ-7 на турели стрелка. Гранатомёт появляется на турели с первым подобранным ящиком выстрелов
    // (RocketCrates), выстрелы — штучные, перезарядка короткая. Стреляет стрелок: в одиночной игре и на мониторе —
    // правая кнопка мыши (прицел тот же, что у пулемёта), в шлеме — левый курок. Выстрел просит сервер, сервер
    // списывает выстрел и рассылает его всем; граната летит у всех одинаково, попадание и урон считает сервер.
    // Убийство гранатой засчитывается стрелку (отметка убийства у него)
    public class RocketLauncher : NetworkBehaviour, IRocketOwner
    {
        [SerializeField] private VRGun gun;
        [SerializeField] private Transform firePoint;
        [Tooltip("Модель гранатомёта на турели: видна, когда он есть")]
        [SerializeField] private GameObject launcherModel;
        [Tooltip("Головная часть выстрела в стволе: видна, когда заряжен")]
        [SerializeField] private GameObject loadedWarhead;
        [SerializeField] private TMP_Text counter;
        [SerializeField] private RadioChatter radio;

        [Header("Выстрел")]
        [SerializeField] private int maxAmmo = 6;
        [SerializeField] private float reloadTime = 1.8f;
        [SerializeField] private float rocketSpeed = 80f;
        [SerializeField] private float maxRange = 280f;
        [Tooltip("Отклонение от направления ствола, на которое можно довернуть по прицелу, градусы")]
        [SerializeField] private float maxAimDeviation = 8f;
        [SerializeField] private LayerMask hitLayerMask = ~0;

        [Header("Взрыв")]
        [SerializeField] private int damage = 1200;
        [SerializeField] private float splashRadius = 7f;
        [SerializeField, Range(0f, 1f)] private float edgeDamage = 0.25f;
        [SerializeField] private float enemyPush = 16000f;

        [Header("Эффекты")]
        [SerializeField] private EnemyRocket rocketPrefab;
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private ParticleSystem backblast;
        [SerializeField] private AudioSource launchAudio;
        [SerializeField] private AudioClip launchSound;
        [SerializeField] private AudioClip reloadSound;

        private readonly NetworkVariable<int> ammo = new NetworkVariable<int>();
        private readonly NetworkVariable<bool> hasLauncher = new NetworkVariable<bool>();
        private readonly NetworkVariable<bool> loaded = new NetworkVariable<bool>(true);
        private readonly Dictionary<int, EnemyRocket> rockets = new Dictionary<int, EnemyRocket>();
        private readonly Dictionary<int, ulong> shooters = new Dictionary<int, ulong>();
        private readonly HashSet<EnemyHealth> splashTargets = new HashSet<EnemyHealth>();
        private int rocketCounter;
        private float reloadedAt;
        private bool triggerHeld;
        private int shownAmmo = -1;

        public int Ammo => ammo.Value;
        public int MaxAmmo => maxAmmo;
        public bool HasLauncher => hasLauncher.Value;
        public bool Ready => hasLauncher.Value && ammo.Value > 0 && loaded.Value;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ammo.Value = 0;
                hasLauncher.Value = false;
                loaded.Value = true;
            }
            ApplyVisuals();
        }

        // Сервер: ящик выстрелов. false — гранатомёт полон, ящик остаётся лежать
        public bool AddAmmo(int count)
        {
            if (!IsServer || count <= 0 || (hasLauncher.Value && ammo.Value >= maxAmmo)) return false;
            bool first = !hasLauncher.Value;
            hasLauncher.Value = true;
            ammo.Value = Mathf.Min(maxAmmo, ammo.Value + count);
            if (first && radio != null) radio.SayCarEvent(RadioChatter.CarEvent.Launcher);
            return true;
        }

        private void Update()
        {
            ApplyVisuals();
            if (IsServer && IsSpawned && !loaded.Value && Time.time >= reloadedAt && ammo.Value > 0)
            {
                loaded.Value = true;
                ReloadedRpc();
            }
            if (IsSpawned && LocalPlayerRole.Controls(PlayerRole.Gunner))
                HandleInput();
        }

        private void HandleInput()
        {
            bool desktop = !ViewModeService.IsVR && gun != null && gun.DesktopAim != null && gun.DesktopAim.isActiveAndEnabled;
            bool pressed;
            if (desktop)
                pressed = Mouse.current != null && Mouse.current.rightButton.isPressed;
            else
                pressed = gun != null && gun.LeftTrigger.action != null && gun.LeftTrigger.action.ReadValue<float>() > 0.8f;
            if (PauseMenu.BlocksInput) pressed = false;
            bool fire = pressed && !triggerHeld;
            triggerHeld = pressed;
            if (!fire || !Ready || firePoint == null) return;

            Vector3 direction = firePoint.forward;
            if (desktop)
            {
                Vector3 aimed = (gun.DesktopAim.AimPoint - firePoint.position).normalized;
                if (Vector3.Angle(aimed, firePoint.forward) <= maxAimDeviation) direction = aimed;
            }
            FireRequestRpc(firePoint.position, direction);
        }

        [Rpc(SendTo.Server)]
        private void FireRequestRpc(Vector3 origin, Vector3 direction, RpcParams rpcParams = default)
        {
            if (!Ready || direction.sqrMagnitude < 0.5f) return;
            // Клиенту не доверяем точку выстрела дальше нескольких метров от ствола
            if (firePoint != null && (origin - firePoint.position).sqrMagnitude > 25f) origin = firePoint.position;
            ammo.Value--;
            loaded.Value = false;
            reloadedAt = Time.time + reloadTime;
            int id = ++rocketCounter;
            shooters[id] = rpcParams.Receive.SenderClientId;
            FireRpc(id, origin, direction.normalized);
            if (ammo.Value == 0 && radio != null) radio.SayCarEvent(RadioChatter.CarEvent.RocketsEmpty);
        }

        // Выстрел РПГ (у обоих игроков): вибрация контроллеров стрелка (VRHaptics)
        public static event System.Action Fired;

        [Rpc(SendTo.Everyone)]
        private void FireRpc(int id, Vector3 origin, Vector3 direction)
        {
            Fired?.Invoke();
            if (rocketPrefab != null)
            {
                EnemyRocket rocket = Instantiate(rocketPrefab, origin, Quaternion.LookRotation(direction));
                rocket.Launch(this, id, direction, rocketSpeed, maxRange / rocketSpeed + 1f);
                rockets[id] = rocket;
            }
            if (backblast != null) backblast.Play();
            if (launchAudio != null && launchSound != null) launchAudio.PlayOneShot(launchSound);
        }

        [Rpc(SendTo.Everyone)]
        private void ReloadedRpc()
        {
            if (launchAudio != null && reloadSound != null) launchAudio.PlayOneShot(reloadSound, 0.8f);
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
                if (hit.distance <= 0f && hit.point == Vector3.zero) continue;
                if (!found || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    found = true;
                }
            }
            return found;
        }

        // Только на сервере: урон врагам вокруг, поджог бочек, снос опор
        public void RocketHit(EnemyRocket rocket, Vector3 point, Vector3 normal)
        {
            ulong shooter = shooters.TryGetValue(rocket.Id, out ulong id) ? id : NetworkManager.ServerClientId;
            shooters.Remove(rocket.Id);

            splashTargets.Clear();
            foreach (Collider collider in Physics.OverlapSphere(point, splashRadius, hitLayerMask, QueryTriggerInteraction.Ignore))
            {
                EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null) splashTargets.Add(enemy);
            }
            foreach (EnemyHealth enemy in splashTargets)
            {
                float closest = splashRadius;
                foreach (Collider collider in enemy.GetComponentsInChildren<Collider>())
                {
                    if (collider.isTrigger || collider is WheelCollider) continue;
                    Vector3 nearest = collider is MeshCollider mesh && !mesh.convex ? collider.bounds.ClosestPoint(point) : collider.ClosestPoint(point);
                    closest = Mathf.Min(closest, Vector3.Distance(point, nearest));
                }
                float falloff = Mathf.Lerp(1f, edgeDamage, closest / splashRadius);
                Rigidbody body = enemy.GetComponent<Rigidbody>();
                if (body != null && !body.isKinematic)
                    body.AddExplosionForce(enemyPush * falloff, point, splashRadius * 1.5f, 1f, ForceMode.Impulse);
                enemy.ApplyServerDamage(Mathf.RoundToInt(damage * falloff), shooter);
            }

            if (PropNetwork.Instance != null) PropNetwork.Instance.IgniteInRadius(point, splashRadius);
            if (PowerLines.Instance != null) PowerLines.Instance.ToppleInRadius(point, splashRadius * 0.5f);

            // Убираем гранату сразу: RPC себе приходит только в следующем кадре
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
                if (rocket != null) rocket.Remove();
            }
            LooseProp.Blast(point, splashRadius * 1.5f, 14f);
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, point, normal.sqrMagnitude > 0.01f ? Quaternion.LookRotation(normal) : Quaternion.identity);
        }

        private void ApplyVisuals()
        {
            bool owned = IsSpawned && hasLauncher.Value;
            if (launcherModel != null && launcherModel.activeSelf != owned) launcherModel.SetActive(owned);
            bool warhead = owned && ammo.Value > 0 && loaded.Value;
            if (loadedWarhead != null && loadedWarhead.activeSelf != warhead) loadedWarhead.SetActive(warhead);
            if (counter != null && shownAmmo != ammo.Value)
            {
                shownAmmo = ammo.Value;
                counter.text = "×" + shownAmmo;
            }
        }
    }
}
