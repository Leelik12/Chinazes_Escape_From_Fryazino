using UnityEngine;
using UnityEngine.UI;
using RacingProject.Turret;

namespace RacingProject.Hud
{
    // Перекрестие одиночной игры: точка, кольцо и четыре штриха. Чем горячее пулемёт, тем шире расходятся
    // штрихи и тем краснее прицел; при перегреве он мигает
    public class SoloCrosshair : MonoBehaviour
    {
        [SerializeField] private VRGun gun;
        [Tooltip("Штрихи: вверх, вниз, влево, вправо")]
        [SerializeField] private RectTransform[] ticks = new RectTransform[4];
        [SerializeField] private Graphic[] tinted = new Graphic[0];
        [Tooltip("Расстояние от центра до штрихов холодного и раскалённого пулемёта, пиксели")]
        [SerializeField] private float coldGap = 14f;
        [SerializeField] private float hotGap = 24f;
        [SerializeField] private Color cold = new Color(0.92f, 1f, 0.88f, 0.95f);
        [SerializeField] private Color hot = new Color(1f, 0.45f, 0.15f, 1f);

        private static readonly Vector2[] Directions = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        private float shownHeat = -1f;

        private void Update()
        {
            float heat = gun != null ? gun.Heat01 : 0f;
            bool overheated = gun != null && gun.IsOverheated;
            if (Mathf.Abs(heat - shownHeat) < 0.005f && !overheated) return;
            shownHeat = heat;

            float gap = Mathf.Lerp(coldGap, hotGap, heat);
            for (int i = 0; i < ticks.Length && i < Directions.Length; i++)
                if (ticks[i] != null)
                    ticks[i].anchoredPosition = Directions[i] * gap;

            Color color = Color.Lerp(cold, hot, heat);
            if (overheated)
                color.a *= 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
            foreach (Graphic graphic in tinted)
                if (graphic != null)
                    graphic.color = color;
        }
    }
}
