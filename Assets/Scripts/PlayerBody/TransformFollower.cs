using UnityEngine;

public class TransformFollower : MonoBehaviour
{
    public Transform target;          //  онтроллер
    public bool followPosition = true;
    public bool followRotation = true;

    private Vector3 positionOffset;
    private Quaternion rotationOffset;

    void Start()
    {
        // –ассчитываем смещение при старте (как будто SetParent с сохранением положени€)
        positionOffset = transform.position - target.position;
        rotationOffset = Quaternion.Inverse(target.rotation) * transform.rotation;
    }

    // —амое важное: LateUpdate Ч чтобы обновить после всех перемещений контроллеров
    void LateUpdate()
    {
        if (!target) return;

        if (followPosition)
            transform.position = target.position + target.rotation * positionOffset;

        if (followRotation)
            transform.rotation = target.rotation * rotationOffset;
    }

    // „тобы максимально убрать микролаги в VR Ч обновл€ем также перед рендером
    void OnBeforeRender()
    {
        if (!target) return;

        if (followPosition)
            transform.position = target.position + target.rotation * positionOffset;

        if (followRotation)
            transform.rotation = target.rotation * rotationOffset;
    }
}
