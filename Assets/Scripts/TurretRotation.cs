using Photon.Pun;
using UnityEngine;


public class TurretRotation : MonoBehaviourPun
{
    [Header("Настройки")]
    public float rotationSpeed = 90f; // градусов в секунду

    float rotation;

    void Update()
    {
        rotation = Input.GetAxis("Horizontal");

        if (rotation != 0f)
        {
            if (Mathf.Abs(rotation) > 0.1f) // мёртвая зона
            {
                transform.Rotate(Vector3.up * rotation * rotationSpeed * Time.deltaTime);
            }
        }
    }
}