using UnityEngine;
using Photon.Pun;
using System.Collections;

public class EnemyGun : MonoBehaviourPun
{
    [Header("Target")]
    public Transform target;
    [Header("Gun Settings")]
    public float fireRate = 0.3f;
    public float damage = 10f;
    public float range = 50f;
    [Tooltip("Максимальный угол разброса в градусах")]
    public float spreadAngle = 5f;
    [Header("Rotation")]
    public float rotationSpeed = 5f;

    [Header("Muzzle Effects")]
    public ParticleSystem muzzleFlash;
    public Light muzzleLight;
    public float lightDuration = 0.05f;

    [Header("Hit Effects")]
    public GameObject hitEffectPrefabDust;
    public GameObject hitEffectPrefabSparks;
    public float hitEffectLifetime = 2f;
    public LayerMask hitLayerMask = ~0;

    private float nextFireTime = 0f;
    private void Start()
    {
        if (target == null)
        {
            GameObject playerCar = GameObject.FindWithTag("Car");
            if (playerCar != null) target = playerCar.transform;
        }
    }
    void Update()
    {
        if (!photonView.IsMine) return; // только мастер управляет поведением

        if (target == null) return;

        // Плавное наведение пулемета на игрока
        Vector3 targetDir = (target.position - transform.position).normalized;
        Quaternion lookRot = Quaternion.LookRotation(targetDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);

        // Стрельба
        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            photonView.RPC(nameof(ShootRPC), RpcTarget.All);
        }
    }

    [PunRPC]
    private void ShootRPC()
    {
        // Разброс
        Vector3 direction = transform.forward;
        direction = Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0) * direction;

        // Muzzle flash и свет
        if (muzzleFlash != null)
            muzzleFlash.Play();
        if (muzzleLight != null)
            StartCoroutine(MuzzleLightFlash());

        // Raycast для попаданий
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, range, hitLayerMask, QueryTriggerInteraction.Ignore))
        {
            // Декали попадания
            if (hitEffectPrefabDust != null)
            {
                GameObject fx1 = Instantiate(hitEffectPrefabDust, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
                Destroy(fx1, hitEffectLifetime);
            }
            if (hitEffectPrefabSparks != null)
            {
                GameObject fx2 = Instantiate(hitEffectPrefabSparks, hit.point + hit.normal * 0.01f, Quaternion.LookRotation(hit.normal));
                Destroy(fx2, hitEffectLifetime);
            }

            // Наносим урон игроку
            PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.RequestDamage((int)damage);
            }
        }
    }

    private IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
    }
}
