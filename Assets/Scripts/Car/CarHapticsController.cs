using UnityEngine;
using Bhaptics.SDK2;

[RequireComponent(typeof(Rigidbody))]
public class CarHapticsController : MonoBehaviour
{
    [Header("������")]
    [SerializeField] private Rigidbody carRigidbody;
    [SerializeField] private PlayerHealth carHealth; // ��� ������ ������ ��������

    [Header("��������� ����������������")]
    [SerializeField] private float accelThreshold = 4f;     // ����� ���������
    [SerializeField] private float brakeThreshold = -4f;    // ����� ����������
    [SerializeField] private float sideAccelThreshold = 3f; // ����� �������� ���������

    private Vector3 lastVelocity;
    private bool lowHealthTriggered = false;

    private void Reset()
    {
        carRigidbody = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (carHealth != null)
        {
            carHealth.OnDamageTaken += OnCarDamage;
            carHealth.OnDeath += OnCarDeath;
        }
    }

    private void OnDestroy()
    {
        if (carHealth != null)
        {
            carHealth.OnDamageTaken -= OnCarDamage;
            carHealth.OnDeath -= OnCarDeath;
        }
    }

    private void Update()
    {
        CheckMovementHaptics();
        CheckHealth();
    }

    private void CheckMovementHaptics()
    {
        Vector3 localVel = transform.InverseTransformDirection(carRigidbody.linearVelocity);
        Vector3 accel = (carRigidbody.linearVelocity - lastVelocity) / Time.deltaTime;
        Vector3 localAccel = transform.InverseTransformDirection(accel);

        // ���������� ��������� � �����/�����
        if (localAccel.z > accelThreshold)
        {
            BhapticsLibrary.Play(eventId:"razgon",startMillis:0, intensity: 0.5f, duration:1,angleX:0,offsetY:0);
            Debug.Log("������ .�������� ����������");
        }
        else if (localAccel.z < brakeThreshold)
        {
            BhapticsLibrary.Play(eventId:"remen_bezopasnosty", startMillis: 0, intensity: 0.5f, duration: 1, angleX: 0, offsetY: 0);
            Debug.Log("���������� .�������� ����������");
        }

        // ������� ��������� � �����/������
        if (Mathf.Abs(localAccel.x) > sideAccelThreshold)
        {
            if (localAccel.x > 0)
            {
                BhapticsLibrary.Play(eventId: "povorot_pravo", startMillis: 0, intensity: 0.5f, duration: 1, angleX: 0, offsetY: 0);
                Debug.Log("������� ������. �������� ����������");
            }
            else
            {
                BhapticsLibrary.Play(eventId: "povorot_levo", startMillis: 0, intensity: 0.5f, duration: 1, angleX: 0, offsetY: 0);
                Debug.Log("������� �����. �������� ����������");
            }
        }

        lastVelocity = carRigidbody.linearVelocity;
    }

    private void CheckHealth()
    {
        if (carHealth == null) return;

        float healthPercent = (float)carHealth.CurrentHealth / carHealth.MaxHealth;

        if (healthPercent <= 0.3f && !lowHealthTriggered)
        {
            lowHealthTriggered = true;
            BhapticsLibrary.Play(eventId:"suit_low_hp", startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
            BhapticsLibrary.Play(eventId:"hand_low_hp", startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
            Debug.Log("������ �������� .�������� ����������");
        }
        else if (healthPercent > 0.3f && lowHealthTriggered)
        {
            lowHealthTriggered = false;
        }
    }

    private void OnCarDamage(int damage)
    {
        BhapticsLibrary.Play(eventId:"damage_hands", startMillis: 0, intensity: 0.5f, duration: 1, angleX: 0, offsetY: 0);
        Debug.Log("������� ���� .�������� ����������");
    }

    private void OnCarDeath()
    {
        BhapticsLibrary.Play(eventId: "suit_low_hp", startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
        BhapticsLibrary.Play(eventId: "hand_low_hp", startMillis: 0, intensity: 1, duration: 1, angleX: 0, offsetY: 0);
        Debug.Log("������ .�������� ����������");
    }
}
