using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviourPun
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 1000;
    [SerializeField] private int currentHealth = 1000;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    public HealthBarGradient HPBAR;

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

    // Урон запрашивается кем угодно, но считается только у владельца
    public void RequestDamage(int damage)
    {
        if (photonView.Owner == null) return;

        if (photonView.IsMine)
        {
            ApplyDamage(damage);
        }
        else
        {
            // просим владельца обработать урон
            photonView.RPC(nameof(RPC_RequestDamageFromOther), photonView.Owner, damage);
        }
    }

    // Получено с другого клиента
    [PunRPC]
    private void RPC_RequestDamageFromOther(int damage)
    {
        if (!photonView.IsMine) return;
        ApplyDamage(damage);
    }

    // Применяем урон только на своём клиенте, потом синхронизируем
    private void ApplyDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        photonView.RPC(nameof(RPC_SyncHealth), RpcTarget.All, currentHealth);

        Debug.Log($"{gameObject.name} получил {damage} урона. Текущее здоровье: {currentHealth}");

        OnDamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    //Рассылаем обновлённое здоровье
    [PunRPC]
    private void RPC_SyncHealth(int newHealth)
    {
        currentHealth = newHealth;

        if (healthSlider != null)
        {
            HPBAR.SetHealth(currentHealth, maxHealth);
        }
    }

    private void Die()
    {
        Debug.Log($"{gameObject.name} умер!");
        OnDeath?.Invoke();

        // перезагрузка сцены для всех — только мастер
        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(SceneManager.GetActiveScene().name);
        }
    }
}
