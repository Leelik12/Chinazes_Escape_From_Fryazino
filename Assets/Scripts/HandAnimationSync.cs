using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using RacingProject.Network;

namespace RacingProject
{
    // Роль None — рука только локальная (руки рига меню): ввод читается всегда и никуда не отправляется
    public class HandAnimationSync : RoleSyncedBehaviour
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
        private static readonly int TriggerParam = Animator.StringToHash("Trigger");
        private static readonly int GripParam = Animator.StringToHash("Grip");

        private float triggerValue;
        private float gripValue;
        private float networkTrigger;
        private float networkGrip;

        void Update()
        {
            if (animator == null) return;

            if (Authority == PlayerRole.None || HasAuthority)
            {
                // Считываем значения с контроллеров (только локально)
                triggerValue = m_TriggerInput.ReadValue();
                gripValue = m_GripInput.ReadValue();

                // Применяем на своём аниматоре
                animator.SetFloat(TriggerParam, triggerValue);
                animator.SetFloat(GripParam, gripValue);
            }
            else
            {
                // Плавная интерполяция значений с сети
                triggerValue = Mathf.Lerp(triggerValue, networkTrigger, Time.deltaTime * smoothSpeed);
                gripValue = Mathf.Lerp(gripValue, networkGrip, Time.deltaTime * smoothSpeed);

                animator.SetFloat(TriggerParam, triggerValue);
                animator.SetFloat(GripParam, gripValue);
            }
            if (IsDriver)
            {
                animator.SetFloat(TriggerParam, 0f);
                animator.SetFloat(GripParam, gripStatic);
            }
            if (RightHand)
            {
                animator.SetFloat(GripParam, 1f);
            }
        }

        // Синхронизация по сети
        public override void Serialize(SyncStream stream)
        {
            if (stream.IsWriting)
            {
                // У водителя рука всегда сжата на руле
                networkTrigger = IsDriver ? 0f : triggerValue;
                networkGrip = IsDriver ? gripStatic : gripValue;
            }
            stream.Serialize(ref networkTrigger);
            stream.Serialize(ref networkGrip);
        }
    }
}
