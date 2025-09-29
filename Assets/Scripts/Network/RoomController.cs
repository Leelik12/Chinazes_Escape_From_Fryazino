using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using ExitGames.Client.Photon; // для Hashtable
using UnityEngine.UI;

public class RoomController : MonoBehaviourPunCallbacks
{
    [Header("Ссылки на XR Rigs")]
    public GameObject driverRig;       // XR Rig водителя (без рук)
    public GameObject gunnerRig;       // XR Rig пулемётчика (с руками)

    [Header("UI Готовности")]
    public Image firstPlayerReadyCircle;
    public Image secondPlayerReadyCircle;
    public Color notReadyColor = Color.red;
    public Color readyColor = Color.green;

    private bool isLocalReady = false;

    public void OnReadyButtonPressed()
    {
        if (isLocalReady) return;

        isLocalReady = true;

        // Устанавливаем CustomProperty "IsReady"
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
                return;
        }

        // Все игроки готовы, активируем риги
        ActivatePlayerRigs();
    }

    private void ActivatePlayerRigs()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            // Мастер-клиент — водитель
            if (driverRig != null)
            {
                driverRig.SetActive(true);
            }
            if (gunnerRig != null)
            {
                gunnerRig.SetActive(false);
            }
        }
        else
        {
            // Второй игрок — пулемётчик
            if (driverRig != null)
            {
                driverRig.SetActive(false);
            }
            if (gunnerRig != null)
            {
                gunnerRig.SetActive(true);
            }
        }
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

        if (PhotonNetwork.PlayerList.Length > 1)
        {
            Player second = PhotonNetwork.PlayerList[1];
            secondPlayerReadyCircle.color = (second.CustomProperties.ContainsKey("IsReady") && (bool)second.CustomProperties["IsReady"])
                ? readyColor : notReadyColor;
        }
    }
}
