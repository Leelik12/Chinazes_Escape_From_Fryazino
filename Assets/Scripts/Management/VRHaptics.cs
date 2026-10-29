using System.Collections.Generic;
using RacingProject.Enemy;
using RacingProject.Network;
using RacingProject.Turret;
using UnityEngine;
using UnityEngine.XR;

namespace RacingProject.Management
{
    // Вибрация контроллеров в VR: у стрелка — каждый выстрел пулемёта (правая рука), выстрел РПГ (обе, сильно),
    // своё убийство и перегрев ствола; у обоих игроков — попадания по машине и жёсткие удары кузова (резкая смена
    // скорости машины, считается по её движению, поэтому работает и у напарника, у которого физики нет).
    // Водитель в VR держит руль G29, и его контроллеры обычно лежат — им вибрация не мешает.
    // Импульсы идут через XR InputDevice (OpenXR их поддерживает), создаётся сам при запуске
    public class VRHaptics : MonoBehaviour
    {
        // Удары кузова: от такого ускорения вибрация начинается и на таком достигает полной силы, м/с²
        private const float ImpactMin = 25f;
        private const float ImpactFull = 120f;

        private static VRHaptics instance;

        private PlayerHealth health;
        private VRGun gun;
        private Vector3 lastPosition;
        private Vector3 lastVelocity;
        private bool tracking;
        private bool wasOverheated;
        private float nextImpactPulse;
        private readonly List<InputDevice> devices = new List<InputDevice>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("VRHaptics");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<VRHaptics>();
        }

        private void OnEnable()
        {
            VRGun.LocalShot += OnShot;
            RocketLauncher.Fired += OnRocket;
            EnemyHealth.Killed += OnKilled;
        }

        private void OnDisable()
        {
            VRGun.LocalShot -= OnShot;
            RocketLauncher.Fired -= OnRocket;
            EnemyHealth.Killed -= OnKilled;
            if (health != null) health.OnDamageTaken -= OnDamage;
        }

        private static bool IsGunner => LocalPlayerRole.Controls(PlayerRole.Gunner);

        private void OnShot()
        {
            Pulse(XRNode.RightHand, 0.35f, 0.04f);
        }

        private void OnRocket()
        {
            if (!IsGunner) return;
            Pulse(XRNode.LeftHand, 1f, 0.25f);
            Pulse(XRNode.RightHand, 0.8f, 0.2f);
        }

        private void OnKilled(Vector3 position, ulong killer)
        {
            var manager = Unity.Netcode.NetworkManager.Singleton;
            if (!IsGunner || manager == null || killer != manager.LocalClientId) return;
            Pulse(XRNode.RightHand, 0.7f, 0.12f);
        }

        private void OnDamage(int damage)
        {
            float strength = Mathf.Clamp(damage / 120f, 0.25f, 1f);
            Pulse(XRNode.LeftHand, strength, 0.1f);
            Pulse(XRNode.RightHand, strength, 0.1f);
        }

        private void FixedUpdate()
        {
            if (!ViewModeService.IsVR) { tracking = false; return; }
            if (health == null)
            {
                health = FindFirstObjectByType<PlayerHealth>();
                if (health == null) return;
                health.OnDamageTaken += OnDamage;
                tracking = false;
            }
            if (gun == null) gun = FindFirstObjectByType<VRGun>();

            // Удар кузова — по скачку скорости машины между шагами физики
            Vector3 position = health.transform.position;
            Vector3 velocity = (position - lastPosition) / Time.fixedDeltaTime;
            if (tracking && Time.time >= nextImpactPulse)
            {
                float accel = (velocity - lastVelocity).magnitude / Time.fixedDeltaTime;
                // Телепорт (возврат на дорогу, рестарт) — не удар
                if (accel > ImpactMin && (position - lastPosition).magnitude < 5f)
                {
                    float strength = Mathf.InverseLerp(ImpactMin, ImpactFull, accel);
                    Pulse(XRNode.LeftHand, Mathf.Lerp(0.3f, 1f, strength), 0.08f + 0.12f * strength);
                    Pulse(XRNode.RightHand, Mathf.Lerp(0.3f, 1f, strength), 0.08f + 0.12f * strength);
                    nextImpactPulse = Time.time + 0.15f;
                }
            }
            lastPosition = position;
            lastVelocity = velocity;
            tracking = true;

            // Перегрев ствола — длинная дрожь в правой руке
            bool overheated = IsGunner && gun != null && gun.IsOverheated;
            if (overheated && !wasOverheated) Pulse(XRNode.RightHand, 0.6f, 0.5f);
            wasOverheated = overheated;
        }

        private void Pulse(XRNode node, float amplitude, float duration)
        {
            if (!ViewModeService.IsVR) return;
            devices.Clear();
            InputDevices.GetDevicesAtXRNode(node, devices);
            foreach (InputDevice device in devices)
                if (device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                    device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), duration);
        }
    }
}
