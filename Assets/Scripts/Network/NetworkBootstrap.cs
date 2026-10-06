using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Network
{
    // Создаёт NetworkManager из префаба, если его ещё нет. NetworkManager сам уходит в DontDestroyOnLoad,
    // поэтому при перезагрузке сцены объект в ней задублировался бы — вместо него в сцене лежит этот загрузчик
    [DefaultExecutionOrder(-1000)]
    public class NetworkBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkManager networkManagerPrefab;

        private void Awake()
        {
            if (NetworkManager.Singleton == null && networkManagerPrefab != null)
                Instantiate(networkManagerPrefab).name = networkManagerPrefab.name;
        }
    }
}
