using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class MachineGunVR : MonoBehaviourPun
{
    [SerializeField] private XRGrabInteractable grabInteractable;

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

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shotSound;

    [Header("Прочее")]
    public string enemyTag = ""; // Тег врага
    private float nextFireTime = 0f;

    [Header("Tracer Settings")]
    [SerializeField] private GameObject tracerPrefab; // Префаб трассера (LineRenderer или пуля)
    [SerializeField] private float tracerSpeed = 200f; // скорость движения трассера
    [SerializeField] private float tracerLifetime = 1f; // сколько живёт трассер

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
        Debug.Log("Левый грип" + LeftGrip.action.ReadValue<float>());
        Debug.Log("Левый тригер" + LeftTrigger.action.ReadValue<float>());
        Debug.Log("Правый грип" + RightGrip.action.ReadValue<float>());
        Debug.Log("Правый Тригер" + RightTrigger.action.ReadValue<float>());
        //Проверка ввода через Input System
        //Debug.Log(grabInteractable.attachTransform);

        //Debug.Log(grabInteractable.attachTransform.name);
        //if (triggerAction.action != null && triggerAction.action.ReadValue<float>() > 0.8f && Time.time >= nextFireTime && grabInteractable.isSelected && IsLoaded)
        if (((LeftGrip.action.ReadValue<float>() > 0.8f && LeftTrigger.action.ReadValue<float>() > 0.8f) || (RightGrip.action.ReadValue<float>() > 0.8f && RightTrigger.action.ReadValue<float>() > 0.8f)) && Time.time >= nextFireTime && grabInteractable.isSelected)
        {
            Debug.Log("Выстрел игрока!");
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
        if (audioSource != null && shotSound != null)
            audioSource.PlayOneShot(shotSound);
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
            // Создаём трассер
            if (tracerPrefab != null)
            {
                StartCoroutine(SpawnTracer(firePoint.position, hit.point));
            }
        }
        else
        {
            StartCoroutine(SpawnTracer(firePoint.position, firePoint.position + firePoint.forward * range));
        }
    }
    IEnumerator SpawnTracer(Vector3 start, Vector3 end)
    {
        GameObject tracer = Instantiate(tracerPrefab, start, Quaternion.identity);

        float distance = Vector3.Distance(start, end);
        float time = 0f;
        float duration = distance / tracerSpeed;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            tracer.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        tracer.transform.position = end;
        if (tracer.transform.position == end) Destroy(tracer);
        Destroy(tracer, tracerLifetime);
    }


    IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
    }
}
