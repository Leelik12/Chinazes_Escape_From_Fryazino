using System;
using System.Collections.Generic;
using LogitechG29.Sample.Input;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class CarControllerSample : MonoBehaviourPun
{
    [Header("Важное, не трогать!")]
    [SerializeField] private InputControllerReader inputControllerReader;
    [SerializeField] private List<AxleInfo> axleInfos;
    private Rigidbody rb;

    [SerializeField] private float maxMotorTorque;
    [SerializeField] private float maxSteeringAngle;
    [SerializeField] private float maxBrakeTorque = 10000f;
    [SerializeField] private float[] gearRatios = { 0f, 3.8f, 2.2f, 1.5f, 1.2f, 1f, 0.8f, -0.5f };

    [Header("Ограничение скорости (км/ч)")]
    [SerializeField] private float[] gearSpeedLimits = { 0f, 20f, 40f, 60f, 90f, 120f, 150f, 20f };

    [Header("Звуки")]
    [SerializeField] private AudioSource Engine;
    [SerializeField] private AudioClip Racing;

    [Header("UI")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text GearText;

    [Header("Визуальный руль")]
    [SerializeField] private Transform steeringWheelVisual;
    [SerializeField] private float visualWheelRotationAngle = 450f;
    [SerializeField] private float steeringSmoothness = 10f;

    private float currentVisualAngle = 0f;

    [Header("Временные переменные")]
    private float throttleInput;
    private float motor;
    private float steering;
    private float finalmotor;
    private int currentGear;
    private float brakeInput;

    private void Start()
    {
        Engine.Play();
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.8f, 0);
    }

    public void FixedUpdate()
    {
        if (!photonView.IsMine) return;

        // Газ
        throttleInput = inputControllerReader.Throttle;

        // Тормоз
        brakeInput = inputControllerReader.Brake > 0.2f ? inputControllerReader.Brake : 0f;

        // Базовый момент
        motor = maxMotorTorque * throttleInput;
        steering = maxSteeringAngle * inputControllerReader.Steering;
        finalmotor = motor;

        if (inputControllerReader.Clutch > 0.6f) // коробас отрабатывает только если сцепа выжата
        {
            if (inputControllerReader.Shifter1)
            {
                currentGear = 1;
                if (GearText) GearText.text = "1";
            }
            else if (inputControllerReader.Shifter2)
            {
                currentGear = 2;
                if (GearText) GearText.text = "2";
            }
            else if (inputControllerReader.Shifter3)
            {
                currentGear = 3;
                if (GearText) GearText.text = "3";
            }
            else if (inputControllerReader.Shifter4)
            {
                currentGear = 4;
                if (GearText) GearText.text = "4";
            }
            else if (inputControllerReader.Shifter5)
            {
                currentGear = 5;
                if (GearText) GearText.text = "5";
            }
            else if (inputControllerReader.Shifter6)
            {
                currentGear = 6;
                if (GearText) GearText.text = "6";
            }
            else if (inputControllerReader.Shifter7)
            {
                currentGear = 7;
                if (GearText) GearText.text = "-1";
            }
            else
            {
                currentGear = 0;
                if (GearText) GearText.text = "N";
            }
        }

        // Ограничение скорости по передаче
        float currentSpeed = rb.linearVelocity.magnitude * 3.6f;
        if (currentGear > 0 && currentGear < gearSpeedLimits.Length)
        {
            if (currentSpeed >= gearSpeedLimits[currentGear])
            {
                finalmotor = 0f; // перестаём ускоряться
            }
        }

        // Применение передаточного отношения
        finalmotor *= gearRatios[currentGear];

        // Аудио двигателя
        float correction = Mathf.Lerp(1f, 1.4f, throttleInput);
        Engine.pitch = correction;

        // UI
        UpdateGauges();
        UpdateSteeringWheelVisual();

        // Передача момента и тормоза на колёса
        foreach (var axleInfo in axleInfos)
        {
            if (axleInfo.steering)
            {
                axleInfo.leftWheel.steerAngle = steering;
                axleInfo.rightWheel.steerAngle = steering;
            }

            if (axleInfo.motor && inputControllerReader.Clutch < 0.5f)
            {
                axleInfo.leftWheel.motorTorque = finalmotor;
                axleInfo.rightWheel.motorTorque = finalmotor;

                axleInfo.leftWheel.brakeTorque = brakeInput * maxBrakeTorque;
                axleInfo.rightWheel.brakeTorque = brakeInput * maxBrakeTorque;
            }
        }
    }

    [Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public bool motor;
        public bool steering;
    }

    private void UpdateGauges()
    {
        float speed = rb.linearVelocity.magnitude * 3.6f;
        if (speedText) speedText.text = $"{Mathf.RoundToInt(speed)} km/h";
    }

    private void UpdateSteeringWheelVisual()
    {
        if (steeringWheelVisual == null) return;

        float targetAngle = inputControllerReader.Steering * visualWheelRotationAngle;
        currentVisualAngle = Mathf.Lerp(currentVisualAngle, targetAngle, Time.deltaTime * steeringSmoothness);
        steeringWheelVisual.localRotation = Quaternion.Euler(0f, 0f, -currentVisualAngle);
    }
}



