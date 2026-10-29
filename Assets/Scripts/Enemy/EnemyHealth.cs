using UnityEngine;
using Unity.Netcode;
using System;
using Random = UnityEngine.Random;
using RacingProject.Props;

namespace RacingProject.Enemy
{
    public class EnemyHealth : NetworkBehaviour
    {
        [SerializeField] private int maxHealth = 1000;
        [Tooltip("Эффект гибели: создаётся у обоих игроков в точке, где враг погиб")]
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Очки за уничтожение (до множителя комбо)")]
        [SerializeField] private int scoreValue = 100;
        [Tooltip("Взрыв при гибели поджигает горящие бочки в этом радиусе и раскидывает разруху в полтора раза дальше")]
        [SerializeField] private float deathIgniteRadius = 7f;
        [Tooltip("Горящий остов после взрыва (EnemyWreck)")]
        [SerializeField] private EnemyWreck.Settings wreck = new EnemyWreck.Settings();
        private int currentHealth;
        private int scaledMaxHealth;

        // Доля прочности для эффектов повреждений и полоски босса: считает сервер, видят оба игрока
        private readonly NetworkVariable<float> healthFraction = new NetworkVariable<float>(1f);

        public float HealthFraction => healthFraction.Value;
        public int ScoreValue => scoreValue;
        // Вызывается только на сервере
        public event Action<GameObject> OnDeath;
        // Враг погиб: у обоих игроков, с точкой гибели и тем, чей урон его добил (для отметки убийства)
        public static event Action<Vector3, ulong> Killed;

        // Урон одного попадания с клиента больше этого не бывает: защита от подменённого клиента
        private const int MaxRequestDamage = 500;
        // В Awake, чтобы здоровье было задано до первого урона
        void Awake()
        {
            currentHealth = maxHealth;
            scaledMaxHealth = maxHealth;
        }

        // Сложность волны: сервер вызывает до спавна врага
        public void ScaleMaxHealth(float multiplier)
        {
            scaledMaxHealth = Mathf.RoundToInt(maxHealth * multiplier);
            currentHealth = scaledMaxHealth;
        }

        // Этот метод вызывается атакующими игроками на своих компьютерах, урон считает сервер
        public void RequestDamage(int damage)
        {
            if (!IsSpawned) return;

            if (IsServer)
                TakeDamage(damage, NetworkManager.LocalClientId);
            else
                TakeDamageRpc(damage);
        }

        // Урон, посчитанный самим сервером (ракета стрелка): убийство засчитывается тому, кто стрелял
        public void ApplyServerDamage(int damage, ulong attacker)
        {
            if (!IsServer || !IsSpawned) return;
            TakeDamage(damage, attacker);
        }

        [Rpc(SendTo.Server)]
        private void TakeDamageRpc(int damage, RpcParams rpcParams = default)
        {
            TakeDamage(Mathf.Clamp(damage, 0, MaxRequestDamage), rpcParams.Receive.SenderClientId);
        }

        private void TakeDamage(int damage, ulong sender)
        {
            // Урон, пришедший после смерти, не должен повторно засчитывать убийство
            if (currentHealth <= 0 || damage <= 0) return;

            currentHealth -= damage;
            healthFraction.Value = Mathf.Clamp01((float)currentHealth / Mathf.Max(1, scaledMaxHealth));
            Debug.Log($"Враг получил {damage} урона от игрока {sender} (осталось {currentHealth})");

            if (currentHealth <= 0)
            {
                Debug.Log("Враг умер");
                OnDeath?.Invoke(gameObject);
                // Горящие бочки рядом с гибнущей машиной взрываются следом
                if (PropNetwork.Instance != null)
                    PropNetwork.Instance.IgniteInRadius(transform.position, deathIgniteRadius);
                // RPC уходит раньше сообщения о деспавне, поэтому клиент успевает показать взрыв
                Rigidbody body = GetComponent<Rigidbody>();
                Vector3 velocity = body != null ? body.linearVelocity : Vector3.zero;
                int seed = Random.Range(int.MinValue, int.MaxValue);
                // RPC себе приходит только в следующем кадре, когда врага уже нет: остов на сервере — сразу
                EnemyWreck.Spawn(gameObject, wreck, velocity, seed);
                ExplodeRpc(transform.position, transform.rotation, velocity, seed, sender);
                NetworkObject.Despawn();
            }
        }

        [Rpc(SendTo.Everyone)]
        private void ExplodeRpc(Vector3 position, Quaternion rotation, Vector3 velocity, int seed, ulong killer)
        {
            // Остов — копия ещё не исчезнувшей машины: у клиента RPC приходит раньше деспавна
            if (!IsServer)
                EnemyWreck.Spawn(gameObject, wreck, velocity, seed);
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, position, rotation);
            LooseProp.Blast(position, deathIgniteRadius * 1.5f, 10f);
            Killed?.Invoke(position, killer);
        }
    }
}
