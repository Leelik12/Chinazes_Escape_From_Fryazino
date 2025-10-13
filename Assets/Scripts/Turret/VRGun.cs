using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRGun : MonoBehaviourPun
{
    [SerializeField] private XRGrabInteractable grabInteractable;
    [Header("Перезарядка")]
    [SerializeField] public float maxAmmo;
    public float currentAmmo;
    // Ссылка на спавнер нового магазина
    [SerializeField] private VRMagazineSpawner magazineSpawner;
    public bool IsLoaded => currentAmmo > 0;
    private VRMagazine currentMagazine;
    public bool IsCharged = true;
    public GameObject emptyMagazinePrefab; // Префаб пустого магазина
    public Transform ejectPoint; // Точка, откуда выпадает магазин
    [SerializeField] private GameObject internalMagazineModel; // Встроенный визуальный магазин (активируется/скрывается)

    [Header("Подвижные части")]
    [SerializeField] private Transform movablePart; // Подвижная часть оружия (затвор)
    [SerializeField] private float recoilDistance = 0.2f; // Расстояние, на которое подвижная часть будет двигаться
    [SerializeField] private float recoilDuration = 0.1f;     // сколько длится отдача (сек)

    [Header("Настройки стрельбы")]
    public float fireRate;
    public float damage;
    public float range = 100f;

    [Header("Muzzle Flash")]
    public ParticleSystem muzzleFlash;
    public Light muzzleLight;
    public float lightDuration; // длительность вспышки света

    [Header("XR")]
    public InputActionProperty triggerAction; // <-- сюда привязываем Input Action с триггера
    public Transform firePoint;
    public InputActionProperty LeftTrigger;
    public InputActionProperty RightTrigger;
    public InputActionProperty LeftGrip;
    public InputActionProperty RightGrip;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shotSound;

    [Header("Декали")]
    [SerializeField] private GameObject hitEffectPrefabDust;
    [SerializeField] private GameObject hitEffectPrefabSparks;
    [SerializeField] private float hitEffectLifetime = 100f;
    [SerializeField] private float effectOffset = 0.01f;

    [Header("Прочее")]
    public string enemyTag = ""; // Тег врага
    private float nextFireTime = 0f;

    string ap = null;
    void Awake()
    {
        triggerAction.action.Enable();
        LeftGrip.action.Enable();
        RightGrip.action.Enable();
        LeftTrigger.action.Enable();
        RightTrigger.action.Enable();
    }
    void Update()
    {
        if (!photonView.IsMine) return;
        //Debug.Log("Левый грип" + LeftGrip.action.ReadValue<float>());
        //Debug.Log("Левый тригер" + LeftTrigger.action.ReadValue<float>());
        //Debug.Log("Правый грип" + RightGrip.action.ReadValue<float>());
        //Debug.Log("Правый Тригер" + RightTrigger.action.ReadValue<float>());
        if (currentAmmo <= 0 && IsCharged)
        {
            IsCharged = false;
            currentMagazine = null;
            EjectMagazine(); // автоматически выбрасывает магазин при окончании патронов
        }
        // Проверка ввода через Input System
        //Debug.Log(grabInteractable.attachTransform);
        if (grabInteractable.attachTransform != null)
        {
            ap = grabInteractable.attachTransform.name;
        } else { ap = null; }
        
        //Debug.Log(grabInteractable.attachTransform.name);
        //if (triggerAction.action != null && triggerAction.action.ReadValue<float>() > 0.8f && Time.time >= nextFireTime && grabInteractable.isSelected && IsLoaded)
        if (((LeftGrip.action.ReadValue<float>() > 0.8f && LeftTrigger.action.ReadValue<float>() > 0.8f && ap.Contains("L")) || (RightGrip.action.ReadValue<float>() > 0.8f && RightTrigger.action.ReadValue<float>() > 0.8f && ap.Contains("R"))) && Time.time >= nextFireTime && grabInteractable.isSelected && IsLoaded)
        {

            currentAmmo--;
            Debug.Log("Выстрел игрока!");
            nextFireTime = Time.time + fireRate;
            photonView.RPC("Shoot", RpcTarget.All, firePoint.position, firePoint.forward);
        }
    }
    [PunRPC]
    public void Shoot(Vector3 origin, Vector3 direction)
    {
        if (currentAmmo <= 0)
        {
            Debug.Log("Выстрел! Осталось патронов: " + currentAmmo);
        }
        // Визуальный эффект
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        if (muzzleLight != null)
            StartCoroutine(MuzzleLightFlash());
        // Звук
        if (audioSource != null && shotSound != null)
            audioSource.PlayOneShot(shotSound);
        photonView.RPC("PlayRecoilRPC", RpcTarget.All);
        RaycastHit hit;
        if (Physics.Raycast(firePoint.position, firePoint.forward, out hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hitEffectPrefabSparks != null)
            {
                Vector3 effectPosition = hit.point + hit.normal * 0.01f;

                Quaternion effectRotation = Quaternion.LookRotation(hit.normal);

                // Декали
                if (hitEffectPrefabDust != null && hitEffectPrefabSparks != null)
                {
                    GameObject fxDust = PhotonNetwork.Instantiate(hitEffectPrefabDust.name, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
                    GameObject fxSparks = PhotonNetwork.Instantiate(hitEffectPrefabSparks.name, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));

                    Destroy(fxDust, hitEffectLifetime);
                    Destroy(fxSparks, hitEffectLifetime);
                }
            }


            //Debug.Log("Попадание в " + hit.collider.tag);
            EnemyHealth target = hit.collider.GetComponentInParent<EnemyHealth>();
            if (target != null)
            {
                target.RequestDamage((int)damage);
            }
        }
    }
    public bool CanInsertMagazine()
    {
        return !IsCharged;
    }
    public void InsertMagazine(VRMagazine magazine)
    {
        IsCharged = true;
        if (currentMagazine != null) { }

        currentMagazine = magazine;
        currentAmmo = maxAmmo;

        // Прячем внешний магазин
        magazine.gameObject.SetActive(false);

        // Активируем встроенный визуальный магазин
        if (internalMagazineModel != null)
            internalMagazineModel.SetActive(true);

        Debug.Log("Магазин вставлен. Патроны: " + currentAmmo);
    }
    public void EjectMagazine()
    {
        // Выкинуть пустой магазин
        if (currentMagazine == null && emptyMagazinePrefab != null && ejectPoint != null)
        {
            PhotonNetwork.Instantiate(emptyMagazinePrefab.name, ejectPoint.position, ejectPoint.rotation);
        }
        //Debug.Log("Встроенный магазин ВЫКЛ");
        // Выключить визуальный встроенный магазин
        internalMagazineModel.SetActive(false);
        internalMagazineModel.gameObject.SetActive(false);
        //Debug.Log("Отключаем встроенный магазин: " + internalMagazineModel.name);
        currentAmmo = 0;
        // Вызов спавна нового магазина через спавнер
        if (magazineSpawner != null)
        {
            magazineSpawner.OnMagazineInserted();
        }
        Debug.Log("Магазин выброшен.");
    }
    IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
    }
    [PunRPC]
    private void PlayRecoilRPC()
    {
        StartCoroutine(MoveRecoil());
    }
    private IEnumerator MoveRecoil()
    {
        if (movablePart == null)
        {
            yield break;
        }
        // 1) Сохраняем исходную локальную позицию
        Vector3 originalLocalPos = movablePart.localPosition;

        // 2) Чистый локальный вектор отдачи: назад по локальной Z
        Vector3 recoilOffsetLocal = new Vector3(0f, 0f, -recoilDistance);

        float halfDur = recoilDuration * 0.5f;
        float timer = 0f;

        // 3) Двигаем затвор назад (половина отдачи)
        while (timer < halfDur)
        {
            float t = timer / halfDur; 
            movablePart.localPosition = Vector3.Lerp(originalLocalPos,
                                                     originalLocalPos + recoilOffsetLocal,
                                                     t);
            timer += Time.deltaTime;
            yield return null;
        }

        // 4) Возвращаем затвор в исходное положение (половина возврата)
        timer = 0f;
        while (timer < halfDur)
        {
            float t = timer / halfDur;
            movablePart.localPosition = Vector3.Lerp(originalLocalPos + recoilOffsetLocal,
                                                     originalLocalPos,
                                                     t);
            timer += Time.deltaTime;
            yield return null;
        }

        // 5) Гарантируем точно исходную позицию
        movablePart.localPosition = originalLocalPos;
    }

}
