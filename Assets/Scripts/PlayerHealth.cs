using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerHealth : MonoBehaviourPun
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;

    public event System.Action<int> OnDamageTaken;
    public event System.Action OnDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    void Start()
    {
        currentHealth = maxHealth;

        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    // Любой игрок вызывает этот метод, чтобы нанести урон
    public void RequestDamage(int damage)
    {
        if (photonView.IsMine)
        {
            photonView.RPC(nameof(TakeDamage), RpcTarget.All, damage);
        }
        else
        {
            photonView.RPC(nameof(TakeDamage), photonView.Owner, damage);
        }
    }

    [PunRPC]
    private void TakeDamage(int damage, PhotonMessageInfo info)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (healthSlider != null)
        {
            healthSlider.value = currentHealth;
        }

        Debug.Log($"{gameObject.name} получил {damage} урона. Текущее здоровье: {currentHealth}");

        OnDamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} умер!");
        OnDeath?.Invoke();
    }
}
