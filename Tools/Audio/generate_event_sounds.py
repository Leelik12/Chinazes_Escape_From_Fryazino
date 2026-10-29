# Генерация звуков событий боя: разряды оборванных проводов ЛЭП (цикл гудения и щелчки), треск и падение
# бетонной опоры, далёкие выстрелы орудий и свист снарядов артналёта, рация в машине — щелчки тангенты
# и неразборчивый голос в эфире (импульсы связок через форманты гласных по слогам, узкая полоса и перегруз).
# Всё синтезируется, сторонних записей и лицензий нет.
# Запуск из корня проекта: python Tools/Audio/generate_event_sounds.py (нужны numpy и scipy).
import importlib.util
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("vehicle", os.path.join(HERE, "generate_vehicle_sounds.py"))
vs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(vs)

SR = vs.SR
vs.OUT = os.path.join(HERE, "..", "..", "Assets", "Content", "Sound", "Generated", "Events")
vs.rng = np.random.default_rng(1986)
rng = np.random.default_rng(1987)

t_axis, bandpass, lowpass, highpass = vs.t_axis, vs.bandpass, vs.lowpass, vs.highpass
place, fade, soft_clip, room, save = vs.place, vs.fade, vs.soft_clip, vs.room, vs.save


def resonators(x, freqs, widths, gains):
    from scipy.signal import lfilter
    y = np.zeros_like(x)
    for f, bw, g in zip(freqs, widths, gains):
        r = np.exp(-np.pi * bw / SR)
        w = 2 * np.pi * f / SR
        y += g * lfilter([1 - r * r], [1, -2 * r * np.cos(w), r * r], x)
    return y


# ---------- провода ----------

def arc_loop():
    # Гудение дуги, цикл 2 с: сетевые 50 Гц с жёсткими гармониками, шипение и неровный треск
    seconds = 2.0
    n = int(seconds * SR)
    t = np.arange(n) / SR
    hum = np.zeros(n)
    for h in range(1, 40):
        hum += np.sin(2 * np.pi * 50 * h * t + rng.uniform(0, 2 * np.pi)) / h ** 0.9
    hum = soft_clip(hum / np.abs(hum).max(), 3.0) * vs.periodic_mod(n, 4, 14, 0.4, 1.0)
    hiss = vs.periodic_noise(n, 2500, 14000, -0.3) * vs.periodic_mod(n, 10, 40, 0.1, 1.0)
    crackle = np.zeros(n)
    for _ in range(90):
        d = int(rng.uniform(0.001, 0.006) * SR)
        vs.place_wrap(crackle, rng.standard_normal(d) * np.exp(-np.arange(d) / SR * 900) * rng.uniform(0.2, 1.0), int(rng.integers(0, n)))
    crackle = vs.circ_highpass(crackle, 1500, 2)
    save("Arc_Loop", vs.circ_lowpass(hum * 0.5, 7000, 2) + hiss * 0.25 + crackle * 0.8, -3.0, loop=True)


def arc_crack(i):
    # Разряд: резкий щелчок, «зуд» с падающим тоном и треск искр
    t = t_axis(0.5)
    snap = highpass(rng.standard_normal(len(t)), 2000, 2) * np.exp(-t * 300)
    f = 2600 * np.exp(-t * 9) + 300
    zap = np.sign(np.sin(2 * np.pi * np.cumsum(f) / SR)) * np.exp(-t * 14) * 0.35
    sparks = np.zeros(len(t))
    for _ in range(25):
        d = int(0.002 * SR)
        place(sparks, rng.standard_normal(d) * rng.uniform(0.2, 1.0), abs(rng.normal(0.05, 0.08)))
    x = snap + lowpass(zap, 6000, 2) + highpass(sparks, 3000, 2) * 0.5
    save("Arc_Crack_%d" % i, room(soft_clip(x, 2.0), 0.6, 0.12), -2.0)


