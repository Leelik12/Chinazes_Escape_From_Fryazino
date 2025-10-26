using UnityEngine;
using Photon.Pun;
using UnityEngine.XR;

public class VRArmIK : MonoBehaviourPun, IPunObservable
{
    [Header("Ссылки")]
    public Animator animator;
    public Transform leftTarget;
    public Transform rightTarget;

    private Vector3 leftPos;
    private Quaternion leftRot;
    private Vector3 rightPos;
    private Quaternion rightRot;

    // Сетевые данные
    private Vector3 networkLeftPos;
    private Quaternion networkLeftRot;
    private Vector3 networkRightPos;
    private Quaternion networkRightRot;

    private float lastLateUpdateTime;
    private float lastBeforeRenderTime;
    private float lastIKTime;

    void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRenderUpdate;
    }

    void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRenderUpdate;
    }

    void LateUpdate()
    {
        lastLateUpdateTime = Time.realtimeSinceStartup;
        if (photonView.IsMine)
        {
            Debug.Log($"[LateUpdate] {lastLateUpdateTime:F4} | Left controller: {leftTarget.position}");
        }
    }

    void OnBeforeRenderUpdate()
    {
        lastBeforeRenderTime = Time.realtimeSinceStartup;
        Debug.Log($"  [OnBeforeRender] {lastBeforeRenderTime:F4} (after LateUpdate: {lastBeforeRenderTime - lastLateUpdateTime:F4}s)");

        if (photonView.IsMine)
        {
            leftPos = leftTarget.position;
            leftRot = leftTarget.rotation;
            rightPos = rightTarget.position;
            rightRot = rightTarget.rotation;
        }
        else
        {
            leftPos = networkLeftPos;
            leftRot = networkLeftRot;
            rightPos = networkRightPos;
            rightRot = networkRightRot;
        }
    }

    void OnAnimatorIK(int layerIndex)
    {
        lastIKTime = Time.realtimeSinceStartup;
        Debug.Log($"    [OnAnimatorIK] {lastIKTime:F4} (after OnBeforeRender: {lastIKTime - lastBeforeRenderTime:F4}s)");

        if (animator == null) return;

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
