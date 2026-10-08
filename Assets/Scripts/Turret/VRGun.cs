using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;
using RacingProject.Enemy;
using RacingProject.Desktop;
using RacingProject.Management;
using RacingProject.Network;
using RacingProject.Props;

namespace RacingProject.Turret
{
    public class VRGun : RoleSyncedBehaviour
    { 
        [SerializeField] private Transform carRoot; // родительский объект (машина, к которой прикрепляется пистолет)

        [Header("Настройки стрельбы")]
        public float fireRate = 0.1f;
        public float damage = 10f;
        public float range = 100f;
        [Tooltip("Слои, в которые попадает выстрел. Триггеры (в том числе коллайдеры самого пистолета) игнорируются")]
        public LayerMask hitLayerMask = ~0;

        [Header("Перегрев")]
        public float heatPerShot = 8f;
        public float heatCooldownRate = 5f;
        public float maxHeat = 100f;
        public Slider heatSlider;
        public BarGradient uiGradient;
        private float currentHeat = 0f;
        private bool isOverheated = false;

        // Нагрев 0–1 и признак перегрева для приборов: у напарника приходят по сети
        public float Heat01 => maxHeat > 0f ? currentHeat / maxHeat : 0f;
        public bool IsOverheated => isOverheated;

        [Header("Подвижные части")]
        [SerializeField] private Transform movablePart;
        [SerializeField] private float recoilDistance = 0.2f;
        [SerializeField] private float recoilDuration = 0.1f;
        [Tooltip("Отдача и гильзы пулемёта на крыше (необязательно)")]
        [SerializeField] private MachineGunRecoil gunRecoil;
        private bool isRecoiling = false;
        private Vector3 initialLocalPos;

        [Header("XR Input")]
        public InputActionProperty LeftTrigger;
        public InputActionProperty RightTrigger;
        public InputActionProperty LeftGrip;
        public InputActionProperty RightGrip;
        public Transform firePoint;

        [Header("Режим монитора")]
        [Tooltip("Прицел мышью: в режиме монитора курок — левая кнопка, выстрел летит в точку под центром экрана")]
        [SerializeField] private DesktopGunnerAim desktopAim;

        [Header("Эффекты")]
        public ParticleSystem muzzleFlash;
        public Light muzzleLight;
        public float lightDuration = 0.05f;
        public AudioSource audioSource;
        public AudioClip shotSound;
        public GameObject hitEffectPrefabDust;
        public GameObject hitEffectPrefabSparks;
        public float hitEffectLifetime = 2f;
        public float effectOffset = 0.01f;
        [Tooltip("Отметка попадания по врагу (видит только стрелок)")]
        [SerializeField] private GameObject hitMarkerPrefab;
        [Tooltip("Трассер — каждая такая пуля (0 — без трассеров)")]
        [SerializeField] private int tracerEvery = 3;
        [Tooltip("Цвет трассера, HDR: яркость больше 1 даёт свечение")]
        [SerializeField, ColorUsage(false, true)] private Color tracerColor = new Color(6f, 1.6f, 0.4f);

        private float nextFireTime = 0f;
        private int shotsSinceTracer;

        // --- Данные для синхронизации позиции ---
        private Vector3 networkLocalPos;
        private Quaternion networkLocalRot;

        // Счётчик выстрелов: пакеты ненадёжные, поэтому передаётся число, а не событие,
        // и потерянный пакет не съедает выстрел
        private byte shotCount;
        private byte remoteShotCount;
        private bool hasRemoteShotCount;
        // Больше выстрелов за пакет не проигрываем, иначе после паузы в сети они слились бы в очередь
        private const int MaxReplayedShots = 3;

        private void Awake()
        {
            LeftGrip.action.Enable();
            RightGrip.action.Enable();
            LeftTrigger.action.Enable();
            RightTrigger.action.Enable();

            if (heatSlider != null)
            {
                heatSlider.maxValue = maxHeat;
                heatSlider.value = 0f;
            }

            if (movablePart != null)
                initialLocalPos = movablePart.localPosition;

            networkLocalPos = transform.localPosition;
            networkLocalRot = transform.localRotation;
        }

        private void Update()
        {
            if (HasAuthority)
            {
                HandleOverheat();
                HandleFireInput();
            }
            else
            {
                // Интерполяция позиции/вращения у других игроков
                transform.localPosition = Vector3.Lerp(transform.localPosition, networkLocalPos, Time.deltaTime * 10f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, networkLocalRot, Time.deltaTime * 10f);
            }
        }

        // --- Перегрев ---
        private void HandleOverheat()
        {
            if (currentHeat > 0f)
            {
                currentHeat -= heatCooldownRate * Time.deltaTime;
                if (currentHeat < 0f) currentHeat = 0f;

                if (heatSlider != null)
                    heatSlider.value = currentHeat;

                if (uiGradient != null)
                    uiGradient.SetOverheat(currentHeat, maxHeat);
            }

            if (isOverheated && currentHeat <= 5f)
                isOverheated = false;
        }

