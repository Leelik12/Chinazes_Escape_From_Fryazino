using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;

namespace RacingProject.Enemy
{
    [RequireComponent(typeof(Rigidbody))]
    // Врагом управляет сервер, клиенту позицию передаёт NetworkTransform на этом же объекте.
    // Машина едет по пути NavMesh, на дорогах притянутому к их оси (RoadNetwork): руль — догонялка точки на пути
    // впереди (pure pursuit), газ и тормоз — по скорости, допустимой перед ближайшими поворотами пути и препятствиями.
    public class EnemyCarController : NetworkBehaviour
    {
        [Header("Target (Player Car)")]
        public Transform target;

        [Header("Navigator (NavMeshAgent holder)")]
        [Tooltip("Агент задаёт только тип агента NavMesh для поиска пути, сам он машину не двигает")]
        public NavMeshAgent navigatorAgent;

        [Header("Car Settings")]
        public float motorForce = 1500f;
        public float reverseForce = 800f;
        public float maxSteerAngle = 30f;
        public float brakeForce = 3000f;
        [Tooltip("Дистанция, на которой стрелок держится от игрока (0 — подъезжать вплотную)")]
        public float stoppingDistance = 5f;
        [Tooltip("Как часто пересчитывать путь до игрока, сек")]
        public float repathInterval = 0.25f;

        [Header("Driving")]
        [Tooltip("Наибольшая скорость, м/с")]
        public float maxSpeed = 30f;
        [Tooltip("Скорость в крутом (90°) повороте пути, м/с")]
        public float cornerSpeed = 9f;
        [Tooltip("Замедление, на которое рассчитывает торможение перед поворотом, м/с²")]
        public float brakingDecel = 9f;
        [Tooltip("Точка пути, за которой едет руль: lookaheadBase + lookaheadPerSpeed × скорость, м")]
        public float lookaheadBase = 6f;
        public float lookaheadPerSpeed = 0.45f;
        [Tooltip("Если машина и цель не дальше этого от дороги (область NavMesh Road), путь идёт только по дорогам, м")]
        public float roadSnapDistance = 25f;
        [Tooltip("На дороге точки пути притягиваются к оси (RoadNetwork): не дальше этого от неё, м")]
        public float laneOffset = 5f;

        private const string RoadArea = "Road";
        // Путь дробится на точки через PathStep метров на первые PathHorizon метров, дальше остаются углы NavMesh
        private const float PathStep = 4f;
        private const float PathHorizon = 400f;
        // Столько секунд после спавна и заднего хода застревание не считается: тяжёлой машине нужно время, чтобы тронуться
        private const float StuckGrace = 2f;

        [Header("Wheels")]
        public WheelCollider frontLeftWheel;
        public WheelCollider frontRightWheel;
        public WheelCollider rearLeftWheel;
        public WheelCollider rearRightWheel;

        public Transform frontLeftMesh;
        public Transform frontRightMesh;
        public Transform rearLeftMesh;
        public Transform rearRightMesh;

        [Header("Extra Wheels (middle axles)")]
        [Tooltip("Колёса средних осей у многоосных машин; ведущие и тормозят вместе с остальными")]
        public WheelCollider[] extraWheels = new WheelCollider[0];
        public Transform[] extraWheelMeshes = new Transform[0];
        [Tooltip("Крутящий момент на все колёса, а не только на передние")]
        public bool driveAllWheels;

        [Header("Ramming")]
        [Tooltip("Таран: машина не держит дистанцию, а вблизи едет прямо в упреждённую точку игрока")]
        public bool ramTarget;
        [Tooltip("С этого расстояния таран перестаёт ехать по навигации и бьёт напрямую")]
        public float ramDistance = 45f;
        [Tooltip("Упреждение по скорости игрока, сек")]
        public float ramLeadTime = 0.5f;

        [Header("Reverse Logic")]
        public float reverseDuration = 1.6f;
        [Tooltip("Медленнее этого при нажатом газе машина считается застрявшей, м/с")]
        public float stuckSpeedThreshold = 1f;
        public float stuckTimeThreshold = 1.2f;
        [Tooltip("После стольких неудачных попыток выехать подряд машину, если игрок далеко, переставляет на путь")]
        public int maxUnstuckAttempts = 4;
        [Tooltip("Ближе этого к игроку машину не переставляют и не переворачивают — он бы это увидел, м")]
        public float hiddenRecoveryDistance = 70f;

