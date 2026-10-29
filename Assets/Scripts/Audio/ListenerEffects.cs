using System;
using UnityEngine;
using RacingProject.Desktop;

namespace RacingProject.Audio
{
    // Что слышит и чувствует игрок у близкого взрыва: мир глохнет (фильтр низких частот на слушателе) и тише,
    // в ушах звенит, камеру трясёт. Создаётся сам при запуске игры и слушает ExplosionEffect.Exploded
    public class ListenerEffects : MonoBehaviour
    {
        // Дальше этого взрыв силы 1 не оглушает, м
        private const float DeafRadius = 26f;
        // Ближе этого — полное оглушение, м
        private const float FullDeafRadius = 6f;
        private const float RecoverTime = 2.6f;
        private const float OpenCutoff = 22000f;
        private const float DeafCutoff = 450f;

        private static ListenerEffects instance;

        // Близкий взрыв: сила 0–1 для вспышки экрана (PostFxEvents)
        public static event Action<float> NearExplosion;
        // Текущая глухота 0–1
        public static float Deafness => instance != null ? instance.deaf : 0f;

        private float deaf;
        private AudioLowPassFilter filter;
        private AudioListener filtered;
        private ThirdPersonCamera desktopCamera;
        private float nextCameraSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("ListenerEffects");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ListenerEffects>();
        }

        private void OnEnable() => ExplosionEffect.Exploded += OnExploded;
        private void OnDisable() => ExplosionEffect.Exploded -= OnExploded;

        private void OnExploded(Vector3 position, float power)
        {
            AudioListener listener = ActiveListener.Current;
            if (listener == null || power <= 0f) return;
            float distance = Vector3.Distance(listener.transform.position, position);
            float radius = DeafRadius * Mathf.Sqrt(power);
            float strength = 1f - Mathf.InverseLerp(FullDeafRadius * power, radius, distance);
            if (strength <= 0f) return;
            strength *= strength;

            float before = deaf;
            deaf = Mathf.Max(deaf, strength);
            // Звон — только от действительно близкого взрыва и не поверх ещё звенящего
            if (strength > 0.35f && before < 0.3f)
                SoundLibrary.Play2D(SoundLibrary.Sfx.DeafRing, Mathf.Lerp(0.3f, 0.8f, strength));

            CameraShake(strength);
            NearExplosion?.Invoke(strength);
        }

        private void CameraShake(float strength)
        {
            if ((desktopCamera == null || !desktopCamera.isActiveAndEnabled) && Time.unscaledTime >= nextCameraSearch)
            {
                nextCameraSearch = Time.unscaledTime + 1f;
                desktopCamera = FindFirstObjectByType<ThirdPersonCamera>();
            }
            if (desktopCamera != null && desktopCamera.isActiveAndEnabled)
                desktopCamera.AddTrauma(strength * 0.9f);
        }

        private void Update()
        {
            AudioListener listener = ActiveListener.Current;
            // Слушатель сменился (VR-риг, камера меню) — фильтр переезжает на новый
            if (listener != filtered)
            {
                if (filter != null) filter.cutoffFrequency = OpenCutoff;
                filtered = listener;
                filter = null;
                if (listener != null && !listener.TryGetComponent(out filter))
                    filter = listener.gameObject.AddComponent<AudioLowPassFilter>();
            }

            deaf = Mathf.MoveTowards(deaf, 0f, Time.unscaledDeltaTime / RecoverTime);
            if (filter != null)
            {
                // Кривая: слух долго остаётся глухим и быстро возвращается в конце
                float k = deaf * deaf * (3f - 2f * deaf);
                filter.enabled = k > 0.001f;
                filter.cutoffFrequency = Mathf.Lerp(OpenCutoff, DeafCutoff, Mathf.Pow(k, 0.35f));
                filter.lowpassResonanceQ = 1f;
            }
            AudioListener.volume = Mathf.Lerp(1f, 0.55f, deaf);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            AudioListener.volume = 1f;
        }
    }
}
