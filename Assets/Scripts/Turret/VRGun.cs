using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.UI;

public class VRGun : MonoBehaviourPun
{
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Настройки стрельбы")]
    public float fireRate = 0.1f;
    public float damage = 10f;
    public float range = 100f;

    [Header("Перегрев")]
    public float heatPerShot = 8f;             // Сколько тепла добавляется за выстрел
    public float heatCooldownRate = 5f;        // Скорость охлаждения в секунду
    public float maxHeat = 100f;               // Предел перегрева
    public Slider heatSlider;                  // UI-слайдер перегрева
    public BarGradient uiGradient;             // Ссылка на общий UI-градиент (для обновления цвета)
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
    private string ap = null;

    void Awake()
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

    void Update()
    {
        if (!photonView.IsMine) return;

        // охлаждение оружия
        if (currentHeat > 0f)
        {
            currentHeat -= heatCooldownRate * Time.deltaTime;
            if (currentHeat < 0f) currentHeat = 0f;

            if (heatSlider != null)
                heatSlider.value = currentHeat;

            if (uiGradient != null)
                uiGradient.SetOverheat(currentHeat, maxHeat);
        }

        // снимаем перегрев, если остыло
        if (isOverheated && currentHeat <= 5f)
        {
            isOverheated = false;
        }

        // определяем, какая рука держит оружие
        if (grabInteractable.attachTransform != null)
        {
            ap = grabInteractable.attachTransform.name;
        }
        else ap = null;

        // проверка ввода
        bool isLeftHand = ap != null && ap.Contains("L");
        bool isRightHand = ap != null && ap.Contains("R");

        bool firePressed =
            (isLeftHand && LeftGrip.action.ReadValue<float>() > 0.8f && LeftTrigger.action.ReadValue<float>() > 0.8f) ||
            (isRightHand && RightGrip.action.ReadValue<float>() > 0.8f && RightTrigger.action.ReadValue<float>() > 0.8f);

        // стрельба, если не перегрелось
        if (firePressed && Time.time >= nextFireTime && grabInteractable.isSelected && !isOverheated)
        {
            nextFireTime = Time.time + fireRate;

            // локально выполняем стрельбу
            ShootLocal(firePoint.position, firePoint.forward);

            // передаём другим клиентам
            photonView.RPC("Shoot", RpcTarget.Others, firePoint.position, firePoint.forward);

            // добавляем нагрев
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

    // Локальный выстрел (стрелку сразу — без задержки)
    private void ShootLocal(Vector3 origin, Vector3 direction)
    {
        if (muzzleFlash != null) muzzleFlash.Play();
        if (muzzleLight != null) StartCoroutine(MuzzleLightFlash());
        if (audioSource != null && shotSound != null) audioSource.PlayOneShot(shotSound);
        StartCoroutine(MoveRecoil());

        // Raycast попадания
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hitEffectPrefabDust != null)
            {
                GameObject fxDust = Instantiate(hitEffectPrefabDust, hit.point + hit.normal * effectOffset, Quaternion.LookRotation(hit.normal));
                Destroy(fxDust, hitEffectLifetime);
            }
            if (hitEffectPrefabSparks != null)
            {
                GameObject fxSparks = Instantiate(hitEffectPrefabSparks, hit.point + hit.normal * effectOffset, Quaternion.LookRotation(hit.normal));
                Destroy(fxSparks, hitEffectLifetime);
            }

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.RequestDamage((int)damage);
            }
        }
    }

    [PunRPC]
    public void Shoot(Vector3 origin, Vector3 direction)
    {
        // Эффекты и отдача только для других игроков
        if (muzzleFlash != null) muzzleFlash.Play();
        if (muzzleLight != null) StartCoroutine(MuzzleLightFlash());
        if (audioSource != null && shotSound != null) audioSource.PlayOneShot(shotSound);
        StartCoroutine(MoveRecoil());
    }

    private IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
    }

    private IEnumerator MoveRecoil()
    {
        if (movablePart == null || isRecoiling) yield break;

        isRecoiling = true;

        Vector3 startPos = initialLocalPos;
        Vector3 recoilPos = startPos + new Vector3(0, 0, -recoilDistance);
        float half = recoilDuration * 0.5f;
        float t = 0f;

        // движение назад
        while (t < half)
        {
            movablePart.localPosition = Vector3.Lerp(startPos, recoilPos, t / half);
            t += Time.deltaTime;
            yield return null;
        }

        // движение вперёд
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
}
