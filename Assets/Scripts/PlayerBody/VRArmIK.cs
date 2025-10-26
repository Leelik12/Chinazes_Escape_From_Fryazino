using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(200)] // выполняется после NetworkedTransformFollower
public class VRArmIK : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform leftTarget;
    public Transform rightTarget;

    [Header("Сглаживание сетевых рук")]
    [Range(1f, 60f)] public float lerpSpeed = 20f;

    private Vector3 networkLeftPos;
    private Quaternion networkLeftRot;
    private Vector3 networkRightPos;
    private Quaternion networkRightRot;

    private Vector3 smoothLeftPos;
    private Quaternion smoothLeftRot;
    private Vector3 smoothRightPos;
    private Quaternion smoothRightRot;

    private void Awake()
    {
        photonView.Synchronization = ViewSynchronization.UnreliableOnChange;
    }

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
            // Плавное сглаживание сетевых рук
            smoothLeftPos = Vector3.Lerp(smoothLeftPos, networkLeftPos, Time.deltaTime * lerpSpeed);
            smoothLeftRot = Quaternion.Slerp(smoothLeftRot, networkLeftRot, Time.deltaTime * lerpSpeed);

            smoothRightPos = Vector3.Lerp(smoothRightPos, networkRightPos, Time.deltaTime * lerpSpeed);
            smoothRightRot = Quaternion.Slerp(smoothRightRot, networkRightRot, Time.deltaTime * lerpSpeed);

            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, smoothLeftPos);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, smoothLeftRot);

            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.RightHand, smoothRightPos);
            animator.SetIKRotation(AvatarIKGoal.RightHand, smoothRightRot);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(leftTarget.position);
            stream.SendNext(leftTarget.rotation);
            stream.SendNext(rightTarget.position);
            stream.SendNext(rightTarget.rotation);
        }
        else
        {
            networkLeftPos = (Vector3)stream.ReceiveNext();
            networkLeftRot = (Quaternion)stream.ReceiveNext();
            networkRightPos = (Vector3)stream.ReceiveNext();
            networkRightRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
