using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RacingProject.Audio;

namespace RacingProject.Hud
{
    // Карточка: подсветка под курсором или лучом, щелчок — выбор, выбранная вспыхивает, остальные гаснут
    public class WaveHudCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private WaveHud hud;
        private int index;
        private Image image;
        private Outline outline;
        private CanvasGroup group;
        private float x;
        private bool hover;
        private float glow;

        public bool Picked { get; set; }

        public void Setup(WaveHud owner, int cardIndex, Image background, Outline border, float column)
        {
            hud = owner;
            index = cardIndex;
            image = background;
            outline = border;
            group = GetComponent<CanvasGroup>();
            x = column;
            group.alpha = 0f;
        }

        public void Animate(float appear, bool locked, float now)
        {
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(x, Mathf.Lerp(-60f, 0f, appear));
            bool lit = (hover && !locked) || Picked;
            glow = Mathf.MoveTowards(glow, lit ? 1f : 0f, Time.deltaTime * 8f);
            float alpha = appear * (locked && !Picked ? 0.45f : 1f);
            group.alpha = alpha;
            image.color = Color.Lerp(WaveHud.Panel, WaveHud.PanelHover, glow);
            // Выбранная рамка мигает, как курсор терминала
            float blink = Picked ? 0.6f + 0.4f * Mathf.Sign(Mathf.Sin(now * 12f)) : 1f;
            outline.effectColor = Color.Lerp(WaveHud.PhosphorDim, WaveHud.Phosphor, glow) * new Color(1f, 1f, 1f, blink);
            transform.localScale = Vector3.one * (1f + 0.04f * glow);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hover = true;
            if (!hud.ChoiceSent) SoundLibrary.Play2D(SoundLibrary.Sfx.GunCooled, 0.25f, 1.6f);
        }

        public void OnPointerExit(PointerEventData eventData) => hover = false;

        public void OnPointerClick(PointerEventData eventData) => hud.Choose(index);
    }
}
