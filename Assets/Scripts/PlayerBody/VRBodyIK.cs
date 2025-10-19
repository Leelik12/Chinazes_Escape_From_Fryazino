using UnityEngine;

public class VRBodyIK : MonoBehaviour
{
    [Header("XR Targets")]
    public Transform headTarget;
    public Transform leftHandTarget;
    public Transform rightHandTarget;

    [Header("Body Bones")]
    public Transform headBone;
    public Transform leftUpperArm;
    public Transform leftForeArm;
    public Transform leftHand;
    public Transform rightUpperArm;
    public Transform rightForeArm;
    public Transform rightHand;
    public Transform spine;

    void LateUpdate()
    {
        // Голова следует за камерой
        headBone.position = headTarget.position;
        headBone.rotation = headTarget.rotation;

        // Левая рука
        AlignLimb(leftUpperArm, leftForeArm, leftHand, leftHandTarget);

        // Правая рука
        AlignLimb(rightUpperArm, rightForeArm, rightHand, rightHandTarget);

        // Вращаем корпус в сторону головы (опционально)
        //Vector3 lookDir = new Vector3(headTarget.forward.x, 0, headTarget.forward.z);
        //spine.forward = Vector3.Lerp(spine.forward, lookDir, Time.deltaTime * 5f);
    }

    void AlignLimb(Transform upper, Transform forearm, Transform hand, Transform target)
    {
        // Простая IK-заглушка для руки
        hand.position = target.position;
        hand.rotation = target.rotation;

        // Плечо поворачивается в сторону цели
        upper.LookAt(target);
        forearm.LookAt(target);
    }
}
