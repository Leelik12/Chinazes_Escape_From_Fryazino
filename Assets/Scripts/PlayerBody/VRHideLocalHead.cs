using UnityEngine;
using Photon.Pun;

public class VRHeadFollowAndHide : MonoBehaviourPun
{
    [Header("Ссылки на объекты")]
    [Tooltip("XR-камера игрока (голова XR Rig)")]
    [SerializeField] private Transform headTarget;

    [Tooltip("Кость головы модели или её объект")]
    [SerializeField] private Transform headBone;

    [Tooltip("Корень, содержащий визуальные части головы (модель, кепка, очки и т.д.)")]
    [SerializeField] private GameObject headVisualRoot;

    [Header("Настройки позиционирования")]
    [Tooltip("Смещение головы относительно камеры, чтобы камера не попадала внутрь головы")]
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 0f, -0.08f);

    private void Start()
    {
        // Локальный игрок — скрываем визуал головы, чтобы не мешал камере
        if (photonView.IsMine && headVisualRoot != null)
        {
            SetHeadVisible(false);
        }
    }

    private void LateUpdate()
    {
        if (headTarget == null || headBone == null)
            return;

        // Синхронизируем позицию и вращение головы
        headBone.position = headTarget.TransformPoint(headOffset);
        headBone.rotation = headTarget.rotation;
    }

    private void SetHeadVisible(bool visible)
    {
        // Скрываем только визуальные рендеры, не трогая коллайдеры и трансформы
        var renderers = headVisualRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.enabled = visible;
        }
    }
}
