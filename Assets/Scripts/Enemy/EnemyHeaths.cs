using UnityEngine;
using Photon.Pun;

public class EnemyHealth : MonoBehaviourPun
{
    [SerializeField] private int maxHealth = 1000;
    private int currentHealth;

    void Start()
    {
        currentHealth = maxHealth;
    }

    // Этот метод будет вызываться атакующими игроками
    public void RequestDamage(int damage)
    {
        // Отправляем RPC хозяину врага
        photonView.RPC("TakeDamage", RpcTarget.MasterClient, damage);
    }

    [PunRPC]
    public void TakeDamage(int damage, PhotonMessageInfo info)
    {
        // Выполняется только на мастер-клиенте (или владельце врага)
        if (!photonView.IsMine) return;

        currentHealth -= damage;
        Debug.Log($"Враг получил {damage} урона от {info.Sender} (осталось {currentHealth})");

        if (currentHealth <= 0)
        {
            Debug.Log("Враг умер");
            PhotonNetwork.Destroy(gameObject);
        }
    }
}
