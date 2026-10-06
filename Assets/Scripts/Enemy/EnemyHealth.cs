using UnityEngine;
using Unity.Netcode;
using System;

namespace RacingProject.Enemy
{
    public class EnemyHealth : NetworkBehaviour
    {
        [SerializeField] private int maxHealth = 1000;
        [Tooltip("Эффект гибели: создаётся у обоих игроков в точке, где враг погиб")]
        [SerializeField] private GameObject explosionPrefab;
        private int currentHealth;
        // Вызывается только на сервере
        public event Action<GameObject> OnDeath;
        // В Awake, чтобы здоровье было задано до первого урона
        void Awake()
        {
            currentHealth = maxHealth;
        }

        // Сложность волны: сервер вызывает до спавна врага
        public void ScaleMaxHealth(float multiplier)
        {
            currentHealth = Mathf.RoundToInt(maxHealth * multiplier);
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

        [Rpc(SendTo.Server)]
        private void TakeDamageRpc(int damage, RpcParams rpcParams = default)
        {
            TakeDamage(damage, rpcParams.Receive.SenderClientId);
        }

        private void TakeDamage(int damage, ulong sender)
        {
            // Урон, пришедший после смерти, не должен повторно засчитывать убийство
            if (currentHealth <= 0) return;

            currentHealth -= damage;
            Debug.Log($"Враг получил {damage} урона от игрока {sender} (осталось {currentHealth})");

            if (currentHealth <= 0)
            {
                Debug.Log("Враг умер");
                OnDeath?.Invoke(gameObject);
                // RPC уходит раньше сообщения о деспавне, поэтому клиент успевает показать взрыв
                ExplodeRpc(transform.position, transform.rotation);
                NetworkObject.Despawn();
            }
        }

        [Rpc(SendTo.Everyone)]
        private void ExplodeRpc(Vector3 position, Quaternion rotation)
        {
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, position, rotation);
        }
    }
}
