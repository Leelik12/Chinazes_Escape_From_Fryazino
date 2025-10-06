using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerHealth : MonoBehaviourPun
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private int currentHealth = 100;

    [Header("UI")]
    [SerializeField] private Slider healthSlider; // Присвойте слайдер через инспектор

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

        // Обновляем слайдер
        if (healthSlider != null)
        {
            healthSlider.value = currentHealth;
        }

        Debug.Log($"{gameObject.name} получил {damage} урона. Текущее здоровье: {currentHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} умер!");
        // Можно отключить контроллер, а потом респаун
        // this.gameObject.SetActive(false);
    }

    // Геттеры для UI или других скриптов
    public int GetCurrentHealth() => currentHealth;
    public int GetMaxHealth() => maxHealth;
}
