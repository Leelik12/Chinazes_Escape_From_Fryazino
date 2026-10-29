using UnityEngine;

namespace RacingProject.Props
{
    // Передаёт удары по коллайдеру модели в BreakableProp на корне префаба: у статичного коллайдера без Rigidbody
    // OnCollisionEnter получает только его собственный объект
    public class BreakableContact : MonoBehaviour
    {
        private BreakableProp prop;

        private void Awake()
        {
            prop = GetComponentInParent<BreakableProp>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (prop != null) prop.OnHit(collision);
        }
    }
}
