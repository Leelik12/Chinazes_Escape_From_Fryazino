using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MachineGunVR : MonoBehaviourPun
{
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Настройки стрельбы")]
    public float fireRate = 0.1f;
    public int damage = 10;
    public float range = 100f;

    [Header("Перегрев")]
    public float maxHeat = 100f;             // максимум тепла
    public float heatPerShot = 10f;          // сколько добавляется за выстрел
    public float coolRate = 15f;             // скорость остывания (в секунду)
    public float overheatCooldown = 5f;      // время блокировки при перегреве
    private float currentHeat = 0f;
    private bool isOverheated = false;

    [Header("UI перегрева")]
    public Slider heatSlider;
    public Gradient heatGradient;
    public Image heatFill;

    [Header("Muzzle Flash")]
    public ParticleSystem muzzleFlash;
    public Light muzzleLight;
    public float lightDuration = 0.05f;

    [Header("XR")]
    public InputActionProperty LeftTrigger;
    public InputActionProperty RightTrigger;
    public InputActionProperty LeftGrip;
    public InputActionProperty RightGrip;
    public Transform firePoint;

    [Header("Декали и аудио")]
    public GameObject hitEffectPrefabDust;
    public GameObject hitEffectPrefabSparks;
    public float hitEffectLifetime = 5f;
    public AudioSource audioSource;
    public AudioClip shotSound;

    private float nextFireTime = 0f;

    void Awake()
    {
        LeftGrip.action.Enable();
        RightGrip.action.Enable();
        LeftTrigger.action.Enable();
        RightTrigger.action.Enable();

        if (heatSlider)
        {
            heatSlider.minValue = 0;
            heatSlider.maxValue = maxHeat;
            heatSlider.value = 0;
            if (heatFill) heatFill.color = heatGradient.Evaluate(0f);
        }
    }

    void Update()
    {
        if (!photonView.IsMine) return;

        // Постепенное охлаждение
        if (currentHeat > 0)
        {
            currentHeat -= coolRate * Time.deltaTime;
            currentHeat = Mathf.Clamp(currentHeat, 0, maxHeat);
            UpdateHeatUI();
        }

        // Проверяем перегрев
        if (isOverheated) return;

        // Проверка нажатия триггеров
        bool isFiring =
            ((LeftGrip.action.ReadValue<float>() > 0.8f && LeftTrigger.action.ReadValue<float>() > 0.8f) ||
             (RightGrip.action.ReadValue<float>() > 0.8f && RightTrigger.action.ReadValue<float>() > 0.8f));

        if (isFiring && Time.time >= nextFireTime && grabInteractable.isSelected)
        {
            nextFireTime = Time.time + fireRate;

            // Добавляем нагрев
            currentHeat += heatPerShot;
            currentHeat = Mathf.Clamp(currentHeat, 0, maxHeat);
            UpdateHeatUI();

            if (currentHeat >= maxHeat)
            {
                StartCoroutine(HandleOverheat());
                return;
            }

            // Вызываем RPC для стрельбы
            photonView.RPC("RPC_Shoot", RpcTarget.All, firePoint.position, firePoint.forward);
        }
    }

    IEnumerator HandleOverheat()
    {
        isOverheated = true;

        // Можно добавить визуальный эффект перегрева (например, звук или вспышку)
        Debug.Log(" Оружие перегрелось!");

        yield return new WaitForSeconds(overheatCooldown);

        isOverheated = false;
        currentHeat = Mathf.Clamp(currentHeat - coolRate * overheatCooldown, 0, maxHeat);
        UpdateHeatUI();
    }

    void UpdateHeatUI()
    {
        if (heatSlider)
        {
            heatSlider.value = currentHeat;
            if (heatFill)
            {
                float t = currentHeat / maxHeat;
                heatFill.color = heatGradient.Evaluate(t);
            }
        }
    }

    [PunRPC]
    void RPC_Shoot(Vector3 origin, Vector3 direction)
    {
        // Muzzle flash и звук
        if (muzzleFlash != null) muzzleFlash.Play();
        if (muzzleLight != null) StartCoroutine(MuzzleLightFlash());
        if (audioSource != null && shotSound != null) audioSource.PlayOneShot(shotSound);

        // Raycast для попаданий
        RaycastHit hit;
        Vector3 hitPoint = origin + direction * range;
        if (Physics.Raycast(origin, direction, out hit, range))
        {
            hitPoint = hit.point;

            // Декали
            if (hitEffectPrefabDust != null && hitEffectPrefabSparks != null)
            {
                GameObject fxDust = PhotonNetwork.Instantiate(hitEffectPrefabDust.name, hitPoint + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
                GameObject fxSparks = PhotonNetwork.Instantiate(hitEffectPrefabSparks.name, hitPoint + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
                Destroy(fxDust, hitEffectLifetime);
                Destroy(fxSparks, hitEffectLifetime);
            }

            EnemyHealth target = hit.collider.GetComponentInParent<EnemyHealth>();
            if (target != null)
            {
                target.RequestDamage((int)damage);
            }
        }
    }

    IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
    }
}
