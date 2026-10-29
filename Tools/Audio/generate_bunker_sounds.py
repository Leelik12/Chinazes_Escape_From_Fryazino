# Генерация звуков бункера меню: циклы (вентиляция, гудение лампы, эфир радиостанции), кнопки терминала
# и тумблеры, далёкий разрыв с осыпающейся пылью, а для выхода из бункера — ревун тревоги, штурвал гермодвери,
# лязг кремальер и открытие тяжёлой двери (срыв уплотнителя, скрип петель, гул массы створки).
# Скрип и скрежет — модель трения «прилип-сорвался»: поток срывов, частота которых растёт со скоростью,
# раскачивает резонансы петли; металл — затухающие моды стального стержня; всё проходит через отклик
# бетонной комнаты. Сторонних записей и лицензий нет.
# Запуск из корня проекта: python Tools/Audio/generate_bunker_sounds.py (нужны numpy и scipy).
# Результат — Assets/Resources/Bunker/*.wav, их загружает BunkerSounds.
import importlib.util
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import lfilter

HERE = os.path.dirname(os.path.abspath(__file__))
spec = importlib.util.spec_from_file_location("vehicle", os.path.join(HERE, "generate_vehicle_sounds.py"))
vs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(vs)

SR = vs.SR
OUT = os.path.join(HERE, "..", "..", "Assets", "Resources", "Bunker")
rng = np.random.default_rng(1983)
vs.rng = np.random.default_rng(1984)

t_axis, bandpass, lowpass, highpass = vs.t_axis, vs.bandpass, vs.lowpass, vs.highpass
place, fade, soft_clip = vs.place, vs.fade, vs.soft_clip


# ---------- общие помощники ----------

def resonators(x, freqs, widths, gains):
    # Банк двухполюсных резонаторов (полосы шириной widths Гц): окраска возбуждения модами тела
    y = np.zeros_like(x)
    for f, bw, g in zip(freqs, widths, gains):
        r = np.exp(-np.pi * bw / SR)
        w = 2 * np.pi * f / SR
        y += g * lfilter([1 - r * r], [1, -2 * r * np.cos(w), r * r], x)
    return y


def bar_modes(t, base, rates, gains):
    # Свободный стальной стержень: обертоны в отношениях 1 : 2,76 : 5,40 : 8,93 : 13,34
    # Фаза нулевая: удар начинается с тишины, без щелчка ступенькой
    ratios = np.array([1.0, 2.756, 5.404, 8.933, 13.34])
    return vs.modes(t, base * ratios * rng.uniform(0.985, 1.015, 5), rates, gains, phase_rng=False)


def room_ir(seconds=0.9):
    # Отклик бетонной комнаты 4×5 м: ранние отражения от стен и потолка и плотный хвост,
    # высокие частоты гаснут быстрее низких
    n = int(seconds * SR)
    tt = np.arange(n) / SR
    ir = np.zeros(n)
    for ms, g in [(6.5, 0.55), (9.8, 0.45), (13.1, 0.4), (17.6, 0.3), (23.4, 0.26), (29.0, 0.2), (34.7, 0.16)]:
        ir[int(ms * SR / 1000)] += g * rng.choice([-1.0, 1.0])
    noise = rng.standard_normal(n) * np.clip((tt - 0.012) / 0.02, 0, 1)
    low = lowpass(noise, 400, 2) * np.exp(-tt * 6.9 / 0.95)
    mid = bandpass(noise, 400, 2500, 2) * np.exp(-tt * 6.9 / 0.7)
    high = highpass(noise, 2500, 2) * np.exp(-tt * 6.9 / 0.35)
    ir += (low * 1.2 + mid * 0.8 + high * 0.35) * 0.09
    return ir


IR = room_ir()


def bunker(x, wet=0.28):
    # Сухой звук плюс отклик комнаты; хвост дописывается в конец
    w = np.convolve(x, IR)
    dry = np.concatenate([x, np.zeros(len(IR) - 1)])
    w *= np.abs(dry).max() / (np.abs(w).max() + 1e-9)
    return dry * (1 - wet) + w * wet


