using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(400)]
public class VRArmIK : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform leftTarget;
    public Transform rightTarget;

    private Vector3 networkLeftPos, networkRightPos;
    private Quaternion networkLeftRot, networkRightRot;

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        if (photonView.IsMine)
        {
            // Локальные контроллеры
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftTarget.rotation);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightTarget.rotation);
        }
        else
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
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Передаем локальные координаты относительно машины
            Vector3 localLeftPos = transform.InverseTransformPoint(leftTarget.position);
            Quaternion localLeftRot = Quaternion.Inverse(transform.rotation) * leftTarget.rotation;
            Vector3 localRightPos = transform.InverseTransformPoint(rightTarget.position);
            Quaternion localRightRot = Quaternion.Inverse(transform.rotation) * rightTarget.rotation;

            stream.SendNext(localLeftPos);
            stream.SendNext(localLeftRot);
            stream.SendNext(localRightPos);
            stream.SendNext(localRightRot);
        }
        else
        {
            // Восстанавливаем мировые позиции
            Vector3 localLeftPos = (Vector3)stream.ReceiveNext();
            Quaternion localLeftRot = (Quaternion)stream.ReceiveNext();
            Vector3 localRightPos = (Vector3)stream.ReceiveNext();
            Quaternion localRightRot = (Quaternion)stream.ReceiveNext();

            networkLeftPos = transform.TransformPoint(localLeftPos);
            networkLeftRot = transform.rotation * localLeftRot;
            networkRightPos = transform.TransformPoint(localRightPos);
            networkRightRot = transform.rotation * localRightRot;
        }
    }
}
