using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingProject.Management
{
    // Экран военного терминала: бегущие строки развёртки, лёгкое мерцание люминофора, часы в шапке,
    // мигающий курсор и «печать» новых сообщений по буквам
    public class TerminalScreen : MonoBehaviour
    {
        [Tooltip("Полупрозрачные строки развёртки (RawImage с повторяющейся текстурой)")]
        [SerializeField] private RawImage scanlines;
        [Tooltip("Скорость сползания строк развёртки, доли текстуры в секунду")]
        [SerializeField] private float scanlineSpeed = 0.6f;
        [Tooltip("Всё содержимое экрана: его прозрачность слегка дрожит")]
        [SerializeField] private CanvasGroup content;
        [SerializeField, Range(0f, 0.2f)] private float flicker = 0.04f;
        [Tooltip("Часы в шапке (необязательно)")]
        [SerializeField] private TMP_Text clock;
        [Tooltip("Мигающий курсор (необязательно)")]
        [SerializeField] private Graphic cursor;
        [Tooltip("Тексты, которые при смене «печатаются» по буквам")]
        [SerializeField] private TMP_Text[] typed = new TMP_Text[0];
        [Tooltip("Букв в секунду при печати")]
        [SerializeField] private float typeSpeed = 90f;

        private string[] lastText;
        private float[] typeStart;
        private float seed;

        private void Awake()
        {
            seed = UnityEngine.Random.value * 100f;
            lastText = new string[typed.Length];
            typeStart = new float[typed.Length];
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            if (scanlines != null)
            {
                Rect uv = scanlines.uvRect;
                uv.y = Mathf.Repeat(-t * scanlineSpeed, 1f);
                scanlines.uvRect = uv;
            }
            if (content != null)
                content.alpha = 1f - flicker * Mathf.PerlinNoise(seed, t * 9f);
            if (clock != null)
                clock.text = DateTime.Now.ToString("HH:mm:ss");
            if (cursor != null)
                cursor.enabled = Mathf.Repeat(t, 1f) < 0.55f;

            for (int i = 0; i < typed.Length; i++)
            {
                TMP_Text text = typed[i];
                if (text == null) continue;
                if (text.text != lastText[i])
                {
                    lastText[i] = text.text;
                    typeStart[i] = t;
                }
                int visible = Mathf.FloorToInt((t - typeStart[i]) * typeSpeed);
                text.maxVisibleCharacters = Mathf.Max(0, visible);
            }
        }
    }
}
