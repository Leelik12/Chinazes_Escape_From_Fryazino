# Генерация звуков машины: слои мотора V8 по оборотам, хлопки в выхлоп, переключение передачи, шины
# (визг, асфальт, гравий, трава), набегающий ветер, удары и скрежет кузова, бьющееся стекло и стук обломков.
# Всё синтезируется из импульсов, резонансов и шума, поэтому у звуков нет сторонних лицензий.
# Запуск из корня проекта: python Tools/Audio/generate_vehicle_sounds.py (нужны numpy и scipy).
# Циклы собираются в частотной области (круговая свёртка), поэтому склеиваются без щелчка.
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, sosfiltfilt

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "Sound", "Generated", "Vehicle")
rng = np.random.default_rng(2021)


# ---------- общие помощники ----------

def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def lowpass(x, hz, order=4):
    return sosfilt(butter(order, hz, "low", fs=SR, output="sos"), x)


def highpass(x, hz, order=4):
    return sosfilt(butter(order, hz, "high", fs=SR, output="sos"), x)


def circ_lowpass(x, hz, order=4):
    # Фильтр без сдвига фазы и с кольцевым продолжением — для циклов
    n = len(x)
    y = sosfiltfilt(butter(order, hz, "low", fs=SR, output="sos"), np.concatenate([x, x, x]))
    return y[n:2 * n]


def circ_highpass(x, hz, order=4):
    n = len(x)
    y = sosfiltfilt(butter(order, hz, "high", fs=SR, output="sos"), np.concatenate([x, x, x]))
    return y[n:2 * n]


def periodic_noise(n, lo, hi, tilt=0.0):
    # Периодический шум в полосе lo..hi Гц (склеивается в цикл); tilt < 0 — спад к высоким
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    gain = ((f >= lo) & (f <= hi)).astype(float)
    with np.errstate(divide="ignore"):
        gain *= np.where(f > 0, (f / max(lo, 1.0)) ** tilt, 0.0)
    x = np.fft.irfft(spec * gain, n)
    return x / (np.abs(x).max() + 1e-9)


def periodic_peaks(n, centers, widths, gains):
    # Периодический шум с узкими пиками спектра (гауссовыми) — тональные шумы: визг шин, гул
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    gain = np.zeros_like(f)
    for c, w, g in zip(centers, widths, gains):
        gain += g * np.exp(-0.5 * ((f - c) / w) ** 2)
    x = np.fft.irfft(spec * gain, n)
    return x / (np.abs(x).max() + 1e-9)


def periodic_mod(n, cycles_lo, cycles_hi, lo, hi):
    # Медленная периодическая огибающая lo..hi: целое число колебаний на цикл
    spec = np.zeros(n // 2 + 1, dtype=complex)
    for k in range(cycles_lo, cycles_hi + 1):
        spec[k] = rng.standard_normal() + 1j * rng.standard_normal()
    e = np.fft.irfft(spec, n)
    e = (e - e.min()) / (e.max() - e.min() + 1e-9)
    return lo + (hi - lo) * e


def decay(t, rate):
    return np.exp(-t * rate)


def modes(t, freqs, rates, gains, phase_rng=True):
    # Затухающие моды (металл, стекло): сумма синусов со своими затуханиями
    y = np.zeros_like(t)
    for f, r, g in zip(freqs, rates, gains):
        ph = rng.uniform(0, 2 * np.pi) if phase_rng else 0.0
        y += g * np.sin(2 * np.pi * f * t + ph) * np.exp(-t * r)
    return y


def place(total, x, at):
    i = int(at * SR)
    if i >= len(total):
        return
    m = min(len(x), len(total) - i)
    total[i:i + m] += x[:m]


def place_wrap(total, x, at_sample):
    # Вставка с переносом через конец — для циклов
    n = len(total)
    idx = (np.arange(len(x)) + at_sample) % n
    np.add.at(total, idx, x)


def fade(x, fin, fout):
    y = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a:
        y[:a] *= np.linspace(0, 1, a)
    if b:
        y[-b:] *= np.linspace(1, 0, b)
    return y


def soft_clip(x, drive):
    return np.tanh(x * drive) / np.tanh(drive)


def room(x, seconds, wet, damp_hz=3000.0):
    # Короткий хвост: экспоненциальный шум, высокие гаснут быстрее
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-tt * 6.9 / seconds)
    ir = lowpass(ir, damp_hz, 2)
    ir[0] = 0.0
    wetx = np.concatenate([np.convolve(x, ir), [0.0]])[:len(x) + n]
    wetx /= np.abs(wetx).max() + 1e-9
    dry = np.concatenate([x, np.zeros(n)])
    dry /= np.abs(dry).max() + 1e-9
    return dry * (1 - wet) + wetx * wet


