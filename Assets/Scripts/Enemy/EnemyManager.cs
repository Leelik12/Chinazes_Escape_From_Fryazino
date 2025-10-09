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
    [SerializeField] private TMP_Text killedTextUI; // локальный UI для игрока
    [SerializeField] private TMP_Text killedTextUI2; // локальный UI для игрока

    [Header("Настройки")]
    [SerializeField] private int enemiesPerWave = 2; // количество врагов в последующих волнах

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
        SpawnInitialEnemies();
    }

    private void SpawnInitialEnemies()
    {
        foreach (var spawn in initialSpawnPoints)
        {
            Debug.Log("Заспавнило");
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
            enemyHealth.OnDeath += OnEnemyKilled; // подписка на событие смерти
        }
    }

    private void OnEnemyKilled(GameObject enemy)
    {
        killedEnemies++;
        UpdateUI();

        activeEnemies.Remove(enemy);

        // Если все текущие враги уничтожены, спавним новую волну
        if (activeEnemies.Count == 0)
        {
            SpawnRandomWave();
        }
    }

    private void SpawnRandomWave()
    {
        // Выбираем случайные spawnPoints, исключая начальные
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
            killedTextUI.text = $"Killed: {killedEnemies}";
            killedTextUI2.text = $"Killed: {killedEnemies}";
        }
    }
}
