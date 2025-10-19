using Photon.Pun;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;


public class VRMagazine : MonoBehaviourPun
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
            // Принудительно отцепляем магазин от руки перед уничтожением
            var grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable != null && grabInteractable.isSelected)
            {
                var interactor = grabInteractable.firstInteractorSelecting;
                if (interactor != null)
                {
                    var xrInteractor = interactor as XRBaseInteractor;
                    var xrInteractable = GetComponent<XRBaseInteractable>();

                    if (xrInteractor != null && xrInteractable != null && xrInteractor.interactionManager != null)
                    {
                        xrInteractor.interactionManager.SelectExit((IXRSelectInteractor)xrInteractor,(IXRSelectInteractable)xrInteractable);
                    }

                }
                grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
            }
            gun.InsertMagazine(this);

            PhotonNetwork.Destroy(gameObject); // удаляем текущий магазин
            Debug.Log("Магазин вставлен!");
        }
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
