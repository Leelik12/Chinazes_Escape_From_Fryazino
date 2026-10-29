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
        // Только сервер: взорвалась бочка или крупная цель (для рации и режиссёра)
        public static event System.Action<ExplosiveBarrel> ServerExploded;

        [Tooltip("Ещё группы со взрывающимися предметами (наполнение карты); номера идут после дочерних бочек")]
        [SerializeField] private Transform[] extraRoots = new Transform[0];

        private ExplosiveBarrel[] barrels;
        private BreakableProp[] breakables;
        // Чтобы машина из нескольких коллайдеров получила урон от одного взрыва один раз
        private readonly HashSet<Object> damagedThisBlast = new HashSet<Object>();

        private void Awake()
        {
            Instance = this;
            // Порядок обхода иерархии одинаков у обоих игроков, поэтому номера совпадают
            var all = new List<ExplosiveBarrel>(GetComponentsInChildren<ExplosiveBarrel>(true));
            foreach (Transform root in extraRoots)
                if (root != null)
                    all.AddRange(root.GetComponentsInChildren<ExplosiveBarrel>(true));
            barrels = all.ToArray();
            for (int i = 0; i < barrels.Length; i++)
                barrels[i].Index = i;
            breakables = BreakableProp.Ordered().ToArray();
            for (int i = 0; i < breakables.Length; i++)
                breakables[i].Index = i;
        }

        // Тот, кто считает физику машины (хост): постройка проломлена — у всех она разваливается
        public void BreakProp(BreakableProp prop, Vector3 velocity, Vector3 point)
        {
            if (IsSpawned && IsServer)
                BreakRpc(prop.Index, velocity, point);
            else
                prop.Break(velocity, point);
        }

        [Rpc(SendTo.Everyone)]
        private void BreakRpc(int index, Vector3 velocity, Vector3 point)
        {
            if (index >= 0 && index < breakables.Length)
                breakables[index].Break(velocity, point);
        }

        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            base.OnDestroy();
        }

        // Бочка из подгруженной сцены (полигон обучения) получает номер после бочек города.
        // Только для одиночной игры: у напарника такой бочки нет, и номера бы разошлись
        public void Register(ExplosiveBarrel barrel)
        {
            barrel.Index = barrels.Length;
            System.Array.Resize(ref barrels, barrels.Length + 1);
            barrels[barrel.Index] = barrel;
        }

        // Урон одного попадания по бочке с клиента больше этого не бывает
        private const int MaxRequestDamage = 500;

        public void RequestBarrelHit(int index, int damage)
        {
            // Без сети (отладка в редакторе) всё считается на месте
            if (!IsSpawned || IsServer)
                ApplyBarrelHit(index, damage);
            else
                BarrelHitRpc(index, damage);
        }

        // Получено с клиента: урон проверяется, клиенту нельзя доверять
        [Rpc(SendTo.Server)]
        private void BarrelHitRpc(int index, int damage)
        {
            ApplyBarrelHit(index, Mathf.Clamp(damage, 0, MaxRequestDamage));
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
            ServerExploded?.Invoke(barrel);
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
