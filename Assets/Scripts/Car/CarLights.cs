using LogitechG29.Sample.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using RacingProject.Network;

namespace RacingProject.Car
{
    // Огни машины игроков: задние фонари горят всегда и вспыхивают при торможении, мигалка на крыше
    // переключается водителем (клавиша L). Тормоз и мигалку задаёт водитель, напарнику они приходят по сети;
    // ритм мигалки считается от времени у каждого игрока
    public class CarLights : RoleSyncedBehaviour
    {
        [SerializeField] private InputControllerReader input;

        [Header("Задние фонари")]
        [Tooltip("Кузов, в котором фонари — отдельный материал (с включённой эмиссией)")]
        [SerializeField] private Renderer body;
        [SerializeField] private int tailLightMaterialIndex = 7;
        [SerializeField, ColorUsage(false, true)] private Color tailLightColor = new Color(1f, 0.04f, 0.02f);
        [Tooltip("Яркость габаритов и стоп-сигналов")]
        [SerializeField] private float runningIntensity = 1.5f;
        [SerializeField] private float brakeIntensity = 8f;
        [Tooltip("Ореолы стоп-сигналов: включаются только при торможении")]
        [SerializeField] private GameObject[] brakeGlows;
        [Tooltip("Педаль тормоза сильнее этого значения зажигает стоп-сигналы")]
        [SerializeField, Range(0f, 1f)] private float brakeThreshold = 0.15f;

        [Header("Мигалка")]
        [SerializeField] private bool lightBarOn = true;
        [SerializeField] private Renderer[] redLamps;
        [SerializeField] private Renderer[] blueLamps;
        [SerializeField] private Light redLight;
        [SerializeField] private Light blueLight;
        [SerializeField, ColorUsage(false, true)] private Color redColor = new Color(6f, 0.15f, 0.1f);
        [SerializeField, ColorUsage(false, true)] private Color blueColor = new Color(0.15f, 0.5f, 7f);
        [Tooltip("Полный цикл: двойная вспышка красным, затем синим, с")]
        [SerializeField] private float flashPeriod = 0.8f;

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock block;
        private bool braking;
        private bool shownBraking = true;
        private float redIntensity, blueIntensity;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (redLight != null) redIntensity = redLight.intensity;
            if (blueLight != null) blueIntensity = blueLight.intensity;
        }

        private void Update()
        {
            if (IsLocallyControlled)
            {
                // Клавиша S тоже приходит в Brake через схему ввода руля
                braking = input != null && input.Brake > brakeThreshold;
                if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
                    lightBarOn = !lightBarOn;
            }

            if (braking != shownBraking)
            {
                shownBraking = braking;
                ApplyTailLights();
            }
            UpdateLightBar();
        }

        private void ApplyTailLights()
        {
            if (body != null && tailLightMaterialIndex < body.sharedMaterials.Length)
            {
                body.GetPropertyBlock(block, tailLightMaterialIndex);
                block.SetColor(EmissionColor, tailLightColor * (braking ? brakeIntensity : runningIntensity));
                body.SetPropertyBlock(block, tailLightMaterialIndex);
            }
            foreach (GameObject glow in brakeGlows)
                if (glow != null) glow.SetActive(braking);
        }

        // Двойная вспышка: в каждой половине цикла лампа своего цвета мигает два раза
        private void UpdateLightBar()
        {
            float phase = lightBarOn ? Mathf.Repeat(Time.time / flashPeriod, 1f) : -1f;
            bool red = phase >= 0f && phase < 0.5f && Strobe(phase * 2f);
            bool blue = phase >= 0.5f && Strobe(phase * 2f - 1f);
            SetLamps(redLamps, red, redColor);
            SetLamps(blueLamps, blue, blueColor);
            if (redLight != null) redLight.intensity = red ? redIntensity : 0f;
            if (blueLight != null) blueLight.intensity = blue ? blueIntensity : 0f;
        }

        private static bool Strobe(float t)
        {
            return t < 0.2f || t > 0.35f && t < 0.55f;
        }

        private void SetLamps(Renderer[] lamps, bool on, Color color)
        {
            foreach (Renderer lamp in lamps)
            {
                if (lamp == null) continue;
                lamp.GetPropertyBlock(block);
                block.SetColor(BaseColor, on ? color : color * 0.04f);
                lamp.SetPropertyBlock(block);
            }
        }

        public override void Serialize(SyncStream stream)
        {
            stream.Serialize(ref braking);
            stream.Serialize(ref lightBarOn);
        }
    }
}
