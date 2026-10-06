using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RacingProject.EditorTools
{
    // Процедурные звуки игры: синтезируются здесь и сохраняются в WAV, чтобы их можно было
    // перегенерировать после правки параметров
    public static class ProceduralSounds
    {
        private const string OutputFolder = "Assets/Content/Sound/Generated";
        private const int SampleRate = 44100;

        [MenuItem("RacingProject/Сгенерировать звуки")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(OutputFolder);
            WriteWav(Path.Combine(OutputFolder, "Explosion.wav"), SynthesizeExplosion(new System.Random(17)));
            WriteWav(Path.Combine(OutputFolder, "HitMarker.wav"), SynthesizeHitMarker());
            WriteWav(Path.Combine(OutputFolder, "EnemyShot.wav"), SynthesizeShot(new System.Random(23)));
            WriteWav(Path.Combine(OutputFolder, "EnemyEngine.wav"), SynthesizeEngine(new System.Random(31)));
            WriteWav(Path.Combine(OutputFolder, "Nitro.wav"), SynthesizeNitro(new System.Random(41)));
            WriteWav(Path.Combine(OutputFolder, "Pickup.wav"), SynthesizePickup());
            AssetDatabase.Refresh();
            Debug.Log("Звуки сгенерированы в " + OutputFolder);
        }

        // Взрыв: короткий треск, удар низкой частоты и затухающий гул; шум фильтруется
        // всё ниже по мере затухания, поэтому хлопок переходит в раскат
        private static float[] SynthesizeExplosion(System.Random random)
        {
            float duration = 3f;
            int length = (int)(duration * SampleRate);
            var samples = new float[length];

            float body = 0f, rumble = 0f, rumble2 = 0f, phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                // Треск в первые миллисекунды
                float crack = noise * Mathf.Exp(-t / 0.012f);

                // Основной хлопок: срез фильтра опускается с ~4 кГц до ~150 Гц
                float cutoff = Mathf.Lerp(150f, 4000f, Mathf.Exp(-t / 0.25f));
                body += OnePoleCoefficient(cutoff) * (noise - body);
                float bodyEnvelope = (1f - Mathf.Exp(-t / 0.004f)) * Mathf.Exp(-t / 0.45f);

                // Удар: синус с падающей частотой 60 → 30 Гц
                float frequency = Mathf.Lerp(30f, 60f, Mathf.Exp(-t / 0.3f));
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t / 0.35f);

                // Раскат: двойной фильтр низких частот, медленное затухание
                rumble += OnePoleCoefficient(120f) * (noise - rumble);
                rumble2 += OnePoleCoefficient(120f) * (rumble - rumble2);
                float rumbleEnvelope = (1f - Mathf.Exp(-t / 0.08f)) * Mathf.Exp(-t / 1.1f);

                samples[i] = crack * 0.5f + body * bodyEnvelope * 2.2f + thump * 0.9f + rumble2 * rumbleEnvelope * 9f;
            }

            FadeOut(samples, 0.3f);
            Normalize(samples, 0.9f, softClip: true);
            return samples;
        }

        // Отклик попадания: короткий металлический «тик» из двух тонов
        private static float[] SynthesizeHitMarker()
        {
            float duration = 0.09f;
            int length = (int)(duration * SampleRate);
            var samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float envelope = (1f - Mathf.Exp(-t / 0.0005f)) * Mathf.Exp(-t / 0.018f);
                float tone = Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 3100f * t) * 0.4f;
                samples[i] = tone * envelope;
            }
            Normalize(samples, 0.7f, softClip: false);
            return samples;
        }

        // Выстрел пулемёта: щелчок, короткий шумовой хлопок и низкий удар
        private static float[] SynthesizeShot(System.Random random)
        {
            float duration = 0.35f;
            int length = (int)(duration * SampleRate);
            var samples = new float[length];

            float body = 0f, phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                float crack = noise * Mathf.Exp(-t / 0.003f);
                body += OnePoleCoefficient(Mathf.Lerp(400f, 3000f, Mathf.Exp(-t / 0.03f))) * (noise - body);
                float bodyEnvelope = (1f - Mathf.Exp(-t / 0.001f)) * Mathf.Exp(-t / 0.05f);

                float frequency = Mathf.Lerp(60f, 130f, Mathf.Exp(-t / 0.03f));
                phase += 2f * Mathf.PI * frequency / SampleRate;
                float thump = Mathf.Sin(phase) * Mathf.Exp(-t / 0.06f);

                samples[i] = crack * 0.6f + body * bodyEnvelope * 2.5f + thump * 0.8f;
            }

            FadeOut(samples, 0.1f);
            Normalize(samples, 0.9f, softClip: true);
            return samples;
        }

        // Двигатель врага, бесшовная петля: вспышки в цилиндрах с частотой 40 Гц (ровно 40 периодов
        // за секунду петли) и немного шума выхлопа. Высоту тона по скорости меняет EnemyEngineSound
        private static float[] SynthesizeEngine(System.Random random)
        {
            float duration = 1f;
            float firing = 40f;
            float crossfade = 0.1f;
            int length = (int)((duration + crossfade) * SampleRate);
            var samples = new float[length];

            float exhaust = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                // Каждый такт — затухающий импульс; гармоники дают «рычание»
                float cyclePhase = (t * firing) % 1f;
                float pulse = Mathf.Exp(-cyclePhase / 0.18f);
                float tone = 0f;
                for (int k = 1; k <= 6; k++)
                    tone += Mathf.Sin(2f * Mathf.PI * firing * k * t) / k;

                exhaust += OnePoleCoefficient(300f) * (noise - exhaust);
                samples[i] = tone * (0.4f + pulse) * 0.5f + exhaust * pulse * 3f;
            }

            samples = MakeLoop(samples, (int)(crossfade * SampleRate));
            Normalize(samples, 0.8f, softClip: true);
            return samples;
        }

        // Нитро, бесшовная петля: шипение струи и низкий гул
        private static float[] SynthesizeNitro(System.Random random)
        {
            float duration = 2f;
            float crossfade = 0.2f;
            int length = (int)((duration + crossfade) * SampleRate);
            var samples = new float[length];

            float low = 0f, high = 0f, roar = 0f;
            for (int i = 0; i < length; i++)
            {
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);

                // Полоса ~800–4000 Гц: разность двух фильтров низких частот
                low += OnePoleCoefficient(800f) * (noise - low);
                high += OnePoleCoefficient(4000f) * (noise - high);
                roar += OnePoleCoefficient(90f) * (noise - roar);

                samples[i] = (high - low) * 1.2f + roar * 6f;
            }

            samples = MakeLoop(samples, (int)(crossfade * SampleRate));
            Normalize(samples, 0.7f, softClip: true);
            return samples;
        }

        // Подбор ремкомплекта: три восходящие ноты
        private static float[] SynthesizePickup()
        {
            float[] notes = { 660f, 880f, 1320f };
            float step = 0.08f;
            float duration = step * notes.Length + 0.3f;
            int length = (int)(duration * SampleRate);
            var samples = new float[length];

            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * SampleRate);
                for (int i = start; i < length; i++)
                {
                    float t = (float)(i - start) / SampleRate;
                    float envelope = (1f - Mathf.Exp(-t / 0.002f)) * Mathf.Exp(-t / 0.12f);
                    samples[i] += (Mathf.Sin(2f * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * notes[n] * t)) * envelope;
                }
            }

            Normalize(samples, 0.7f, softClip: false);
            return samples;
        }

        // Петля без щелчка на стыке: хвост длиной crossfade плавно перетекает в начало
        private static float[] MakeLoop(float[] samples, int crossfade)
        {
            int length = samples.Length - crossfade;
            var loop = new float[length];
            Array.Copy(samples, loop, length);
            for (int i = 0; i < crossfade; i++)
            {
                float w = (float)i / crossfade;
                loop[i] = samples[length + i] * (1f - w) + samples[i] * w;
            }
            return loop;
        }

        private static float OnePoleCoefficient(float cutoff)
        {
            return 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / SampleRate);
        }

        private static void FadeOut(float[] samples, float seconds)
        {
            int fade = Mathf.Min(samples.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < fade; i++)
                samples[samples.Length - 1 - i] *= (float)i / fade;
        }

        private static void Normalize(float[] samples, float peak, bool softClip)
        {
            float max = 0f;
            foreach (float s in samples)
                max = Mathf.Max(max, Mathf.Abs(s));
            if (max <= 0f) return;

            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i] / max;
                // Мягкое ограничение делает хлопок плотнее, не давая цифровых перегрузок
                samples[i] = softClip ? (float)Math.Tanh(s * 1.8f) / (float)Math.Tanh(1.8f) * peak : s * peak;
            }
        }

        // 16-битный моно WAV
        private static void WriteWav(string path, float[] samples)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                int dataSize = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataSize);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataSize);
                foreach (float s in samples)
                    writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(s, -1f, 1f) * short.MaxValue));
            }
        }
    }
}
