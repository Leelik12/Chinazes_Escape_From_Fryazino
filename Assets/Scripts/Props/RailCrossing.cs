using UnityEngine;

namespace RacingProject.Props
{
    // Железнодорожный переезд: шлагбаумы и светофоры по обе стороны пути. Закрывает его поезд (ElectricTrain) —
    // у каждого игрока сам, по положению поезда: красные фонари мигают попеременно, звенит звонок, стрелы
    // опускаются за armTime. Стрелы — только картинка, коллайдеров у них нет: машина проскочит, если успеет
    public class RailCrossing : MonoBehaviour
    {
        [Tooltip("Где переезд на линии, м от её начала")]
        [SerializeField] private float trackDistance;
        [Tooltip("Стрелы шлагбаумов в опущенном положении; подняты — повёрнуты на liftAngle вокруг своей оси Z")]
        [SerializeField] private Transform[] arms = new Transform[0];
        [SerializeField] private float liftAngle = -84f;
        [SerializeField] private float armTime = 3f;
        [Tooltip("Фонари светофоров: чётные мигают вместе, нечётные — в другую фазу")]
        [SerializeField] private Renderer[] lamps = new Renderer[0];
        [SerializeField] private Light[] lights = new Light[0];
        [SerializeField] private Material lampOn;
        [SerializeField] private Material lampOff;
        [SerializeField] private float blinkRate = 1.4f;
        [SerializeField] private AudioSource bell;

        public float TrackDistance => trackDistance;
        public bool Closed => closed;

        private bool closed;
        private float lift = 1f;
        private Quaternion[] down;
        private int phase = -1;

        private void Awake()
        {
            down = new Quaternion[arms.Length];
            for (int i = 0; i < arms.Length; i++)
                if (arms[i] != null) down[i] = arms[i].localRotation;
            Apply();
            SetLamps(-1);
        }

        public void SetClosed(bool value)
        {
            if (closed == value) return;
            closed = value;
            if (bell != null)
            {
                if (closed) bell.Play();
                else bell.Stop();
            }
            if (!closed) SetLamps(-1);
        }

        private void Update()
        {
            float target = closed ? 0f : 1f;
            if (!Mathf.Approximately(lift, target))
            {
                lift = Mathf.MoveTowards(lift, target, Time.deltaTime / armTime);
                Apply();
            }
            if (closed) SetLamps((int)(Time.time * blinkRate * 2f) & 1);
        }

        private void Apply()
        {
            for (int i = 0; i < arms.Length; i++)
                if (arms[i] != null) arms[i].localRotation = down[i] * Quaternion.Euler(0f, 0f, liftAngle * lift);
        }

        // Какая пара фонарей горит: 0 — чётные, 1 — нечётные, -1 — все погашены
        private void SetLamps(int lit)
        {
            if (lit == phase) return;
            phase = lit;
            for (int i = 0; i < lamps.Length; i++)
            {
                bool on = lit >= 0 && (i & 1) == lit;
                if (lamps[i] != null) lamps[i].sharedMaterial = on ? lampOn : lampOff;
                if (i < lights.Length && lights[i] != null) lights[i].enabled = on;
            }
        }
    }
}