        [Header("Obstacle Check")]
        [Tooltip("Запас до препятствия за бампером на малой скорости, м")]
        public float frontCheckDistance = 4f;
        public LayerMask obstacleMask = ~0;
        [Tooltip("Предметы с Rigidbody легче этого машина не объезжает, а сбивает, кг")]
        public float pushableMass = 300f;

        [Header("Ambush")]
        [Tooltip("Засада: машина стоит, пока игрок не подъедет ближе triggerDistance или не начнёт стрелять по ней")]
        public bool waitInAmbush;
        public float ambushTriggerDistance = 90f;
        [Tooltip("Дольше этого засада не ждёт, иначе волна может не кончиться, с")]
        public float ambushMaxWait = 40f;

        private Rigidbody rb;
        private Rigidbody targetBody;
        private EnemyHealth health;
        private float wheelBase = 3f;
        private float halfLength = 2.5f;
        private float halfWidth = 1f;
        private float probeHeight = 0.8f;
        private readonly List<Collider> ownColliders = new List<Collider>();

        // Путь NavMesh и точка, куда он ведёт
        private NavMeshPath path;
        private readonly List<Vector3> corners = new List<Vector3>();
        private readonly List<Vector3> rawCorners = new List<Vector3>();
        private int agentTypeId;
        private float repathTimer;
        private Vector3 destination;
        // Стрелок держится сбоку-сзади от игрока, сторону выбирает при спавне
        private float slotSide;

        // Выезд из застревания
        private bool reversing;
        private float reverseTimer;
        private float reverseSteer;
        private float stuckTimer;
        private int unstuckAttempts;
        private float lastUnstuckTime = float.NegativeInfinity;
        private float flippedTimer;
        private float ambushTimer;
        private float progressTimer;
        private Vector3 progressAnchor;
        private const float ProgressWindow = 10f;
        private const float MinProgress = 8f;
        private bool turningAround;
        private float turnForwardTimer;
        // Длина фазы разворота: длинной машине нужно дольше
        private float TurnPhaseDuration => 0.8f + wheelBase * 0.15f;

        // Колёса у клиента: машина кинематическая, WheelCollider не крутятся,
        // поэтому вращение мешей считается по пройденному пути, а угол руля задаёт сервер
        private readonly NetworkVariable<float> networkSteerAngle = new NetworkVariable<float>();
        private float remoteWheelSpin;
        private Vector3 lastRemotePosition;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = new Vector3(0f, -0.5f, 0f);
            health = GetComponent<EnemyHealth>();

            // Агент только хранит тип агента; путь считает NavMesh.CalculatePath, иначе агент спорил бы с физикой
            agentTypeId = navigatorAgent != null ? navigatorAgent.agentTypeID : 0;
            if (navigatorAgent != null)
                navigatorAgent.enabled = false;
            path = new NavMeshPath();
            slotSide = (GetInstanceID() & 1) == 0 ? 1f : -1f;
            // Чтобы враги одной волны не считали путь в одном кадре
            repathTimer = Random.value * repathInterval;

            // Физику считает только сервер, у клиента её интерполяция спорила бы с NetworkTransform
            if (IsSpawned && !IsServer)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                rb.isKinematic = false;
            }

            if (target == null)
            {
                GameObject playerCar = GameObject.FindWithTag(Tags.Car);
                if (playerCar != null) target = playerCar.transform;
            }
            if (target != null)
                targetBody = target.GetComponentInParent<Rigidbody>();

