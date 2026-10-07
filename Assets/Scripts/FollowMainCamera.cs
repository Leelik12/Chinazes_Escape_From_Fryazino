using UnityEngine;

namespace RacingProject
{
    // Держит объект у активной камеры (у водителя и стрелка камеры разные): так частицы атмосферы —
    // пепел и туман у земли — рождаются вокруг игрока, а не по всей карте
    public class FollowMainCamera : MonoBehaviour
    {
        [Tooltip("Смещение от камеры в мировых осях, м")]
        [SerializeField] private Vector3 offset;

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera != null)
                transform.position = camera.transform.position + offset;
        }
    }
}
