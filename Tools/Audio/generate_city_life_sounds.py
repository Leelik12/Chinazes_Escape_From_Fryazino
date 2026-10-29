# Генерация звуков жизни города: карканье ворон (хриплый тон с формантами, «кар» и «кар-кар»), взлёт стаи
# (хлопки крыльев многих птиц) и лай бездомных собак (короткий тон с формантами и шумовой атакой, визг).
# Звуки играют Assets/Scripts/Effects/CityBirds.cs и Assets/Scripts/Effects/StrayDogs.cs.
# Всё синтезируется, сторонних записей и лицензий нет.
# Запуск из корня проекта: python Tools/Audio/generate_city_life_sounds.py (нужны numpy и scipy).
import importlib.util
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("vehicle", os.path.join(HERE, "generate_vehicle_sounds.py"))
vs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(vs)

SR = vs.SR
vs.OUT = os.path.join(HERE, "..", "..", "Assets", "Content", "Sound", "Generated", "CityLife")
rng = np.random.default_rng(1991)

bandpass, lowpass, highpass = vs.bandpass, vs.lowpass, vs.highpass
place, fade, room, save = vs.place, vs.fade, vs.room, vs.save


def resonators(x, freqs, widths, gains):
    from scipy.signal import lfilter
    y = np.zeros_like(x)
    for f, bw, g in zip(freqs, widths, gains):
        r = np.exp(-np.pi * bw / SR)
        w = 2 * np.pi * f / SR
        y += g * lfilter([1 - r], [1, -2 * r * np.cos(w), r * r], x)
    return y


def voiced(seconds, f0_start, f0_end, jitter, rasp):
    # Импульсы «связок» с плавающей высотой; rasp — доля шума (хрип)
    n = int(seconds * SR)
    t = np.arange(n) / SR
    f0 = np.linspace(f0_start, f0_end, n) * (1 + jitter * np.cumsum(rng.standard_normal(n)) / np.sqrt(n) * 0.3)
    f0 *= 1 + 0.04 * np.sin(2 * np.pi * rng.uniform(25, 40) * t)
    ph = np.cumsum(f0) / SR
    saw = 2 * (ph - np.floor(ph)) - 1
    return saw + rasp * rng.standard_normal(n), t


def caw_syllable(length, pitch):
    src, t = voiced(length, pitch * 1.08, pitch * 0.82, 0.6, 0.9)
    y = resonators(src, (1150, 1750, 2900), (260, 320, 500), (1.0, 0.8, 0.35))
    env = np.minimum(1, t / 0.015) * np.exp(-np.maximum(0, t - length * 0.55) * 18)
    return y * env


def crow_caw(i):
    # «Кар» или «кар-кар»: птицы разные по высоте
    pitch = rng.uniform(380, 470)
    out = np.zeros(int(1.0 * SR))
    count = 1 if i == 0 else 2
    at = 0.02
    for _ in range(count):
        length = rng.uniform(0.22, 0.32)
        place(out, caw_syllable(length, pitch * rng.uniform(0.95, 1.05)), at)
        at += length + rng.uniform(0.12, 0.2)
    save("Crow_Caw_%d" % i, room(highpass(out, 300), 0.4, 0.15), -3.0)


def crow_flock():
    # Взлёт стаи: у каждой птицы — серия хлопков крыльев с частотой 7–11 в секунду, затухает по мере набора высоты
    seconds = 1.8
    out = np.zeros(int(seconds * SR))
    for _ in range(7):
        rate = rng.uniform(7, 11)
        start = rng.uniform(0.0, 0.35)
        beats = int(rng.uniform(7, 12))
        for k in range(beats):
            at = start + k / rate + rng.uniform(-0.006, 0.006)
            m = int(0.05 * SR)
            burst = bandpass(rng.standard_normal(m), 250, 2600) * np.hanning(m) * (1 - k / beats) ** 1.5
            if at < seconds - 0.06:
                place(out, burst * rng.uniform(0.5, 1.0), at)
    save("Crow_Flock", room(out, 0.5, 0.2), -4.0)


def dog_bark(i):
    # «Гав»: шумовая атака, тон с подъёмом и спадом, форманты пасти; у разных собак разная высота
    pitch = (230, 300, 180)[i]
    out = np.zeros(int(0.9 * SR))
    at = 0.02
    for _ in range(1 if i == 1 else 2):
        length = rng.uniform(0.11, 0.16)
        src, t = voiced(length, pitch * 1.25, pitch * 0.85, 0.3, 0.45)
        y = resonators(src, (650, 1350, 2600), (180, 260, 420), (1.0, 0.7, 0.3))
        env = np.minimum(1, t / 0.008) * np.exp(-np.maximum(0, t - length * 0.35) * 25)
        attack = highpass(rng.standard_normal(int(0.02 * SR)), 1200) * np.hanning(int(0.02 * SR)) * 0.4
        place(out, attack, at)
        place(out, y * env / (np.abs(y).max() + 1e-9), at + 0.005)
        at += length + rng.uniform(0.18, 0.28)
    save("Dog_Bark_%d" % i, room(out, 0.4, 0.12), -2.0)


def dog_yelp():
    # Визг испуганной собаки: высокий тон, скользящий вниз
    src, t = voiced(0.35, 1100, 650, 0.2, 0.15)
    y = resonators(src, (1500, 2600, 3800), (300, 400, 600), (1.0, 0.6, 0.25))
    env = np.minimum(1, t / 0.01) * np.exp(-t * 6)
    save("Dog_Yelp", room(y * env, 0.35, 0.12), -3.0)


if __name__ == "__main__":
    for i in range(3):
        crow_caw(i)
    crow_flock()
    for i in range(3):
        dog_bark(i)
    dog_yelp()
