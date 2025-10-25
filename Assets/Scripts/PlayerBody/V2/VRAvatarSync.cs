using Photon.Pun;
using UnityEngine;

public class VRAvatarSync : MonoBehaviourPun
{
    [Header("—сылки на XR-трекинг")]
    public Transform headTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header(" ости модели игрока")]
    public Transform headBone;
    public Transform leftHandBone;
    public Transform rightHandBone;

    [Header("—мещени€ дл€ головы (чтобы не влезала в камеру)")]
    public Vector3 headOffset = new Vector3(0, 0, -0.06f);

    private Vector3 netHeadPos, netLeftPos, netRightPos;
    private Quaternion netHeadRot, netLeftRot, netRightRot;

    private void Update()
    {
        if (photonView.IsMine)
        {
            // Ћокальный игрок Ч управл€ем моделью напр€мую
            UpdateBones(headTarget, headBone, headOffset);
            UpdateBones(leftHandTarget, leftHandBone);
            UpdateBones(rightHandTarget, rightHandBone);
        }
        else
        {
            // »нтерпол€ци€ сетевых позиций
            headBone.position = Vector3.Lerp(headBone.position, netHeadPos, Time.deltaTime * 10f);
            leftHandBone.position = Vector3.Lerp(leftHandBone.position, netLeftPos, Time.deltaTime * 10f);
            rightHandBone.position = Vector3.Lerp(rightHandBone.position, netRightPos, Time.deltaTime * 10f);

            headBone.rotation = Quaternion.Slerp(headBone.rotation, netHeadRot, Time.deltaTime * 10f);
            leftHandBone.rotation = Quaternion.Slerp(leftHandBone.rotation, netLeftRot, Time.deltaTime * 10f);
            rightHandBone.rotation = Quaternion.Slerp(rightHandBone.rotation, netRightRot, Time.deltaTime * 10f);
        }
    }

    private void UpdateBones(Transform target, Transform bone, Vector3 offset = default)
    {
        if (target == null || bone == null) return;
        bone.position = target.TransformPoint(offset);
        bone.rotation = target.rotation;
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(headBone.position);
            stream.SendNext(headBone.rotation);
            stream.SendNext(leftHandBone.position);
            stream.SendNext(leftHandBone.rotation);
            stream.SendNext(rightHandBone.position);
            stream.SendNext(rightHandBone.rotation);
        }
        else
        {
            netHeadPos = (Vector3)stream.ReceiveNext();
            netHeadRot = (Quaternion)stream.ReceiveNext();
            netLeftPos = (Vector3)stream.ReceiveNext();
            netLeftRot = (Quaternion)stream.ReceiveNext();
            netRightPos = (Vector3)stream.ReceiveNext();
            netRightRot = (Quaternion)stream.ReceiveNext();
        }
    }
}
