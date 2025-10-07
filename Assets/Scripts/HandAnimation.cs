using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

public class HandAnimation : MonoBehaviour
{
    [SerializeField]
    XRInputValueReader<float> m_TriggerInput;

    [SerializeField]
    XRInputValueReader<float> m_GripInput;

    [SerializeField] 
    Animator animator;

    void Update()
    {
        animator.SetFloat("Trigger", m_TriggerInput.ReadValue());
        animator.SetFloat("Grip", m_GripInput.ReadValue());
    }
}