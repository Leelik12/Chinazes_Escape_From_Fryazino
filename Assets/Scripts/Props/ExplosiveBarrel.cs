using System.Collections.Generic;
using UnityEngine;
using RacingProject.Enemy;
using RacingProject.Tutorial;

namespace RacingProject.Props
{
    // Горящая бочка: от пуль, гранаты или соседнего взрыва взрывается — ранит врагов рядом (и машину игроков,
    // если она близко), раскидывает предметы. Попадания и урон считает сервер через PropNetwork,
    // взрыв видят оба игрока. Сама бочка — LooseProp: машина её просто сбивает.
    // Тот же компонент у крупных целей наполнения карты (резервуары, брошенные машины): у них серия взрывов
    // по корпусу, после взрыва остаётся пожар и обгоревший корпус, а взрывная волна валит столбы ЛЭП (PowerLines)
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

        [Header("Крупная цель (резервуар, машина)")]
        [Tooltip("Сколько взрывов подряд: у резервуара — серия по корпусу")]
        [SerializeField] private int blastCount = 1;
        [Tooltip("Разброс точек серии от центра, м, и пауза между взрывами, с")]
        [SerializeField] private float blastSpread = 0f;
        [SerializeField] private float blastInterval = 0.15f;
        [Tooltip("Загораются после взрыва: пожар на месте резервуара или машины")]
        [SerializeField] private GameObject[] afterObjects = new GameObject[0];
        [Tooltip("Получают обгоревший материал после взрыва")]
        [SerializeField] private Renderer[] charRenderers = new Renderer[0];
        [SerializeField] private Material charMaterial;

        public int Index { get; set; }
        public bool Exploded { get; private set; }
        // Сервер: бочка уже взрывается — взрыв у всех отыграет RPC чуть позже, повторно её не взрываем
        public bool Detonated { get; private set; }
        public float ChainDelay => chainDelay;
        // Крупная цель (резервуар, машина): для рации и режиссёра
        public bool IsLarge => blastCount > 1;

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
                // ClosestPoint умеют только простые формы и выпуклый меш; невыпуклый MeshCollider (статичные пропсы,
                // здания), террейн и колёса — по габаритам
                bool simple = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider || collider is MeshCollider mesh && mesh.convex;
                Vector3 nearest = simple ? collider.ClosestPoint(point) : collider.bounds.ClosestPoint(point);
                float falloff = 1f - Mathf.Clamp01(Vector3.Distance(point, nearest) / radius);

                EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
                if (enemy != null && PropNetwork.Instance.MarkDamaged(enemy))
                    enemy.RequestDamage(Mathf.RoundToInt(enemyDamage * falloff));

                TutorialTarget target = collider.GetComponentInParent<TutorialTarget>();
                if (target != null && PropNetwork.Instance.MarkDamaged(target))
                    target.Hit(Mathf.RoundToInt(enemyDamage * falloff));

                PlayerHealth player = collider.GetComponentInParent<PlayerHealth>();
                if (player != null && PropNetwork.Instance.MarkDamaged(player))
                    player.RequestDamage(Mathf.RoundToInt(playerDamage * falloff), point);

                ExplosiveBarrel other = collider.GetComponentInParent<ExplosiveBarrel>();
                if (other != null && other != this && !other.Detonated)
                    PropNetwork.Instance.ChainExplode(other);
            }
            // Ударная волна валит столбы ЛЭП рядом
            if (PowerLines.Instance != null)
                PowerLines.Instance.ToppleInRadius(point, radius * 0.6f);
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
            // Серия по корпусу крупной цели: точки зависят только от номера, у обоих игроков одинаковы
            for (int i = 1; i < blastCount; i++)
            {
                float angle = i * 137.5f + Index * 31f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * blastSpread * (0.5f + 0.5f * (i % 2));
                StartCoroutine(BlastLater(point + offset + Vector3.up * (i % 3), i * blastInterval));
            }
            foreach (GameObject after in afterObjects)
                if (after != null)
                    after.SetActive(true);
            if (charMaterial != null)
                foreach (Renderer r in charRenderers)
                    if (r != null)
                    {
                        var materials = r.sharedMaterials;
                        for (int m = 0; m < materials.Length; m++) materials[m] = charMaterial;
                        r.sharedMaterials = materials;
                    }

            LooseProp.Blast(point, radius * 1.5f, propPush);
            PushedCars.Clear();
            foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                Rigidbody rb = collider.attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb.GetComponent<LooseProp>() == null && PushedCars.Add(rb))
                    rb.AddExplosionForce(carPush, point, radius, 0.5f, ForceMode.VelocityChange);
            }
        }

        private System.Collections.IEnumerator BlastLater(Vector3 point, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, point, Quaternion.identity);
        }

        private static readonly HashSet<Rigidbody> PushedCars = new HashSet<Rigidbody>();
    }
}
