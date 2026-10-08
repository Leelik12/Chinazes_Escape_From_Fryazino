# Генерация звуков окружения: ветер, треск огня, далёкие выстрелы, взрывы и сирена.
# Всё синтезируется из шума и простых сигналов, поэтому у звуков нет сторонних лицензий.
# Запуск из корня проекта: python Tools/Audio/generate_ambient.py (нужны numpy и scipy).
# Циклы (ветер, огонь) собираются из периодического шума через БПФ, поэтому склеиваются без щелчка.
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, fftconvolve

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "Sound", "Generated", "Ambient")
rng = np.random.default_rng(1945)


def band_noise(n, lo, hi, tilt=0.0):
    # Периодический шум в полосе lo..hi Гц; tilt < 0 — спад к высоким частотам
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    gain = ((f >= lo) & (f <= hi)).astype(float)
    with np.errstate(divide="ignore"):
        gain *= np.where(f > 0, (f / max(lo, 1.0)) ** tilt, 0.0)
    x = np.fft.irfft(spec * gain, n)
    return x / (np.abs(x).max() + 1e-9)


def slow_envelope(n, hz, lo, hi):
    # Периодическая медленная огибающая со случайными колебаниями до hz Гц, значения lo..hi
    e = band_noise(n, 0.01, hz)
    e = (e - e.min()) / (e.max() - e.min() + 1e-9)
    return lo + (hi - lo) * e


def lowpass(x, hz, order=4):
    return sosfilt(butter(order, hz, "low", fs=SR, output="sos"), x)


def highpass(x, hz, order=4):
    return sosfilt(butter(order, hz, "high", fs=SR, output="sos"), x)


def reverb(x, seconds, wet, damp_hz=2500.0):
    # Хвост открытого города: экспоненциально затухающий шум, высокие частоты гаснут быстрее
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-t * 6.9 / seconds)
    ir = lowpass(ir, damp_hz, 2)
    ir[0] = 0.0
    tail = np.concatenate([fftconvolve(x, ir), [0.0]])
    tail /= np.abs(tail).max() + 1e-9
    dry = np.concatenate([x, np.zeros(n)])
    dry /= np.abs(dry).max() + 1e-9
    return dry * (1.0 - wet) + tail * wet


def fade(x, fin, fout):
    y = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a:
        y[:a] *= np.linspace(0, 1, a)
    if b:
        y[-b:] *= np.linspace(1, 0, b)
    return y


def save(name, x, peak_db=-1.0, trim=True):
    x = np.asarray(x, dtype=float)
    x = x / (np.abs(x).max() + 1e-9) * 10 ** (peak_db / 20)
    if trim:
        # Хвост тише -70 дБ в 16 битах всё равно становится нулями — отрезаем его с короткой подводкой
        loud = np.nonzero(np.abs(x) > 10 ** (-70 / 20))[0]
        end = min(len(x), loud[-1] + int(0.05 * SR)) if len(loud) else len(x)
        x = fade(x[:end], 0.0, 0.05)
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("saved", name, round(len(x) / SR, 1), "s")


def wind(seconds=40):
    # Низкий гул и свист порывами; каналы — разный шум с общей огибающей, чтобы ветер был широким
    n = seconds * SR
    gust = slow_envelope(n, 0.12, 0.25, 1.0) ** 1.5
    whistle_env = slow_envelope(n, 0.2, 0.0, 1.0) ** 3
    channels = []
    for _ in range(2):
        low = band_noise(n, 40, 500, -1.0) * gust
        high = band_noise(n, 700, 3500, -1.5) * gust * 0.35
        # Свист: узкая полоса, центр которой гуляет вместе с порывом
        whistle = band_noise(n, 820, 980) * whistle_env * 0.25
        channels.append(low + high + whistle)
    return np.stack(channels, axis=1)


