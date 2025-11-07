using System;
using System.Collections;
using _2DOF;
using UnityEngine;

public class CarTelemetryHandler : MonoBehaviour
{
    private const float WAIT_TIME = SendingData.WAIT_TIME / 1000f;

    [Header("References")]
    [SerializeField] private Transform vehicleTransform;
    [SerializeField] private Rigidbody rigidbody;

    [Header("Effect Factors")]
    [Tooltip("��������� ������ ��������� ��������� �� ���������� ��������� (�����/�����).")]
    [SerializeField] private float accelPitchFactor = 0.02f;

    [Tooltip("��������� ������ ��������� ��������� �� ������� ��������� (� ��������).")]
    [SerializeField] private float cornerRollFactor = 0.02f;

    [Header("Impact settings")]
    [Tooltip("��������� ���� ������� ��� �����.")]
    [SerializeField] private float impactFactor = 0.015f;

    [Tooltip("�������� ��������� ������� �����.")]
    [SerializeField] private float impactDamping = 2.5f;

    [Header("Blending and Limits")]
    [Tooltip("������������ ���� ��������� �� ������� (Pitch).")]
    [SerializeField] private float maxPitch = 10f;
    [Tooltip("������������ ���� ��������� �� ����� (Roll).")]
    [SerializeField] private float maxRoll = 10f;

    [Tooltip("������� ��������� �������� ��������� ������ �� ��������� ������� ���� (0.5 = 50%).")]
    [Range(0f, 1f)][SerializeField] private float realRotationWeight = 0.5f;

    [Tooltip("�������� ����������� ��������� ��������.")]
    [SerializeField] private float smoothSpeed = 5f;

    private ObjectTelemetryData _telemetryData;
    private SendingData _sendingData;

    private Vector3 lastVelocity;
    private float currentPitch;
    private float currentRoll;
    private float impactPitch;
    private float impactRoll;

    private void Awake()
    {
        _sendingData = new SendingData();
        _telemetryData = _sendingData.ObjectTelemetryData;
    }

    private void OnEnable()
    {
        StartCoroutine(TelemetryHandler());
        _sendingData.SendingStart();

        lastVelocity = rigidbody.linearVelocity;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _sendingData.SendingStop();
    }

    private IEnumerator TelemetryHandler()
    {
        while (true)
        {
            if (_telemetryData == null)
            {
                yield return new WaitForSeconds(WAIT_TIME * 2);
                continue;
            }

            UpdatePlatformMotion();
            yield return new WaitForSeconds(WAIT_TIME);
        }
    }

    private void UpdatePlatformMotion()
    {
        Vector3 velocity = rigidbody.linearVelocity;
        Vector3 acceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;

        // --- ��������� �����: ��������� ---
        float forwardAccel = Vector3.Dot(acceleration, vehicleTransform.forward);
        float lateralAccel = Vector3.Dot(acceleration, vehicleTransform.right);

        float targetEffectPitch = -forwardAccel * accelPitchFactor; // ���������� = ������ �����
        float targetEffectRoll = -lateralAccel * cornerRollFactor;

        // --- �������� �����: ������ ���������� ---
        Vector3 localEuler = vehicleTransform.localRotation.eulerAngles;

        // ������������ ���� � �������� -180..180
        float realPitch = NormalizeAngle(localEuler.x);
        float realRoll = NormalizeAngle(localEuler.z);

        // --- ������ ������������ ---
        impactPitch = Mathf.Lerp(impactPitch, 0f, Time.deltaTime * impactDamping);
        impactRoll = Mathf.Lerp(impactRoll, 0f, Time.deltaTime * impactDamping);

        // --- ����������� ---
        float finalPitch =
            (realPitch * realRotationWeight) +
            ((targetEffectPitch + impactPitch) * (1f - realRotationWeight));

        float finalRoll =
            (realRoll * realRotationWeight) +
            ((targetEffectRoll + impactRoll) * (1f - realRotationWeight));

        // --- ����������� ---
        finalPitch = Mathf.Clamp(finalPitch, -maxPitch, maxPitch);
        finalRoll = Mathf.Clamp(finalRoll, -maxRoll, maxRoll);

        // --- ����������� ---
        currentPitch = Mathf.Lerp(currentPitch, finalPitch, Time.deltaTime * smoothSpeed);
        currentRoll = Mathf.Lerp(currentRoll, finalRoll, Time.deltaTime * smoothSpeed);

        //Debug.Log(currentPitch);
        //Debug.Log(currentRoll);

        // --- �������� �� ��������� ---
        _telemetryData.Angles = new Vector3(currentPitch, 0f, currentRoll);
        _telemetryData.Velocity = velocity;

        lastVelocity = velocity;
    }

    private void OnCollisionEnter(Collision collision)
    {
        float impactForce = collision.relativeVelocity.magnitude;
        impactPitch = -impactForce * impactFactor;
        impactRoll = UnityEngine.Random.Range(-impactForce, impactForce) * impactFactor * 0.5f;
    }

    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        return angle;
    }
}
