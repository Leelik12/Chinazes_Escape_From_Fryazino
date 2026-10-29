# Генерация звуков боя и погоды: свист пролетающих пуль, попадания по металлу, бетону и земле, гильзы,
# шипение перегретого ствола и щелчок остывшего, сердцебиение при малой прочности, сигнал босса,
# звон в ушах после близкого взрыва, дождь (по земле и по крыше машины), гром и всплеск машины в воде.
# Всё синтезируется из шума и простых сигналов, поэтому у звуков нет сторонних лицензий.
# Запуск из корня проекта: python Tools/Audio/generate_combat_sounds.py (нужны numpy и scipy).
import os
import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, sosfiltfilt

SR = 44100
ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "Sound", "Generated")
rng = np.random.default_rng(1943)


def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def lowpass(x, hz, order=4):
    return sosfilt(butter(order, hz, "low", fs=SR, output="sos"), x)


def highpass(x, hz, order=4):
    return sosfilt(butter(order, hz, "high", fs=SR, output="sos"), x)


def circ(fn, x, *args):
    n = len(x)
    return fn(np.concatenate([x, x, x]), *args)[n:2 * n]


def zlp(x, hz, order=2):
    return sosfiltfilt(butter(order, hz, "low", fs=SR, output="sos"), x)


def zhp(x, hz, order=2):
    return sosfiltfilt(butter(order, hz, "high", fs=SR, output="sos"), x)


def periodic_noise(n, lo, hi, tilt=0.0):
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    gain = ((f >= lo) & (f <= hi)).astype(float)
    with np.errstate(divide="ignore"):
        gain *= np.where(f > 0, (f / max(lo, 1.0)) ** tilt, 0.0)
    x = np.fft.irfft(spec * gain, n)
    return x / (np.abs(x).max() + 1e-9)


def periodic_mod(n, k_lo, k_hi, lo, hi):
    spec = np.zeros(n // 2 + 1, dtype=complex)
    for k in range(k_lo, k_hi + 1):
        spec[k] = rng.standard_normal() + 1j * rng.standard_normal()
    e = np.fft.irfft(spec, n)
    e = (e - e.min()) / (e.max() - e.min() + 1e-9)
    return lo + (hi - lo) * e


def decay(t, rate):
    return np.exp(-t * rate)


def modes(t, freqs, rates, gains):
    y = np.zeros_like(t)
    for f, r, g in zip(freqs, rates, gains):
        y += g * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t * r)
    return y


def place(total, x, at):
    i = int(at * SR)
    if i >= len(total):
        return
    m = min(len(x), len(total) - i)
    total[i:i + m] += x[:m]


def place_wrap(total, x, at_sample):
    idx = (np.arange(len(x)) + at_sample) % len(total)
    np.add.at(total, idx, x)


def fade(x, fin, fout):
    y = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a:
        y[:a] *= np.linspace(0, 1, a)
    if b:
        y[-b:] *= np.linspace(1, 0, b)
    return y


def tail(x, seconds, wet, damp_hz=3000.0):
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-tt * 6.9 / seconds)
    ir = lowpass(ir, damp_hz, 2)
    ir[0] = 0.0
    w = np.concatenate([np.convolve(x, ir), [0.0]])[:len(x) + n]
    w /= np.abs(w).max() + 1e-9
    d = np.concatenate([x, np.zeros(n)])
    d /= np.abs(d).max() + 1e-9
    return d * (1 - wet) + w * wet


