using UnityEngine;

namespace RacingProject.Enemy
{
    // Граната РПГ в полёте. Летит по прямой с постоянной скоростью у всех игроков одинаково,
    // попадание проверяет только копия на сервере (owner.IsServer), взрыв всем рассылает гранатомёт
    public class EnemyRocket : MonoBehaviour
    {
        [Tooltip("Дымный след: при взрыве отцепляется и догорает сам")]
        [SerializeField] private ParticleSystem trail;
        [SerializeField] private float trailFadeTime = 4f;
        [Tooltip("Радиус проверки попадания, м")]
        [SerializeField] private float hitRadius = 0.4f;

        public int Id { get; private set; }

        private EnemyRocketLauncher owner;
        private Vector3 velocity;
        private float dieTime;

        public void Launch(EnemyRocketLauncher launcher, int id, Vector3 direction, float speed, float lifetime)
        {
            owner = launcher;
            Id = id;
            velocity = direction * speed;
            transform.rotation = Quaternion.LookRotation(direction);
            dieTime = Time.time + lifetime;
        }

        private void Update()
        {
            if (Time.time >= dieTime)
            {
                Remove();
                return;
            }

            Vector3 step = velocity * Time.deltaTime;
            if (owner != null && owner.IsServer && owner.FindHit(transform.position, step, hitRadius, out RaycastHit hit))
            {
                transform.position = hit.point;
                owner.RocketHit(this, hit.point, hit.normal);
                return;
            }
            transform.position += step;
        }

        // След остаётся висеть в воздухе и рассеивается, сама граната пропадает сразу
        public void Remove()
        {
            if (trail != null)
            {
                trail.transform.SetParent(null, true);
                trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(trail.gameObject, trailFadeTime);
            }
            Destroy(gameObject);
        }
    }
}
