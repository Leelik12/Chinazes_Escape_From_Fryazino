using UnityEngine;

namespace RacingProject.Management
{
    // Включает на риге то, что нужно текущему режиму игры: XR-компоненты (трекинг, контроллеры,
    // интеракторы) в VR, управление мышью и клавиатурой в режиме монитора
    [DefaultExecutionOrder(-100)]
    public class ViewModeObjects : MonoBehaviour
    {
        [Header("Только VR")]
        [SerializeField] private GameObject[] vrOnlyObjects;
        [SerializeField] private Behaviour[] vrOnlyBehaviours;

        [Header("Только монитор")]
        [SerializeField] private GameObject[] desktopOnlyObjects;
        [SerializeField] private Behaviour[] desktopOnlyBehaviours;

        private void Awake()
        {
            bool vr = ViewModeService.IsVR;

            SetActive(vrOnlyObjects, vr);
            SetEnabled(vrOnlyBehaviours, vr);
            SetActive(desktopOnlyObjects, !vr);
            SetEnabled(desktopOnlyBehaviours, !vr);
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null) return;
            foreach (var obj in objects)
                if (obj != null) obj.SetActive(active);
        }

        private static void SetEnabled(Behaviour[] behaviours, bool enabled)
        {
            if (behaviours == null) return;
            foreach (var behaviour in behaviours)
                if (behaviour != null) behaviour.enabled = enabled;
        }
    }
}
