using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class GrabParenter : MonoBehaviour
{
    Vector3 gun;
    public void OnGrab(SelectEnterEventArgs args)
    {
        gun = args.interactableObject.transform.localScale;
        Debug.Log("Взял");
        args.interactableObject.transform.SetParent(args.interactorObject.transform);
        args.interactableObject.transform.localScale = gun;
    }
    public void OnUngrab(SelectExitEventArgs args)
    {
        args.interactableObject.transform.localScale = gun;
        Debug.Log("Отпустил");
        args.interactableObject.transform.SetParent(null);
    }
}
