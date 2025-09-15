using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class CheckpointManager : MonoBehaviour
{
    [Header("Чекпоинты")]
    public List<Transform> checkpoints; // список всех чекпоинтов трассы

    [Header("UI")]
    public TMP_Text checkpointText;


    private int currentCheckpoint = 0;

    void Start()
    {
        UpdateUI();
    }

    public void ReachCheckpoint(Transform checkpoint)
    {
        if (currentCheckpoint >= checkpoints.Count) return;

        // Проверяем, что игрок дошёл до нужного чекпоинта по порядку
        if (checkpoints[currentCheckpoint] == checkpoint)
        {
            currentCheckpoint++;
            UpdateUI();
            if (currentCheckpoint == checkpoints.Count)
            {
                SceneManager.LoadScene(1);
            }
        }
    }

    void UpdateUI()
    {
        if (checkpointText)
        {
            checkpointText.text = $"{currentCheckpoint} / {checkpoints.Count}";
        }
    }
}