def save(name, x, peak_db=-1.0, loop=False):
    x = np.asarray(x, dtype=float)
    if not loop:
        loud = np.nonzero(np.abs(x) > np.abs(x).max() * 10 ** (-66 / 20))[0]
        end = min(len(x), loud[-1] + int(0.03 * SR)) if len(loud) else len(x)
        x = fade(x[:end], 0.0, 0.03)
    else:
        x = x - x.mean()
    x = x / (np.abs(x).max() + 1e-9) * 10 ** (peak_db / 20)
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("saved", name, round(len(x) / SR, 2), "s")


# ---------- мотор ----------

# Порядок работы V8 с крестообразным валом: выпуск по рядам идёт неравномерно — отсюда «бульканье».
# Импульсы цилиндров: доля такта сгорания (смещение от равномерного) и сила
V8_OFFSETS = np.array([0.0, 0.03, -0.02, 0.04, -0.01, 0.025, -0.035, 0.015])
V8_GAINS = np.array([1.0, 0.78, 0.93, 0.70, 0.98, 0.83, 0.88, 0.74])


def engine_layer(rpm_target, seconds, roughness, harsh):
    # Цикл из целого числа рабочих циклов (два оборота), поэтому склеивается точно
    cycle_rate = rpm_target / 120.0
    cycles = max(1, int(round(seconds * cycle_rate)))
    n = int(round(cycles * SR / cycle_rate))
    rpm = cycles / n * SR * 120.0
    cycle_len = n / cycles

    # Моменты вспышек (дробные отсчёты) и их сила
    times, amps = [], []
    for c in range(cycles):
        lope = 1.0 + (roughness * 0.5 * rng.standard_normal() if roughness > 0.4 else 0.0)
        for k in range(8):
            jitter = roughness * 0.015 * rng.standard_normal()
            times.append((c + (k + V8_OFFSETS[k] * 0.5 + jitter) / 8.0) * cycle_len)
            amps.append(V8_GAINS[k] * lope * (1.0 + roughness * 0.25 * rng.standard_normal()))
    times = np.array(times) % n
    amps = np.clip(np.array(amps), 0.6, 1.45)

    # Спектр серии импульсов с дробными задержками: сумма amp * e^(-i w t)
    f = np.fft.rfftfreq(n, 1.0 / SR)
    w = -2j * np.pi * f / SR
    pulses = np.zeros(len(f), dtype=complex)
    for i in range(0, len(times), 64):
        pulses += (amps[i:i + 64, None] * np.exp(w[None, :] * times[i:i + 64, None])).sum(axis=0)

    # Ядро вспышки в выхлопе: резонансы трубы и глушителя плюс короткий шумовой удар сгорания
    kt = np.arange(int(0.12 * SR)) / SR
    kernel = modes(kt, [62, 118, 205, 330, 520, 870], [38, 42, 55, 70, 95, 140],
                   [1.0, 0.9, 0.55, 0.35, 0.22, 0.12 * (1 + harsh)], phase_rng=False)
    burst = rng.standard_normal(len(kt)) * np.exp(-kt * (420 - 160 * harsh))
    kernel += lowpass(burst, 1800 + 2600 * harsh, 2) * (0.5 + 0.6 * harsh)
    kernel *= np.exp(-kt * 10)
    kspec = np.fft.rfft(kernel, n)
    exhaust = np.fft.irfft(pulses * kspec, n)

    # Впуск: шум, промодулированный частотой вспышек
    fire_hz = rpm / 60.0 * 4.0
    tt = np.arange(n) / SR
    am = 0.6 + 0.4 * np.cos(2 * np.pi * fire_hz * tt) ** 2
    intake = periodic_noise(n, 300, 3500, -0.6) * am

    # Механика: подвывание ремней и цепи ГРМ на кратных оборотах
    rev_hz = rpm / 60.0
    whine = 0.6 * np.sin(2 * np.pi * rev_hz * 14 * tt) + 0.35 * np.sin(2 * np.pi * rev_hz * 23 * tt)
    whine *= periodic_mod(n, 1, 3, 0.6, 1.0)

    x = exhaust / (np.abs(exhaust).max() + 1e-9)
    x += 0.10 * (0.4 + harsh) * intake + 0.025 * whine
    x = soft_clip(x * 1.1, 1.2 + 0.9 * harsh)
    x = circ_lowpass(x, 2400 + 2600 * harsh, 2)
    x = circ_highpass(x, 28, 2)
    return x, rpm


