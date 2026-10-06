using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RacingProject.EditorTools
{
    // Релизная сборка под Windows одной командой: без отладочной консоли, а папка с отладочными
    // символами Burst удаляется, так что Builds/Release можно сразу копировать на другой ПК
    public static class ReleaseBuildMenu
    {
        private const string OutputFolder = "Builds/Release";

        [MenuItem("RacingProject/Собрать релиз (Windows)")]
        public static void BuildWindowsRelease()
        {
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("Релизная сборка: в Build Profiles нет включённых сцен");
                return;
            }

            // В названии игры есть «:», а в имени файла он недопустим
            string fileName = new string(PlayerSettings.productName.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
            string exePath = Path.Combine(OutputFolder, fileName + ".exe");
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("Релизная сборка не удалась: " + report.summary.result);
                return;
            }

            // Отладочные символы Burst нужны только для разбора падений, игроку их не передают
            foreach (string folder in Directory.GetDirectories(OutputFolder, "*_BurstDebugInformation_DoNotShip"))
                Directory.Delete(folder, true);

            Debug.Log("Релизная сборка готова: " + Path.GetFullPath(exePath)
                      + " (" + report.summary.totalSize / (1024 * 1024) + " МБ)");
            EditorUtility.RevealInFinder(exePath);
        }
    }
}