        // --- Стрельба ---
        private void HandleFireInput()
        {

            bool useDesktopAim = !ViewModeService.IsVR && desktopAim != null && desktopAim.isActiveAndEnabled;
            bool firePressed = useDesktopAim
                ? desktopAim.FirePressed
                : RightTrigger.action.ReadValue<float>() > 0.8f;

            if (firePressed && Time.time >= nextFireTime && !isOverheated)
            {
                nextFireTime = Time.time + fireRate;

                Vector3 direction = useDesktopAim
                    ? (desktopAim.AimPoint - firePoint.position).normalized
                    : firePoint.forward;

                // локальный выстрел
                ShootLocal(firePoint.position, direction);

                // напарник увидит выстрел по счётчику в Serialize
                shotCount++;

                currentHeat += heatPerShot;
                if (currentHeat >= maxHeat)
                {
                    currentHeat = maxHeat;
                    isOverheated = true;
                    Debug.Log("Оружие перегрелось!");
                }

                if (heatSlider != null)
                    heatSlider.value = currentHeat;

                if (uiGradient != null)
                    uiGradient.SetOverheat(currentHeat, maxHeat);
            }
        }

        private void ShootLocal(Vector3 origin, Vector3 direction)
        {
            PlayShotFeedback();

            if (CastShot(origin, direction, out RaycastHit hit))
            {
                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                {
                    enemy.RequestDamage((int)damage);
                    if (hitMarkerPrefab != null)
                        Instantiate(hitMarkerPrefab, hit.point, Quaternion.identity);
                }

                ExplosiveBarrel barrel = hit.collider.GetComponentInParent<ExplosiveBarrel>();
                if (barrel != null)
                    barrel.RequestHit((int)damage);
            }
        }


        // Полёт пули без урона: трассер, искры и след. У напарника луч идёт из ствола по позе пулемёта из сети,
        // поэтому он видит те же попадания, что и стрелок
        private bool CastShot(Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            bool hasHit = Physics.Raycast(origin, direction, out hit, range, hitLayerMask, QueryTriggerInteraction.Ignore);

            if (tracerEvery > 0 && ++shotsSinceTracer >= tracerEvery)
            {
                shotsSinceTracer = 0;
                ShotEffects.SpawnTracer(origin, hasHit ? hit.point : origin + direction * range, tracerColor);
            }

            if (hasHit)
            {
                ShotEffects.SpawnImpact(hitEffectPrefabDust, hitEffectPrefabSparks, hit.point, hit.normal, effectOffset, hitEffectLifetime);
                ShotEffects.SpawnBulletHole(hit);
                LooseProp.PushHit(hit, direction);
            }
            return hasHit;
        }

        // Вспышка, звук и движение затвора — одинаково у стрелка и у второго игрока
        private void PlayShotFeedback()
        {
            ShotEffects.PlayMuzzle(this, muzzleFlash, muzzleLight, lightDuration);
            if (audioSource != null && shotSound != null) audioSource.PlayOneShot(shotSound);
            if (gunRecoil != null) gunRecoil.Kick();
            StartCoroutine(MoveRecoil());
        }

        private IEnumerator MoveRecoil()
        {
            if (movablePart == null || isRecoiling) yield break;
            isRecoiling = true;

            Vector3 startPos = initialLocalPos;
            Vector3 recoilPos = startPos + new Vector3(0, 0, -recoilDistance);
            float half = recoilDuration * 0.5f;
            float t = 0f;

            while (t < half)
            {
                movablePart.localPosition = Vector3.Lerp(startPos, recoilPos, t / half);
                t += Time.deltaTime;
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                movablePart.localPosition = Vector3.Lerp(recoilPos, startPos, t / half);
                t += Time.deltaTime;
                yield return null;
            }

            movablePart.localPosition = startPos;
            isRecoiling = false;
        }

        // --- СЕТЕВАЯ СИНХРОНИЗАЦИЯ ПОЗИЦИИ И ВЫСТРЕЛОВ ---
        public override void Serialize(SyncStream stream)
        {
            if (stream.IsWriting)
            {
                // У владельца: отправляем позицию/вращение относительно машины
                if (carRoot != null)
                {
                    networkLocalPos = carRoot.InverseTransformPoint(transform.position);
                    networkLocalRot = Quaternion.Inverse(carRoot.rotation) * transform.rotation;
                }
                else
                {
                    networkLocalPos = transform.localPosition;
                    networkLocalRot = transform.localRotation;
                }
            }

            // У других клиентов: получаем локальные координаты относительно машины
            stream.Serialize(ref networkLocalPos);
            stream.Serialize(ref networkLocalRot);

            byte count = shotCount;
            stream.Serialize(ref count);
            if (!stream.IsWriting)
                ReplayRemoteShots(count);

            // Нагрев — для приборов водителя; байта хватает для шкалы
            byte heat = (byte)Mathf.RoundToInt(Mathf.Clamp01(Heat01) * 255f);
            bool overheated = isOverheated;
            stream.Serialize(ref heat);
            stream.Serialize(ref overheated);
            if (!stream.IsWriting)
            {
                currentHeat = heat / 255f * maxHeat;
                isOverheated = overheated;
                if (heatSlider != null)
                    heatSlider.value = currentHeat;
                if (uiGradient != null)
                    uiGradient.SetOverheat(currentHeat, maxHeat);
            }
        }

        private void ReplayRemoteShots(byte count)
        {
            // Первый пакет только запоминает счётчик: старые выстрелы не проигрываются
            if (!hasRemoteShotCount)
            {
                hasRemoteShotCount = true;
                remoteShotCount = count;
                return;
            }

            int newShots = (byte)(count - remoteShotCount);
            remoteShotCount = count;
            for (int i = 0; i < Mathf.Min(newShots, MaxReplayedShots); i++)
            {
                PlayShotFeedback();
                if (firePoint != null)
                    CastShot(firePoint.position, firePoint.forward, out _);
            }
        }
    }
}
