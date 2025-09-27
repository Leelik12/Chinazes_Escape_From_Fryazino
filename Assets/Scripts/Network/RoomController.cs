using Photon.Pun;
using UnityEngine;

public class RoomController : MonoBehaviourPunCallbacks
{
    [Header("Имена префабов в Resources")]
    public string carPrefabName = "CarPrefab";       // имя префаба машины
    public string turretPrefabName = "TurretPrefab"; // имя префаба турели

    [Header("Точка спавна машины")]
    public Transform vehicleSpawnPoint;

    public override void OnJoinedRoom()
    {
        base.OnJoinedRoom();

        // Проверяем, если я мастер-клиент (первый игрок)
        if (PhotonNetwork.IsMasterClient)
        {
            Vector3 pos = vehicleSpawnPoint != null ? vehicleSpawnPoint.position : Vector3.zero;
            Quaternion rot = vehicleSpawnPoint != null ? vehicleSpawnPoint.rotation : Quaternion.identity;

            GameObject car = PhotonNetwork.Instantiate(carPrefabName, pos, rot);
            car.tag = "Car";

            Debug.Log("Спавн машины для MasterClient");
        }
        else
        {
            // Второй игрок = турель
            // Находим машину
            GameObject car = GameObject.FindWithTag("Car");

            Vector3 spawnPos = car != null ? car.transform.position + new Vector3(0, 1.5f, 0) : new Vector3(2, 0, 0);
            GameObject turret = PhotonNetwork.Instantiate(turretPrefabName, spawnPos, Quaternion.identity);

            // Привязываем турель к машине локально
            if (car != null)
            {
                turret.transform.SetParent(car.transform);
                Debug.Log("туррель привязана к машине");
                turret.transform.localPosition = new Vector3(0, 1.5f, 0);
                turret.transform.localRotation = Quaternion.identity;
            }

            Debug.Log("Спавн турели для второго игрока");
        }
    }
}
