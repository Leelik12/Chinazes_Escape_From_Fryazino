using UnityEngine;
using Photon.Pun;

public class EnemyGunner : MonoBehaviourPun
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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
