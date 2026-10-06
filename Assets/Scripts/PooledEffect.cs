using System.Collections.Generic;
using UnityEngine;

namespace RacingProject
{
    // Экземпляр эффекта из пула ShotEffects: проигрывается заново и через lifetime возвращается в пул
    public class PooledEffect : MonoBehaviour
    {
        private ParticleSystem[] particles;
        private Queue<PooledEffect> owner;

        private void Awake()
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void Play(Queue<PooledEffect> pool, float lifetime)
        {
            owner = pool;
            gameObject.SetActive(true);
            foreach (var ps in particles)
            {
                ps.Clear(true);
                ps.Play(true);
            }

            CancelInvoke(nameof(Release));
            Invoke(nameof(Release), lifetime);
        }

        private void Release()
        {
            gameObject.SetActive(false);
            owner?.Enqueue(this);
        }
    }
}
