using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace RacingProject.Management
{
    public enum ViewMode { Desktop, VR }

    // Режим игры на этом компьютере: VR, если шлем подключён и отдаёт картинку, иначе монитор.
    // Автозапуск XR в XR Plug-in Management выключен, XR поднимается здесь до загрузки сцены.
    // Игрок может выбрать монитор и при подключённом шлеме (переключатель в настройках меню),
    // выбор хранится в PlayerPrefs. Режим переживает перезагрузку сцены
    public static class ViewModeService
    {
        // Аргумент запуска, принудительно включающий режим монитора даже при подключённом шлеме
        public const string DesktopArgument = "-desktop";
        private const string PreferDesktopKey = "PreferDesktop";

        public static ViewMode Current { get; private set; } = ViewMode.Desktop;
        public static bool IsVR => Current == ViewMode.VR;

        // Выбор игрока «играть без VR», сохраняется между запусками
        public static bool PreferDesktop
        {
            get => PlayerPrefs.GetInt(PreferDesktopKey, 0) == 1;
            private set => PlayerPrefs.SetInt(PreferDesktopKey, value ? 1 : 0);
        }

        // Режим сменился во время игры; риги применяют его заново
        public static event Action<ViewMode> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Changed = null;
            bool desktop = HasArgument(DesktopArgument) || PreferDesktop;
            Current = desktop || !TryStartXR() ? ViewMode.Desktop : ViewMode.VR;
            Debug.Log($"[ViewMode] Режим игры: {Current}");
        }

        // Переключение из меню. Без шлема VR не включится: режим останется «монитор»
        public static void SetPreferDesktop(bool preferDesktop)
        {
            PreferDesktop = preferDesktop;

            ViewMode target = preferDesktop ? ViewMode.Desktop : ViewMode.VR;
            if (target == Current) return;

            if (target == ViewMode.Desktop)
                StopXR();
            else if (!TryStartXR())
                return;

            Current = target;
            Debug.Log($"[ViewMode] Режим игры переключён: {Current}");
            Changed?.Invoke(Current);
        }

        private static bool TryStartXR()
        {
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null) return false;

            if (manager.activeLoader == null)
                manager.InitializeLoaderSync();
            if (manager.activeLoader == null) return false;

            manager.StartSubsystems();

            // Рантайм OpenXR может подняться и без шлема, поэтому проверяем, что дисплей действительно работает
            var display = manager.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>();
            if (display == null || !display.running)
            {
                StopXR();
                return false;
            }

            // В редакторе срабатывает при выходе из Play Mode: без остановки следующий запуск XR не поднимется
            Application.quitting += StopXR;
            return true;
        }

        private static void StopXR()
        {
            Application.quitting -= StopXR;

            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || manager.activeLoader == null) return;

            manager.StopSubsystems();
            manager.DeinitializeLoader();
        }

        private static bool HasArgument(string argument)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, argument, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
