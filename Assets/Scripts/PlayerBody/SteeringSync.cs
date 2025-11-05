using UnityEngine;
using Photon.Pun;

public class SteeringSync : MonoBehaviourPun, IPunObservable
{
    public float steeringAngle;
    private float networkAngle;
    public float smoothSpeed = 10f;

    void Update()
    {
        if (photonView.IsMine)
        {
            // локальный игрок управляет углом руля
            steeringAngle = Mathf.Lerp(steeringAngle, Input.GetAxis("Horizontal") * 45f, Time.deltaTime * 5f);
            transform.localRotation = Quaternion.Euler(0, 0, -steeringAngle);
        }
        else
        {
            // плавно обновляем поворот с сети
            transform.localRotation = Quaternion.Lerp(
                transform.localRotation,
                Quaternion.Euler(0, 0, -networkAngle),
                Time.deltaTime * smoothSpeed
            );
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
            stream.SendNext(steeringAngle);
        else
            networkAngle = (float)stream.ReceiveNext();
    }
}
