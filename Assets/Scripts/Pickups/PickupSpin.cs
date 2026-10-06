using UnityEngine;

namespace RacingProject.Pickups
{
    // Вращение и покачивание подбираемого предмета, чтобы его было видно издалека
    public class PickupSpin : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 90f;
        [SerializeField] private float bobHeight = 0.25f;
        [SerializeField] private float bobSpeed = 2f;

        private Vector3 basePosition;

        private void Awake()
        {
            basePosition = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.World);
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
        }
    }
}
