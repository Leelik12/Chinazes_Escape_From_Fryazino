using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;
using RacingProject.Enemy;

namespace RacingProject.Turret
{
    public class VRGun : MonoBehaviourPun, IPunObservable
    { 
        [SerializeField] private Transform carRoot; // родительский объект (машина, к которой прикрепляется пистолет)

        [Header("Настройки стрельбы")]
        public float fireRate = 0.1f;
        public float damage = 10f;
        public float range = 100f;

        [Header("Перегрев")]
        public float heatPerShot = 8f;
        public float heatCooldownRate = 5f;
        public float maxHeat = 100f;
        public Slider heatSlider;
        public BarGradient uiGradient;
        private float currentHeat = 0f;
        private bool isOverheated = false;

        [Header("Подвижные части")]
        [SerializeField] private Transform movablePart;
        [SerializeField] private float recoilDistance = 0.2f;
        [SerializeField] private float recoilDuration = 0.1f;
        private bool isRecoiling = false;
        private Vector3 initialLocalPos;

        [Header("XR Input")]
        public InputActionProperty LeftTrigger;
        public InputActionProperty RightTrigger;
        public InputActionProperty LeftGrip;
        public InputActionProperty RightGrip;
        public Transform firePoint;

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

        private float nextFireTime = 0f;

        // --- Данные для синхронизации позиции ---
        private Vector3 networkLocalPos;
        private Quaternion networkLocalRot;

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
        }

        private void Update()
        {
            if (photonView.IsMine)
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

            bool firePressed =
                (RightTrigger.action.ReadValue<float>() > 0.8f);

            if (firePressed && Time.time >= nextFireTime && !isOverheated)
            {
                nextFireTime = Time.time + fireRate;

                // локальный выстрел
                ShootLocal(firePoint.position, firePoint.forward);

                // синхронизируем с другими
                photonView.RPC(nameof(Shoot), RpcTarget.Others, firePoint.position, firePoint.forward);

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

            if (Physics.Raycast(origin, direction, out RaycastHit hit, range))
            {
                ShotEffects.SpawnImpact(hitEffectPrefabDust, hitEffectPrefabSparks, hit.point, hit.normal, effectOffset, hitEffectLifetime);

                EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null)
                    enemy.RequestDamage((int)damage);
            }
        }

        [PunRPC]
        public void Shoot(Vector3 origin, Vector3 direction)
        {
            PlayShotFeedback();
        }

        // Вспышка, звук и движение затвора — одинаково у стрелка и у второго игрока
        private void PlayShotFeedback()
        {
            ShotEffects.PlayMuzzle(this, muzzleFlash, muzzleLight, lightDuration);
            if (audioSource != null && shotSound != null) audioSource.PlayOneShot(shotSound);
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

        // --- СЕТЕВАЯ СИНХРОНИЗАЦИЯ ПОЗИЦИИ ---
        public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // У владельца: отправляем позицию/вращение относительно машины
                if (carRoot != null)
                {
                    Vector3 localPos = carRoot.InverseTransformPoint(transform.position);
                    Quaternion localRot = Quaternion.Inverse(carRoot.rotation) * transform.rotation;
                    stream.SendNext(localPos);
                    stream.SendNext(localRot);
                }
                else
                {
                    stream.SendNext(transform.localPosition);
                    stream.SendNext(transform.localRotation);
                }
            }
            else
            {
                // У других клиентов: получаем локальные координаты относительно машины
                networkLocalPos = (Vector3)stream.ReceiveNext();
                networkLocalRot = (Quaternion)stream.ReceiveNext();
            }
        }
    }
}