def bunker_loop(x, wet=0.3):
    # То же для цикла: свёртка круговая, хвост конца ложится на начало
    n = len(x)
    h = np.zeros(n)
    m = min(n, len(IR))
    h[:m] = IR[:m]
    w = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(h), n)
    w *= np.abs(x).max() / (np.abs(w).max() + 1e-9)
    return x * (1 - wet) + w * wet


def slip_train(n, rate, amp, jitter=0.1):
    # Поток срывов трения: rate (Гц) и amp — массивы по отсчётам; срыв — короткий щелчок
    out = np.zeros(n)
    phase = 0.0
    click_n = int(0.0012 * SR)
    click = rng.standard_normal(click_n) * np.exp(-np.arange(click_n) / SR * 3500)
    step = rate / SR
    threshold = 1.0
    for i in range(n):
        phase += step[i]
        if phase >= threshold:
            phase -= threshold
            threshold = 1.0 + rng.uniform(-jitter, jitter)
            a = amp[i] * rng.uniform(0.6, 1.0)
            m = min(click_n, n - i)
            out[i:i + m] += click[:m] * a
            out[i] += a
    return out


def smooth_walk(n, hz, depth):
    # Медленное случайное блуждание вокруг 1: низкочастотный шум глубиной depth
    w = lowpass(rng.standard_normal(n), hz, 2)
    return 1 + depth * w / (np.abs(w).max() + 1e-9)


def ease_speed(n):
    # Скорость при плавном разгоне и торможении (производная smoothstep), пик — 1
    k = np.linspace(0, 1, n)
    return 4 * k * (1 - k)


def save(name, x, peak_db=-1.0, loop=False):
    x = np.asarray(x, dtype=float)
    if loop:
        x = x - x.mean()
    else:
        loud = np.nonzero(np.abs(x) > np.abs(x).max() * 10 ** (-60 / 20))[0]
        end = min(len(x), loud[-1] + int(0.03 * SR)) if len(loud) else len(x)
        x = fade(x[:end], 0.0, 0.03)
    x = x / (np.abs(x).max() + 1e-9) * 10 ** (peak_db / 20)
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (x * 32767).astype(np.int16))
    print("saved", name, round(len(x) / SR, 2), "s")


# ---------- выход из бункера ----------

def klaxon():
    # Электромеханический ревун: гудок 0,6 с и пауза 0,4 с. Мембрана даёт «пилу» с хрипом, рупор
    # выделяет форманты около 700, 1600 и 2900 Гц; на старте якорь разгоняется — тон чуть подъезжает
    n = SR
    t = np.arange(n) / SR
    on = 0.6
    f0 = 233.0 * (1 - 0.06 * np.exp(-t / 0.05))
    phase = np.cumsum(f0) / SR
    x = np.zeros(n)
    for h in range(1, 40):
        f = 233.0 * h
        if f > 9000:
            break
        weight = 1 / h ** 1.1 * (0.15 + np.exp(-0.5 * ((f - 700) / 260) ** 2)
                                 + 0.8 * np.exp(-0.5 * ((f - 1600) / 380) ** 2)
                                 + 0.45 * np.exp(-0.5 * ((f - 2900) / 500) ** 2))
        x += weight * np.sin(2 * np.pi * h * phase + rng.uniform(0, 2 * np.pi))
    rasp = 1 + 0.1 * np.sin(2 * np.pi * 50 * t) + 0.04 * lowpass(rng.standard_normal(n), 200, 2) * 8
    env = np.clip(t / 0.03, 0, 1) * np.clip((on - t) / 0.06, 0, 1)
    x = soft_clip(x * rasp / np.abs(x).max(), 1.6) * env
    # Рупор не бывает идеально чистым: немного шума потока в полосе формант
    x += bandpass(rng.standard_normal(n), 600, 3000, 2) * env * 0.04
    x = lowpass(x, 4500, 4)
    save("AlarmKlaxon", bunker_loop(x, 0.35), -2.0, loop=True)


