#region

using System;
using System.Collections.Generic;
using LogitechG29.Sample.Input;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

public class CarControllerSample : MonoBehaviour
{
    [Header("Важное, не трогать!")]
    [SerializeField] private InputControllerReader inputControllerReader; // инпутер
    [SerializeField] private List<AxleInfo> axleInfos; // информация о каждой отдельной оси
    private Rigidbody rb;
    [SerializeField]
    private float maxMotorTorque; // максимальный крутящий момент, который двигатель может приложить к колесу

    [SerializeField] private float maxSteeringAngle; // максимальный угол поворота, который может иметь колесо
    private float[] gearRatios = {0f, 3.8f, 2.2f, 1.5f, 1.2f , 1f, 0.8f, -0.5f}; //передатка коробки

    [Header("Звуки")]
    [SerializeField] private AudioSource Engine; //источник звука
    [SerializeField] private AudioClip Racing; // звук движка
    [Header("UI")]
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text GearText;
    private void Start()
    {
        Engine.Play();
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }
    public void FixedUpdate()
    {
        var speed = 0f;
        if (inputControllerReader.Throttle != 0)
        {
            speed = inputControllerReader.Throttle;
        }
        else if (inputControllerReader.Brake != 0)
        {
            speed = -inputControllerReader.Brake;
        }

        var motor = maxMotorTorque * speed;
        var steering = maxSteeringAngle * inputControllerReader.Steering;
        var finalmotor = motor;
        //коробка
        if(inputControllerReader.Clutch > 0.5f) // коробас отрабатывает только если сцепа выжата
        {
            if (inputControllerReader.Shifter1)
            {
                finalmotor = motor * gearRatios[1];
                if (GearText) GearText.text = "1";
            }
            else if (inputControllerReader.Shifter2)
            {
                finalmotor = motor * gearRatios[2];
                if (GearText) GearText.text = "2";
            }
            else if (inputControllerReader.Shifter3)
            {
                finalmotor = motor * gearRatios[3];
                if (GearText) GearText.text = "3";
            }
            else if (inputControllerReader.Shifter4)
            {
                finalmotor = motor * gearRatios[4];
                if (GearText) GearText.text = "4";
            }
            else if (inputControllerReader.Shifter5)
            {
                finalmotor = motor * gearRatios[5];
                if (GearText) GearText.text = "5";
            }
            else if (inputControllerReader.Shifter6)
            {
                finalmotor = motor * gearRatios[6];
                if (GearText) GearText.text = "6";
            }
            else if (inputControllerReader.Shifter7)
            {
                finalmotor = motor * gearRatios[7];
                if (GearText) GearText.text = "-1";
            }
            else
            {
                finalmotor = motor * gearRatios[0];
                if (GearText) GearText.text = "N";
            }
        }
        //аудио двигла
        float correction = Mathf.Lerp(1f, 1.4f, inputControllerReader.Throttle);
        Engine.pitch = correction;
        //выход на меню
        if (inputControllerReader.Return)
        {
            SceneManager.LoadSceneAsync(0);
        }
        //UI
        if (speedText) speedText.text = $"{Mathf.RoundToInt(speed)} km/h";
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
}