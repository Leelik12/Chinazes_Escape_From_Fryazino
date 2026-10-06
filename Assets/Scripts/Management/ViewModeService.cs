using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace RacingProject.Management
{
    public enum ViewMode { Desktop, VR }

    // Режим игры на этом компьютере: VR, если шлем подключён и отдаёт картинку, иначе монитор.
    // Автозапуск XR в XR Plug-in Management выключен, XR поднимается здесь до загрузки сцены.
    // Режим выбирается один раз за запуск и переживает перезагрузку сцены
    public static class ViewModeService
    {
        // Аргумент запуска, принудительно включающий режим монитора даже при подключённом шлеме
        public const string DesktopArgument = "-desktop";

        public static ViewMode Current { get; private set; } = ViewMode.Desktop;
        public static bool IsVR => Current == ViewMode.VR;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Current = HasArgument(DesktopArgument) || !TryStartXR() ? ViewMode.Desktop : ViewMode.VR;
            Debug.Log($"[ViewMode] Режим игры: {Current}");
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