def wheel_turn():
    # Штурвал гермодвери, 1,3 с (как wheelTime): срыв прикипевшего винта с глухим стуком, затем
    # скрежет резьбы — срывы трения чаще на скорости — и редкие пощёлкивания тяг
    seconds = 1.3
    n = int(seconds * SR)
    total = np.zeros(n + int(0.6 * SR))
    v = ease_speed(n)
    rate = (35 + 140 * v) * smooth_walk(n, 4, 0.2)
    amp = 0.15 + 0.85 * v
    slips = np.concatenate([slip_train(n, rate, amp, 0.2), np.zeros(int(0.5 * SR))])
    groan = resonators(slips, [610, 980, 1470, 2230, 3150], [35, 45, 60, 80, 110], [1.0, 0.8, 0.6, 0.4, 0.25])
    body = resonators(slips, [150, 260], [12, 18], [0.6, 0.4])
    grit = bandpass(rng.standard_normal(n), 1500, 7000, 2) * 0.3 * v * smooth_walk(n, 30, 0.6)
    grit = np.concatenate([grit, np.zeros(len(slips) - n)])
    turn = groan / (np.abs(groan).max() + 1e-9) + body / (np.abs(body).max() + 1e-9) * 0.5 + grit
    place(total, turn * 0.8, 0.0)
    tb = t_axis(0.5)
    breakaway = bar_modes(tb, 210, [9, 14, 22, 32, 45], [0.7, 0.5, 0.3, 0.2, 0.1])
    breakaway += np.sin(2 * np.pi * 85 * tb) * np.exp(-tb * 30) * 1.2
    breakaway += highpass(rng.standard_normal(len(tb)), 1500, 2) * np.exp(-tb * 400) * 0.8
    place(total, breakaway, 0.0)
    for at in [0.35, 0.62, 0.86, 1.08]:
        tc = t_axis(0.15)
        c = bar_modes(tc, rng.uniform(900, 1200), [40, 60, 80, 100, 120], [0.4, 0.3, 0.2, 0.1, 0.05])
        c += highpass(rng.standard_normal(len(tc)), 2500, 2) * np.exp(-tc * 900) * 0.6
        place(total, c * rng.uniform(0.25, 0.4), at + rng.uniform(-0.03, 0.03))
    save("WheelRatchet", bunker(total, 0.3), -2.0)


def dog_clank(i):
    # Кремальера откидывается и бьёт в упор: короткий контакт, звон стального рычага и глухой отклик створки
    t = t_axis(0.9)
    base = [292.0, 318.0, 271.0][i]
    ring = bar_modes(t, base, [6, 10, 17, 27, 40], [0.8, 0.55, 0.35, 0.2, 0.1])
    thump = np.sin(2 * np.pi * (92 + 6 * i) * t) * np.exp(-t * 22) * 1.3
    contact = highpass(rng.standard_normal(len(t)), 1800, 2) * np.exp(-t * 600)
    x = soft_clip(ring * 0.7 + thump + contact * 0.9, 1.4)
    pre = t_axis(0.05)
    release = highpass(rng.standard_normal(len(pre)), 3000, 2) * np.exp(-pre * 500) * 0.15
    total = np.zeros(len(t) + int(0.06 * SR))
    place(total, release, 0.0)
    place(total, x, 0.05)
    save("DogClank_%d" % i, bunker(total, 0.32), -1.0)


