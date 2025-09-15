using System.Collections.Generic;
using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    public List<Checkpoint> checkpoints; // все чекпоинты трассы в порядке
    private int nextCheckpointIndex = 0;  // индекс следующего чекпоинта
    public int lapsCompleted = 0;

    public void PassCheckpoint(Checkpoint cp)
    {
        if (checkpoints[nextCheckpointIndex] == cp)
        {
            // Пройден правильный чекпоинт
            nextCheckpointIndex++;

            if (nextCheckpointIndex >= checkpoints.Count)
            {
                // Круг завершён
                lapsCompleted++;
                nextCheckpointIndex = 0;
                Debug.Log("Круг завершён! Кругов пройдено: " + lapsCompleted);
            }
            else
            {
                Debug.Log("Чекпоинт пройден: " + nextCheckpointIndex);
            }
        }
        else
        {
            // Игрок прошёл чекпоинт не по порядку
            Debug.Log("Пропущен чекпоинт или не по порядку!");
        }
    }

    public int GetNextCheckpointIndex()
    {
        return nextCheckpointIndex;
    }
}
