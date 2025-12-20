using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using Photon.Pun;

public class HandAnimationSync : MonoBehaviourPun, IPunObservable
{
    [Header("XR Input (только для локального игрока)")]
    [SerializeField] private XRInputValueReader<float> m_TriggerInput;
    [SerializeField] private XRInputValueReader<float> m_GripInput;
    [SerializeField] private bool RightHand = false;
    [Header("Аниматор руки")]
    [SerializeField] private Animator animator;

    [Header("Настройки интерполяции")]
    [Range(5f, 30f)] public float smoothSpeed = 12f;

    [Header("Настройки водителя")]
    [SerializeField] private float gripStatic = 1f;
    [SerializeField] private bool IsDriver = false;
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
        if (IsDriver)
        {
            animator.SetFloat("Trigger", 0f);
            animator.SetFloat("Grip", gripStatic);
        }
        if (RightHand)
        {
            animator.SetFloat("Grip", 1f);
        }
    }

    // Синхронизация по сети
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (IsDriver)
            {
                // Отправляем локальные значения другим игрокам
                stream.SendNext(0f);
                stream.SendNext(gripStatic);
            }
            else
            {
                // Отправляем локальные значения другим игрокам
                stream.SendNext(triggerValue);
                stream.SendNext(gripValue);
            }
        }
        else
        {
            // Получаем значения от сети
            networkTrigger = (float)stream.ReceiveNext();
            networkGrip = (float)stream.ReceiveNext();
        }
    }
}
