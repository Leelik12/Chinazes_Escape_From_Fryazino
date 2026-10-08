using System;
using UnityEngine;

namespace RacingProject.Management
{
    // Звуки бункера меню, синтезированные при первом обращении: гул вентиляции, гудение ламп, эфир радиостанции
    // с морзянкой, щелчки кнопок терминала и тумблеров, далёкий разрыв, а для выхода из бункера — ревун тревоги,
    // треск штурвала, лязг кремальер и скрип тяжёлой двери. Циклы склеиваются без щелчка:
    // частоты тонов кратны длине цикла, шум в конце плавно переходит в начало. Сторонних файлов и лицензий нет
    public static class BunkerSounds
    {
        private const int Rate = 44100;

        private static AudioClip vent, buzz, radio, hover, click, toggle, rumble, klaxon, ratchet, clank, creak;

        public static AudioClip Vent => vent != null ? vent : vent = Make("BunkerVent", 8f, true, VentSample);
        public static AudioClip LampBuzz => buzz != null ? buzz : buzz = Make("BunkerLampBuzz", 2f, true, BuzzSample);
        public static AudioClip Radio => radio != null ? radio : radio = Make("BunkerRadio", 12f, true, RadioSample);
        public static AudioClip Hover => hover != null ? hover : hover = Make("TerminalHover", 0.05f, false, HoverSample);
        public static AudioClip Click => click != null ? click : click = Make("TerminalClick", 0.12f, false, ClickSample);
        public static AudioClip Toggle => toggle != null ? toggle : toggle = Make("ToggleSnap", 0.1f, false, ToggleSample);
        public static AudioClip Rumble => rumble != null ? rumble : rumble = Make("DistantShelling", 5f, false, RumbleSample);
        // Ревун периодичен сам по себе (целое число периодов, гудок начинается и кончается в тишине), склейка не нужна
        public static AudioClip Klaxon => klaxon != null ? klaxon : klaxon = Make("AlarmKlaxon", 1f, false, KlaxonSample);
        public static AudioClip Ratchet => ratchet != null ? ratchet : ratchet = Make("WheelRatchet", 1.3f, false, RatchetSample);
        public static AudioClip Clank => clank != null ? clank : clank = Make("DogClank", 0.5f, false, ClankSample);
        public static AudioClip Creak => creak != null ? creak : creak = Make("DoorCreak", 2.4f, false, CreakSample);

        // Генератор заполняет буфер целиком: ему нужны и шум, и состояние фильтров
        private delegate void Fill(float[] data, float length, System.Random rnd);

