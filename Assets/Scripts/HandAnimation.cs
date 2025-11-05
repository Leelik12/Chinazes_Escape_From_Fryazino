using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Photon.Pun;

public class HandAnimationSync : MonoBehaviourPun, IPunObservable
{
    [Header("XR Input (только для локального игрока)")]
    [SerializeField] private XRInputValueReader<float> m_TriggerInput;
    [SerializeField] private XRInputValueReader<float> m_GripInput;

    [Header("Аниматор руки")]
    [SerializeField] private Animator animator;

    [Header("Настройки интерполяции")]
    [Range(5f, 30f)] public float smoothSpeed = 12f;

    private float triggerValue;
    private float gripValue;
    private float networkTrigger;
    private float networkGrip;

    void Update()
    {
        if (animator == null) return;

        if (photonView.IsMine)
        {
            // Считываем значения с контроллеров (только локально)
            triggerValue = m_TriggerInput.ReadValue();
            gripValue = m_GripInput.ReadValue();

            // Применяем на своём аниматоре
            animator.SetFloat("Trigger", triggerValue);
            animator.SetFloat("Grip", gripValue);
        }
        else
        {
            // Плавная интерполяция значений с сети
            triggerValue = Mathf.Lerp(triggerValue, networkTrigger, Time.deltaTime * smoothSpeed);
            gripValue = Mathf.Lerp(gripValue, networkGrip, Time.deltaTime * smoothSpeed);

            animator.SetFloat("Trigger", triggerValue);
            animator.SetFloat("Grip", gripValue);
        }
    }

    // Синхронизация по сети
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Отправляем локальные значения другим игрокам
            stream.SendNext(triggerValue);
            stream.SendNext(gripValue);
        }
        else
        {
            // Получаем значения от сети
            networkTrigger = (float)stream.ReceiveNext();
            networkGrip = (float)stream.ReceiveNext();
        }
    }
}
