using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRMagazine : MonoBehaviour
{
    public int ammoAmount = 30;
    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Weapon")) return;

        VRGun gun = other.GetComponent<VRGun>();
        if (gun != null && gun.CanInsertMagazine())
        {
            gun.InsertMagazine(this);
            Destroy(gameObject);
        }
        Debug.Log("Магазин вставлен!");

    }

    public void LockInPlace()
    {
        if (rb) rb.isKinematic = true;
        if (grabInteractable) grabInteractable.enabled = false;
    }

    public void Unlock()
    {
        if (rb) rb.isKinematic = false;
        if (grabInteractable) grabInteractable.enabled = true;
    }
}
