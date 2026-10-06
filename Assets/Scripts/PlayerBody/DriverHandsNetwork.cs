using UnityEngine;
using Photon.Pun;

public class DriverHandsNetwork : MonoBehaviourPun
{
    [Header("Руль")]
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float maxSteeringAngle = 180f; // ±180°

    [Header("Анкорные точки для рук")]
    [SerializeField] private Transform leftTopAnchor;
    [SerializeField] private Transform leftBottomAnchor;
    [SerializeField] private Transform rightTopAnchor;
    [SerializeField] private Transform rightBottomAnchor;

    [Header("IK Targets")]
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;

    private float currentSteeringAngle; // локально вычисляемый угол
    private float networkSteeringAngle; // угол для всех клиентов

    void Update()
    {
        if (photonView.IsMine)
        {
            // Вычисляем угол руля относительно локальной оси
            currentSteeringAngle = steeringWheel.localEulerAngles.y;
            if (currentSteeringAngle > 180f) currentSteeringAngle -= 360f;

            // Отправляем угол по сети
            photonView.RPC(nameof(RPC_UpdateSteeringAngle), RpcTarget.Others, currentSteeringAngle);
        }
        else
        {
            // Второй игрок: используем угол с сети
            currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, networkSteeringAngle, Time.deltaTime * 10f);
        }

        UpdateHandTargets(currentSteeringAngle);
    }

    private void UpdateHandTargets(float steeringAngle)
    {
        float t = (steeringAngle + maxSteeringAngle) / (2f * maxSteeringAngle); // 0..1

        // Левая рука
        leftHandTarget.position = Vector3.Lerp(leftTopAnchor.position, leftBottomAnchor.position, t);
        leftHandTarget.rotation = Quaternion.Slerp(leftTopAnchor.rotation, leftBottomAnchor.rotation, t);

        // Правая рука
        rightHandTarget.position = Vector3.Lerp(rightTopAnchor.position, rightBottomAnchor.position, t);
        rightHandTarget.rotation = Quaternion.Slerp(rightTopAnchor.rotation, rightBottomAnchor.rotation, t);
    }

    [PunRPC]
    private void RPC_UpdateSteeringAngle(float angle)
    {
        networkSteeringAngle = angle;
    }
}
