using UnityEngine;
using Photon.Pun;

public class VRControllerProxyUpdater : MonoBehaviourPun
{
    [Header("XR контроллеры (реальные)")]
    public Transform leftController;
    public Transform rightController;

    [Header("Target объекты, за которыми следит IK")]
    public Transform leftTarget;
    public Transform rightTarget;

    private void OnEnable()
    {
        // Подписываемся на событие перед рендером кадра
        Application.onBeforeRender += UpdateProxyPositions;
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= UpdateProxyPositions;
    }

    private void LateUpdate()
    {
        // Для страховки — обновляем и здесь, если вдруг кадр пропущен
        UpdateProxyPositions();
    }

    private void UpdateProxyPositions()
    {
        // Только для локального игрока
        if (!photonView.IsMine) return;

        if (leftController && leftTarget)
        {
            leftTarget.position = leftController.position;
            leftTarget.rotation = leftController.rotation;
        }

        if (rightController && rightTarget)
        {
            rightTarget.position = rightController.position;
            rightTarget.rotation = rightController.rotation;
        }
    }
}
