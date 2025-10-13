using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class GrabParenter : MonoBehaviour
{
    Vector3 gun;
    public GameObject parent;
    public void OnGrab(SelectEnterEventArgs args)
    {
        args.interactableObject.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
        gun = args.interactableObject.transform.localScale;
        Debug.Log("Взял");
        args.interactableObject.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
        args.interactableObject.transform.SetParent(args.interactorObject.transform);
        args.interactableObject.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
    }
    public void OnUngrab(SelectExitEventArgs args)
    {
        args.interactableObject.transform.localScale = gun;
        Debug.Log("Отпустил");
        args.interactableObject.transform.SetParent(parent.transform);
        args.interactableObject.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);

    }
}