def door_open():
    # Тяжёлая створка, 2,2 с (как doorTime): срыв резинового уплотнителя с шипением выравнивающегося
    # давления, скрип петель (срывы трения раскачивают резонансы петли, тон тянется вслед за скоростью),
    # низкий гул массы и мягкий стук упора в конце
    seconds = 2.2
    n = int(seconds * SR)
    total = np.zeros(n + int(1.2 * SR))
    v = ease_speed(n)
    tt = np.arange(n) / SR

    # Скрип: рывками на трогании, протяжно в середине
    rate = (40 + 140 * v) * smooth_walk(n, 2.5, 0.28)
    amp = (0.1 + 0.9 * v ** 0.7) * smooth_walk(n, 6, 0.35) * np.clip((n - np.arange(n)) / (0.2 * SR), 0, 1)
    tail = int(0.5 * SR)
    slips = np.concatenate([slip_train(n, rate, amp, 0.12), np.zeros(tail)])
    hinge = resonators(slips, [430, 905, 1380, 2120, 2960], [14, 20, 28, 40, 55], [1.0, 0.75, 0.55, 0.35, 0.2])
    hinge /= np.abs(hinge).max() + 1e-9
    leaf = resonators(slips, [118, 196, 305], [6, 9, 14], [0.8, 0.6, 0.4])
    leaf /= np.abs(leaf).max() + 1e-9
    creak = (hinge * 0.75 + leaf * 0.45) * np.concatenate([np.clip(tt / 0.15, 0, 1), np.ones(tail)])

    # Масса створки: низкий гул и шорох по скорости
    rumble = lowpass(rng.standard_normal(n), 140, 4)
    rumble = rumble / (np.abs(rumble).max() + 1e-9) * v * 0.55
    rumble = np.concatenate([rumble, np.zeros(tail)])
    place(total, creak + rumble, 0.12)

    # Уплотнитель: глухой «чпок» и шипение воздуха, гаснущее за полсекунды
    ts = t_axis(0.9)
    pop = np.sin(2 * np.pi * 70 * ts * (1 - 0.3 * ts)) * np.exp(-ts * 18) * 0.9
    hiss = bandpass(rng.standard_normal(len(ts)), 1800, 8000, 2) * np.clip(ts / 0.01, 0, 1) * np.exp(-ts * 4.5) * 0.35
    place(total, pop + hiss, 0.0)

    # Упор: створка доходит медленно — стук мягкий
    te = t_axis(0.6)
    stop = np.sin(2 * np.pi * 80 * te) * np.exp(-te * 25) * 0.5 + bar_modes(te, 240, [10, 16, 25, 35, 50], [0.2, 0.12, 0.08, 0.05, 0.03])
    stop *= np.clip(te / 0.008, 0, 1)
    place(total, stop, 0.12 + seconds - 0.05)

    save("DoorCreak", bunker(total, 0.3), -1.5)


# ---------- комната ----------

def vent():
    # Вентиляция, цикл 8 с: шум воздуха в коробе с резонансами трубы, лопастной тон мотора и лёгкий гул сети
    seconds = 8.0
    n = int(seconds * SR)
    t = np.arange(n) / SR
    air = vs.periodic_noise(n, 30, 3000, -0.9)
    duct = vs.periodic_peaks(n, [95, 190, 285, 410], [6, 9, 12, 18], [1.0, 0.6, 0.4, 0.25])
    blade = 0.12 * np.sin(2 * np.pi * 225 * t) + 0.05 * np.sin(2 * np.pi * 450 * t)
    hum = 0.06 * np.sin(2 * np.pi * 50 * t) + 0.04 * np.sin(2 * np.pi * 100 * t)
    sway = vs.periodic_mod(n, 2, 6, 0.8, 1.0)
    x = (air * 0.7 + duct * 0.5) * sway + blade * vs.periodic_mod(n, 1, 4, 0.6, 1.0) + hum
    save("BunkerVent", vs.circ_lowpass(x, 2500, 2), -3.0, loop=True)


