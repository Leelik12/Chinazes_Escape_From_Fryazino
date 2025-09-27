using Photon.Pun;
using UnityEngine;

public class MachineGun : MonoBehaviourPun
{
    [Header("Rotation Settings")]
    public float horizontalSpeed = 2.0f;
    public float verticalSpeed = 2.0f;
    public float maxVerticalAngle = 35f;
    public float minVerticalAngle = -20f;

    [Header("Shooting Settings")]
    public float fireRate = 10f;
    public float range = 100f;
    public Transform firePoint;
    public LayerMask hitLayers;
    public ParticleSystem muzzleFlash;

    [Header("Audio Settings")]
    public AudioClip fireSound;
    public float volume = 0.5f;
    public bool loopFireSound = false;

    private float nextFireTime;
    private float currentHorizontalAngle;
    private float currentVerticalAngle;
    private AudioSource audioSource;

    void Start()
    {
        if (!photonView.IsMine)
        {
            var cam = GetComponentInChildren<Camera>(true);
            if (cam != null) cam.gameObject.SetActive(false);
        }
        // Настраиваем AudioSource
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.clip = fireSound;
        audioSource.volume = volume;
        audioSource.loop = loopFireSound;
    }

    void Update()
    {
        if (!photonView.IsMine) return;
        HandleRotation();
        HandleShooting();
    }

    void HandleRotation()
    {
        // Получаем ввод мыши
        float mouseX = Input.GetAxis("Mouse X") * horizontalSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * verticalSpeed;

        // Изменяем углы вращения
        currentHorizontalAngle += mouseX;
        currentVerticalAngle -= mouseY;

        // Ограничиваем вертикальный угол
        currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);

        // Применяем вращение
        transform.localEulerAngles = new Vector3(currentVerticalAngle, currentHorizontalAngle, 0f);
    }

    void HandleShooting()
    {
        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + 1f / fireRate;
            Shoot();
        }

        // Останавливаем звук, когда прекращаем стрельбу
        if (Input.GetButtonUp("Fire1") && loopFireSound && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    void Shoot()
    {
        // Воспроизводим эффект дульного вспышки
        if (muzzleFlash != null)
            muzzleFlash.Play();

        // Воспроизводим звук выстрела
        PlayFireSound();

        // Создаем луч
        Ray ray = new Ray(firePoint.position, firePoint.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, range, hitLayers))
        {
            // Обрабатываем попадание
            Debug.Log("Попадание в: " + hit.collider.name);

            // Здесь можно добавить логику нанесения урона
            // if (hit.collider.TryGetComponent<Enemy>(out Enemy enemy))
            // {
            //     enemy.TakeDamage(damage);
            // }
        }

        // Визуализация луча (появляется только в редакторе)
        Debug.DrawRay(ray.origin, ray.direction * range, Color.red, 0.1f);
    }

    void PlayFireSound()
    {
        if (fireSound == null) return;

        if (loopFireSound)
        {
            // Для непрерывного звука пулемета
            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            // Для отдельных звуков выстрелов
            audioSource.PlayOneShot(fireSound, volume);
        }
    }
}