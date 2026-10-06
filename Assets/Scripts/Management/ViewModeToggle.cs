using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Переключатель «Играть без VR» в настройках меню
    [RequireComponent(typeof(Toggle))]
    public class ViewModeToggle : MonoBehaviour
    {
        private Toggle toggle;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnEnable()
        {
            Refresh(ViewModeService.Current);
            ViewModeService.Changed += Refresh;
        }

        private void OnDisable()
        {
            ViewModeService.Changed -= Refresh;
        }

        private void OnValueChanged(bool desktop)
        {
            ViewModeService.SetPreferDesktop(desktop);
            // Без шлема VR не включится — возвращаем галочку к фактическому режиму
            Refresh(ViewModeService.Current);
        }

        private void Refresh(ViewMode mode)
        {
            toggle.SetIsOnWithoutNotify(mode == ViewMode.Desktop);
        }
    }
}
