using UnityEngine;

namespace RacingProject.Tutorial
{
    // Машина-мишень на полигоне обучения: от пуль и взрыва бочки загорается и взрывается, вместо неё остаётся остов.
    // Обучение — одиночная игра, поэтому попадания считаются на месте, без сети
    public class TutorialTarget : MonoBehaviour
    {
        [SerializeField] private int hitPoints = 300;
        [Tooltip("Целая машина: выключается при уничтожении")]
        [SerializeField] private GameObject intact;
        [Tooltip("Сгоревший остов: включается при уничтожении")]
        [SerializeField] private GameObject wreck;
        [SerializeField] private GameObject explosionPrefab;

        private int currentHitPoints;

        public bool Destroyed { get; private set; }

        private void Awake()
        {
            currentHitPoints = hitPoints;
            if (wreck != null) wreck.SetActive(false);
        }

        public void Hit(int damage)
        {
            if (Destroyed) return;
            currentHitPoints -= damage;
            if (currentHitPoints <= 0)
                Explode();
        }

        private void Explode()
        {
            Destroyed = true;
            if (intact != null) intact.SetActive(false);
            if (wreck != null) wreck.SetActive(true);
            if (explosionPrefab != null)
                Instantiate(explosionPrefab, transform.position + Vector3.up, Quaternion.identity);
        }
    }
}
