using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RacingProject.EditorTools
{
    // XR Management на время сборки добавляет свои настройки в Preloaded Assets, и Unity успевает
    // записать их в ProjectSettings.asset. После сборки XR убирает их только из памяти, поэтому файл
    // остаётся изменённым. Здесь список запоминается до всех обработчиков сборки, а после всех
    // возвращается и сохраняется на диск
    public static class PreloadedAssetsBuildGuard
    {
        private static Object[] savedAssets;

        private class Remember : IPreprocessBuildWithReport
        {
            public int callbackOrder => int.MinValue;

            public void OnPreprocessBuild(BuildReport report)
            {
                savedAssets = PlayerSettings.GetPreloadedAssets();
                // Упавшая сборка не вызывает обработчики после сборки — тогда вернём список
                // при первом обновлении редактора после неё
                EditorApplication.delayCall += RestoreSavedAssets;
            }
        }

        private class Restore : IPostprocessBuildWithReport
        {
            public int callbackOrder => int.MaxValue;

            public void OnPostprocessBuild(BuildReport report)
            {
                RestoreSavedAssets();
            }
        }

        private static void RestoreSavedAssets()
        {
            if (savedAssets == null) return;

            PlayerSettings.SetPreloadedAssets(savedAssets);
            savedAssets = null;

            PlayerSettings[] settings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (settings.Length > 0)
                EditorUtility.SetDirty(settings[0]);
            AssetDatabase.SaveAssets();
        }
    }
}
