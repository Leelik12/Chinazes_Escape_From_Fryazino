using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections;

public class HandBasedGrabPoint : XRGrabInteractable
{
    [Header("Настройки хватов")]
    [SerializeField] private Transform rightHandAttachTransform;
    [SerializeField] private Transform leftHandAttachTransform;
    [SerializeField] private Collider TouchCollider;
    [Header("Настройки кинематики")]
    [Tooltip("Задержка перед возвратом isKinematic = true после подбора, чтобы оружие успело притянуться к руке.")]
    [SerializeField] private float reenableKinematicDelay = 0.1f;

    private bool savedHasRigidbody = false;
    private bool savedIsKinematic;
    private bool savedUseGravity;

    protected override void OnSelectEntering(SelectEnterEventArgs args)
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            savedHasRigidbody = true;
            savedIsKinematic = rb.isKinematic;
            savedUseGravity = rb.useGravity;
            TouchCollider.enabled = false;
            // временно делаем не кинематическим, чтобы XR смог корректно прижать объект к руке
            rb.isKinematic = false;
        }
        else savedHasRigidbody = false;

        // выбираем правильный attachTransform в зависимости от руки
        if (args.interactorObject != null)
        {
            var interactorTransform = args.interactorObject.transform;
            if (interactorTransform.CompareTag("RightHand") && rightHandAttachTransform != null)
                attachTransform = rightHandAttachTransform;
            else if (interactorTransform.CompareTag("LeftHand") && leftHandAttachTransform != null)
                attachTransform = leftHandAttachTransform;
        }

        base.OnSelectEntering(args);
        SetLayerRecursively(gameObject, 7);

        // включаем возврат кинематики с задержкой
        if (rb != null)
            StartCoroutine(ReenableKinematicAfterDelay(rb, reenableKinematicDelay));
    }

    private IEnumerator ReenableKinematicAfterDelay(Rigidbody rb, float delay)
    {
        yield return new WaitForSeconds(delay);
        rb.isKinematic = true;
        rb.useGravity = savedUseGravity;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        SetLayerRecursively(gameObject, 0);
        base.OnSelectExited(args);

        if (savedHasRigidbody)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                TouchCollider.enabled = true;
                rb.isKinematic = savedIsKinematic;
                rb.useGravity = savedUseGravity;
            }
        }

    }

    public static void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
