using System.Collections.Generic;
using UnityEngine;

namespace RacingProject
{
    // Трассер из пула ShotEffects: светящийся отрезок длиной length летит со скоростью speed
    // от дула к точке попадания и возвращается в пул, когда хвост долетел
    public class Tracer : MonoBehaviour
    {
        private const float Speed = 260f;
        private const float Length = 7f;
        private const float Width = 0.06f;

        private LineRenderer line;
        private Queue<Tracer> owner;
        private Vector3 from, direction;
        private float distance, travelled;

        private void Awake()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = Width * 0.4f;
            line.endWidth = Width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
        }

        public void Launch(Queue<Tracer> pool, Vector3 start, Vector3 end, Material material)
        {
            owner = pool;
            from = start;
            Vector3 path = end - start;
            distance = path.magnitude;
            direction = distance > 0.001f ? path / distance : Vector3.forward;
            travelled = 0f;
            line.sharedMaterial = material;
            gameObject.SetActive(true);
            Place();
        }

        private void Update()
        {
            travelled += Speed * Time.deltaTime;
            if (travelled - Length >= distance)
            {
                gameObject.SetActive(false);
                owner?.Enqueue(this);
                return;
            }
            Place();
        }

        // Хвост (начало линии) тоньше головы
        private void Place()
        {
            float head = Mathf.Min(travelled, distance);
            float tail = Mathf.Clamp(travelled - Length, 0f, head);
            line.SetPosition(0, from + direction * tail);
            line.SetPosition(1, from + direction * head);
        }
    }
}
