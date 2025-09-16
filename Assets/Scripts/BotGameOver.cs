using UnityEngine;

public class BotGameOver : MonoBehaviour
{
    public GameManager Manager;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Manager.BotOver = true;
            Debug.Log("Бот доехал!");
        }
    }
}
