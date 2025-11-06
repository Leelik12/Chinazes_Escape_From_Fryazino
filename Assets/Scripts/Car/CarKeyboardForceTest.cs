using UnityEngine;
using Photon.Pun;

public class CarKeyboardImpulseTest : MonoBehaviourPun
{
    [Header("Параметры импульса")]
    public float impulseForce = 5000f;       // сила импульса
    public float cooldownTime = 5f;          // задержка между импульсами (в секундах)

    private Rigidbody rb;
    private float cooldownTimer = 0f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        // обновляем таймер перезарядки
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        // ждём, пока перезарядка не закончится
        if (cooldownTimer > 0f) return;

        // направление импульса (только вперёд/назад)
        Vector3 direction = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.I)) direction = transform.forward;   // вперёд
        if (Input.GetKeyDown(KeyCode.K)) direction = -transform.forward;  // назад

        if (direction != Vector3.zero)
        {
            ApplyImpulse(direction);
            cooldownTimer = cooldownTime; // сбрасываем таймер перезарядки
        }
    }

    private void ApplyImpulse(Vector3 direction)
    {
        // применяем импульс
        rb.AddForce(direction * impulseForce, ForceMode.Impulse);
    }
}
