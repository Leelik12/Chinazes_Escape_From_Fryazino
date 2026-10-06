using UnityEngine;
using RacingProject.Network;

namespace RacingProject.PlayerBody
{
    [DefaultExecutionOrder(400)]
    public class VRHeadIK : MonoBehaviour
    {
        [Header("Ссылки")]
        public Animator animator;
        public Transform headProxy;      // Прокси-точка в машине, повторяет XR камеру
        public GameObject headVisualRoot; // Меш головы
        [Tooltip("Чьё это тело: у этого игрока голова скрыта, чтобы не загораживать камеру")]
        public PlayerRole bodyRole = PlayerRole.Driver;

        [Header("Смещение головы")]
        public Vector3 headOffset = Vector3.zero;

        [Header("Настройки")]
        [Range(0f, 1f)]
        public float headWeight = 1f;    // влияние на голову
        [Range(0f, 1f)]
        public float bodyWeight = 0f;    // влияние на тело (0 = только голова)
        [Range(0f, 1f)]
        public float eyesWeight = 0.3f;  // глаза/шея
        [Range(0f, 1f)]
        public float clampWeight = 0.7f; // ограничение угла поворота головы

        private Renderer[] headRenderers;
        private bool? headVisible;

        void Awake()
        {
            if (headVisualRoot != null)
                headRenderers = headVisualRoot.GetComponentsInChildren<Renderer>(true);
        }

        // Роль игрока задаётся при старте игры, поэтому видимость проверяется каждый кадр,
        // но рендеры переключаются только при изменении
        void Update()
        {
            // Скрываем голову локального игрока
            SetHeadVisible(LocalPlayerRole.Current != bodyRole);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || headProxy == null) return;

            Vector3 lookPos = headProxy.position + headOffset + headProxy.forward * 10f;

            // Настройка IK
            animator.SetLookAtWeight(
                headWeight,
                bodyWeight,
                eyesWeight,
                clampWeight,
                0.5f // плавность
            );

            animator.SetLookAtPosition(lookPos);
        }

        private void SetHeadVisible(bool visible)
        {
            if (headRenderers == null || headVisible == visible) return;
            headVisible = visible;
            foreach (var r in headRenderers)
                r.enabled = visible;
        }
    }
}
