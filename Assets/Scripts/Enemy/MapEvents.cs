using System.Collections.Generic;
using RacingProject.Pickups;
using RacingProject.Props;
using Unity.Netcode;
using UnityEngine;

namespace RacingProject.Enemy
{
    // События карты во время волн (решает сервер, раз в eventInterval):
    // — обрыв ЛЭП: штаб предупреждает, и через warnTime опора у дороги впереди падает поперёк неё
    //   (провода под током искрят на асфальте — PowerLines), дорога перекрыта, надо объезжать;
    // — сброс снабжения: штаб называет точку на дороге, через dropDelay там ложатся ящик выстрелов к РПГ
    //   и ремкомплект (RocketCrates, RepairKits — их видят оба игрока);
    // — электричка: штаб предупреждает о поезде через ближний к машине переезд, и он проходит по линии
    //   (ElectricTrain): закрывает переезды, сносит врагов на путях, машину бьёт.
    // Сами события по сети не передаются: падение опоры, ящики, поезд и рация рассылают своё
    public class MapEvents : MonoBehaviour
    {
        [SerializeField] private EnemyManager enemyManager;
        [SerializeField] private PlayerHealth carHealth;
        [SerializeField] private RadioChatter radio;
        [SerializeField] private RepairKits repairKits;
        [SerializeField] private RocketCrates rocketCrates;

        [Tooltip("Пауза до первого события и между событиями, с")]
        [SerializeField] private Vector2 firstEvent = new Vector2(60f, 90f);
        [SerializeField] private Vector2 eventInterval = new Vector2(100f, 160f);

        [Header("Обрыв ЛЭП")]
        [SerializeField] private Vector2 poleDistance = new Vector2(90f, 260f);
        [Tooltip("Опора не дальше этого от оси дороги, м")]
        [SerializeField] private float poleRoadDistance = 20f;
        [SerializeField] private float warnTime = 5f;

        [Header("Сброс снабжения")]
        [SerializeField] private Vector2 dropDistance = new Vector2(150f, 320f);
        [SerializeField] private float dropDelay = 20f;

        [Header("Электричка")]
        [Tooltip("Переезд не дальше этого от машины, м")]
        [SerializeField] private float crossingDistance = 500f;

        private enum Pending { None, Pole, Drop }
        private enum Kind { Pole, Drop, Train }

        private float nextEvent = -1f;
        private Pending pending;
        private float pendingAt;
        private Vector3 pendingPoint;
        private Vector2Int pendingPole;
        private Kind lastKind = Kind.Train;
        private readonly List<Vector3> poles = new List<Vector3>();
        private readonly List<Vector2Int> poleIds = new List<Vector2Int>();
        private readonly List<int> picks = new List<int>();

        private static bool IsServer
        {
            get
            {
                NetworkManager manager = NetworkManager.Singleton;
                return manager != null && manager.IsServer;
            }
        }

        private void Update()
        {
            if (!IsServer || enemyManager == null || carHealth == null || enemyManager.Wave < 1 || carHealth.IsDead) return;
            if (nextEvent < 0f) nextEvent = Time.time + Random.Range(firstEvent.x, firstEvent.y);

            if (pending != Pending.None)
            {
                if (Time.time >= pendingAt) Fire();
                return;
            }
            // На передышке события не начинаются, таймер ждёт
            if (enemyManager.BreakActive) nextEvent = Mathf.Max(nextEvent, Time.time + 10f);
            if (Time.time < nextEvent) return;
            nextEvent = Time.time + Random.Range(eventInterval.x, eventInterval.y);

            // По кругу: опора, сброс, поезд; если для очередного нет места — следующее
            bool started = false;
            for (int i = 1; i <= 3 && !started; i++) started = Begin((Kind)(((int)lastKind + i) % 3));
            if (!started) nextEvent = Time.time + 20f;
        }

        private bool Begin(Kind kind)
        {
            bool started = kind == Kind.Pole ? StartPole() : kind == Kind.Drop ? StartDrop() : StartTrain();
            if (started) lastKind = kind;
            return started;
        }

