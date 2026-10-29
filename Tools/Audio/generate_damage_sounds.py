# Генерация звуков поломок машины и гранатомёта стрелка: хлопок пробитой шины, скрежет голого диска
# по асфальту (цикл), шипение пара из пробитого радиатора (цикл), выстрел РПГ-7 (хлопок вышибного заряда,
# рёв реактивного двигателя), щелчок перезарядки и подбор ящика с выстрелами.
# Всё синтезируется, сторонних записей и лицензий нет.
# Запуск из корня проекта: python Tools/Audio/generate_damage_sounds.py (нужны numpy и scipy).
import importlib.util
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("vehicle", os.path.join(HERE, "generate_vehicle_sounds.py"))
vs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(vs)

SR = vs.SR
vs.OUT = os.path.join(HERE, "..", "..", "Assets", "Content", "Sound", "Generated", "Damage")
vs.rng = np.random.default_rng(2001)
rng = np.random.default_rng(2002)

t_axis, bandpass, lowpass, highpass = vs.t_axis, vs.bandpass, vs.lowpass, vs.highpass
place, fade, soft_clip, room, save, modes = vs.place, vs.fade, vs.soft_clip, vs.room, vs.save, vs.modes


def tire_burst(i):
    # Хлопок шины: резкий фронт давления, низкий «бух» объёма воздуха, хлопанье разорванной резины
    t = t_axis(1.2)
    x = np.zeros(len(t))
    edge = rng.standard_normal(int(0.004 * SR)) * np.linspace(1, 0, int(0.004 * SR))
    place(x, highpass(edge, 600, 2) * 1.5, 0.0)
    thump = np.sin(2 * np.pi * (70 + 12 * i) * t) * np.exp(-t * 22) * np.clip(t / 0.002, 0, 1)
    burst = lowpass(rng.standard_normal(len(t)), 2400, 2) * np.exp(-t * 30)
    x += thump * 0.9 + burst * 0.7
    # Обрывки резины шлёпают по арке колеса
    for k in range(6):
        at = 0.08 + k * rng.uniform(0.07, 0.11)
        dur = int(0.03 * SR)
        tt = np.arange(dur) / SR
        flap = bandpass(rng.standard_normal(dur), 150, 1500, 2) * np.exp(-tt * 120)
        place(x, flap * 0.35 * np.exp(-k * 0.45), at)
    hiss = bandpass(rng.standard_normal(len(t)), 1500, 8000, 2) * np.exp(-t * 4) * 0.12
    save("Tire_Burst_%d" % i, room(x + hiss, 0.5, 0.18, 2500.0), -1.0)


def rim_scrape():
    # Голый диск по асфальту, цикл 2 с: визгливый скрежет стали с тональными пиками обода,
    # неровные рывки по швам дороги и шорох крошки
    seconds = 2.0
    n = int(seconds * SR)
    grind = vs.periodic_peaks(n, [1850, 2900, 4300, 6100], [70, 90, 140, 200], [1.0, 0.8, 0.55, 0.3])
    body = vs.periodic_noise(n, 300, 9000, -0.5)
    chatter = np.zeros(n)
    for _ in range(140):
        d = int(rng.uniform(0.004, 0.012) * SR)
        tt = np.arange(d) / SR
        vs.place_wrap(chatter, rng.standard_normal(d) * np.exp(-tt * 400) * rng.uniform(0.2, 1.0), int(rng.integers(0, n)))
    chatter = vs.circ_highpass(chatter, 800, 2)
    x = grind * vs.periodic_mod(n, 3, 16, 0.4, 1.0) * 0.6 + body * 0.35 + chatter * 0.5
    save("Rim_Scrape_Loop", vs.circ_lowpass(x, 11000, 2), -2.0, loop=True)


