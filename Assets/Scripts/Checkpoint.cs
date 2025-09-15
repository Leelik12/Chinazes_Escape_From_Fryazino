using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    // —сылка на центральный менеджер
    public CheckpointManager checkpointManager;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) // провер€ем, что это машина игрока
        {
            checkpointManager.PassCheckpoint(this);
        }
    }
}
