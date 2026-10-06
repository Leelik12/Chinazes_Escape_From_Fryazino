using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;

namespace RacingProject.Enemy
{
    public class EnemyManager : NetworkBehaviour
    {
        [Header("Спавнпоинты")]
        [Tooltip("Все точки спавна врагов")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("Начальные точки спавна (2 врага)")]
        [SerializeField] private Transform[] initialSpawnPoints;

        [Header("Префаб врага")]
        [Tooltip("Префаб с NetworkObject, зарегистрированный в списке сетевых префабов NetworkManager")]
        [SerializeField] private GameObject enemyPrefab;

        [Header("UI")]
        [SerializeField] private TMP_Text killedTextUI;

        [Header("Настройки")]
        [SerializeField] private int enemiesPerWave = 2;

        // Враги живут и умирают на сервере, счёт убийств видят оба игрока
        private readonly NetworkVariable<int> killedEnemies = new NetworkVariable<int>();

        private List<GameObject> activeEnemies = new List<GameObject>();
        private bool gameStarted = false;

        void Start()
        {
            UpdateUI();
        }

        public override void OnNetworkSpawn()
        {
            killedEnemies.OnValueChanged += OnKillsChanged;
            UpdateUI();
        }

        public override void OnNetworkDespawn()
        {
            killedEnemies.OnValueChanged -= OnKillsChanged;
        }

        // Вызывается при старте игры из RoomController
        public void StartGame()
        {
            if (gameStarted) return;
            gameStarted = true;

            // Только сервер отвечает за спавн врагов и счёт
            if (IsServer)
            {
                killedEnemies.Value = 0;
                SpawnInitialEnemies();
            }
        }

        private void SpawnInitialEnemies()
        {
            foreach (var spawn in initialSpawnPoints)
            {
                Debug.Log($"[EnemyManager] Сервер спавнит врага в {spawn.name}");
                SpawnEnemyAt(spawn.position, spawn.rotation);
            }
        }

        private void SpawnEnemyAt(Vector3 position, Quaternion rotation)
        {
            GameObject enemy = Instantiate(enemyPrefab, position, rotation);
            // Враги уничтожаются вместе со сценой при перезапуске раунда
            enemy.GetComponent<NetworkObject>().Spawn(true);
            activeEnemies.Add(enemy);

            EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.OnDeath += OnEnemyKilled;
            }
        }

        // Только на сервере
        private void OnEnemyKilled(GameObject enemy)
        {
            killedEnemies.Value++;

            activeEnemies.Remove(enemy);

            // Сервер решает, когда начинать новую волну
            if (activeEnemies.Count == 0)
            {
                SpawnRandomWave();
            }
        }

        private void SpawnRandomWave()
        {
            List<Transform> availablePoints = new List<Transform>(spawnPoints);
            foreach (var p in initialSpawnPoints) availablePoints.Remove(p);

            int spawned = 0;
            while (spawned < enemiesPerWave && availablePoints.Count > 0)
            {
                int index = Random.Range(0, availablePoints.Count);
                Transform chosen = availablePoints[index];
                availablePoints.RemoveAt(index);

                SpawnEnemyAt(chosen.position, chosen.rotation);
                spawned++;
            }
        }

        private void OnKillsChanged(int previous, int current)
        {
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (killedTextUI != null)
            {
                killedTextUI.text = $"Убито: {killedEnemies.Value}";
            }
        }
    }
}
