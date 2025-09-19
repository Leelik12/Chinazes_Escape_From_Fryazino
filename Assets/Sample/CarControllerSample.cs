#region

using System;
using System.Collections.Generic;
using LogitechG29.Sample.Input;
using UnityEngine;

#endregion

public class CarControllerSample : MonoBehaviour
{
    [Header("Важное, не трогать!")]
    [SerializeField] private InputControllerReader inputControllerReader; // инпутер
    [SerializeField] private List<AxleInfo> axleInfos; // информация о каждой отдельной оси

    [SerializeField]
    private float maxMotorTorque; // максимальный крутящий момент, который двигатель может приложить к колесу

    [SerializeField] private float maxSteeringAngle; // максимальный угол поворота, который может иметь колесо
    [SerializeField] private float[] gearRatios = {0f, 3.8f, 2.2f, 1.5f, 1.2f , 1f, 0.8f, -3.8f}; //передатка коробки

    [Header("Звуки")]
    [SerializeField] private AudioSource Engine;
    [SerializeField] private AudioClip Idle;
    [SerializeField] private AudioClip Racing;

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
            }
            else if (inputControllerReader.Shifter2)
            {
                finalmotor = motor * gearRatios[2];
            }
            else if (inputControllerReader.Shifter3)
            {
                finalmotor = motor * gearRatios[3];
            }
            else if (inputControllerReader.Shifter4)
            {
                finalmotor = motor * gearRatios[4];
            }
            else if (inputControllerReader.Shifter5)
            {
                finalmotor = motor * gearRatios[5];
            }
            else if (inputControllerReader.Shifter6)
            {
                finalmotor = motor * gearRatios[6];
            }
            else if (inputControllerReader.Shifter7)
            {
                finalmotor = motor * gearRatios[7];
            }
            else
            {
                finalmotor = motor * gearRatios[0];
            }
        }
        //аудио двигла


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