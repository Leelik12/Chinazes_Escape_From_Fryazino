using UnityEngine;
using Photon.Pun;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
        // Создаём магазин
        GameObject newMag = PhotonNetwork.Instantiate(
            magazinePrefab.name,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // Убеждаемся, что он активен
        newMag.SetActive(true);

        // Сбрасываем Rigidbody
        Rigidbody rb = newMag.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Переинициализируем XRGrabInteractable
        var grab = newMag.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.interactionManager = null;
            grab.selectEntered.RemoveAllListeners();
            grab.selectExited.RemoveAllListeners();

            // Перезапускаем компонент (важно для второго и следующих магазинов)
            grab.enabled = false;
            grab.enabled = true;
        }

        // Назначаем родителя
        newMag.transform.SetParent(this.transform, true);

        Debug.Log("Магазин появился и готов к взаимодействию!");
        canSpawn = true;

    }

}