def engine_layers():
    specs = [("Engine_Idle", 820, 2.0, 0.55, 0.0),
             ("Engine_Low", 1900, 2.0, 0.25, 0.25),
             ("Engine_Mid", 3400, 1.6, 0.15, 0.55),
             ("Engine_High", 5400, 1.4, 0.10, 0.85)]
    for name, rpm, sec, rough, harsh in specs:
        x, real = engine_layer(rpm, sec, rough, harsh)
        save(name, x, -1.5, loop=True)
        print("   ", name, "rpm", round(real, 1))


def backfire(i):
    t = t_axis(0.6)
    pop = rng.standard_normal(len(t)) * decay(t, 70 + 25 * i)
    pop = bandpass(pop, 180, 2600, 2)
    thump = np.sin(2 * np.pi * (75 + 20 * i) * t * (1 - 0.3 * t)) * decay(t, 28)
    crack = highpass(rng.standard_normal(len(t)) * decay(t, 900), 2000, 2) * 0.35
    x = soft_clip(pop * 1.4 + thump * 1.1 + crack, 2.5)
    # Иногда двойной хлопок
    if i % 2 == 1:
        y = np.zeros(len(t) + int(0.09 * SR))
        place(y, x, 0.0)
        place(y, x * 0.6, 0.075 + 0.02 * i)
        x = y
    save("Backfire_%d" % i, room(x, 0.35, 0.22), -2.0)


def gear_shift(i):
    t = t_axis(0.35)
    clunk = modes(t, [140 + 25 * i, 410, 980, 2300], [45, 60, 90, 160], [1.0, 0.5, 0.25, 0.1])
    click = highpass(rng.standard_normal(len(t)) * decay(t, 400), 1500, 2) * 0.4
    x = np.zeros(len(t))
    place(x, clunk * 0.5 + click * 0.5, 0.0)
    place(x, clunk * 0.9, 0.045)
    save("GearShift_%d" % i, x, -4.0)


# ---------- шины, дорога, ветер ----------

def tyre_skid(seconds=3.0):
    n = int(seconds * SR)
    # Визг: несколько узких полос, частоты которых медленно гуляют; щебет от прилипания-срыва протектора
    x = np.zeros(n)
    for c, g in [(820, 1.0), (1240, 0.8), (1710, 0.55), (2480, 0.35), (3350, 0.18)]:
        band = periodic_peaks(n, [c, c * 1.04], [22, 30], [1.0, 0.6])
        x += g * band * periodic_mod(n, 2, 7, 0.55, 1.0)
    tt = np.arange(n) / SR
    chirp = 0.75 + 0.25 * np.sin(2 * np.pi * 17 * tt) * periodic_mod(n, 3, 9, 0.3, 1.0)
    x *= chirp
    hiss = periodic_noise(n, 1500, 7000, -1.0) * 0.18
    rumble = periodic_noise(n, 60, 400, -0.5) * 0.25
    save("Tyre_Skid", x + hiss + rumble, -1.5, loop=True)


