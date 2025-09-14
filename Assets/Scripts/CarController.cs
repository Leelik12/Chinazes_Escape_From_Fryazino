using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("��������� ��������")]
    public float acceleration = 15000f;   // ���� �������
    public float steering = 45f;         // ���� ��������
    public float maxSpeed = 50f;         // ������������ ��������

    [Header("��������� ������")]
    public float drag = 0.98f;           // �������������
    public float angularDrag = 0.95f;    // ������������� ��������
    [Header("�������������")]
    public float downforce = 100f; // ���� �������� � �����
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0); // ����� ���� ���� ��� ������������
    }

    private void FixedUpdate()
    {
        float moveInput = Input.GetAxis("Vertical");   // W/S ��� 
        float steerInput = Input.GetAxis("Horizontal"); // A/D ��� 

        // ����������� ��������
        if (rb.linearVelocity.magnitude < maxSpeed)
        {
            rb.AddForce(transform.forward * moveInput * acceleration * Time.fixedDeltaTime);
        }

        // ������� (�������� ������ ���� ������ ���������)
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            rb.MoveRotation(rb.rotation * Quaternion.Euler(0, steerInput * steering * Time.fixedDeltaTime, 0));
        }

        // ��������� "������"
        rb.linearVelocity *= drag;
        rb.angularVelocity *= angularDrag;
        rb.AddForce(-transform.up * downforce * rb.linearVelocity.magnitude);
    }
}
