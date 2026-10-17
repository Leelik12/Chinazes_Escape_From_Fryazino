using System;
using System.Collections;
using UnityEngine;
using RacingProject.Enemy;

namespace RacingProject.Tutorial
{
    // Засада на полигоне: гранатомётчик на холме стреляет в остановившуюся машину. Граната — та же, что у врагов
    // (EnemyRocket: модель и дымный след), но ведёт её этот компонент и сам доворачивает на машину, так что
    // попадание гарантировано. Урон задаёт TutorialCourse — машина теряет заметную часть прочности перед ремонтом
    public class TutorialRpg : MonoBehaviour
    {
        [SerializeField] private Transform firePoint;
        [SerializeField] private EnemyRocket rocketPrefab;
        [SerializeField] private GameObject explosionPrefab;
        [Tooltip("Выхлоп назад при выстреле")]
        [SerializeField] private ParticleSystem backblast;
        [SerializeField] private AudioSource launchAudio;
        [Tooltip("Граната на трубе: видна, пока гранатомёт заряжен")]
        [SerializeField] private GameObject loadedRocket;
        [SerializeField] private float speed = 38f;
        [Tooltip("Доворот на цель, градусы в секунду")]
        [SerializeField] private float turnRate = 150f;
        [Tooltip("Взрыв, когда граната ближе этого к центру машины, м")]
        [SerializeField] private float hitDistance = 1.8f;
        [SerializeField] private float explosionPush = 9000f;
        [SerializeField] private float splashRadius = 7f;

        // Стреляет в машину; onHit — после взрыва
        public void Fire(Rigidbody target, PlayerHealth health, int damage, Action onHit)
        {
            StartCoroutine(Fly(target, health, damage, onHit));
        }

        private IEnumerator Fly(Rigidbody target, PlayerHealth health, int damage, Action onHit)
        {
            Vector3 aim = target.worldCenterOfMass;
            Vector3 direction = (aim - firePoint.position).normalized;
            EnemyRocket rocket = Instantiate(rocketPrefab, firePoint.position, Quaternion.LookRotation(direction));
            // Сам EnemyRocket ведёт гранату врагов по прямой и проверяет попадание через гранатомёт — здесь ведём сами
            rocket.enabled = false;
            if (loadedRocket != null) loadedRocket.SetActive(false);
            if (backblast != null) backblast.Play();
            if (launchAudio != null) launchAudio.Play();

            while (true)
            {
                aim = target.worldCenterOfMass;
                Vector3 toTarget = aim - rocket.transform.position;
                if (toTarget.magnitude <= hitDistance) break;
                direction = Vector3.RotateTowards(direction, toTarget.normalized, turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
                Vector3 step = direction * speed * Time.deltaTime;
                // Не перелетаем через цель за один кадр
                if (step.magnitude >= toTarget.magnitude) break;
                rocket.transform.SetPositionAndRotation(rocket.transform.position + step, Quaternion.LookRotation(direction));
                yield return null;
            }

            Vector3 point = rocket.transform.position;
            rocket.Remove();
            if (explosionPrefab != null) Instantiate(explosionPrefab, point, Quaternion.LookRotation(-direction));
            if (health != null) health.RequestDamage(damage);
            if (!target.isKinematic)
                target.AddExplosionForce(explosionPush, point, splashRadius, 0.6f, ForceMode.Impulse);
            onHit?.Invoke();
        }
    }
}
