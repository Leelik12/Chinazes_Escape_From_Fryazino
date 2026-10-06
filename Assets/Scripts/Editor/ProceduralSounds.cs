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
