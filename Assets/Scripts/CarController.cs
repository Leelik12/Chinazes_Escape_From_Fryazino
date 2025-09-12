using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Настройки движения")]
    public float acceleration = 15000f;   // сила разгона
    public float steering = 45f;         // угол поворота
    public float maxSpeed = 50f;         // максимальная скорость

    [Header("Настройки физики")]
    public float drag = 0.98f;           // сопротивление
    public float angularDrag = 0.95f;    // сопротивление вращению
    [Header("Дополнительно")]
    public float downforce = 100f; // сила прижатия к земле
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // центр масс ниже для устойчивости
    }

    private void FixedUpdate()
    {
        float moveInput = Input.GetAxis("Vertical");   // W/S или 
        float steerInput = Input.GetAxis("Horizontal"); // A/D или 

        // ограничение скорости
        if (rb.velocity.magnitude < maxSpeed)
        {
            rb.AddForce(transform.forward * moveInput * acceleration * Time.fixedDeltaTime);
        }

        // поворот (работает только если машина двигается)
        if (rb.velocity.magnitude > 0.1f)
        {
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, steerInput * steering * Time.fixedDeltaTime, 0));
        }

        // добавляем "трение"
        rb.linearVelocity *= drag;
        rb.angularVelocity *= angularDrag;
        rb.AddForce(-transform.up * downforce * rb.velocity.magnitude);
    }
}
