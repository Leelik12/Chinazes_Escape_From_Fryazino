using UnityEngine;
using RacingProject.Turret;

namespace RacingProject.Desktop
{
    // Риг одиночной игры: камера от третьего лица и экранный HUD. Включает его RoomController вместо ригов
    // водителя и стрелка. Пулемёт на крыше на это время целится и стреляет по прицелу этой камеры,
    // а не по прицелу стрелка из его рига
    public class SoloRig : MonoBehaviour
    {
        [SerializeField] private DesktopGunnerAim aim;
        [SerializeField] private TurretGunAim turretAim;
        [SerializeField] private VRGun gun;

        private void OnEnable()
        {
            if (turretAim != null) turretAim.DesktopAim = aim;
            if (gun != null) gun.DesktopAim = aim;
        }
    }
}
