using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;

public class EnemyManager : MonoBehaviourPun
{
    [Header("Спавнпоинты")]
    [Tooltip("Все точки спавна врагов")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("Начальные точки спавна (2 врага)")]
    [SerializeField] private Transform[] initialSpawnPoints;

    [Header("Префаб врага")]
    [SerializeField] private GameObject enemyPrefab;

    [Header("UI")]
    [SerializeField] private TMP_Text killedTextUI;

    [Header("Настройки")]
    [SerializeField] private int enemiesPerWave = 2;

    private List<GameObject> activeEnemies = new List<GameObject>();
    private int killedEnemies = 0;
    private bool gameStarted = false;

    void Start()
    {
        UpdateUI();
    }

    // Вызывается при старте игры из RoomController
    public void StartGame()
    {
        if (gameStarted) return;
        gameStarted = true;

        // Только мастер-клиент отвечает за спавн врагов
        if (PhotonNetwork.IsMasterClient)
        {
            SpawnInitialEnemies();
        }
    }

    private void SpawnInitialEnemies()
    {
        foreach (var spawn in initialSpawnPoints)
        {
            Debug.Log($"[EnemyManager] Мастер спавнит врага в {spawn.name}");
            SpawnEnemyAt(spawn.position, spawn.rotation);
        }
    }

    private void SpawnEnemyAt(Vector3 position, Quaternion rotation)
    {
        GameObject enemy = PhotonNetwork.Instantiate(enemyPrefab.name, position, rotation);
        activeEnemies.Add(enemy);

        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += OnEnemyKilled;
        }
    }

    private void OnEnemyKilled(GameObject enemy)
    {
        killedEnemies++;
        UpdateUI();

        activeEnemies.Remove(enemy);

        // Только мастер решает, когда начинать новую волну
        if (PhotonNetwork.IsMasterClient && activeEnemies.Count == 0)
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

    private void UpdateUI()
    {
        if (killedTextUI != null)
        {
            killedTextUI.text = $"Убито: {killedEnemies}";
        }
    }
}
