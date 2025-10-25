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

    // Для плавного движения чужих рук
    private Vector3 smoothLeftPos;
    private Quaternion smoothLeftRot;
    private Vector3 smoothRightPos;
    private Quaternion smoothRightRot;

    private float lerpSpeed = 15f;
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        if (photonView.IsMine)
        {
            // локально — напрямую
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftTarget.rotation);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightTarget.position);
            animator.SetIKRotation(AvatarIKGoal.RightHand, rightTarget.rotation);
        }
        else
        {
            // плавная интерполяция между полученными значениями
            smoothLeftPos = Vector3.Lerp(smoothLeftPos, networkLeftPos, Time.deltaTime * lerpSpeed);
            smoothLeftRot = Quaternion.Slerp(smoothLeftRot, networkLeftRot, Time.deltaTime * lerpSpeed);
            smoothRightPos = Vector3.Lerp(smoothRightPos, networkRightPos, Time.deltaTime * lerpSpeed);
            smoothRightRot = Quaternion.Slerp(smoothRightRot, networkRightRot, Time.deltaTime * lerpSpeed);

            animator.SetIKPosition(AvatarIKGoal.LeftHand, smoothLeftPos);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, smoothLeftRot);
            animator.SetIKPosition(AvatarIKGoal.RightHand, smoothRightPos);
            animator.SetIKRotation(AvatarIKGoal.RightHand, smoothRightRot);
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