def lamp_buzz():
    # Дроссель люминесцентной лампы, цикл 2 с: 100 Гц и гармоники, верх срезан корпусом, лёгкий дребезг
    seconds = 2.0
    n = int(seconds * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for h in range(1, 30):
        x += np.sin(2 * np.pi * 100 * h * t + rng.uniform(0, 2 * np.pi)) / h ** 1.2 * (1.6 if h in (2, 4) else 1.0)
    x = vs.circ_lowpass(x, 2200, 2)
    rattle = vs.periodic_mod(n, 3, 9, 0.8, 1.0)
    save("BunkerLampBuzz", x * rattle + vs.periodic_noise(n, 2000, 8000) * 0.01, -3.0, loop=True)


def radio():
    # Эфир на КВ, цикл 12 с: шум в полосе приёмника с замираниями, тихий свист биений,
    # треск разрядов и обрывок морзянки «СОС» на 750 Гц через узкий телеграфный фильтр
    seconds = 12.0
    n = int(seconds * SR)
    t = np.arange(n) / SR
    hiss = vs.periodic_noise(n, 300, 3200, -0.3)
    fading = vs.periodic_mod(n, 2, 9, 0.35, 1.0)
    whistle = 0.04 * np.sin(2 * np.pi * 1210 * t) * vs.periodic_mod(n, 1, 3, 0.0, 1.0)
    crackle = np.zeros(n)
    for _ in range(40):
        d = int(0.003 * SR)
        vs.place_wrap(crackle, rng.standard_normal(d) * np.exp(-np.arange(d) / SR * 1500) * rng.uniform(0.3, 1.2), int(rng.integers(0, n)))
    gate = np.zeros(n)
    dot, pos = 0.075, 3.0
    for c in "... --- ...":
        length = dot if c == "." else dot * 3 if c == "-" else dot * 2
        if c != " ":
            a, b = int(pos * SR), int((pos + length) * SR)
            gate[a:b] = 1.0
        pos += length + dot
    gate = vs.circ_lowpass(gate, 120, 2)
    morse = np.sin(2 * np.pi * 750 * t) * np.clip(gate, 0, 1) * 0.5
    crackle = vs.circ_lowpass(vs.circ_highpass(crackle, 300, 2), 3200, 4)
    x = hiss * fading * 0.55 + whistle + crackle * 1.2 + morse
    save("BunkerRadio", x, -3.0, loop=True)


def hover():
    # Тихий тик реле: лёгкий щелчок якоря и короткий звон пружины
    t = t_axis(0.12)
    x = highpass(rng.standard_normal(len(t)), 2500, 2) * np.exp(-t * 1200)
    x += np.sin(2 * np.pi * 3100 * t) * np.exp(-t * 220) * 0.35
    save("TerminalHover", bunker(x, 0.12), -6.0)


def click():
    # Клавиша терминала: нажатие (щелчок контакта и стук о дно) и отпускание через 60 мс
    t = t_axis(0.2)
    press = highpass(rng.standard_normal(len(t)), 1800, 2) * np.exp(-t * 900)
    press += np.sin(2 * np.pi * 1450 * t) * np.exp(-t * 120) * 0.3
    press += np.sin(2 * np.pi * 210 * t) * np.exp(-t * 80) * 0.5
    release = highpass(rng.standard_normal(len(t)), 2500, 2) * np.exp(-t * 1100) * 0.45
    total = np.zeros(int(0.32 * SR))
    place(total, press, 0.0)
    place(total, release, 0.06)
    save("TerminalClick", bunker(total, 0.15), -3.0)


def toggle():
    # Тумблер: пружина перебрасывает рычажок — резкий щелчок, металлический призвук и стук о панель
    t = t_axis(0.25)
    x = highpass(rng.standard_normal(len(t)), 2000, 2) * np.exp(-t * 700)
    x += bar_modes(t, 1250, [60, 90, 130, 170, 220], [0.35, 0.25, 0.15, 0.08, 0.04])
    x += np.sin(2 * np.pi * 170 * t) * np.exp(-t * 60) * 0.7
    save("ToggleSnap", bunker(soft_clip(x, 1.3), 0.15), -2.0)


def rumble():
    # Далёкий разрыв сквозь грунт: глухой удар, затяжной раскат и сыплющаяся с потолка пыль
    seconds = 5.0
    n = int(seconds * SR)
    t = np.arange(n) / SR
    boom = lowpass(rng.standard_normal(n), 90, 4)
    boom = boom / (np.abs(boom).max() + 1e-9) * np.clip(t / 0.06, 0, 1) * np.exp(-t * 1.1)
    thump = np.sin(2 * np.pi * 36 * t * (1 - 0.1 * t)) * np.exp(-t * 3.5) * 0.9
    rattle = bandpass(rng.standard_normal(n), 300, 1800, 2) * np.exp(-((t - 0.25) / 0.2) ** 2) * 0.08
    dust = np.zeros(n)
    for _ in range(260):
        at = 0.5 + rng.exponential(0.9)
        if at > seconds - 0.1:
            continue
        d = int(0.004 * SR)
        g = rng.standard_normal(d) * np.exp(-np.arange(d) / SR * 1500)
        place(dust, g * np.exp(-(at - 0.5) * 0.6) * rng.uniform(0.2, 1.0), at)
    dust = bandpass(dust, 2500, 9000, 2) * 0.05
    save("DistantShelling", boom * 1.4 + thump + rattle + dust, -1.0)


if __name__ == "__main__":
    klaxon()
    wheel_turn()
    for i in range(3):
        dog_clank(i)
    door_open()
    vent()
    lamp_buzz()
    radio()
    hover()
    click()
    toggle()
    rumble()