        private bool StartPole()
        {
            PowerLines lines = PowerLines.Instance;
            RoadNetwork roads = RoadNetwork.Instance;
            if (lines == null || roads == null) return false;
            lines.StandingPoles(poles, poleIds);
            Transform car = carHealth.transform;
            // Впереди по ходу машины, у самой дороги
            Vector3 ahead = car.forward;
            ahead.y = 0f;
            picks.Clear();
            Vector3 best = Vector3.zero;
            for (int i = 0; i < poles.Count; i++)
            {
                Vector3 to = poles[i] - car.position;
                to.y = 0f;
                float distance = to.magnitude;
                if (distance < poleDistance.x || distance > poleDistance.y) continue;
                if (ahead.sqrMagnitude > 0.01f && Vector3.Dot(to / distance, ahead.normalized) < 0.3f) continue;
                if (!roads.Nearest(poles[i], poleRoadDistance, out _, out _, out _)) continue;
                picks.Add(i);
            }
            if (picks.Count == 0) return false;
            int pick = picks[Random.Range(0, picks.Count)];
            roads.Nearest(poles[pick], poleRoadDistance, out Vector3 center, out _, out _);
            pending = Pending.Pole;
            pendingAt = Time.time + warnTime;
            pendingPole = poleIds[pick];
            pendingPoint = center;
            Say($"Внимание! Рушится ЛЭП {Where(poles[pick])} — дорогу перекроет, провода под током. Объезжайте!");
            return true;
        }

        private bool StartDrop()
        {
            if (rocketCrates == null && repairKits == null) return false;
            if (!RoadPoint(carHealth.transform.position, dropDistance, out Vector3 point)) return false;
            pending = Pending.Drop;
            pendingAt = Time.time + dropDelay;
            pendingPoint = point;
            Say($"Вертушка сбросит вам ящики {Where(point)} через {Mathf.RoundToInt(dropDelay)} секунд: выстрелы к РПГ и ремкомплект.");
            return true;
        }

        private bool StartTrain()
        {
            ElectricTrain train = ElectricTrain.Instance;
            if (train == null || train.Running) return false;
            RailCrossing nearest = null;
            float best = crossingDistance;
            foreach (RailCrossing crossing in train.Crossings)
            {
                if (crossing == null) continue;
                Vector3 to = crossing.transform.position - carHealth.transform.position;
                to.y = 0f;
                if (to.magnitude < best)
                {
                    best = to.magnitude;
                    nearest = crossing;
                }
            }
            if (nearest == null) return false;
            int dir = Random.value < 0.5f ? 1 : -1;
            if (!train.Launch(dir)) return false;
            int seconds = Mathf.RoundToInt(train.TimeTo(nearest.TrackDistance, dir) / 5f) * 5;
            Say($"Электричка пройдёт через переезд {Where(nearest.transform.position)} примерно через {seconds} секунд. На путях не стойте!");
            return true;
        }

        private void Fire()
        {
            Pending kind = pending;
            pending = Pending.None;
            if (kind == Pending.Pole && PowerLines.Instance != null)
                PowerLines.Instance.ToppleToward(pendingPole, pendingPoint);
            else if (kind == Pending.Drop)
            {
                if (rocketCrates != null) rocketCrates.DropSupply(pendingPoint);
                if (repairKits != null) repairKits.Drop(pendingPoint + Vector3.right * 3f);
                Say("Груз сброшен! Забирайте, пока не растащили.");
            }
        }

        private void Say(string text)
        {
            if (radio != null) radio.SayObjective(text);
        }

        private string Where(Vector3 position)
        {
            Vector3 to = position - carHealth.transform.position;
            to.y = 0f;
            return $"в {Mathf.RoundToInt(to.magnitude / 10f) * 10} м {RadioChatter.CompassTo(to)}";
        }

        private static bool RoadPoint(Vector3 from, Vector2 range, out Vector3 result)
        {
            result = from;
            RoadNetwork roads = RoadNetwork.Instance;
            if (roads == null || roads.Roads == null) return false;
            var candidates = new List<Vector3>();
            foreach (RoadNetwork.Road road in roads.Roads)
                foreach (Vector3 p in road.points)
                {
                    Vector3 d = p - from;
                    d.y = 0f;
                    float distance = d.magnitude;
                    if (distance >= range.x && distance <= range.y) candidates.Add(p);
                }
            if (candidates.Count == 0) return false;
            result = candidates[Random.Range(0, candidates.Count)];
            return true;
        }
    }
}
