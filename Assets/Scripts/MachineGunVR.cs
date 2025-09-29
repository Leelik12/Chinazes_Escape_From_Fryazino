using Photon.Pun;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
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
    public float lightDuration;

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

    [Header("Трассер")]
    public GameObject tracerPrefab;
    public float tracerSpeed = 200f;
    public float tracerLifetime = 1f;

    private float nextFireTime = 0f;

    void Awake()
    {
        LeftGrip.action.Enable();
        RightGrip.action.Enable();
        LeftTrigger.action.Enable();
        RightTrigger.action.Enable();
    }

    void Update()
    {
        if (!photonView.IsMine) return; // управление только своим ригом

        if (((LeftGrip.action.ReadValue<float>() > 0.8f && LeftTrigger.action.ReadValue<float>() > 0.8f) ||
             (RightGrip.action.ReadValue<float>() > 0.8f && RightTrigger.action.ReadValue<float>() > 0.8f)) &&
            Time.time >= nextFireTime &&
            grabInteractable.isSelected)
        {
            nextFireTime = Time.time + fireRate;

            // Вызываем RPC для стрельбы, чтобы все игроки увидели
            photonView.RPC("RPC_Shoot", RpcTarget.All, firePoint.position, firePoint.forward);
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

            // Урон
            if (hit.collider.CompareTag("Head") || hit.collider.CompareTag("Body") || hit.collider.CompareTag("Leg"))
            {
                EnemyHeaths target = hit.collider.GetComponentInParent<EnemyHeaths>();
                if (target != null)
                {
                    float finalDamage = damage;
                    if (hit.collider.CompareTag("Head")) finalDamage *= 2f;
                    else if (hit.collider.CompareTag("Leg")) finalDamage *= 0.5f;

                    target.TakeDamage(finalDamage);
                }
            }
        }

        // Трассер
        if (tracerPrefab != null)
        {
            StartCoroutine(SpawnTracer(origin, hitPoint));
        }
    }

    IEnumerator SpawnTracer(Vector3 start, Vector3 end)
    {
        GameObject tracer = PhotonNetwork.Instantiate(tracerPrefab.name, start, Quaternion.identity);

        float distance = Vector3.Distance(start, end);
        float duration = distance / tracerSpeed;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            tracer.transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        tracer.transform.position = end;
        Destroy(tracer, tracerLifetime);
    }

    IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
    }
}