def fire(seconds=12):
    # Гул пламени и треск: короткие щелчки высокочастотного шума с пуассоновскими интервалами
    n = seconds * SR
    roar = band_noise(n, 50, 700, -1.0) * slow_envelope(n, 3.0, 0.5, 1.0) * 0.5
    crackle = np.zeros(n)
    t = 0.0
    while True:
        t += rng.exponential(1.0 / 14.0)
        if t >= seconds:
            break
        length = int(rng.uniform(0.002, 0.025) * SR)
        burst = rng.standard_normal(length) * np.exp(-np.linspace(0, rng.uniform(4, 9), length))
        start = int(t * SR)
        idx = (np.arange(length) + start) % n  # хвост переносится в начало: цикл без шва
        crackle[idx] += burst * rng.uniform(0.15, 1.0) ** 2
    crackle = highpass(crackle, 1800)
    return roar + crackle * 0.9


def shot(distance_lp):
    # Один далёкий выстрел: короткий удар шума и низкий «бух», вблизи звонче
    length = int(0.12 * SR)
    t = np.arange(length) / SR
    crack = rng.standard_normal(length) * np.exp(-t * 90)
    thump = np.sin(2 * np.pi * 70 * t) * np.exp(-t * 35)
    return lowpass(crack, distance_lp) + thump * 0.6


def gunfire(bursts, rate, lp, seconds):
    # Очереди: bursts — число выстрелов в каждой очереди, rate — выстрелов в секунду
    n = int(seconds * SR)
    x = np.zeros(n)
    t = rng.uniform(0.05, 0.3)
    for count in bursts:
        for _ in range(count):
            s = shot(lp) * rng.uniform(0.7, 1.0)
            i = int(t * SR)
            if i + len(s) < n:
                x[i: i + len(s)] += s
            t += 1.0 / rate * rng.uniform(0.9, 1.1)
        t += rng.uniform(0.4, 1.2)
    return fade(reverb(x, 2.2, 0.55, 2200), 0.0, 0.4)


def explosion(seconds, lp):
    # Далёкий взрыв: глухой удар с быстрой атакой и долгим раскатом
    n = int(seconds * SR)
    t = np.arange(n) / SR
    boom = lowpass(rng.standard_normal(n), lp, 2) * np.exp(-t * 2.2) * np.minimum(t / 0.015, 1.0)
    rumble = lowpass(rng.standard_normal(n), 90, 2) * np.exp(-t * 0.7) * 0.8
    debris = highpass(rng.standard_normal(n), 900) * np.exp(-((t - 0.6) ** 2) / 0.08) * 0.04
    return fade(reverb(boom + rumble + debris, 3.5, 0.5, 900), 0.0, 1.0)


def siren(seconds=26):
    # Сирена воздушной тревоги: воющий тон с обертонами поднимается, держится и опадает, издалека
    n = int(seconds * SR)
    t = np.arange(n) / SR
    cycle = 13.0
    phase = (t % cycle) / cycle
    rise = np.clip(phase / 0.35, 0, 1)
    fall = np.clip((1.0 - phase) / 0.3, 0, 1)
    shape = np.minimum(rise, fall) ** 0.7
    freq = 180 + 260 * shape + 3 * np.sin(2 * np.pi * 0.4 * t)
    ph = 2 * np.pi * np.cumsum(freq) / SR
    tone = sum(np.sin(k * ph) / k ** 1.3 for k in range(1, 7))
    tone *= 0.3 + 0.7 * shape
    tone = lowpass(tone, 1400)
    return fade(reverb(tone, 3.0, 0.6, 1500)[:n], 3.0, 4.0)


if __name__ == "__main__":
    save("Ambient_Wind", wind(), trim=False)
    save("Ambient_Fire", fire(), trim=False)
    save("Distant_Gunfire_1", gunfire([6, 4, 9], 10, 1600, 5.0))
    save("Distant_Gunfire_2", gunfire([3, 3, 2, 5], 11, 1200, 5.5))
    save("Distant_Gunfire_3", gunfire([1, 1, 1], 1.5, 1800, 4.5))
    save("Distant_Explosion_1", explosion(7.0, 220))
    save("Distant_Explosion_2", explosion(8.0, 160))
    save("Distant_Siren", siren())