def tyre_road(seconds=3.0):
    n = int(seconds * SR)
    # Шорох протектора по асфальту и низкий гул шин
    roar = periodic_noise(n, 40, 900, -1.2)
    hiss = periodic_noise(n, 900, 5000, -1.5) * 0.25
    tread = periodic_peaks(n, [180, 360], [25, 40], [1.0, 0.4]) * 0.3
    x = (roar + hiss + tread) * periodic_mod(n, 1, 4, 0.8, 1.0)
    save("Tyre_Road", x, -2.0, loop=True)


def gravel(seconds=3.0):
    n = int(seconds * SR)
    x = np.zeros(n)
    # Хруст: тысячи коротких щелчков камешков разной величины
    count = int(900 * seconds)
    for _ in range(count):
        size = rng.uniform(0.0, 1.0)
        dur = int((0.002 + 0.01 * size) * SR)
        tt = np.arange(dur) / SR
        f = rng.uniform(1800, 6500) * (1.0 - 0.6 * size)
        click = np.sin(2 * np.pi * f * tt + rng.uniform(0, 6.28)) * np.exp(-tt * (900 - 600 * size))
        click += rng.standard_normal(dur) * np.exp(-tt * 1500) * 0.5
        place_wrap(x, click * rng.uniform(0.15, 1.0) * (0.4 + size), int(rng.integers(0, n)))
    rumble = periodic_noise(n, 50, 500, -1.0) * 0.35
    x = x / (np.abs(x).max() + 1e-9) + rumble
    save("Tyre_Gravel", circ_lowpass(x, 9000, 2), -2.0, loop=True)


def grass(seconds=3.0):
    n = int(seconds * SR)
    swish = periodic_noise(n, 600, 6000, -0.8) * periodic_mod(n, 4, 14, 0.35, 1.0)
    thump = periodic_noise(n, 40, 250, -1.0) * 0.5
    save("Tyre_Grass", swish + thump, -3.0, loop=True)


def wind_rush(seconds=4.0):
    n = int(seconds * SR)
    # Набегающий поток: широкая полоса с горбом около 400 Гц, порывы и свист у зеркал и стоек
    body = periodic_noise(n, 30, 6000, -0.9) * periodic_mod(n, 1, 5, 0.65, 1.0)
    whistle = periodic_peaks(n, [1150, 1720], [35, 50], [1.0, 0.6]) * periodic_mod(n, 2, 6, 0.0, 1.0) * 0.12
    buffet = periodic_noise(n, 20, 120, -1.0) * periodic_mod(n, 3, 10, 0.2, 1.0) * 0.6
    save("Wind_Rush", body + whistle + buffet, -2.0, loop=True)


# ---------- удары и разрушения ----------

def metal_crunch(i, heavy):
    seconds = 1.4 if heavy else 0.7
    t = t_axis(seconds)
    # Глухой удар по массе
    f0 = (58 if heavy else 95) + 8 * i
    thump = np.sin(2 * np.pi * f0 * t * (1 - 0.25 * t)) * decay(t, 14 if heavy else 26)
    # Металл: неравномерные моды панели
    base = rng.uniform(260, 420)
    ratios = np.array([1.0, 1.59, 2.14, 2.92, 3.71, 4.95, 6.3])
    ring = modes(t, base * ratios * rng.uniform(0.97, 1.03, len(ratios)),
                 np.array([9, 12, 16, 22, 30, 42, 60]) * (1.4 if heavy else 2.2),
                 [0.7, 0.55, 0.45, 0.35, 0.25, 0.18, 0.12])
    # Сминание: густые щелчки в первые десятые доли секунды
    crumple = np.zeros(len(t))
    for _ in range(140 if heavy else 50):
        at = abs(rng.normal(0, 0.09 if heavy else 0.05))
        dur = int(0.004 * SR)
        tt = np.arange(dur) / SR
        c = rng.standard_normal(dur) * np.exp(-tt * 1200)
        place(crumple, c * rng.uniform(0.2, 1.0) * np.exp(-at * 8), at)
    crumple = bandpass(crumple, 900, 7000, 2)
    noise = lowpass(rng.standard_normal(len(t)) * decay(t, 30), 2500, 2)
    x = thump * 1.3 + ring * 0.6 + crumple * 0.9 + noise * 0.7
    x = soft_clip(x, 2.0 if heavy else 1.5)
    save(("Impact_Heavy_%d" if heavy else "Impact_Light_%d") % i, room(x, 0.5, 0.18), -1.0 if heavy else -3.0)


