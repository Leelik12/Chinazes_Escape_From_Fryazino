using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class ReturnToPointOnRelease : MonoBehaviour
{
    [Header("Точка возврата")]
    public Transform returnPoint; // куда возвращать

    [Header("Параметры возврата")]
    public bool smoothReturn = true; // если true — возвращаем плавно
    public float returnSpeed = 5f;   // скорость возврата при smoothReturn

    private XRGrabInteractable grab;
    private bool isReturning = false;

    void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectExited.AddListener(OnRelease);
        grab.selectEntered.AddListener(OnGrab);
    }

    private void OnGrab(SelectEnterEventArgs arg)
    {
        isReturning = false;
    }

    private void OnRelease(SelectExitEventArgs arg)
    {
        if (returnPoint != null)
        {
            if (smoothReturn)
            {
                isReturning = true;
            }
            else
            {
                // моментально
                transform.position = returnPoint.position;
                transform.rotation = returnPoint.rotation;
            }
        }
    }

    void Update()
    {
        if (isReturning && returnPoint != null)
        {
            // Плавно возвращаем
            transform.position = Vector3.Lerp(transform.position, returnPoint.position, Time.deltaTime * returnSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, returnPoint.rotation, Time.deltaTime * returnSpeed);

            // Останавливаем, если достаточно близко
            if (Vector3.Distance(transform.position, returnPoint.position) < 0.01f &&
                Quaternion.Angle(transform.rotation, returnPoint.rotation) < 1f)
            {
                isReturning = false;
                transform.position = returnPoint.position;
                transform.rotation = returnPoint.rotation;
            }
        }
    }
}
