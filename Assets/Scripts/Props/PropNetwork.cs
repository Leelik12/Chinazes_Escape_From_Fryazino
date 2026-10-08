using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace RacingProject.Props
{
    // Сеть для разрухи: бочки — обычные объекты сцены, у обоих игроков одни и те же,
    // поэтому их можно звать по номеру в иерархии. Попадания засчитывает сервер, взрыв рассылается всем
    public class PropNetwork : NetworkBehaviour
    {
        public static PropNetwork Instance { get; private set; }

        private ExplosiveBarrel[] barrels;
        // Чтобы машина из нескольких коллайдеров получила урон от одного взрыва один раз
        private readonly HashSet<Object> damagedThisBlast = new HashSet<Object>();

        private void Awake()
        {
            Instance = this;
            barrels = GetComponentsInChildren<ExplosiveBarrel>(true);
            for (int i = 0; i < barrels.Length; i++)
                barrels[i].Index = i;
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            base.OnDestroy();
        }

        public void RequestBarrelHit(int index, int damage)
        {
            // Без сети (отладка в редакторе) всё считается на месте
            if (!IsSpawned || IsServer)
                ApplyBarrelHit(index, damage);
            else
                BarrelHitRpc(index, damage);
        }

        [Rpc(SendTo.Server)]
        private void BarrelHitRpc(int index, int damage)
        {
            ApplyBarrelHit(index, damage);
        }

        private void ApplyBarrelHit(int index, int damage)
        {
            if (index < 0 || index >= barrels.Length) return;
            if (barrels[index].ApplyHit(damage))
                Explode(barrels[index]);
        }

        // Только сервер: соседняя бочка взрывается чуть позже, цепочка видна
        public void ChainExplode(ExplosiveBarrel barrel)
        {
            StartCoroutine(ExplodeLater(barrel, barrel.ChainDelay));
        }

        private IEnumerator ExplodeLater(ExplosiveBarrel barrel, float delay)
        {
            yield return new WaitForSeconds(delay);
            Explode(barrel);
        }

        // Только сервер: true, если цель в этом взрыве ещё не получала урон
        public bool MarkDamaged(Object target)
        {
            return damagedThisBlast.Add(target);
        }

        private void Explode(ExplosiveBarrel barrel)
        {
            if (!barrel.TryDetonate()) return;
            damagedThisBlast.Clear();
            barrel.DealDamage();
            if (IsSpawned)
                ExplodeRpc(barrel.Index);
            else
                barrel.PlayExplosion();
        }

        [Rpc(SendTo.Everyone)]
        private void ExplodeRpc(int index)
        {
            if (index >= 0 && index < barrels.Length)
                barrels[index].PlayExplosion();
        }

        // Только сервер: граната и другие взрывы поджигают бочки в радиусе
        public void IgniteInRadius(Vector3 point, float radius)
        {
            foreach (Collider collider in Physics.OverlapSphere(point, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                ExplosiveBarrel barrel = collider.GetComponentInParent<ExplosiveBarrel>();
                if (barrel != null && !barrel.Detonated)
                    ChainExplode(barrel);
            }
        }
    }
}