def scrape(seconds=3.0):
    n = int(seconds * SR)
    # Скрежет металла по бетону: шум через резонансы листа и неровные рывки «прилип-сорвался»
    base = periodic_noise(n, 200, 9000, -0.4)
    spec = np.fft.rfft(base)
    f = np.fft.rfftfreq(n, 1.0 / SR)
    gain = 0.25 * np.ones_like(f)
    for c, w, g in [(480, 25, 1.0), (1130, 35, 0.9), (2210, 45, 0.8), (3380, 60, 0.6), (5120, 80, 0.4), (7300, 100, 0.25)]:
        gain += g * np.exp(-0.5 * ((f - c) / w) ** 2) * 6
    x = np.fft.irfft(spec * gain, n)
    x /= np.abs(x).max() + 1e-9
    stick = np.zeros(n)
    for _ in range(int(60 * seconds)):
        dur = int(0.012 * SR)
        tt = np.arange(dur) / SR
        place_wrap(stick, rng.standard_normal(dur) * np.exp(-tt * 300) * rng.uniform(0.3, 1.0), int(rng.integers(0, n)))
    x = x * periodic_mod(n, 3, 12, 0.45, 1.0) + bandpass(stick, 300, 4000, 2) * 0.8
    grit = periodic_noise(n, 3000, 12000, -0.5) * 0.15
    save("Scrape", x + grit, -2.0, loop=True)


def glass_break(i):
    t = t_axis(1.8)
    x = np.zeros(len(t))
    # Удар-трещина
    crack = highpass(rng.standard_normal(int(0.03 * SR)) * np.exp(-np.arange(int(0.03 * SR)) / SR * 160), 1200, 2)
    place(x, crack * 1.4, 0.0)
    # Осколки: короткие звонкие моды, падают всё реже
    for k in range(70):
        at = rng.exponential(0.22) + (0.0 if k < 10 else 0.02)
        f = rng.uniform(2200, 9000)
        dur = int(rng.uniform(0.03, 0.12) * SR)
        tt = np.arange(dur) / SR
        shard = modes(tt, [f, f * 1.47, f * 2.3], [80, 110, 160], [1.0, 0.5, 0.25])
        place(x, shard * rng.uniform(0.1, 0.8) * np.exp(-at * 2.2), at)
    tinkle = highpass(rng.standard_normal(len(t)) * decay(t, 9), 4000, 2) * 0.12
    save("Glass_Break_%d" % i, room(x + tinkle, 0.4, 0.15), -2.0)


def debris_clatter(i):
    t = t_axis(1.6)
    x = np.zeros(len(t))
    # Отскоки детали: удары всё чаще и слабее
    at, gap, amp = 0.0, rng.uniform(0.16, 0.24), 1.0
    base = rng.uniform(380, 900)
    ratios = np.array([1.0, 2.31, 3.9, 5.4])
    while amp > 0.06 and at < 1.4:
        dur = int(0.25 * SR)
        tt = np.arange(dur) / SR
        hit = modes(tt, base * ratios * rng.uniform(0.98, 1.02, 4), [25, 40, 60, 90], [1.0, 0.5, 0.3, 0.2])
        hit += lowpass(rng.standard_normal(dur) * np.exp(-tt * 250), 3000, 2) * 0.6
        place(x, hit * amp, at)
        at += gap
        gap *= rng.uniform(0.55, 0.75)
        amp *= rng.uniform(0.5, 0.7)
    save("Debris_Clatter_%d" % i, x, -4.0)


if __name__ == "__main__":
    engine_layers()
    for i in range(4):
        backfire(i)
    for i in range(2):
        gear_shift(i)
    tyre_skid()
    tyre_road()
    gravel()
    grass()
    wind_rush()
    for i in range(3):
        metal_crunch(i, True)
        metal_crunch(i, False)
    scrape()
    for i in range(2):
        glass_break(i)
    for i in range(4):
        debris_clatter(i)
