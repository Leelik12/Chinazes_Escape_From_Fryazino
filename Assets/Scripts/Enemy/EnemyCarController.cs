using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Rigidbody))]
public class EnemyCarController : MonoBehaviourPun
{
    [Header("Цель (игрок/машина)")]
    public Transform target;

    [Header("Параметры движения")]
    public float speed = 10f;
    public float rotationSpeed = 5f;
    public float stoppingDistance = 5f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (!photonView.IsMine && PhotonNetwork.IsConnected)
        {
            // Этот объект управляется хостом, остальные просто наблюдают
            rb.isKinematic = true;
            return;
        }

        if (target == null)
        {
            // Ищем машину игрока с тегом "Car"
            GameObject playerCar = GameObject.FindWithTag("Car");
            if (playerCar != null)
                target = playerCar.transform;
        }
    }

    void FixedUpdate()
    {
        if (!photonView.IsMine) return; // управление только у MasterClient
        if (target == null) return;

        // направление на игрока
        Vector3 direction = (target.position - transform.position).normalized;

        // плавный поворот
        Quaternion lookRotation = Quaternion.LookRotation(new Vector3(direction.x, 0, direction.z));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, rotationSpeed * Time.fixedDeltaTime);

        // движение вперёд
        float distance = Vector3.Distance(transform.position, target.position);
        if (distance > stoppingDistance)
        {
            rb.MovePosition(transform.position + transform.forward * speed * Time.fixedDeltaTime);
        }
    }
}
