# Звуки электрички и переезда: гудок-тифон в два тона, цикл проходящего поезда (гул тяговых моторов, рокот
# тележек и стук колёс на стыках рельсов парами по тележкам) и цикл звонка переезда.
# Всё синтезируется, сторонних записей и лицензий нет.
# Запуск из корня проекта: python Tools/Audio/generate_train_sounds.py (нужны numpy и scipy).
import importlib.util
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("vehicle", os.path.join(HERE, "generate_vehicle_sounds.py"))
vs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(vs)

SR = vs.SR
vs.OUT = os.path.join(HERE, "..", "..", "Assets", "Content", "Sound", "Generated", "Train")
vs.rng = np.random.default_rng(2001)
rng = np.random.default_rng(2002)

t_axis, bandpass, lowpass, highpass = vs.t_axis, vs.bandpass, vs.lowpass, vs.highpass
place_wrap, fade, soft_clip, room, save = vs.place_wrap, vs.fade, vs.soft_clip, vs.room, vs.save


def periodic_noise(n, lo, hi):
    # Шум в полосе lo..hi Гц, периодичный по длине n: цикл склеивается без щелчка
    spec = np.fft.rfft(rng.standard_normal(n))
    f = np.fft.rfftfreq(n, 1.0 / SR)
    spec *= ((f >= lo) & (f <= hi))
    x = np.fft.irfft(spec, n)
    return x / (np.abs(x).max() + 1e-9)


def horn():
    # Тифон: два низких тона с богатыми гармониками, лёгкое плавание высоты, резкая атака и хвост улицы
    t = t_axis(2.4)
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.2 * t)
    x = np.zeros_like(t)
    for f0, g in ((311.0, 1.0), (370.0, 0.8)):
        phase = 2 * np.pi * np.cumsum(f0 * vib) / SR
        for k in range(1, 9):
            x += g * np.sin(k * phase) / k ** 1.1
    env = np.clip(t / 0.07, 0, 1) * np.clip((2.1 - t) / 0.3, 0, 1)
    x = soft_clip(bandpass(x * env, 180, 4200), 1.8)
    save("Train_Horn", room(x, 1.8, 0.35, 2500.0), -1.0)


def pass_loop():
    # Проход поезда, 4 с: рокот тележек, гул моторов, стук колёс по стыкам — пары ударов по тележкам вагонов
    n = int(4.0 * SR)
    t = np.arange(n) / SR
    rumble = lowpass(periodic_noise(n, 20, 400), 260) * 0.9
    hiss = highpass(periodic_noise(n, 1500, 9000), 2000) * 0.08
    motor = np.zeros(n)
    for k, g in ((1, 1.0), (2, 0.5), (3, 0.25)):
        motor += g * np.sin(2 * np.pi * 4.0 * 152 * k * t / 4.0)  # целое число периодов на 4 с
    motor *= 0.12 * (1 + 0.3 * np.sin(2 * np.pi * 0.5 * t))
    clack = np.zeros(int(0.12 * SR))
    tc = np.arange(len(clack)) / SR
    clack = bandpass(rng.standard_normal(len(clack)), 250, 3000) * np.exp(-tc * 45)
    clack += np.sin(2 * np.pi * 140 * tc) * np.exp(-tc * 30) * 0.8
    x = rumble + hiss + motor
    for at in (0.0, 0.14, 1.0, 1.14, 2.0, 2.14, 3.0, 3.14):
        place_wrap(x, clack * rng.uniform(0.7, 1.0), int((at + rng.uniform(-0.01, 0.01)) % 4.0 * SR))
    save("Train_Pass", soft_clip(x, 1.2), -2.0, loop=True)


def crossing_bell():
    # Звонок переезда: удары молоточка по чашке ~2.5 в секунду, металлические обертоны; цикл 1.2 с
    n = int(1.2 * SR)
    x = np.zeros(n)
    strike = t_axis(0.4)
    ding = np.zeros_like(strike)
    for f, g, d in ((1180.0, 1.0, 9.0), (2870.0, 0.5, 14.0), (4310.0, 0.3, 20.0), (5650.0, 0.15, 26.0)):
        ding += g * np.sin(2 * np.pi * f * strike) * np.exp(-strike * d)
    ding += bandpass(rng.standard_normal(len(strike)), 3000, 8000) * np.exp(-strike * 200) * 0.4
    for k in range(3):
        place_wrap(x, ding, int(k * 0.4 * SR))
    save("Crossing_Bell", x, -3.0, loop=True)


if __name__ == "__main__":
    horn()
    pass_loop()
    crossing_bell()
