using UnityEngine;

public class EnemyHeaths : MonoBehaviour
{
    [SerializeField] private int maxHealth=1000;
    [SerializeField] private int currentHealth=1000;
    void Start()
    {
        currentHealth = maxHealth;
    }
    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log("Враг получил урон!");
        if (currentHealth <= 0)
        {
            Debug.Log("Враг умер");
        }
    }
}
