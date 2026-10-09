using TMPro;
using UnityEngine;

namespace RacingProject.Management
{
    // Номер версии сборки (Player Settings → Version) в шапке главного экрана меню
    [RequireComponent(typeof(TMP_Text))]
    public class VersionLabel : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<TMP_Text>().text = "v" + Application.version;
        }
    }
}
