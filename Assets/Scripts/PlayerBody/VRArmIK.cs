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

    void Start()
    {
        // Чтобы избежать рывка при первом получении сетевых данных
        smoothLeftPos = leftTarget.position;
        smoothLeftRot = leftTarget.rotation;
        smoothRightPos = rightTarget.position;
        smoothRightRot = rightTarget.rotation;
    }

    void LateUpdate()
    {
        if (animator == null) return;

        if (photonView.IsMine)
        {
            ApplyIK(leftTarget.position, leftTarget.rotation, rightTarget.position, rightTarget.rotation);
        }
        else
        {
            // Плавное сглаживание сетевых рук
            smoothLeftPos = Vector3.Lerp(smoothLeftPos, networkLeftPos, Time.deltaTime * lerpSpeed);
            smoothLeftRot = Quaternion.Slerp(smoothLeftRot, networkLeftRot, Time.deltaTime * lerpSpeed);

            smoothRightPos = Vector3.Lerp(smoothRightPos, networkRightPos, Time.deltaTime * lerpSpeed);
            smoothRightRot = Quaternion.Slerp(smoothRightRot, networkRightRot, Time.deltaTime * lerpSpeed);

            ApplyIK(smoothLeftPos, smoothLeftRot, smoothRightPos, smoothRightRot);
        }
    }

    private void ApplyIK(Vector3 leftPos, Quaternion leftRot, Vector3 rightPos, Quaternion rightRot)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 1);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, leftPos);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, leftRot);

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 1);
        animator.SetIKPosition(AvatarIKGoal.RightHand, rightPos);
        animator.SetIKRotation(AvatarIKGoal.RightHand, rightRot);
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
