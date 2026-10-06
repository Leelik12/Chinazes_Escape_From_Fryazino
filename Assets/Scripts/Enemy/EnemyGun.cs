using UnityEngine;
using Photon.Pun;
using System.Collections;

public class EnemyGun : MonoBehaviourPun, IPunObservable
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

    // Для сетевой интерполяции
    private Quaternion networkRotation;
    private float syncLerpSpeed = 10f;

    private void Start()
    {
        if (target == null)
        {
            GameObject playerCar = GameObject.FindWithTag("Car");
            if (playerCar != null)
                target = playerCar.transform;
        }

        networkRotation = transform.rotation;
    }

    private void Update()
    {
        if (photonView.IsMine)
        {
            HandleTurretLogic();
        }
        else
        {
            // плавная интерполяция поворота
            transform.rotation = Quaternion.Slerp(transform.rotation, networkRotation, Time.deltaTime * syncLerpSpeed);
        }
    }

    private void HandleTurretLogic()
    {
        if (target == null) return;

        // Плавное наведение пулемета на игрока
        Vector3 targetDir = (target.position - transform.position).normalized;
        Quaternion lookRot = Quaternion.LookRotation(targetDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, rotationSpeed * Time.deltaTime);

        // Стрельба
        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;
            Fire();
        }
    }

    // Попадание и урон считает только владелец, остальным рассылается результат для эффектов,
    // иначе каждый клиент бросал свой разброс и наносил урон повторно
    private void Fire()
    {
        // Разброс относительно направления ствола
        Vector3 direction = transform.rotation * Quaternion.Euler(
            Random.Range(-spreadAngle, spreadAngle),
            Random.Range(-spreadAngle, spreadAngle),
            0) * Vector3.forward;

        bool hasHit = Physics.Raycast(transform.position, direction, out RaycastHit hit, range, hitLayerMask, QueryTriggerInteraction.Ignore);
        if (hasHit)
        {
            PlayerHealth player = hit.collider.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.RequestDamage((int)damage);
            }
        }

        photonView.RPC(nameof(ShootRPC), RpcTarget.All, hasHit, hit.point, hit.normal);
    }

    [PunRPC]
    private void ShootRPC(bool hasHit, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Muzzle flash и свет
        if (muzzleFlash != null)
            muzzleFlash.Play();
        if (muzzleLight != null)
            StartCoroutine(MuzzleLightFlash());

        if (!hasHit) return;

        // Эффекты попадания
        if (hitEffectPrefabDust != null)
        {
            GameObject fx1 = Instantiate(hitEffectPrefabDust, hitPoint + hitNormal * 0.01f, Quaternion.LookRotation(hitNormal));
            Destroy(fx1, hitEffectLifetime);
        }
        if (hitEffectPrefabSparks != null)
        {
            GameObject fx2 = Instantiate(hitEffectPrefabSparks, hitPoint + hitNormal * 0.01f, Quaternion.LookRotation(hitNormal));
            Destroy(fx2, hitEffectLifetime);
        }
    }

    private IEnumerator MuzzleLightFlash()
    {
        muzzleLight.enabled = true;
        yield return new WaitForSeconds(lightDuration);
        muzzleLight.enabled = false;
    }

    // --- Сетевая передача вращения ---
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // только мастер (владелец) передаёт своё направление
            stream.SendNext(transform.rotation);
        }
        else
        {
            // клиенты принимают и плавно интерполируют
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }
}