def pole_crack():
    # Ломается бетон у основания опоры: хруст и скрежет арматуры
    t = t_axis(1.0)
    crunch = np.zeros(len(t))
    for _ in range(160):
        at = abs(rng.normal(0.12, 0.15))
        d = int(0.006 * SR)
        place(crunch, rng.standard_normal(d) * np.exp(-np.arange(d) / SR * 800) * rng.uniform(0.2, 1.0) * np.exp(-at * 3), at)
    crunch = bandpass(crunch, 300, 6000, 2)
    groan = resonators(rng.standard_normal(len(t)) * np.exp(-t * 3) * 0.05, [180, 420, 760], [20, 30, 40], [1.0, 0.7, 0.4])
    thud = np.sin(2 * np.pi * 60 * t) * np.exp(-t * 18)
    save("Pole_Crack", room(crunch + groan * 6 + thud * 0.8, 0.9, 0.15), -1.0)


def pole_crash():
    # Опора бьёт о землю: тяжёлый удар, отскок и сыплющиеся куски
    t = t_axis(2.2)
    thud = lowpass(rng.standard_normal(len(t)), 140, 4) * np.exp(-t * 7)
    thud = thud / np.abs(thud).max() + np.sin(2 * np.pi * 48 * t * (1 - 0.15 * t)) * np.exp(-t * 6) * 0.9
    bounce = np.zeros(len(t))
    place(bounce, np.sin(2 * np.pi * 70 * t[:SR // 3]) * np.exp(-t[:SR // 3] * 25) * 0.5, 0.32)
    debris = np.zeros(len(t))
    for _ in range(90):
        at = 0.05 + rng.exponential(0.35)
        d = int(0.02 * SR)
        tt = np.arange(d) / SR
        hit = (rng.standard_normal(d) * np.exp(-tt * 300) + np.sin(2 * np.pi * rng.uniform(500, 1600) * tt) * np.exp(-tt * 120))
        place(debris, hit * rng.uniform(0.1, 0.5) * np.exp(-at * 2), at)
    x = soft_clip(thud + bounce + bandpass(debris, 400, 6000, 2) * 0.6, 1.5)
    save("Pole_Crash", room(x, 1.2, 0.2), -0.5)


# ---------- артиллерия ----------

def artillery_launch(i):
    # Далёкий выстрел орудия: глухой хлопок через километры воздуха и долгий раскат с эхом от холмов
    seconds = 4.0
    t = t_axis(seconds)
    boom = lowpass(rng.standard_normal(len(t)), 220, 4) * np.clip(t / 0.01, 0, 1) * np.exp(-t * 2.2)
    boom /= np.abs(boom).max()
    body = np.sin(2 * np.pi * (52 + 6 * i) * t) * np.exp(-t * 8)
    x = boom + body * 0.7
    for delay, gain in [(0.55, 0.35), (1.3, 0.22), (2.1, 0.12)]:
        echo = np.zeros(len(t))
        place(echo, lowpass(x, 160, 2), delay + rng.uniform(-0.1, 0.1))
        x = x + echo * gain
    save("Artillery_Launch_%d" % i, room(x, 2.0, 0.3, 800.0), -2.0)


def artillery_whistle(i):
    # Свист падающего снаряда, 1,4 с: тон сползает вниз и громче к земле, у самого разрыва — рёв воздуха
    seconds = 1.4
    t = t_axis(seconds)
    k = t / seconds
    f0 = [1250.0, 1150.0, 1380.0][i]
    f = f0 * (1 - 0.55 * k ** 1.6)
    phase = 2 * np.pi * np.cumsum(f) / SR
    tone = np.sin(phase) + 0.25 * np.sin(2 * phase) + 0.08 * np.sin(3 * phase)
    tone *= 1 + 0.06 * np.sin(2 * np.pi * 7 * t)
    air = bandpass(rng.standard_normal(len(t)), 500, 5000, 2)
    env = 0.08 + 0.92 * k ** 2.2
    x = tone * env * 0.7 + air * (0.05 + 0.5 * k ** 4)
    x = fade(x, 0.05, 0.0)
    save("Artillery_Whistle_%d" % i, x, -2.0)


# ---------- рация ----------

def radio_chain(x, drive=3.0, noise=0.04):
    # Тракт рации: узкая полоса, перегруз, шум эфира
    y = bandpass(x, 380, 2900, 4)
    y = soft_clip(y / (np.abs(y).max() + 1e-9), drive)
    y += bandpass(rng.standard_normal(len(y)), 500, 4000, 2) * noise
    return y


def squelch(open_):
    # Тангента: щелчок и короткий всплеск шума эфира (при отпускании — «кшш» подлиннее)
    seconds = 0.12 if open_ else 0.28
    t = t_axis(seconds)
    click = highpass(rng.standard_normal(len(t)), 2500, 2) * np.exp(-t * 900)
    burst = bandpass(rng.standard_normal(len(t)), 600, 5000, 2) * np.exp(-t * (40 if open_ else 14)) * 0.6
    beep = np.zeros(len(t))
    if not open_:
        tb = t_axis(0.09)
        place(beep, np.sin(2 * np.pi * 1450 * tb) * np.clip(tb / 0.005, 0, 1) * np.clip((0.09 - tb) / 0.01, 0, 1) * 0.35, 0.03)
    save("Radio_Squelch_%s" % ("In" if open_ else "Out"), click + burst + beep, -3.0)


VOWELS = {
    "a": (730, 1090, 2440), "o": (570, 840, 2410), "e": (530, 1840, 2480),
    "i": (300, 2200, 2950), "u": (320, 870, 2250), "y": (400, 1600, 2400),
}


def babble(name, seconds, pitch, drive, noise):
    # Неразборчивая речь: слоги — согласный (шумовой всплеск) и гласный. Источник — импульсы связок со спадом
    # спектра (как у настоящего голоса), форманты плавно переходят от прошлой гласной к новой (три отрезка),
    # громкость слогов разная, высота плавает по фразе и падает к её концу
    from scipy.signal import lfilter
    n = int(seconds * SR)
    out = np.zeros(n)
    at = 0.05
    prev = VOWELS["a"]
    while at < seconds - 0.25:
        syl = rng.uniform(0.09, 0.2)
        vowel = VOWELS[rng.choice(list(VOWELS.keys()))]
        m = int(syl * SR)
        tt = np.arange(m) / SR
        k = at / seconds
        f0 = pitch * (1.1 - 0.22 * k) * (1 + 0.08 * np.sin(2 * np.pi * rng.uniform(1, 3) * tt + rng.uniform(0, 6)))
        ph = np.cumsum(f0) / SR
        pulses = np.zeros(m)
        pulses[np.nonzero(np.diff(np.floor(ph)) > 0)[0]] = 1.0
        # Спад спектра связок: двойной однополюсный фильтр
        a = np.exp(-2 * np.pi * 250 / SR)
        source = lfilter([1 - a], [1, -a], lfilter([1 - a], [1, -a], pulses)) * 30
        source += rng.standard_normal(m) * 0.02
        voiced = np.zeros(m)
        third = m // 3
        for j in range(3):
            w = (j + 1) / 3.0
            formants = [p0 * (1 - w) + p1 * w for p0, p1 in zip(prev, vowel)]
            seg = slice(j * third, m if j == 2 else (j + 1) * third)
            voiced[seg] = resonators(source, formants, (80, 100, 140), (1.0, 0.6, 0.3))[seg]
        prev = vowel
        env = np.sin(np.pi * np.clip(tt / syl, 0, 1)) ** 0.6
        level = rng.uniform(0.35, 1.0)
        if rng.random() < 0.7:
            c = int(rng.uniform(0.02, 0.05) * SR)
            cons = highpass(rng.standard_normal(c), rng.uniform(1800, 4000), 2) * np.sin(np.pi * np.arange(c) / c) * 0.12 * level
            place(out, cons, at)
            at += c / SR * 0.7
        place(out, voiced * env / (np.abs(voiced).max() + 1e-9) * level, at)
        at += syl * 0.85 + (rng.uniform(0.12, 0.25) if rng.random() < 0.12 else 0.0)
    save(name, radio_chain(out, drive, noise), -3.0)


if __name__ == "__main__":
    arc_loop()
    for i in range(3):
        arc_crack(i)
    pole_crack()
    pole_crash()
    for i in range(2):
        artillery_launch(i)
    for i in range(3):
        artillery_whistle(i)
    squelch(True)
    squelch(False)
    for i in range(4):
        babble("Radio_Hq_%d" % i, rng.uniform(1.6, 2.4), 125.0, 1.4, 0.03)
    for i in range(3):
        babble("Radio_Intercept_%d" % i, rng.uniform(1.6, 2.4), 98.0, 2.6, 0.08)
