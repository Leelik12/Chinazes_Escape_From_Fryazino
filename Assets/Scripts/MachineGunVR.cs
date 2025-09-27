using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MachineGunVR : MonoBehaviourPun
{
    [SerializeField] private XRGrabInteractable grabInteractable;

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
        //Debug.Log("Левый грип" + LeftGrip.action.ReadValue<float>());
        //Debug.Log("Левый тригер" + LeftTrigger.action.ReadValue<float>());
        //Debug.Log("Правый грип" + RightGrip.action.ReadValue<float>());
        //Debug.Log("Правый Тригер" + RightTrigger.action.ReadValue<float>());
        // Проверка ввода через Input System
        //Debug.Log(grabInteractable.attachTransform);
        if (grabInteractable.attachTransform != null)
        {
            ap = grabInteractable.attachTransform.name;
        }
        else { ap = null; }

        //Debug.Log(grabInteractable.attachTransform.name);
        //if (triggerAction.action != null && triggerAction.action.ReadValue<float>() > 0.8f && Time.time >= nextFireTime && grabInteractable.isSelected && IsLoaded)
        if (((LeftGrip.action.ReadValue<float>() > 0.8f && LeftTrigger.action.ReadValue<float>() > 0.8f && ap.Contains("L")) || (RightGrip.action.ReadValue<float>() > 0.8f && RightTrigger.action.ReadValue<float>() > 0.8f && ap.Contains("R"))) && Time.time >= nextFireTime && grabInteractable.isSelected)
        {
            //Debug.Log("Выстрел игрока!");
            nextFireTime = Time.time + fireRate;
            Shoot();
        }
    }

    public void Shoot()
    {
        // Визуальный эффект
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }

        if (muzzleLight != null)
            StartCoroutine(MuzzleLightFlash());
        StartCoroutine(MoveRecoil());
        RaycastHit hit;
        if (Physics.Raycast(firePoint.position, firePoint.forward, out hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hitEffectPrefabSparks != null)
            {
                Vector3 effectPosition = hit.point + hit.normal * 0.01f;

                Quaternion effectRotation = Quaternion.LookRotation(hit.normal);

                GameObject fx2 = Instantiate(hitEffectPrefabDust, effectPosition, effectRotation);
                GameObject fx3 = Instantiate(hitEffectPrefabSparks, effectPosition, effectRotation);

                Destroy(fx2, hitEffectLifetime);
                Destroy(fx3, hitEffectLifetime);
            }


            //Debug.Log("Попадание в " + hit.collider.tag);
            if (hit.collider.CompareTag("Head") || hit.collider.CompareTag("Leg") || hit.collider.CompareTag("Body"))
            {
                float finalDamage = damage;
                // Определяем зону попадания
                string hitPartName = hit.collider.name.ToLower();


                if (hit.collider.CompareTag("Head"))
                {
                    finalDamage *= 2f;
                    //Debug.Log("Headshot!");
                }
                else if (hitPartName.Contains("leg"))
                {
                    finalDamage *= 0.5f;
                }
                else
                {
                    // тело — обычный урон
                    //Debug.Log("Body shot!");
                }

                EnemyHeaths target = null;
                if (hit.collider.GetComponentInParent<EnemyHeaths>() == null)
                {
                    target = null;
                }
                else
                {
                    target = hit.collider.GetComponentInParent<EnemyHeaths>();
                }
                if (target != null)
                {
                    if (target != null) { target.TakeDamage(finalDamage); }
                }
            }
        }
    }

    IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
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
