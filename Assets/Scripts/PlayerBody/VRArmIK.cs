using UnityEngine;
using Photon.Pun;

public class VRArmIK : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform leftTarget;
    public Transform rightTarget;

    // Сетевые данные для других игроков
    private Vector3 networkLeftPos;
    private Quaternion networkLeftRot;
    private Vector3 networkRightPos;
    private Quaternion networkRightRot;
    private void Awake()
    {
        photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
    }
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        // Если это локальный игрок — используем реальные контроллеры
        if (photonView.IsMine)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftTarget.rotation);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightTarget.rotation);
        }
        else // если это чужой игрок — используем сетевые данные
        {
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, networkLeftPos);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, networkLeftRot);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, networkRightPos);
            animator.SetIKRotation(AvatarIKGoal.RightHand, networkRightRot);
        }

        if (photonView.IsMine)
        {
            Debug.Log($"IK Update time: {Time.time:F4}, Left: {leftTarget.position}");
            Debug.DrawLine(leftTarget.position, leftTarget.position + Vector3.up * 0.1f, Color.red);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Отправляем свои руки
            stream.SendNext(leftTarget.position);
            stream.SendNext(leftTarget.rotation);
            stream.SendNext(rightTarget.position);
            stream.SendNext(rightTarget.rotation);
        }
        else
        {
            // Получаем чужие руки
            networkLeftPos = (Vector3)stream.ReceiveNext();
            networkLeftRot = (Quaternion)stream.ReceiveNext();
            networkRightPos = (Vector3)stream.ReceiveNext();
            networkRightRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
