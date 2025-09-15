using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public CheckpointManager manager;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            manager.ReachCheckpoint(transform);
        }
    }
}
