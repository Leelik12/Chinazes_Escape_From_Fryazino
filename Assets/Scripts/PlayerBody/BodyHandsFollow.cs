using UnityEngine;

public class BodyHandsFollow : MonoBehaviour
{
    public Transform leftTarget;
    public Transform rightTarget;
    public Transform leftHandBone;
    public Transform rightHandBone;

    void LateUpdate()
    {
        // Позиция и вращение
        leftHandBone.position = leftTarget.position;
        leftHandBone.rotation = leftTarget.rotation;

        rightHandBone.position = rightTarget.position;
        rightHandBone.rotation = rightTarget.rotation;
    }
}