def steam_hiss():
    # Пар из радиатора, цикл 3 с: широкое шипение с медленными толчками давления и бульканье антифриза
    seconds = 3.0
    n = int(seconds * SR)
    hiss = vs.periodic_noise(n, 1200, 14000, -0.2)
    surge = vs.periodic_mod(n, 2, 7, 0.55, 1.0)
    gurgle = np.zeros(n)
    for _ in range(18):
        d = int(rng.uniform(0.02, 0.05) * SR)
        tt = np.arange(d) / SR
        f = rng.uniform(180, 420)
        bub = np.sin(2 * np.pi * f * (1 + 2.5 * tt) * tt) * np.sin(np.pi * tt / tt[-1])
        vs.place_wrap(gurgle, bub * rng.uniform(0.2, 0.6), int(rng.integers(0, n)))
    save("Steam_Hiss_Loop", hiss * surge * 0.8 + gurgle * 0.35, -3.0, loop=True)


def rpg_launch():
    # РПГ-7: хлопок вышибного заряда, через 0,1 с — рёв реактивного двигателя гранаты, уходящий вдаль
    t = t_axis(4.0)
    x = np.zeros(len(t))
    pop = lowpass(rng.standard_normal(len(t)), 1800, 2) * np.exp(-t * 40) * np.clip(t / 0.001, 0, 1)
    boom = np.sin(2 * np.pi * 58 * t) * np.exp(-t * 14)
    x += pop * 1.2 + boom * 0.8
    k = np.clip((t - 0.1) / 0.05, 0, 1)
    roar = bandpass(rng.standard_normal(len(t)), 300, 6000, 2)
    roar *= k * np.exp(-np.clip(t - 0.1, 0, None) * 1.6) * (1 + 0.3 * np.sin(2 * np.pi * 31 * t))
    whoosh = highpass(rng.standard_normal(len(t)), 3000, 2) * k * np.exp(-np.clip(t - 0.1, 0, None) * 2.5)
    x += roar * 0.7 + whoosh * 0.25
    x = fade(soft_clip(x, 1.6), 0.0, 0.8)
    save("Rpg_Launch", room(x, 1.2, 0.22, 1800.0), -1.0)


def rpg_reload():
    # Выстрел вставлен в ствол: шорох тубуса, металлический щелчок фиксатора
    t = t_axis(0.6)
    x = np.zeros(len(t))
    slide = bandpass(rng.standard_normal(int(0.18 * SR)), 800, 5000, 2) * np.linspace(0.2, 1, int(0.18 * SR))
    place(x, slide * 0.3, 0.0)
    tt = t_axis(0.3)
    click = modes(tt, [2400, 3700, 5900], [90, 130, 200], [1.0, 0.6, 0.35], phase_rng=False)
    click += highpass(rng.standard_normal(len(tt)), 3000, 2) * np.exp(-tt * 300) * 0.8
    place(x, click, 0.2)
    clunk = np.sin(2 * np.pi * 210 * tt) * np.exp(-tt * 45)
    place(x, clunk * 0.5, 0.21)
    save("Rpg_Reload", room(x, 0.25, 0.12, 3000.0), -3.0)


def crate_pickup():
    # Ящик с выстрелами на борт: глухой удар дерева и звяк металла внутри
    t = t_axis(0.8)
    x = np.zeros(len(t))
    wood = modes(t, [180, 410, 760, 1250], [30, 45, 70, 110], [1.0, 0.6, 0.35, 0.2], phase_rng=False)
    wood += lowpass(rng.standard_normal(len(t)), 900, 2) * np.exp(-t * 60) * 0.6
    x += wood * np.clip(t / 0.002, 0, 1)
    tt = t_axis(0.5)
    clink = modes(tt, [1900, 2650, 4100], [20, 30, 45], [0.6, 0.4, 0.25], phase_rng=False)
    place(x, clink * 0.35, 0.05)
    place(x, clink * 0.2, 0.13)
    save("Crate_Pickup", room(x, 0.3, 0.15, 2500.0), -3.0)


if __name__ == "__main__":
    for i in range(2):
        tire_burst(i)
    rim_scrape()
    steam_hiss()
    rpg_launch()
    rpg_reload()
    crate_pickup()
