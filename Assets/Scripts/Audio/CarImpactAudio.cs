using UnityEngine;
using UnityEngine.Audio;
using RacingProject.Car;

namespace RacingProject.Audio
{
    // Звуки кузова: хруст металла по силе удара, скрежет при трении о стену или машину, звон стекла и стук деталей
    // при отрыве. События приходят из CarDeformation у обоих игроков. При отрыве стекла — ещё и россыпь осколков
    [RequireComponent(typeof(CarDeformation))]
    public class CarImpactAudio : MonoBehaviour
    {
        [Tooltip("Скорость сближения, с которой удар считается тяжёлым, м/с")]
        [SerializeField] private float heavySpeed = 9f;
        [Tooltip("Скорость, на которой удар звучит в полную силу, м/с")]
        [SerializeField] private float fullSpeed = 22f;

        [Header("Скрежет")]
        [SerializeField] private AudioClip scrapeClip;
        [SerializeField] private AudioMixerGroup output;
        [SerializeField, Range(0f, 1f)] private float scrapeVolume = 0.8f;

        [Header("Осколки")]
        [Tooltip("Россыпь осколков стекла при отрыве фар и окон (необязательно)")]
        [SerializeField] private ParticleSystem glassShards;
        [Tooltip("Искры от сильного удара (необязательно)")]
        [SerializeField] private ParticleSystem impactSparks;

        private CarDeformation deformation;
        private AudioSource scrapeSource;

        private void Awake()
        {
            deformation = GetComponent<CarDeformation>();
            if (scrapeClip != null)
            {
                var go = new GameObject("Scrape");
                go.transform.SetParent(transform, false);
                scrapeSource = go.AddComponent<AudioSource>();
                scrapeSource.clip = scrapeClip;
                scrapeSource.loop = true;
                scrapeSource.volume = 0f;
                scrapeSource.spatialBlend = 1f;
                scrapeSource.minDistance = 5f;
                scrapeSource.maxDistance = 80f;
                scrapeSource.dopplerLevel = 0f;
                scrapeSource.outputAudioMixerGroup = output;
            }
        }

        private void OnEnable()
        {
            if (deformation == null) return;
            deformation.Impacted += OnImpacted;
            deformation.PartsDetached += OnPartsDetached;
        }

        private void OnDisable()
        {
            if (deformation == null) return;
            deformation.Impacted -= OnImpacted;
            deformation.PartsDetached -= OnPartsDetached;
        }

        private void Update()
        {
            if (scrapeSource == null) return;
            float scrape = deformation.Scrape01;
            scrapeSource.volume = Mathf.MoveTowards(scrapeSource.volume, scrapeVolume * Mathf.Sqrt(scrape), Time.deltaTime * 4f);
            scrapeSource.pitch = Mathf.Lerp(0.8f, 1.2f, scrape);
            // Петля крутится, только пока слышна: так она каждый раз начинается с нового места
            if (scrapeSource.volume > 0.001f && !scrapeSource.isPlaying)
            {
                scrapeSource.time = Random.Range(0f, scrapeClip.length);
                scrapeSource.Play();
            }
            else if (scrapeSource.volume <= 0.001f && scrapeSource.isPlaying)
                scrapeSource.Stop();
        }

        private void OnImpacted(Vector3 point, float speed)
        {
            float strength = Mathf.Clamp01(speed / fullSpeed);
            if (speed >= heavySpeed)
            {
                SoundLibrary.Play(SoundLibrary.Sfx.ImpactHeavy, point, Mathf.Lerp(0.6f, 1f, strength), Mathf.Lerp(1.05f, 0.9f, strength));
                if (impactSparks != null)
                {
                    impactSparks.transform.position = point;
                    impactSparks.Emit(Mathf.RoundToInt(Mathf.Lerp(10f, 40f, strength)));
                }
            }
            else
                SoundLibrary.Play(SoundLibrary.Sfx.ImpactLight, point, Mathf.Lerp(0.35f, 0.85f, speed / heavySpeed));
        }

        private void OnPartsDetached(Vector3 point, int count, bool glass)
        {
            SoundLibrary.Play(SoundLibrary.Sfx.DebrisClatter, point, Mathf.Clamp01(0.6f + count * 0.15f));
            if (!glass) return;
            SoundLibrary.Play(SoundLibrary.Sfx.Glass, point);
            if (glassShards != null)
            {
                glassShards.transform.position = point;
                glassShards.Emit(40);
            }
        }
    }
}
