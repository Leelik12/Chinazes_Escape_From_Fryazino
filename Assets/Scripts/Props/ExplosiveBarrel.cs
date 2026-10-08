using System.Collections.Generic;
using UnityEngine;
using RacingProject.Enemy;

namespace RacingProject.Props
{
    // Горящая бочка: от пуль, гранаты или соседнего взрыва взрывается — ранит врагов рядом (и машину игроков,
    // если она близко), раскидывает предметы. Попадания и урон считает сервер через PropNetwork,
    // взрыв видят оба игрока. Сама бочка — LooseProp: машина её просто сбивает
    public class ExplosiveBarrel : MonoBehaviour
    {
        [Tooltip("Сколько урона от пуль выдерживает бочка")]
        [SerializeField] private int hitPoints = 30;

        [Header("Взрыв")]
        [SerializeField] private float radius = 9f;
        [Tooltip("Урон врагу в центре взрыва, к краю радиуса падает до нуля")]
        [SerializeField] private int enemyDamage = 900;
        [Tooltip("Урон машине игроков в центре взрыва")]
        [SerializeField] private int playerDamage = 150;
        [Tooltip("Прирост скорости машин в центре взрыва, м/с")]
        [SerializeField] private float carPush = 6f;
        [Tooltip("Прирост скорости предметов разрухи в центре взрыва, м/с")]
        [SerializeField] private float propPush = 14f;
        [Tooltip("Соседние бочки в радиусе взрываются по цепочке с такой задержкой, с")]
        [SerializeField] private float chainDelay = 0.25f;
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Огонь, дым и свет: гаснут при взрыве")]
        [SerializeField] private GameObject[] fireObjects = new GameObject[0];

        public int Index { get; set; }
        public bool Exploded { get; private set; }
        // Сервер: бочка уже взрывается — взрыв у всех отыграет RPC чуть позже, повторно её не взрываем
        public bool Detonated { get; private set; }
        public float ChainDelay => chainDelay;

        private int currentHitPoints;

        private void Awake()
        {
            currentHitPoints = hitPoints;
        }

        // Попадание на любом компьютере; засчитывает сервер
        public void RequestHit(int damage)
        {
            if (Exploded || PropNetwork.Instance == null) return;
            PropNetwork.Instance.RequestBarrelHit(Index, damage);
        }

        // Только сервер: true, если бочка от этого попадания взорвалась
        public bool ApplyHit(int damage)
        {
            if (Detonated) return false;
            currentHitPoints -= damage;
            return currentHitPoints <= 0;
        }

        // Только сервер: false, если бочка уже взрывается
        public bool TryDetonate()
        {
            if (Detonated) return false;
            Detonated = true;
            return true;
        }

        // Только сервер: урон машинам и соседние бочки
        public void DealDamage()
        {
            Vector3 point = transform.position;
            foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(point, collider.ClosestPoint(point)) / radius);

                EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null && PropNetwork.Instance.MarkDamaged(enemy))
                    enemy.RequestDamage(Mathf.RoundToInt(enemyDamage * falloff));

                PlayerHealth player = collider.GetComponentInParent<PlayerHealth>();
                if (player != null && PropNetwork.Instance.MarkDamaged(player))
                    player.RequestDamage(Mathf.RoundToInt(playerDamage * falloff));

                ExplosiveBarrel other = collider.GetComponentInParent<ExplosiveBarrel>();
                if (other != null && other != this && !other.Detonated)
                    PropNetwork.Instance.ChainExplode(other);
            }
        }

        // У всех игроков: огонь гаснет, вспышка, взрывная волна. Машины толкает только тот,
        // у кого их физика (хост), у второго игрока они кинематические
        public void PlayExplosion()
        {
            if (Exploded) return;
            Exploded = true;

            Vector3 point = transform.position;
            foreach (GameObject fire in fireObjects)
                if (fire != null)
                    fire.SetActive(false);
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, point, Quaternion.identity);

            LooseProp.Blast(point, radius * 1.5f, propPush);
            PushedCars.Clear();
            foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                Rigidbody rb = collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb.GetComponent<LooseProp>() == null && PushedCars.Add(rb))
                    rb.AddExplosionForce(carPush, point, radius, 0.5f, ForceMode.VelocityChange);
            }
        }

        private static readonly HashSet<Rigidbody> PushedCars = new HashSet<Rigidbody>();
    }
}