        private static AudioClip Make(string name, float length, bool loop, Fill fill)
        {
            var data = new float[Mathf.RoundToInt(length * Rate)];
            fill(data, length, new System.Random(name.GetHashCode()));
            // Переход 0,2 с: в нём целое число периодов всех тонов, поэтому они не гасят друг друга
            if (loop) data = LoopCrossfade(data, Rate / 5);
            Normalize(data, 0.9f);
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void VentSample(float[] d, float length, System.Random rnd)
        {
            // Низкий гул мотора (50 Гц и гармоники) и шум воздуха в коробе
            float low = 0f, air = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = Noise(rnd);
                low += (noise - low) * 0.004f;
                air += (noise - air) * 0.05f;
                float wobble = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / length * 3f);
                d[i] = 0.35f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * 100f * t)
                     + 0.06f * Mathf.Sin(2f * Mathf.PI * 150f * t) + 2.5f * low * wobble + 0.25f * air;
            }
        }

        private static void BuzzSample(float[] d, float length, System.Random rnd)
        {
            // Дроссель лампы: 100 Гц с богатыми гармониками и лёгким дребезгом
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate, s = 0f;
                for (int h = 1; h <= 8; h++)
                    s += Mathf.Sin(2f * Mathf.PI * 100f * h * t) / (h * h * 0.6f + 0.4f);
                float rattle = 1f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / length * 5f);
                d[i] = s * rattle + Noise(rnd) * 0.05f;
            }
        }

        private static void RadioSample(float[] d, float length, System.Random rnd)
        {
            // Эфир: полосовой шум с замираниями, сверху — обрывок морзянки «СОС» на 750 Гц
            float lp = 0f, hp = 0f, prev = 0f;
            const string morse = "... --- ...";
            const float dot = 0.08f, start = 3f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise(rnd);
                lp += (n - lp) * 0.35f;
                hp = 0.97f * (hp + lp - prev);
                prev = lp;
                float fade = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / length * 2f) * Mathf.Sin(2f * Mathf.PI * t / length * 7f);
                float crackle = rnd.NextDouble() < 0.0004 ? (float)(rnd.NextDouble() * 2 - 1) * 3f : 0f;
                d[i] = hp * 0.6f * fade + crackle;
                if (MorseOn(morse, t - start, dot))
                    d[i] += 0.35f * Mathf.Sin(2f * Mathf.PI * 750f * t);
            }
        }

        private static void KlaxonSample(float[] d, float length, System.Random rnd)
        {
            // Гудок 0,6 с и пауза: «пила» 220 Гц с хрипом 30 Гц
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.6f - t) / 0.03f);
                float saw = 2f * Mathf.Repeat(220f * t, 1f) - 1f;
                float rasp = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 30f * t);
                d[i] = Mathf.Max(0f, env) * (saw * 0.7f + Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.3f) * rasp;
            }
        }

        private static void RatchetSample(float[] d, float length, System.Random rnd)
        {
            // Штурвал: частые щелчки собачки, сначала туго и редко, потом быстрее
            float next = 0.05f;
            float lastClick = -1f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                if (t >= next)
                {
                    lastClick = t;
                    next = t + Mathf.Lerp(0.11f, 0.05f, t / length);
                }
                float k = lastClick >= 0f ? t - lastClick : 1f;
                d[i] = Noise(rnd) * Mathf.Exp(-k * 700f) + Mathf.Sin(2f * Mathf.PI * 1900f * k) * Mathf.Exp(-k * 160f) * 0.5f
                     + Mathf.Sin(2f * Mathf.PI * 240f * k) * Mathf.Exp(-k * 50f) * 0.3f;
            }
        }

        private static void ClankSample(float[] d, float length, System.Random rnd)
        {
            // Удар стального рычага: неравные обертоны звенят и быстро глохнут
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] = Noise(rnd) * Mathf.Exp(-t * 300f) * 0.8f
                     + Mathf.Sin(2f * Mathf.PI * 185f * t) * Mathf.Exp(-t * 14f) * 0.6f
                     + Mathf.Sin(2f * Mathf.PI * 412f * t) * Mathf.Exp(-t * 18f) * 0.4f
                     + Mathf.Sin(2f * Mathf.PI * 697f * t) * Mathf.Exp(-t * 25f) * 0.3f
                     + Mathf.Sin(2f * Mathf.PI * 1310f * t) * Mathf.Exp(-t * 40f) * 0.2f;
            }
        }

        private static void CreakSample(float[] d, float length, System.Random rnd)
        {
            // Скрип петель тяжёлой двери: «пила» с плавающей высотой через резонанс и шорох трения
            float phase = 0f, lp = 0f, bp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float f = 85f + 30f * Mathf.Sin(t * 2.3f) + 12f * Mathf.Sin(t * 7.1f);
                phase = Mathf.Repeat(phase + f / Rate, 1f);
                float saw = 2f * phase - 1f;
                // Полосовой фильтр на 900 Гц даёт металлический призвук
                lp += 0.13f * bp;
                bp += 0.13f * (saw - lp - 0.25f * bp);
                float env = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((length - t) / 0.6f);
                d[i] = env * (bp * 0.8f + saw * 0.15f + Noise(rnd) * 0.12f);
            }
        }

        private static bool MorseOn(string code, float t, float dot)
        {
            if (t < 0f) return false;
            float pos = 0f;
            foreach (char c in code)
            {
                float len = c == '.' ? dot : c == '-' ? dot * 3f : dot * 2f;
                if (t < pos + len) return c != ' ';
                pos += len + dot;
            }
            return false;
        }

        private static void HoverSample(float[] d, float length, System.Random rnd)
        {
            // Тихий тик реле
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] = Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 180f) * 0.6f + Noise(rnd) * Mathf.Exp(-t * 900f) * 0.4f;
            }
        }

        private static void ClickSample(float[] d, float length, System.Random rnd)
        {
            // Клавиша терминала: два щелчка контактов и короткий звон корпуса
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate, t2 = t - 0.028f;
                float s = Noise(rnd) * Mathf.Exp(-t * 600f) + Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 70f) * 0.5f;
                if (t2 > 0f) s += Noise(rnd) * Mathf.Exp(-t2 * 700f) * 0.7f + Mathf.Sin(2f * Mathf.PI * 900f * t2) * Mathf.Exp(-t2 * 90f) * 0.3f;
                d[i] = s;
            }
        }

        private static void ToggleSample(float[] d, float length, System.Random rnd)
        {
            // Тумблер: резкий щелчок пружины, металлический призвук и глухой стук о панель
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                d[i] = Noise(rnd) * Mathf.Exp(-t * 500f) + Mathf.Sin(2f * Mathf.PI * 3400f * t) * Mathf.Exp(-t * 120f) * 0.5f
                     + Mathf.Sin(2f * Mathf.PI * 160f * t) * Mathf.Exp(-t * 60f) * 0.6f;
            }
        }

        private static void RumbleSample(float[] d, float length, System.Random rnd)
        {
            // Далёкий разрыв через грунт: глухой удар, раскат и осыпающийся мелкий треск
            float a = 0f, b = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = (float)i / Rate;
                float n = Noise(rnd);
                a += (n - a) * 0.003f;
                b += (a - b) * 0.01f;
                float env = Mathf.Clamp01(t / 0.08f) * Mathf.Exp(-t * 0.9f);
                float thump = Mathf.Sin(2f * Mathf.PI * 38f * t) * Mathf.Exp(-t * 3f);
                float debris = t > 0.4f && rnd.NextDouble() < 0.002 * Math.Exp(-t) ? Noise(rnd) * 0.4f : 0f;
                d[i] = (b * 40f + a * 6f) * env + thump * 0.8f + debris;
            }
        }

        private static float Noise(System.Random rnd) => (float)(rnd.NextDouble() * 2.0 - 1.0);

        // Хвост длиной fade накладывается на начало: цикл становится короче на fade и склеивается без щелчка,
        // потому что первый отсчёт продолжает последний
        private static float[] LoopCrossfade(float[] d, int fade)
        {
            fade = Mathf.Min(fade, d.Length / 2);
            int n = d.Length - fade;
            var result = new float[n];
            Array.Copy(d, result, n);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                result[i] = d[i] * k + d[n + i] * (1f - k);
            }
            return result;
        }

        private static void Normalize(float[] d, float peak)
        {
            float max = 0f;
            foreach (float s in d) max = Mathf.Max(max, Mathf.Abs(s));
            if (max < 1e-6f) return;
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }
    }
}
