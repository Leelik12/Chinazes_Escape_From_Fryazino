using UnityEngine;
using Photon.Pun;

public class VRMagazineSpawner : MonoBehaviourPun
{
    [Header("Настройки спавна")]
    [Tooltip("Префаб магазина, который будет появляться после вставки старого.")]
    [SerializeField] private GameObject magazinePrefab;

    [Tooltip("Точка, где будет появляться новый магазин.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Задержка перед спавном нового магазина после вставки (секунды).")]
    [SerializeField] private float respawnDelay = 1.5f;

    private bool canSpawn = true;

    /// <summary>
    /// Вызывается, когда магазин вставлен в оружие.
    /// </summary>
    public void OnMagazineInserted()
    {
        if (!canSpawn) return;
        canSpawn = false;

        // Вызываем спавн магазина через Photon
        photonView.RPC(nameof(SpawnNewMagazineRPC), RpcTarget.AllBuffered);
    }

    [PunRPC]
    private void SpawnNewMagazineRPC()
    {
        StartCoroutine(SpawnAfterDelay());
    }

    private System.Collections.IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        if (magazinePrefab == null || spawnPoint == null)
        {
            Debug.LogWarning("MagazineSpawner: не назначен префаб или точка спавна!");
            yield break;
        }

        // Создаём новый магазин через Photon
        GameObject newMag = PhotonNetwork.Instantiate(
            magazinePrefab.name,
            spawnPoint.position,
            spawnPoint.rotation
        );

        newMag.SetActive(true);

        newMag.transform.SetParent(this.transform);

        Debug.Log("Магазин появился!");
        canSpawn = true;
    }

}
