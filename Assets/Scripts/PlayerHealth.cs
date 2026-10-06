using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

namespace RacingProject
{
    public class PlayerHealth : NetworkBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int maxHealth = 1000;
        [SerializeField] private int currentHealth = 1000;

        [Header("UI")]
        [SerializeField] private Slider healthSlider;
        public BarGradient HPBAR;

        public event System.Action<int> OnDamageTaken;
        public event System.Action OnDeath;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;

        // Пишет только сервер, оба игрока получают изменения через OnValueChanged
        private readonly NetworkVariable<int> networkHealth = new NetworkVariable<int>();

        private bool isDead;

        void Start()
        {
            currentHealth = maxHealth;

            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                networkHealth.Value = maxHealth;
            networkHealth.OnValueChanged += OnHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            networkHealth.OnValueChanged -= OnHealthChanged;
        }

        // Урон запрашивается кем угодно, но считается только на сервере
        public void RequestDamage(int damage)
        {
            if (isDead || !IsSpawned) return;

            if (IsServer)
            {
                ApplyDamage(damage);
            }
            else
            {
                // просим сервер обработать урон
                RequestDamageRpc(damage);
            }
        }

        // Получено с клиента
        [Rpc(SendTo.Server)]
        private void RequestDamageRpc(int damage)
        {
            ApplyDamage(damage);
        }

        // Здоровье меняет только сервер, остальным его рассылает NetworkVariable
        private void ApplyDamage(int damage)
        {
            if (isDead) return;

            networkHealth.Value = Mathf.Clamp(networkHealth.Value - damage, 0, maxHealth);
        }

        // Обновлённое здоровье; события урона и смерти срабатывают у обоих игроков
        private void OnHealthChanged(int previous, int newHealth)
        {
            if (isDead) return;

            int damage = currentHealth - newHealth;
            currentHealth = newHealth;

            if (HPBAR != null)
                HPBAR.SetHealth(currentHealth, maxHealth);
            else if (healthSlider != null)
                healthSlider.value = currentHealth;

            if (damage > 0)
                OnDamageTaken?.Invoke(damage);

            if (currentHealth <= 0)
                Die();
        }

        // Перезапуск раунда выполняет RoomController по событию OnDeath
        private void Die()
        {
            isDead = true;
            Debug.Log($"{gameObject.name} уничтожен");
            OnDeath?.Invoke();
        }
    }
}
