using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerHealth : MonoBehaviourPun
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 1000;
    [SerializeField] private int currentHealth = 1000;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    public BarGradient HPBAR;

    public event System.Action<int> OnDamageTaken;
    public event System.Action OnDeath;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private bool isDead;

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
        if (isDead || photonView.Owner == null) return;

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
        if (isDead) return;

        int newHealth = Mathf.Clamp(currentHealth - damage, 0, maxHealth);

        // RpcTarget.All выполняется локально сразу, поэтому здоровье владельца меняется здесь же
        photonView.RPC(nameof(RPC_SyncHealth), RpcTarget.All, newHealth);
    }

    // Рассылаем обновлённое здоровье; события урона и смерти срабатывают на всех клиентах
    [PunRPC]
    private void RPC_SyncHealth(int newHealth)
    {
        if (isDead) return;

        int damage = currentHealth - newHealth;
        currentHealth = newHealth;

        if (HPBAR != null)
            HPBAR.SetHealth(currentHealth, maxHealth);
        else if (healthSlider != null)
            healthSlider.value = currentHealth;

        if (damage > 0)
            OnDamageTaken?.Invoke(damage);

        if (currentHealth <= 0)
            Die();
    }

    // Перезапуск раунда выполняет RoomController по событию OnDeath
    private void Die()
    {
        isDead = true;
        Debug.Log($"{gameObject.name} уничтожен");
        OnDeath?.Invoke();
    }
}