def save(folder, name, x, peak_db=-1.0, loop=False):
    x = np.asarray(x, dtype=float)
    if loop:
        x = x - x.mean()
    else:
        loud = np.nonzero(np.abs(x) > np.abs(x).max() * 10 ** (-66 / 20))[0]
        end = min(len(x), loud[-1] + int(0.03 * SR)) if len(loud) else len(x)
        x = fade(x[:end], 0.0, 0.03)
    x = x / (np.abs(x).max() + 1e-9) * 10 ** (peak_db / 20)
    out = os.path.join(ROOT, folder)
    os.makedirs(out, exist_ok=True)
    wavfile.write(os.path.join(out, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("saved", folder + "/" + name, round(len(x) / SR, 2), "s")


# ---------- пули ----------

def whizz(i):
    # Пролёт пули рядом: щелчок ударной волны (N-волна) и свист, уходящий вниз по эффекту Доплера
    t = t_axis(0.45)
    x = np.zeros(len(t))
    nw = int(0.0012 * SR)
    nwave = np.linspace(1, -1, nw)
    crack = np.zeros(int(0.02 * SR))
    crack[:nw] = nwave
    crack = highpass(crack, 400, 2)
    place(x, crack * 0.9, 0.0)
    f0, f1 = rng.uniform(4200, 5200), rng.uniform(1300, 1800)
    sweep = f0 + (f1 - f0) * (1 / (1 + np.exp(-(t - 0.08) * 45)))
    noise = rng.standard_normal(len(t))
    # Узкая полоса, которая едет по частоте: модуляция шума синусом с той же частотой
    phase = 2 * np.pi * np.cumsum(sweep) / SR
    whistle = lowpass(noise, 500, 2) * np.sin(phase) * 3.0
    env = np.exp(-((t - 0.07) / 0.06) ** 2)
    x += whistle * env * 0.7
    x += highpass(noise, 3000, 2) * env * 0.15
    save("Combat", "Bullet_Whizz_%d" % i, x, -3.0)


def hit_metal(i):
    # Звон пули по броне: резкий удар и негармоничные моды листа
    t = t_axis(0.6)
    base = rng.uniform(1300, 2100)
    ring = modes(t, base * np.array([1.0, 1.73, 2.51, 3.6, 4.4]), [18, 24, 30, 40, 55], [1.0, 0.7, 0.5, 0.35, 0.2])
    knock = lowpass(rng.standard_normal(len(t)) * decay(t, 260), 1500, 2)
    click = highpass(rng.standard_normal(len(t)) * decay(t, 1500), 3000, 2)
    save("Combat", "Hit_Metal_%d" % i, ring * 0.6 + knock * 0.8 + click * 0.6, -3.0)


def hit_concrete(i):
    t = t_axis(0.5)
    crack = highpass(rng.standard_normal(len(t)) * decay(t, 260), 900, 2)
    thud = lowpass(rng.standard_normal(len(t)) * decay(t, 70), 600, 2)
    grit = np.zeros(len(t))
    for _ in range(25):
        at = rng.exponential(0.05)
        dur = int(0.004 * SR)
        tt = np.arange(dur) / SR
        place(grit, rng.standard_normal(dur) * np.exp(-tt * 900) * rng.uniform(0.1, 0.6) * np.exp(-at * 10), 0.005 + at)
    x = crack * 0.8 + thud * 0.6 + bandpass(grit, 1500, 8000, 2) * 0.9
    save("Combat", "Hit_Concrete_%d" % i, tail(x, 0.25, 0.12), -4.0)


def hit_dirt(i):
    t = t_axis(0.4)
    thud = lowpass(rng.standard_normal(len(t)) * decay(t, 45), 350, 2)
    splat = bandpass(rng.standard_normal(len(t)) * decay(t, 35), 600, 3500, 2) * np.minimum(1, t * 200)
    save("Combat", "Hit_Dirt_%d" % i, thud * 1.0 + splat * 0.5, -6.0)


def casing(i):
    # Латунная гильза по металлу крыши: два-три отскока
    t = t_axis(0.7)
    x = np.zeros(len(t))
    base = rng.uniform(3000, 3800)
    at, amp = 0.0, 1.0
    for _ in range(3):
        dur = int(0.18 * SR)
        tt = np.arange(dur) / SR
        hit = modes(tt, base * np.array([1.0, 2.35, 3.7, 5.2]), [30, 45, 70, 95], [1.0, 0.6, 0.35, 0.2])
        hit += highpass(rng.standard_normal(dur) * np.exp(-tt * 2000), 4000, 2) * 0.4
        place(x, hit * amp, at)
        at += rng.uniform(0.06, 0.12)
        amp *= rng.uniform(0.35, 0.55)
    save("Combat", "Casing_%d" % i, x, -8.0)


# ---------- состояние машины и пулемёта ----------

def overheat_hiss():
    # Пар и шипение раскалённого ствола: шум с быстрой атакой и треском
    t = t_axis(2.2)
    hiss = bandpass(rng.standard_normal(len(t)), 2500, 11000, 2)
    env = np.minimum(1, t * 25) * np.exp(-t * 1.4)
    sizzle = np.zeros(len(t))
    for _ in range(160):
        at = rng.uniform(0, 1.8)
        dur = int(0.003 * SR)
        place(sizzle, rng.standard_normal(dur) * rng.uniform(0.2, 1.0) * np.exp(-at * 1.2), at)
    x = hiss * env + highpass(sizzle, 3000, 2) * 0.6
    save("Combat", "Gun_Overheat", x, -6.0)


def gun_cooled():
    # Затвор взведён: два металлических щелчка
    t = t_axis(0.3)
    x = np.zeros(len(t))
    for at, g in [(0.0, 0.7), (0.07, 1.0)]:
        dur = int(0.12 * SR)
        tt = np.arange(dur) / SR
        c = modes(tt, [1700, 3900, 6100], [90, 140, 200], [1.0, 0.6, 0.3]) + highpass(rng.standard_normal(dur) * np.exp(-tt * 900), 2000, 2) * 0.6
        place(x, c * g, at)
    save("Combat", "Gun_Cooled", x, -5.0)


def heartbeat():
    # Цикл из двух ударов «тук-тук» на 105 ударах в минуту, глухо, как в шлеме
    bpm = 105
    n = int(round(60.0 / bpm * SR))
    x = np.zeros(n)
    for at, g, f in [(0.0, 1.0, 52), (0.16, 0.7, 46)]:
        dur = int(0.22 * SR)
        tt = np.arange(dur) / SR
        beat = np.sin(2 * np.pi * f * tt * (1 - 0.6 * tt)) * np.exp(-tt * 22) * np.minimum(1, tt * 90)
        beat *= np.minimum(1, (tt[-1] - tt) * 40)
        place_wrap(x, beat * g, int(at * SR))
    save("Combat", "Heartbeat", x, -2.0, loop=True)


def boss_horn():
    # Сигнал приближения босса: низкий ревун из двух тонов с биением и эхом между домами
    t = t_axis(2.6)
    env = np.minimum(1, t * 6) * np.minimum(1, (2.4 - t).clip(0) * 4)
    x = np.zeros(len(t))
    for f, g in [(73.4, 1.0), (110.0, 0.7), (146.8, 0.4)]:
        drift = f * (1 + 0.004 * np.sin(2 * np.pi * 0.7 * t))
        phase = 2 * np.pi * np.cumsum(drift) / SR
        saw = 2 * ((phase / (2 * np.pi)) % 1.0) - 1
        x += g * saw
    x = lowpass(x, 900, 4) * env
    x = np.tanh(x * 1.5)
    save("Combat", "Boss_Horn", tail(x, 2.5, 0.35, 1500), -2.0)


def deaf_ring():
    # Звон в ушах после близкого взрыва: тон около 3,7 кГц с лёгким биением и шумовой подложкой
    t = t_axis(4.5)
    tone = np.sin(2 * np.pi * 3720 * t) + 0.5 * np.sin(2 * np.pi * 3731 * t)
    env = np.minimum(1, t * 8) * np.exp(-t * 0.8)
    hush = lowpass(rng.standard_normal(len(t)), 300, 2) * 0.3 * np.exp(-t * 1.2)
    save("Combat", "Deaf_Ring", tone * env * 0.5 + hush, -8.0)


# ---------- погода ----------

def rain_loop(name, seconds, drops_per_s, f_lo, f_hi, body_hz, roof):
    n = int(seconds * SR)
    x = np.zeros(n)
    for _ in range(int(drops_per_s * seconds)):
        dur = int(rng.uniform(0.004, 0.02 if roof else 0.012) * SR)
        tt = np.arange(dur) / SR
        f = rng.uniform(f_lo, f_hi)
        drop = np.sin(2 * np.pi * f * tt * (1 + 2.5 * tt)) * np.exp(-tt * rng.uniform(250, 600))
        drop += rng.standard_normal(dur) * np.exp(-tt * 1200) * 0.6
        place_wrap(x, drop * rng.uniform(0.05, 1.0) ** 2, int(rng.integers(0, n)))
    x /= np.abs(x).max() + 1e-9
    wash = periodic_noise(n, body_hz, 12000, -0.7) * periodic_mod(n, 1, 4, 0.75, 1.0)
    x = x * (0.9 if roof else 0.55) + wash * (0.25 if roof else 0.7)
    if roof:
        drum = periodic_noise(n, 80, 600, -1.0) * 0.25
        x += drum
    save("Weather", name, x, -2.0, loop=True)


def thunder(i):
    t = t_axis(9.0)
    x = np.zeros(len(t))
    # Раскат: несколько ударов разряда, затем длинный рокот, высокие гаснут первыми
    for k in range(rng.integers(3, 7)):
        at = rng.uniform(0.0, 0.6) + k * rng.uniform(0.05, 0.25)
        dur = int(0.6 * SR)
        tt = np.arange(dur) / SR
        strike = rng.standard_normal(dur) * np.minimum(1, tt * 60) * np.exp(-tt * 7) * rng.uniform(0.5, 1.0)
        place(x, strike * np.minimum(1, (tt[-1] - tt) * 20), at)
    rumble = lowpass(rng.standard_normal(len(t)), 160, 4) * np.minimum(1, t * 3) * np.exp(-t * 0.45)
    rumble *= 0.6 + 0.4 * np.abs(lowpass(rng.standard_normal(len(t)), 3, 2) * 40).clip(0, 1)
    rumble *= np.minimum(1, (t[-1] - t) * 2)
    x = lowpass(x, 900 - 200 * i, 4) * 0.8 + rumble * 2.0
    save("Weather", "Thunder_%d" % i, tail(x, 3.0, 0.4, 600), -1.0)


def splash(i):
    # Машина падает в воду: глухой удар о поверхность, шум брызг и бульканье пузырей (синус с растущим тоном)
    t = t_axis(3.0)
    thump = lowpass(rng.standard_normal(len(t)), 220, 4) * np.minimum(1, t * 300) * decay(t, 9) * 3.0
    spray = bandpass(rng.standard_normal(len(t)), 500, 7000) * np.minimum(1, t * 80) * decay(t, 3.2 + i)
    spray *= 0.6 + 0.4 * np.abs(lowpass(rng.standard_normal(len(t)), 12, 2) * 30).clip(0, 1)
    x = thump + spray
    for k in range(rng.integers(40, 60)):
        dur = rng.uniform(0.02, 0.08)
        tt = t_axis(dur)
        f0 = rng.uniform(300, 1400)
        # Пузырь: тон поднимается по мере всплытия
        phase = 2 * np.pi * np.cumsum(f0 * (1 + tt * rng.uniform(4, 12))) / SR
        bubble = np.sin(phase) * np.exp(-tt * rng.uniform(40, 90)) * rng.uniform(0.1, 0.35)
        place(x, bubble, rng.uniform(0.05, 2.4) ** 1.3)
    save("Weather", "Water_Splash_%d" % i, tail(x, 0.8, 0.2, 2500), -2.0)


if __name__ == "__main__":
    for i in range(3):
        whizz(i)
    for i in range(4):
        hit_metal(i)
    for i in range(3):
        hit_concrete(i)
        hit_dirt(i)
    for i in range(4):
        casing(i)
    overheat_hiss()
    gun_cooled()
    heartbeat()
    boss_horn()
    deaf_ring()
    rain_loop("Rain", 4.0, 2600, 900, 4500, 400, False)
    rain_loop("Rain_Roof", 3.0, 520, 300, 1800, 150, True)
    for i in range(2):
        thunder(i)
    for i in range(2):
        splash(i)
