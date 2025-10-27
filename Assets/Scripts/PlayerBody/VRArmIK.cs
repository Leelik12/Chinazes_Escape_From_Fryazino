using UnityEngine;
using Photon.Pun;

[DefaultExecutionOrder(400)] // чтобы LateUpdate отрабатывал после NetworkedTransformFollower
public class VRArmIK : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform leftTarget;
    public Transform rightTarget;

    [Header("Сглаживание сетевых рук")]
    [Range(1f, 60f)] public float lerpSpeed = 20f;

    private Vector3 networkLeftPos, networkRightPos;
    private Quaternion networkLeftRot, networkRightRot;
    private Vector3 smoothLeftPos, smoothRightPos;
    private Quaternion smoothLeftRot, smoothRightRot;

    void Start()
    {
        smoothLeftPos = leftTarget.position;
        smoothLeftRot = leftTarget.rotation;
        smoothRightPos = rightTarget.position;
        smoothRightRot = rightTarget.rotation;
    }

    void LateUpdate()
    {
        if (!photonView.IsMine)
        {
            // Плавная интерполяция сетевых рук
            smoothLeftPos = Vector3.Lerp(smoothLeftPos, networkLeftPos, Time.deltaTime * lerpSpeed);
            smoothLeftRot = Quaternion.Slerp(smoothLeftRot, networkLeftRot, Time.deltaTime * lerpSpeed);

            smoothRightPos = Vector3.Lerp(smoothRightPos, networkRightPos, Time.deltaTime * lerpSpeed);
            smoothRightRot = Quaternion.Slerp(smoothRightRot, networkRightRot, Time.deltaTime * lerpSpeed);
        }

        ApplyIK();
    }

    void ApplyIK()
    {
        if (animator == null) return;

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
        else
        {
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
            // Отправляем локальные координаты относительно машины
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
