using UnityEngine;

public class VisualHands : MonoBehaviour
{
    public GameObject LeftController;
    public GameObject RightController;
    public GameObject LeftHand;
    public GameObject RightHand;

    // Update is called once per frame
    void LateUpdate()
    {
        LeftHand.transform.position = LeftController.transform.position;
        LeftHand.transform.rotation = LeftController.transform.rotation;
        RightHand.transform.position = RightController.transform.position;
        RightController.transform.rotation = RightController.transform.rotation;
    }
}
