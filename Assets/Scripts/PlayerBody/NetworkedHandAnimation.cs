using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Photon.Pun;

[DefaultExecutionOrder(500)]
public class NetworkedHandAnimation : MonoBehaviourPun
{
    [Header("XR Input")]
    public XRController xrController; // Контроллер XR (левый или правый)

    [Header("Animator")]
    public Animator animator;

    [Header("Параметры аниматора")]
    public string gripParam = "Grip";
    public string triggerParam = "Trigger";

    [Header("Сглаживание")]
    [Range(0f, 20f)] public float lerpSpeed = 10f;

    // Локальные значения
    private float gripValue;
    private float triggerValue;

    // Сетевые значения
    private float networkGrip;
    private float networkTrigger;

    void Update()
    {
        if (photonView.IsMine)
        {
            // Читаем XR Input
            xrController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float g);
            xrController.inputDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float t);

            gripValue = g;
            triggerValue = t;

            // Сразу выставляем в Animator
            animator.SetFloat(gripParam, gripValue);
            animator.SetFloat(triggerParam, triggerValue);
        }
        else
        {
            // Плавное сглаживание для других игроков
            gripValue = Mathf.Lerp(gripValue, networkGrip, Time.deltaTime * lerpSpeed);
            triggerValue = Mathf.Lerp(triggerValue, networkTrigger, Time.deltaTime * lerpSpeed);

            animator.SetFloat(gripParam, gripValue);
            animator.SetFloat(triggerParam, triggerValue);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Отправляем текущие значения пальцев
            stream.SendNext(gripValue);
            stream.SendNext(triggerValue);
        }
        else
        {
            // Получаем значения для других игроков
            networkGrip = (float)stream.ReceiveNext();
            networkTrigger = (float)stream.ReceiveNext();
        }
    }
}
