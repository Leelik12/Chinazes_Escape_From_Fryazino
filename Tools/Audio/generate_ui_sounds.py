# Генерация игровых сигналов: подтверждение убийства, начало волны, конец волны и выбор улучшения.
# Всё синтезируется из простых сигналов и шума, поэтому у звуков нет сторонних лицензий.
# Запуск из корня проекта: python Tools/Audio/generate_ui_sounds.py (нужны numpy и scipy).
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "Sound", "Generated")
rng = np.random.default_rng(1961)


def t_axis(seconds):
    return np.arange(int(SR * seconds)) / SR


def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def lowpass(x, hz, order=4):
    return sosfilt(butter(order, hz, "low", fs=SR, output="sos"), x)


def decay(t, rate):
    return np.exp(-t * rate)


def place(total, x, at):
    # Кладёт сигнал x в буфер total с момента at секунд
    start = int(at * SR)
    end = min(len(total), start + len(x))
    total[start:end] += x[: end - start]


def save(name, x, peak_db=-1.0):
    x = x - np.mean(x)
    x = x / (np.abs(x).max() + 1e-9) * 10 ** (peak_db / 20)
    # Короткий спад в начале и конце, чтобы не щёлкало
    n = min(len(x) // 4, int(0.004 * SR))
    ramp = np.linspace(0.0, 1.0, n)
    x[:n] *= ramp
    x[-n:] *= ramp[::-1]
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print(name, round(len(x) / SR, 2), "с")


def metal_ping(freq, seconds, rate):
    # Металлический звон: несколько негармоничных обертонов с разным затуханием
    t = t_axis(seconds)
    partials = [(1.0, 1.0), (2.76, 0.45), (5.4, 0.25), (8.93, 0.12)]
    x = sum(a * np.sin(2 * np.pi * freq * k * t) * decay(t, rate * (1 + 0.6 * i)) for i, (k, a) in enumerate(partials))
    return x


def kill_confirm():
    # Убийство: глухой удар и два восходящих звонких тона — слышно поверх стрельбы и взрыва
    total = np.zeros(int(SR * 0.7))
    t = t_axis(0.12)
    thump = np.sin(2 * np.pi * (140 - 400 * t) * t) * decay(t, 30)
    thump += lowpass(rng.standard_normal(len(t)), 900) * decay(t, 45) * 0.6
    place(total, thump * 0.9, 0.0)
    place(total, metal_ping(1180, 0.5, 9), 0.02)
    place(total, metal_ping(1580, 0.55, 8) * 0.9, 0.11)
    return total


def wave_start():
    # Начало волны: тревожный двухтональный гудок, как у старой сирены ГО, коротко
    seconds = 1.4
    t = t_axis(seconds)
    f = np.where((t % 0.35) < 0.175, 620, 465)
    phase = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sign(np.sin(phase)) * 0.35 + np.sin(phase) * 0.65
    tone = lowpass(tone, 2600)
    env = np.minimum(1.0, t / 0.05) * np.minimum(1.0, (seconds - t) / 0.3)
    return tone * env


def wave_clear():
    # Волна пройдена: три мягких восходящих тона
    total = np.zeros(int(SR * 1.1))
    for i, freq in enumerate([523.25, 659.25, 783.99]):
        t = t_axis(0.6)
        x = np.sin(2 * np.pi * freq * t) + 0.3 * np.sin(2 * np.pi * freq * 2 * t)
        x *= decay(t, 5) * np.minimum(1.0, t / 0.01)
        place(total, x * 0.8, i * 0.12)
    return total


def card_pick():
    # Выбор улучшения: щелчок тумблера и короткий подъём тона
    total = np.zeros(int(SR * 0.45))
    t = t_axis(0.03)
    click = bandpass(rng.standard_normal(len(t)), 1500, 6000) * decay(t, 160)
    place(total, click, 0.0)
    t = t_axis(0.35)
    sweep = np.sin(2 * np.pi * (500 * t + 900 * t * t)) * decay(t, 9) * np.minimum(1.0, t / 0.01)
    place(total, sweep * 0.7, 0.02)
    return total


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    save("KillConfirm", kill_confirm())
    save("WaveStart", wave_start(), peak_db=-3.0)
    save("WaveClear", wave_clear(), peak_db=-3.0)
    save("CardPick", card_pick())
