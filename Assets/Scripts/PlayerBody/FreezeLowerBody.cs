using UnityEngine;

public class FreezeLowerBody : MonoBehaviour
{
    public Transform hips;
    public Transform LeftLeg;
    public Transform RightLeg;
    private Vector3 initialPosition;
    private Vector3 initialPositionLeftLeg;
    private Vector3 initialPositionRightLeg;
    private Quaternion initialRotation;
    private Quaternion initialRotationLeftLeg;
    private Quaternion initialRotationRightLeg;

    void Start()
    {
        initialPosition = hips.position;
        initialRotation = hips.rotation;
        initialPositionLeftLeg = LeftLeg.position;
        initialPositionRightLeg = RightLeg.position;
        initialRotationLeftLeg = LeftLeg.rotation;
        initialRotationRightLeg = RightLeg.rotation;
    }

    void LateUpdate()
    {
        hips.position = initialPosition;
        hips.rotation = initialRotation;
        LeftLeg.position = initialPositionLeftLeg;
        RightLeg.position = initialPositionRightLeg;
        LeftLeg.rotation = initialRotationLeftLeg;
        RightLeg.rotation = initialRotationRightLeg;
    }
}
