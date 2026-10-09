using LogitechG29.Sample.Input;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using RacingProject.Network;

namespace RacingProject.Car
{
    // Машина игроков перевернулась или застряла (газ нажат, а она стоит, например уткнулась носом в овраг):
    // через несколько секунд водитель может поставить её на колёса — удерживать R (на руле G29 — Options).
    // Машина ставится на ближайшую дорогу (или другое проходимое место NavMesh) носом туда же, куда смотрела.
    // Считает тот, у кого физика машины и управление (водитель-хост или одиночная игра); подсказку показывают экраны
    public class CarRecovery : MonoBehaviour
    {
        [SerializeField] private InputControllerReader input;
        [Tooltip("Через сколько секунд на боку или крыше можно перевернуть машину")]
        [SerializeField] private float flippedDelay = 2f;
        [Tooltip("Через сколько секунд с нажатым газом и без движения машина считается застрявшей")]
        [SerializeField] private float stuckDelay = 4f;
        [Tooltip("Медленнее этого (км/ч) с нажатым газом — машина не едет")]
        [SerializeField] private float stuckSpeedKmh = 4f;
        [Tooltip("Сколько секунд удерживать кнопку")]
        [SerializeField] private float holdTime = 1.2f;
        [Tooltip("Где искать дорогу и проходимое место, м")]
        [SerializeField] private float searchRadius = 60f;
        [Tooltip("Насколько выше точки NavMesh ставится начало машины, м")]
        [SerializeField] private float dropHeight = 2f;

        private const string RoadArea = "Road";
        private const string CarAgentName = "Car";

        private Rigidbody body;
        private float flippedTime;
        private float stuckTime;
        private float hold;

        // Можно ставить машину на колёса; доля удержания кнопки 0..1 — для подсказки на экранах
        public bool Available { get; private set; }
        public float HoldProgress => holdTime > 0f ? Mathf.Clamp01(hold / holdTime) : 0f;
        public bool Flipped => transform.up.y < 0.5f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (!LocalPlayerRole.Controls(PlayerRole.Driver) || !LocalPlayerRole.SimulatesPhysics || body == null)
            {
                Available = false;
                hold = 0f;
                return;
            }

            float dt = Time.deltaTime;
            flippedTime = Flipped ? flippedTime + dt : 0f;
            bool pushing = input != null && (input.Throttle > 0.3f || input.Brake > 0.3f);
            // Отпущенный газ (чтобы нажать R) застревание не сбрасывает — только движение
            if (body.linearVelocity.magnitude * 3.6f >= stuckSpeedKmh) stuckTime = 0f;
            else if (pushing) stuckTime += dt;
            Available = flippedTime > flippedDelay || stuckTime > stuckDelay;

            Keyboard keyboard = Keyboard.current;
            bool holding = (keyboard != null && keyboard.rKey.isPressed) || (input != null && input.Options);
            hold = Available && holding ? hold + dt : 0f;
            if (hold >= holdTime)
                Recover();
        }

        private void Recover()
        {
            hold = 0f;
            flippedTime = 0f;
            stuckTime = 0f;
            Available = false;

            Vector3 position = transform.position;
            int agent = CarAgentType();
            NavMeshQueryFilter road = new NavMeshQueryFilter { agentTypeID = agent, areaMask = 1 << NavMesh.GetAreaFromName(RoadArea) };
            NavMeshQueryFilter any = new NavMeshQueryFilter { agentTypeID = agent, areaMask = NavMesh.AllAreas };
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, searchRadius, road)
                || NavMesh.SamplePosition(position, out hit, searchRadius, any))
                position = hit.position;
            position += Vector3.up * dropHeight;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            Quaternion rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward : Vector3.forward);

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        // NavMesh для машин запечён под тип агента «Car» (по нему ездят враги); без него — тип по умолчанию
        private static int CarAgentType()
        {
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                int id = NavMesh.GetSettingsByIndex(i).agentTypeID;
                if (NavMesh.GetSettingsNameFromID(id) == CarAgentName)
                    return id;
            }
            return 0;
        }
    }
}
