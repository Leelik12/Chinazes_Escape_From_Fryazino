#region

using System;
using System.Collections.Generic;
using LogitechG29.Sample.Input;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;
#endregion

public class CarControllerSample : MonoBehaviourPun
{
    [Header("Важное, не трогать!")]
    [SerializeField] private InputControllerReader inputControllerReader; // инпутер
    [SerializeField] private List<AxleInfo> axleInfos; // информация о каждой отдельной оси
    private Rigidbody rb;
    [SerializeField]
    private float maxMotorTorque; // максимальный крутящий момент, который двигатель может приложить к колесу

    [SerializeField] private float maxSteeringAngle; // максимальный угол поворота, который может иметь колесо
    [SerializeField] private float[] gearRatios = {0f, 3.8f, 2.2f, 1.5f, 1.2f , 1f, 0.8f, -0.5f}; //передатка коробки

    [Header("Звуки")]
    [SerializeField] private AudioSource Engine; //источник звука
    [SerializeField] private AudioClip Racing; // звук движка
    [Header("UI")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text GearText;
    public RectTransform rpmNeedle;
    public float rpmMaxAngle = -220f;
    public float rpmMinAngle = 40f;

    [Header("Временные переменные")]
    private float speed;
    private float motor;
    private float steering;
    private float finalmotor;
    private int currentGear;
    private void Start()
    {
        Engine.Play();
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }
    public void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        if (inputControllerReader.Throttle != 0)
        {
            speed = inputControllerReader.Throttle;
        }
        else if (inputControllerReader.Brake != 0)
        {
            speed = -inputControllerReader.Brake;
        }

        motor = maxMotorTorque * speed;
        steering = maxSteeringAngle * inputControllerReader.Steering;
        finalmotor = motor;
        //коробка
        if(inputControllerReader.Clutch > 0.6f) // коробас отрабатывает только если сцепа выжата
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
                currentGear = -1;
                if (GearText) GearText.text = "-1";
            }
            else
            {
                currentGear = 0;
                if (GearText) GearText.text = "N";
            }
        }
        finalmotor = motor * gearRatios[currentGear];
        //аудио двигла
        float correction = Mathf.Lerp(1f, 1.4f, inputControllerReader.Throttle);
        Engine.pitch = correction;
        //выход на меню
        //if (inputControllerReader.Return)
        //{
        //    SceneManager.LoadSceneAsync(0);
        //}

        //UI
        UpdateGauges();

        //передача вращающего момента
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
            }
        }
    }

    [Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public bool motor; // это колесо прикреплено к мотору?
        public bool steering; // применяет ли это колесо угол поворота?
    }
    void UpdateGauges()
    {
        float speed = rb.linearVelocity.magnitude * 3.6f;

        if (speedText) speedText.text = $"{Mathf.RoundToInt(speed)} km/h";
        if (GearText) GearText.text = $"{currentGear}";

        if (rpmNeedle)
        {
            float rpmNorm = Mathf.Clamp01(inputControllerReader.Throttle / 1);
            rpmNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(rpmMinAngle, rpmMaxAngle, rpmNorm));
        }
    }
}