            MeasureCar();
            progressAnchor = transform.position;
            // Машина падает на точку спавна и разгоняется с места — это не застревание
            stuckTimer = -StuckGrace;
            lastRemotePosition = transform.position;
        }

        // Габариты машины в её осях: с какого места пускать лучи на препятствия и какая колёсная база у руля
        private void MeasureCar()
        {
            GetComponentsInChildren(true, ownColliders);
            Bounds local = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Collider col in ownColliders)
            {
                if (col.isTrigger || col is WheelCollider) continue;
                // Границы в осях самого коллайдера: мировой AABB повёрнутой машины раздувает габариты
                Bounds b;
                if (col is BoxCollider box) b = new Bounds(box.center, box.size);
                else if (col is MeshCollider mesh && mesh.sharedMesh != null) b = mesh.sharedMesh.bounds;
                else continue;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = transform.InverseTransformPoint(col.transform.TransformPoint(corner));
                    if (!any) { local = new Bounds(p, Vector3.zero); any = true; }
                    else local.Encapsulate(p);
                }
            }
            Vector3 scale = transform.lossyScale;
            if (any)
            {
                halfLength = Mathf.Max(Mathf.Abs(local.min.z), Mathf.Abs(local.max.z)) * scale.z;
                halfWidth = local.extents.x * scale.x;
                probeHeight = Mathf.Clamp((local.min.y + local.extents.y * 0.6f) * scale.y, 0.4f, 2f);
            }
            if (frontLeftWheel != null && rearLeftWheel != null)
                wheelBase = Mathf.Max(1.5f, Vector3.Distance(frontLeftWheel.transform.position, rearLeftWheel.transform.position));
        }

        void FixedUpdate()
        {
            // Только сервер управляет движением
            if (!IsSpawned || IsServer)
            {
                HandleMovement();
                if (IsSpawned)
                    networkSteerAngle.Value = frontLeftWheel.steerAngle;
            }
            else
            {
                // У клиента машину двигает NetworkTransform, здесь только колёса
                AnimateRemoteWheels();
            }
        }

        private void HandleMovement()
        {
            UpdateWheelPoses();
            if (target == null)
                return;

            float dt = Time.fixedDeltaTime;
            Vector3 position = transform.position;
            float distanceToPlayer = Vector3.Distance(position, target.position);
            Vector3 targetVelocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;

            if (waitInAmbush)
            {
                ambushTimer += dt;
                bool hurt = health != null && health.HealthFraction < 0.999f;
                if (distanceToPlayer > ambushTriggerDistance && !hurt && ambushTimer < ambushMaxWait)
                {
                    Drive(0f, 0f, 1f);
                    return;
                }
                waitInAmbush = false;
            }

            if (RecoverIfFlipped(distanceToPlayer, dt))
                return;
            if (CheckProgress(position, distanceToPlayer, dt))
                return;

            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                destination = ChooseDestination(targetVelocity);
                Vector3 start = PathStart(position);
                RebuildPath(start, destination);
                if (start != position)
                {
                    // Путь от точки впереди сразу идёт назад — цель позади. Тогда строим его от самой машины,
                    // иначе она гналась бы за точкой впереди, которая при каждом пересчёте уезжает дальше
                    if (PathTurnsBack(start))
                        RebuildPath(position, destination);
                    else
                        corners.Insert(0, position);
                }
            }

            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

            if (reversing)
            {
                reverseTimer -= dt;
                bool behindBlocked = ProbeBlocked(-transform.forward, halfLength + 1.5f);
                if (reverseTimer <= 0f || behindBlocked)
                {
                    reversing = false;
                    stuckTimer = -StuckGrace;
                    // Разворот в три приёма: после заднего хода — вперёд на полной выворотке
                    if (turningAround)
                        turnForwardTimer = TurnPhaseDuration;
                    turningAround = false;
                }
                else
                {
                    Drive(reverseSteer, -1f, 0f);
                    return;
                }
            }

            // Куда рулить: точка пути впереди, вблизи у тарана — прямо в упреждённую точку игрока
            float lookahead = lookaheadBase + lookaheadPerSpeed * Mathf.Max(0f, forwardSpeed);
            Vector3 aimPoint = PointAlongPath(position, lookahead);
            bool directRam = ramTarget && distanceToPlayer < ramDistance;
            if (directRam)
                aimPoint = target.position + targetVelocity * ramLeadTime;

            Vector3 toAim = aimPoint - position;
            toAim.y = 0f;
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            float alpha = Vector3.SignedAngle(flatForward, toAim, Vector3.up) * Mathf.Deg2Rad;

            // Цель позади и скорость мала — разворот в три приёма: задним ходом с рулём в обратную сторону,
            // потом вперёд на полной выворотке; если сзади тесно — сразу вперёд (застревание ловит проверка ниже)
            // Точка прицела на малой скорости ближе 8 м, поэтому близость проверяем по цели пути
            bool turnAround = Mathf.Abs(alpha) > 110f * Mathf.Deg2Rad && forwardSpeed < 4f && (destination - position).sqrMagnitude > 64f;
            if (turnForwardTimer > 0f)
            {
                turnForwardTimer -= dt;
                turnAround = Mathf.Abs(alpha) > 50f * Mathf.Deg2Rad;
                if (!turnAround)
                    turnForwardTimer = 0f;
            }
            else if (turnAround && !ProbeBlocked(-transform.forward, halfLength + 2f))
            {
                StartReverse(alpha > 0f ? -1f : 1f, TurnPhaseDuration);
                turningAround = true;
                return;
            }

            float steerDeg = Mathf.Atan2(2f * wheelBase * Mathf.Sin(alpha), Mathf.Max(lookahead, toAim.magnitude)) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(steerDeg / maxSteerAngle, -1f, 1f);
            // Pure pursuit даёт почти нулевой руль, когда точка прямо сзади: тогда полная выворотка
            if (turnAround || Mathf.Abs(alpha) > 90f * Mathf.Deg2Rad)
                steer = alpha > 0f ? 1f : -1f;
            else if (Mathf.Abs(steer) > 0.3f && InnerSideBlocked(steer))
                steer *= 0.5f;  // внутренний угол поворота занят — задние колёса срежут его, входим шире

            // Скорость: не быстрее, чем позволяют повороты пути впереди
            float desiredSpeed = directRam ? maxSpeed : SpeedForPath(position, forwardSpeed);
            if (turnAround)
                desiredSpeed = Mathf.Min(desiredSpeed, 5f);

            // Стрелок у игрока подстраивается под его скорость, а не тормозит в ноль
            if (!ramTarget && stoppingDistance > 0f)
            {
                float playerSpeed = Vector3.Dot(targetVelocity, transform.forward);
                float slotDistance = Vector3.Distance(position, destination);
                if (distanceToPlayer < stoppingDistance * 2.5f)
                {
                    float pace = Mathf.Max(0f, playerSpeed) + Mathf.Clamp(slotDistance * 0.8f, 0f, 15f);
                    // Игрок стоит, а стрелок уже на дистанции стрельбы — держит позицию, а не лезет в занятое место
                    if (playerSpeed < 2f && (slotDistance < 4f || distanceToPlayer < stoppingDistance * 1.3f))
                        pace = 0f;
                    desiredSpeed = Mathf.Min(desiredSpeed, pace);
                }
            }

            // Препятствие впереди: подруливаем в свободную сторону и сбрасываем скорость под тормозной путь
            float probeLength = halfLength + frontCheckDistance + Mathf.Max(0f, forwardSpeed) * 0.9f;
            float hitDistance = ProbeDistance(transform.forward, probeLength);
            if (!directRam && hitDistance < probeLength)
            {
                float left = ProbeDistance(Quaternion.AngleAxis(-28f, Vector3.up) * transform.forward, probeLength);
                float right = ProbeDistance(Quaternion.AngleAxis(28f, Vector3.up) * transform.forward, probeLength);
                float urgency = 1f - hitDistance / probeLength;
                steer = Mathf.Clamp(steer + (right >= left ? 1f : -1f) * urgency * 1.2f, -1f, 1f);
                float room = Mathf.Max(0f, hitDistance - halfLength - 1.5f);
                desiredSpeed = Mathf.Min(desiredSpeed, Mathf.Sqrt(2f * brakingDecel * room) + 2f);
            }

            float throttle;
            float brake = 0f;
            float speedError = desiredSpeed - forwardSpeed;
            if (desiredSpeed <= 0.1f && forwardSpeed < 1f)
            {
                throttle = 0f;
                brake = 1f;
            }
            else if (forwardSpeed < -1f)
            {
                // Ещё катится назад после заднего хода — сначала остановиться
                throttle = 0f;
                brake = 1f;
            }
            else if (speedError >= 0f)
            {
                throttle = Mathf.Clamp01(speedError / 3f + 0.25f);
            }
            else
            {
                throttle = 0f;
                brake = Mathf.Clamp01(-speedError / 4f);
            }

            // Застревание: газ есть, а машина стоит — сдаём назад, руль в обратную сторону от нужной
            if ((throttle > 0.2f || forwardSpeed < -1f) && forwardSpeed < stuckSpeedThreshold)
                stuckTimer += dt;
            else
                stuckTimer = stuckTimer > 0f ? Mathf.Max(0f, stuckTimer - dt) : Mathf.Min(0f, stuckTimer + dt);

            if (stuckTimer >= stuckTimeThreshold)
            {
                if (Time.time - lastUnstuckTime > 15f)
                    unstuckAttempts = 0;
                unstuckAttempts++;
                lastUnstuckTime = Time.time;

                if (unstuckAttempts > maxUnstuckAttempts && distanceToPlayer > hiddenRecoveryDistance && RelocateOnPath(position))
                    return;

                StartReverse(steer >= 0f ? -1f : 1f, reverseDuration);
                return;
            }

            Drive(steer, throttle, brake);
        }

        // Стрелок едет в точку сбоку-сзади от игрока, таран и прочие — к самому игроку
        private Vector3 ChooseDestination(Vector3 targetVelocity)
        {
            Vector3 goal = target.position;
            if (ramTarget || stoppingDistance <= 0f)
                return goal;

            Vector3 back = targetVelocity.sqrMagnitude > 4f ? -targetVelocity.normalized : -target.forward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = -target.forward;
            back.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, back) * slotSide;
            Vector3 slot = goal + (back * 0.6f + side * 0.8f).normalized * stoppingDistance;

            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(slot, out NavMeshHit hit, 8f, filter))
                return hit.position;
            // Сбоку места нет — пробуем другую сторону
            slotSide = -slotSide;
            return goal;
        }

        // На ходу путь строится от точки впереди машины: так маршрут продолжается вперёд и не перескакивает
        // на равный по длине вариант, ради которого пришлось бы разворачиваться
        private Vector3 PathStart(Vector3 position)
        {
            float speed = Vector3.Dot(rb.linearVelocity, transform.forward);
            if (speed < 2f) return position;
            Vector3 ahead = transform.forward;
            ahead.y = 0f;
            Vector3 point = position + ahead.normalized * Mathf.Clamp(halfLength + speed, 6f, 25f);
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            return NavMesh.SamplePosition(point, out NavMeshHit hit, 3f, filter) ? hit.position : position;
        }

        // Уходит ли путь, построенный от точки впереди, назад: первые метров 10 пути направлены против хода машины
        private bool PathTurnsBack(Vector3 start)
        {
            Vector3 ahead = PointAlongPath(start, 10f) - start;
            ahead.y = 0f;
            return ahead.sqrMagnitude > 1f && Vector3.Dot(ahead.normalized, transform.forward) < -0.3f;
        }

        // Есть ли препятствие сбоку от задней оси с той стороны, куда поворачиваем
        private bool InnerSideBlocked(float steer)
        {
            Vector3 side = transform.right * Mathf.Sign(steer);
            Vector3 origin = transform.position - transform.forward * (wheelBase * 0.5f) + transform.up * probeHeight;
            return Physics.Raycast(origin, side, out RaycastHit hit, halfWidth + 1.2f, obstacleMask, QueryTriggerInteraction.Ignore)
                && !ownColliders.Contains(hit.collider) && hit.normal.y < 0.6f
                && (hit.rigidbody == null || hit.rigidbody.isKinematic || hit.rigidbody.mass >= pushableMass);
        }

        private void RebuildPath(Vector3 from, Vector3 to)
        {
            corners.Clear();
            // Сначала путь только по дорогам: путь по всей сетке срезает повороты через газоны с деревьями.
            // Если машина или цель далеко от дороги — по всей сетке
            int roadMask = 1 << NavMesh.GetAreaFromName(RoadArea);
            if (!TryPath(from, to, roadMask, roadSnapDistance, out Vector3 roadEnd)
                || (roadEnd - to).sqrMagnitude > roadSnapDistance * roadSnapDistance)
            {
                corners.Clear();
                TryPath(from, to, NavMesh.AllAreas, 10f, out _);
            }
            else if ((roadEnd - to).sqrMagnitude > 4f)
            {
                // Цель рядом с дорогой — последний отрезок до неё по прямой
                corners.Add(to);
            }
            // Пути нет (машина вне сетки) — едем напрямую
            if (corners.Count == 0)
            {
                corners.Add(from);
                corners.Add(to);
            }
            FollowLanes();
        }

        // Путь NavMesh идёт по внутренней кромке проходимой области, и машина цепляет обочину, фонари и углы домов.
        // Дробим начало пути на частые точки и на дорогах сдвигаем их к оси, оставляя смещение не больше полосы
        private void FollowLanes()
        {
            RoadNetwork roads = RoadNetwork.Instance;
            if (roads == null || corners.Count < 2) return;
            rawCorners.Clear();
            rawCorners.AddRange(corners);
            corners.Clear();
            corners.Add(rawCorners[0]);
            float total = 0f;
            int i = 0;
            for (; i < rawCorners.Count - 1 && total < PathHorizon; i++)
            {
                Vector3 a = rawCorners[i], b = rawCorners[i + 1];
                float length = Vector3.Distance(a, b);
                int steps = Mathf.Max(1, Mathf.CeilToInt(length / PathStep));
                bool lastSegment = i == rawCorners.Count - 2;
                for (int s = 1; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)steps);
                    // Конец пути — место у игрока, его не двигаем
                    corners.Add(lastSegment && s == steps ? p : ToLane(roads, p));
                }
                total += length;
            }
            for (i++; i < rawCorners.Count; i++)
                corners.Add(rawCorners[i]);
        }

        private Vector3 ToLane(RoadNetwork roads, Vector3 point)
        {
            if (!roads.Nearest(point, 16f, out Vector3 center, out Vector3 direction, out float roadHalfWidth))
                return point;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            float lateral = Vector3.Dot(point - center, side);
            // Точка за обочиной — путь здесь идёт не по этой дороге
            if (Mathf.Abs(lateral) > roadHalfWidth + 3f)
                return point;
            float limit = Mathf.Max(0f, Mathf.Min(laneOffset, roadHalfWidth - halfWidth - 1f));
            Vector3 result = center + side * Mathf.Clamp(lateral, -limit, limit);
            result.y = point.y;
            return result;
        }

        private bool TryPath(Vector3 from, Vector3 to, int areaMask, float snap, out Vector3 end)
        {
            end = to;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = areaMask };
            if (!NavMesh.SamplePosition(from, out NavMeshHit startHit, snap, filter)
                || !NavMesh.SamplePosition(to, out NavMeshHit endHit, snap, filter)
                || !NavMesh.CalculatePath(startHit.position, endHit.position, filter, path)
                || path.status != NavMeshPathStatus.PathComplete)
                return false;
            end = endHit.position;
            corners.AddRange(path.corners);
            return corners.Count > 0;
        }

        // Точка на пути в distance метрах дальше ближайшей к машине точки пути
        private Vector3 PointAlongPath(Vector3 position, float distance)
        {
            int segment = ClosestSegment(position, out Vector3 closest);
            float left = distance;
            Vector3 from = closest;
            for (int i = segment + 1; i < corners.Count; i++)
            {
                float len = Vector3.Distance(from, corners[i]);
                if (len >= left)
                    return Vector3.Lerp(from, corners[i], left / Mathf.Max(len, 0.001f));
                left -= len;
                from = corners[i];
            }
            return corners[corners.Count - 1];
        }

        private int ClosestSegment(Vector3 position, out Vector3 closest)
        {
            closest = corners[0];
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < corners.Count - 1; i++)
            {
                Vector3 a = corners[i], b = corners[i + 1];
                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(position - a, ab) / ab.sqrMagnitude) : 0f;
                Vector3 p = a + ab * t;
                float d = (p - position).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                    closest = p;
                }
            }
            return best;
        }

        // Скорость, с которой можно ехать, чтобы успеть сбросить её перед каждым поворотом пути в пределах тормозного пути
        private float SpeedForPath(Vector3 position, float speed)
        {
            float allowed = maxSpeed;
            int segment = ClosestSegment(position, out Vector3 closest);
            float horizon = speed * speed / (2f * brakingDecel) + 25f;

            // Машина смотрит мимо пути — сначала довернуть
            Vector3 heading = transform.forward;
            Vector3 toPath = PointAlongPath(position, 8f) - position;
            heading.y = 0f;
            toPath.y = 0f;
            if (toPath.sqrMagnitude > 1f)
                allowed = Mathf.Lerp(maxSpeed, cornerSpeed, Mathf.Clamp01(Vector3.Angle(heading, toPath) / 90f));

            // Поворот в каждой точке пути меряем по хордам на три точки назад и вперёд (около 12 м при частых точках):
            // на раздробленном пути угол между соседними отрезками мал и крутого поворота не показал бы
            float travelled = 0f;
            Vector3 from = closest;
            for (int i = segment + 1; i < corners.Count - 1 && travelled < horizon; i++)
            {
                travelled += Vector3.Distance(from, corners[i]);
                from = corners[i];
                Vector3 dirIn = corners[i] - corners[Mathf.Max(segment, i - 3)];
                Vector3 dirOut = corners[Mathf.Min(corners.Count - 1, i + 3)] - corners[i];
                dirIn.y = 0f;
                dirOut.y = 0f;
                if (dirIn.sqrMagnitude < 0.25f || dirOut.sqrMagnitude < 0.25f) continue;
                float turn = Vector3.Angle(dirIn, dirOut);
                float limit = Mathf.Lerp(maxSpeed, cornerSpeed, Mathf.Clamp01(turn / 90f));
                allowed = Mathf.Min(allowed, Mathf.Sqrt(limit * limit + 2f * brakingDecel * travelled));
            }
            return allowed;
        }

        // Расстояние до препятствия по лучу (сфера) от середины машины; земля, своя машина и лёгкие предметы не считаются
        private float ProbeDistance(Vector3 direction, float length)
        {
            Vector3 origin = transform.position + transform.up * probeHeight;
            float radius = Mathf.Max(0.3f, halfWidth * 0.6f);
            RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, length, obstacleMask, QueryTriggerInteraction.Ignore);
            float nearest = length;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance <= 0f || hit.distance >= nearest) continue;
                if (hit.normal.y > 0.6f) continue;
                if (ownColliders.Contains(hit.collider)) continue;
                Rigidbody body = hit.rigidbody;
                if (body != null && body != targetBody && !body.isKinematic && body.mass < pushableMass) continue;
                nearest = hit.distance;
            }
            return nearest;
        }

        private bool ProbeBlocked(Vector3 direction, float length)
        {
            return ProbeDistance(direction, length) < length;
        }

        private void StartReverse(float steer, float duration)
        {
            reversing = true;
            reverseTimer = duration;
            reverseSteer = steer;
            stuckTimer = 0f;
        }

        private static readonly float[] RelocateDistances = { 15f, 25f, 40f, 60f };
        private readonly Collider[] overlapBuffer = new Collider[16];

        // Крайний случай, пока игрок далеко: переставить машину вперёд по пути и повернуть по нему.
        // Ближняя точка часто упирается в то же препятствие, поэтому берём первую, где машине хватает места
        private bool RelocateOnPath(Vector3 position)
        {
            if (corners.Count < 2) return false;
            NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
            foreach (float distance in RelocateDistances)
            {
                Vector3 point = PointAlongPath(position, distance);
                Vector3 ahead = PointAlongPath(position, distance + 6f) - point;
                ahead.y = 0f;
                if (!NavMesh.SamplePosition(point, out NavMeshHit hit, 4f, filter)) continue;
                Quaternion rotation = ahead.sqrMagnitude > 0.01f ? Quaternion.LookRotation(ahead) : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                Vector3 placed = hit.position + Vector3.up * 1.5f;
                if (!HasRoom(placed, rotation)) continue;
                PlaceAt(placed, rotation);
                unstuckAttempts = 0;
                return true;
            }
            return false;
        }

        // Свободен ли объём машины в этой точке: земля, своя машина и лёгкие предметы не мешают
        private bool HasRoom(Vector3 position, Quaternion rotation)
        {
            // Коробка приподнята над землёй (position уже на 1,5 м выше NavMesh), чтобы не задевать дорогу на уклоне
            Vector3 half = new Vector3(halfWidth + 0.3f, 1f, halfLength + 0.5f);
            int count = Physics.OverlapBoxNonAlloc(position + Vector3.up * 1f, half, overlapBuffer, rotation, obstacleMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider col = overlapBuffer[i];
                if (col is TerrainCollider || ownColliders.Contains(col)) continue;
                Rigidbody body = col.attachedRigidbody;
                if (body != null && !body.isKinematic && body.mass < pushableMass) continue;
                return false;
            }
            return true;
        }

        // Страховка от любых зависаний: если за progressWindow машина сдвинулась меньше чем на minProgress,
        // а игрок далеко, её переставляет на путь, а если не вышло — на ближайшую дорогу
        private bool CheckProgress(Vector3 position, float distanceToPlayer, float dt)
        {
            progressTimer += dt;
            if (progressTimer < ProgressWindow) return false;
            bool stalled = (position - progressAnchor).sqrMagnitude < MinProgress * MinProgress;
            progressTimer = 0f;
            progressAnchor = position;
            if (!stalled || distanceToPlayer < hiddenRecoveryDistance) return false;
            if (RelocateOnPath(position)) return true;

            NavMeshQueryFilter road = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = 1 << NavMesh.GetAreaFromName(RoadArea) };
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 80f, road)) return false;
            Vector3 toTarget = target.position - hit.position;
            toTarget.y = 0f;
            PlaceAt(hit.position + Vector3.up * 1.5f, toTarget.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toTarget) : transform.rotation);
            return true;
        }

        // Машина на боку или крыше дольше 2,5 с — ставим на колёса (если игрок не смотрит в упор)
        private bool RecoverIfFlipped(float distanceToPlayer, float dt)
        {
            if (transform.up.y > 0.4f || rb.linearVelocity.sqrMagnitude > 4f)
            {
                flippedTimer = 0f;
                return false;
            }
            flippedTimer += dt;
            if (flippedTimer < 2.5f || distanceToPlayer < 20f)
            {
                Drive(0f, 0f, 0f);
                return true;
            }
            flippedTimer = 0f;
            PlaceAt(transform.position + Vector3.up * 2f, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            return true;
        }

        private void PlaceAt(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            reversing = false;
            stuckTimer = -StuckGrace;
        }

        // steer −1…1, throttle −1…1 (меньше нуля — задний ход), brake 0…1
        private void Drive(float steer, float throttle, float brake)
        {
            float angle = steer * maxSteerAngle;
            frontLeftWheel.steerAngle = angle;
            frontRightWheel.steerAngle = angle;
            SetMotorTorque(throttle >= 0f ? throttle * motorForce : throttle * reverseForce);
            ApplyBrake(brake * brakeForce);
        }

        private void SetMotorTorque(float torque)
        {
            frontLeftWheel.motorTorque = torque;
            frontRightWheel.motorTorque = torque;
            if (!driveAllWheels) return;
            rearLeftWheel.motorTorque = torque;
            rearRightWheel.motorTorque = torque;
            foreach (WheelCollider wheel in extraWheels)
                wheel.motorTorque = torque;
        }

        private void ApplyBrake(float brake)
        {
            frontLeftWheel.brakeTorque = brake;
            frontRightWheel.brakeTorque = brake;
            rearLeftWheel.brakeTorque = brake;
            rearRightWheel.brakeTorque = brake;
            foreach (WheelCollider wheel in extraWheels)
                wheel.brakeTorque = brake;
        }

        private void UpdateWheelPoses()
        {
            UpdateWheelPose(frontLeftWheel, frontLeftMesh);
            UpdateWheelPose(frontRightWheel, frontRightMesh);
            UpdateWheelPose(rearLeftWheel, rearLeftMesh);
            UpdateWheelPose(rearRightWheel, rearRightMesh);
            for (int i = 0; i < extraWheels.Length && i < extraWheelMeshes.Length; i++)
                UpdateWheelPose(extraWheels[i], extraWheelMeshes[i]);
        }

        private void UpdateWheelPose(WheelCollider col, Transform mesh)
        {
            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.position = pos;
            mesh.rotation = rot;
        }

        private void AnimateRemoteWheels()
        {
            float distance = Vector3.Dot(transform.position - lastRemotePosition, transform.forward);
            lastRemotePosition = transform.position;

            float radius = rearLeftWheel != null ? rearLeftWheel.radius : 0.35f;
            remoteWheelSpin = Mathf.Repeat(remoteWheelSpin + distance / radius * Mathf.Rad2Deg, 360f);

            SetRemoteWheelRotation(frontLeftWheel, frontLeftMesh, networkSteerAngle.Value);
            SetRemoteWheelRotation(frontRightWheel, frontRightMesh, networkSteerAngle.Value);
            SetRemoteWheelRotation(rearLeftWheel, rearLeftMesh, 0f);
            SetRemoteWheelRotation(rearRightWheel, rearRightMesh, 0f);
            for (int i = 0; i < extraWheels.Length && i < extraWheelMeshes.Length; i++)
                SetRemoteWheelRotation(extraWheels[i], extraWheelMeshes[i], 0f);
        }

        private void SetRemoteWheelRotation(WheelCollider col, Transform mesh, float steer)
        {
            if (col == null || mesh == null) return;
            mesh.rotation = col.transform.rotation * Quaternion.Euler(remoteWheelSpin, steer, 0f);
        }
    }
}
