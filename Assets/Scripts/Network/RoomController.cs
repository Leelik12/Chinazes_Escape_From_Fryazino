using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon;
using UnityEngine.UI;
using System.Collections;

public class RoomController : MonoBehaviourPunCallbacks
{
    [Header("Prefabs")]
    public string carPrefabName = "CarPrefab";
    public string turretPrefabName = "TurretPrefab";

    [Header("Spawn Point")]
    public Transform vehicleSpawnPoint;

    [Header("UI Ready")]
    public Image firstPlayerReadyCircle;
    public Image secondPlayerReadyCircle;
    public Color notReadyColor = Color.red;
    public Color readyColor = Color.green;

    private bool isLocalReady = false;

    // Номер машины для привязки турели
    private int carViewID = -1;

    public void OnReadyButtonPressed()
    {
        if (isLocalReady) return;

        isLocalReady = true;

        // Устанавливаем CustomProperty для Photon
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable { { "IsReady", true } };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        UpdateReadyUI();
        CheckAllPlayersReady();
    }

    private void CheckAllPlayersReady()
    {
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.ContainsKey("IsReady") || !(bool)player.CustomProperties["IsReady"])
                return; // Кто-то еще не готов
        }

        // Все готовы — начинаем спавн
        StartCoroutine(SpawnPlayersCoroutine());
    }

    private IEnumerator SpawnPlayersCoroutine()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Спавн машины с водителем
            Vector3 pos = vehicleSpawnPoint.position;
            Quaternion rot = vehicleSpawnPoint.rotation;

            GameObject car = PhotonNetwork.Instantiate(carPrefabName, pos, rot);
            car.tag = "Car";

            // Сохраняем ViewID машины в CustomProperties
            carViewID = car.GetComponent<PhotonView>().ViewID;
            ExitGames.Client.Photon.Hashtable carProp = new ExitGames.Client.Photon.Hashtable { { "CarViewID", carViewID } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(carProp);

            Debug.Log("Master spawned car with ViewID: " + carViewID);
        }
        else
        {
            // Второй игрок — турель
            // Ждем, пока мастер запишет CarViewID
            while (carViewID == -1)
            {
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    if (player.IsMasterClient && player.CustomProperties.ContainsKey("CarViewID"))
                    {
                        carViewID = (int)player.CustomProperties["CarViewID"];
                        break;
                    }
                }
                yield return null;
            }

            GameObject car = PhotonView.Find(carViewID).gameObject;
            Vector3 spawnPos = car.transform.position + new Vector3(0, 1.5f, 0);

            // Спавн турели локально
            GameObject turret = PhotonNetwork.Instantiate(turretPrefabName, spawnPos, Quaternion.identity);

            // RPC, чтобы все клиенты правильно привязали турель к машине
            photonView.RPC("AttachTurretRPC", RpcTarget.AllBuffered, car.GetComponent<PhotonView>().ViewID, turret.GetComponent<PhotonView>().ViewID);
        }
    }

    [PunRPC]
    private void AttachTurretRPC(int carID, int turretID)
    {
        GameObject car = PhotonView.Find(carID).gameObject;
        GameObject turret = PhotonView.Find(turretID).gameObject;

        turret.transform.SetParent(car.transform, false);
        turret.transform.localPosition = new Vector3(0, 1.5f, 0);
        turret.transform.localRotation = Quaternion.identity;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (changedProps.ContainsKey("IsReady"))
        {
            UpdateReadyUI();
            CheckAllPlayersReady();
        }
    }

    private void UpdateReadyUI()
    {
        if (PhotonNetwork.PlayerList.Length < 2) return;

        Player first = PhotonNetwork.PlayerList[0];
        firstPlayerReadyCircle.color = (first.CustomProperties.ContainsKey("IsReady") && (bool)first.CustomProperties["IsReady"])
            ? readyColor : notReadyColor;

        Player second = PhotonNetwork.PlayerList[1];
        secondPlayerReadyCircle.color = (second.CustomProperties.ContainsKey("IsReady") && (bool)second.CustomProperties["IsReady"])
            ? readyColor : notReadyColor;
    }
}
