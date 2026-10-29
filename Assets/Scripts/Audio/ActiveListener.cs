using UnityEngine;

namespace RacingProject.Audio
{
    // Включённый AudioListener (VR-риг, камера одиночной игры или меню): ищется не чаще раза в полсекунды
    public static class ActiveListener
    {
        private static AudioListener cached;
        private static float nextSearch;

        public static AudioListener Current
        {
            get
            {
                if (cached != null && cached.isActiveAndEnabled) return cached;
                if (Time.unscaledTime < nextSearch) return cached != null && cached.isActiveAndEnabled ? cached : null;
                nextSearch = Time.unscaledTime + 0.5f;
                cached = null;
                foreach (AudioListener listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                {
                    if (listener.isActiveAndEnabled)
                    {
                        cached = listener;
                        break;
                    }
                }
                return cached;
            }
        }

        public static Vector3 Position
        {
            get
            {
                AudioListener listener = Current;
                return listener != null ? listener.transform.position : Vector3.zero;
            }
        }
    }
}
