using UnityEngine;

namespace RacingProject.Props
{
    // Передаёт удар по коллайдеру опоры ЛЭП в PowerLines. Ставится на объекты с коллайдерами опор при запуске
    public class PowerPoleContact : MonoBehaviour
    {
        public PowerLines owner;
        public int line;
        public int pole;

        private void OnCollisionEnter(Collision collision)
        {
            if (owner != null)
                owner.OnPoleHit(line, pole, collision);
        }
    }
}
