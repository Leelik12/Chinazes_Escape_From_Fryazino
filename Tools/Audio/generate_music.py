# Генерация динамической музыки боя: пять слоёв одной длины (16 тактов, 96 ударов в минуту, ре минор), которые
# MusicDirector смешивает по накалу боя, и короткая развязка на зачистку волны.
#   Music_Calm    — затишье: тёмный пэд из расстроенных «пил» и редкие колокольчики;
#   Music_Pulse   — пульс: бас восьмыми с фильтром и тиканье шестнадцатыми;
#   Music_Drums   — барабаны: бочка, малый в большом зале, сбивки томов, удары по железу;
#   Music_Tension — напряжение: струнное остинато шестнадцатыми и медные акценты на смене аккорда;
#   Music_Boss    — босс: хор на гласной «а» и медленная сирена;
#   Music_Clear   — развязка: ре мажор нарастает и гаснет (не цикл).
# Слои — циклы: ноты и отзвук зала переносятся через конец в начало. Громкость слоёв сведена заранее
# и нормирована общим множителем, поэтому в игре все слои на громкости 1 звучат сбалансированно.
# Всё синтезируется, сторонних записей и лицензий нет.
# Запуск из корня проекта: python Tools/Audio/generate_music.py (нужны numpy и scipy).
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, lfilter, sosfilt

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Content", "Sound", "Generated", "Music")
rng = np.random.default_rng(1941)

BPM = 96.0
BEAT = 60.0 / BPM
BAR = 4 * BEAT
BARS = 16
LOOP = BARS * BAR
N = int(round(LOOP * SR))

# Аккорды по два такта (ступени ре минора): Dm, Bb, Gm, A, Dm, F, Gm, A — корень в MIDI и лад
CHORDS = [(50, "m"), (46, "M"), (43, "m"), (45, "M"), (50, "m"), (41, "M"), (43, "m"), (45, "M")]


def hz(midi):
    return 440.0 * 2 ** ((midi - 69) / 12.0)


def chord_notes(root, kind):
    return [root, root + (3 if kind == "m" else 4), root + 7]


def chord_at(bar):
    return CHORDS[(bar // 2) % len(CHORDS)]


# ---------- примитивы ----------

def saw(freq, n, phase=0.0):
    p = (phase + np.cumsum(np.full(n, freq) if np.isscalar(freq) else freq) / SR) % 1.0
    return 2 * p - 1


def lowpass(x, f, order=2):
    return sosfilt(butter(order, min(f, SR * 0.45), "low", fs=SR, output="sos"), x)


def highpass(x, f, order=2):
    return sosfilt(butter(order, f, "high", fs=SR, output="sos"), x)


def bandpass(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x)


def sweep_lowpass(x, cutoff):
    # Однополюсный фильтр с меняющейся частотой среза (огибающая фильтра у баса и меди), дважды для крутизны
    a = np.exp(-2 * np.pi * np.clip(cutoff, 20, SR * 0.45) / SR)
    for _ in range(2):
        y = np.empty_like(x)
        s = 0.0
        for i in range(len(x)):
            s = a[i] * s + (1 - a[i]) * x[i]
            y[i] = s
        x = y
    return x


def resonators(x, freqs, widths, gains):
    y = np.zeros_like(x)
    for f, bw, g in zip(freqs, widths, gains):
        r = np.exp(-np.pi * bw / SR)
        w = 2 * np.pi * f / SR
        y += g * lfilter([1 - r * r], [1, -2 * r * np.cos(w), r * r], x)
    return y


def adsr(n, a, d, s, r, hold):
    # Огибающая: атака a, спад d до уровня s, удержание до hold, затухание r (всё в секундах)
    t = np.arange(n) / SR
    env = np.where(t < a, t / max(a, 1e-4), s + (1 - s) * np.exp(-(t - a) / max(d, 1e-4)))
    release = np.clip(1 - (t - hold) / max(r, 1e-4), 0, 1)
    return env * np.where(t > hold, release, 1.0)


def put(track, x, at):
    # Вставка с переносом через конец цикла
    i = int(round(at * SR)) % N
    idx = (np.arange(len(x)) + i) % N
    np.add.at(track, idx, x)


def hall(x, seconds=2.8, wet=0.3, damp=4000.0, loop=True):
    # Зал: плотный экспоненциальный хвост, высокие гаснут быстрее; для цикла свёртка круговая
    n = int(seconds * SR)
    t = np.arange(n) / SR
    noise = rng.standard_normal(n)
    ir = lowpass(noise, 600) * np.exp(-t * 6.9 / seconds) * 1.3 + highpass(noise, 600) * np.exp(-t * 6.9 / (seconds * 0.5))
    ir = lowpass(ir, damp)
    ir[: int(0.015 * SR)] *= np.linspace(0, 1, int(0.015 * SR))
    if loop:
        h = np.zeros(len(x))
        h[:min(n, len(x))] = ir[:min(n, len(x))]
        w = np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(h), len(x))
    else:
        w = np.convolve(x, ir)[:len(x)]
    w *= np.sqrt(np.mean(x ** 2) / (np.mean(w ** 2) + 1e-12))
    return x * (1 - wet) + w * wet


# ---------- слои ----------

def calm():
    track = np.zeros(N)
    # Пэд: на каждый аккорд — три голоса по три «пилы» с расстройкой, мягкая атака, хвост на следующий аккорд
    for c in range(len(CHORDS)):
        root, kind = CHORDS[c]
        at = c * 2 * BAR
        length = 2 * BAR + 1.6
        n = int(length * SR)
        t = np.arange(n) / SR
        voice = np.zeros(n)
        for note in [root - 12] + chord_notes(root, kind) + [root + 12]:
            for cents in (-9, 0, 8):
                voice += saw(hz(note) * 2 ** (cents / 1200), n, rng.random())
        cutoff = 500 + 350 * (0.5 + 0.5 * np.sin(2 * np.pi * t / (2 * BAR) - 1.2))
        voice = sweep_lowpass(voice, cutoff)
        put(track, voice * adsr(n, 1.4, 1.0, 1.0, 1.6, 2 * BAR), at)
    # Глубокий бас-тон на корне ре (целое число периодов на цикл — без щелчка на стыке)
    t = np.arange(N) / SR
    track = track / np.abs(track).max() + 0.25 * np.sin(2 * np.pi * round(hz(38) * LOOP) / LOOP * t) * (0.8 + 0.2 * np.sin(2 * np.pi * t / LOOP))
    # Колокольчики: редкие ноты пентатоники с FM-призвуком
    bells = np.zeros(N)
    scale = [62, 65, 67, 69, 72, 74, 77]
    for bar in range(0, BARS, 2):
        for k in range(rng.integers(1, 3)):
            note = scale[rng.integers(len(scale))]
            at = bar * BAR + rng.integers(0, 8) * BEAT / 2
            n = int(3.0 * SR)
            tb = np.arange(n) / SR
            mod = np.sin(2 * np.pi * hz(note) * 3.5 * tb) * 2.2 * np.exp(-tb * 4)
            bell = np.sin(2 * np.pi * hz(note) * tb + mod) * np.exp(-tb * 1.6) * np.clip(tb / 0.004, 0, 1)
            put(bells, bell * rng.uniform(0.5, 0.9), at)
    track += bells * 0.18
    return hall(track, 3.5, 0.45, 3000)


def pulse():
    track = np.zeros(N)
    t16 = BEAT / 4
    # Бас восьмыми на корне аккорда: акцент на сильные доли, фильтр раскрывается на каждой ноте
    for bar in range(BARS):
        root, kind = chord_at(bar)
        for e in range(8):
            note = root - 12 + (12 if e in (3, 7) else 0)
            at = bar * BAR + e * BEAT / 2
            n = int(0.32 * SR)
            tt = np.arange(n) / SR
            x = saw(hz(note), n) + saw(hz(note) * 1.004, n, 0.3)
            accent = 1.0 if e % 4 == 0 else 0.7
            x = sweep_lowpass(x, 180 + 1500 * accent * np.exp(-tt * 14))
            put(track, x * adsr(n, 0.004, 0.12, 0.55, 0.06, 0.24) * accent, at)
    track /= np.abs(track).max()
    # Тиканье: короткий щелчок шестнадцатыми, громче на долях
    ticks = np.zeros(N)
    click_n = int(0.03 * SR)
    for s in range(BARS * 16):
        tt = np.arange(click_n) / SR
        c = highpass(rng.standard_normal(click_n), 6000) * np.exp(-tt * 260)
        put(ticks, c * (0.9 if s % 4 == 0 else 0.45 if s % 2 == 0 else 0.25), s * t16)
    track += ticks * 0.22
    return hall(track, 1.6, 0.18, 6000)


def drums():
    track = np.zeros(N)
    snares = np.zeros(N)
    t16 = BEAT / 4

    def kick():
        n = int(0.5 * SR)
        tt = np.arange(n) / SR
        f = 45 + 85 * np.exp(-tt * 28)
        return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 7) + highpass(rng.standard_normal(n), 3000) * np.exp(-tt * 300) * 0.2

    def snare():
        n = int(0.4 * SR)
        tt = np.arange(n) / SR
        body = np.sin(2 * np.pi * 185 * tt) * np.exp(-tt * 25)
        wires = bandpass(rng.standard_normal(n), 1400, 7000) * np.exp(-tt * 13)
        return body * 0.6 + wires

    def tom(f):
        n = int(0.6 * SR)
        tt = np.arange(n) / SR
        return np.sin(2 * np.pi * np.cumsum(f * (1 + 0.5 * np.exp(-tt * 20))) / SR) * np.exp(-tt * 6)

    def metal():
        n = int(0.9 * SR)
        tt = np.arange(n) / SR
        y = np.zeros(n)
        for f, r, g in [(310, 6, 1.0), (840, 9, 0.7), (1530, 12, 0.5), (2290, 16, 0.35), (3410, 22, 0.2)]:
            y += g * np.sin(2 * np.pi * f * tt) * np.exp(-tt * r)
        return y * np.clip(tt / 0.002, 0, 1) + highpass(rng.standard_normal(n), 2000) * np.exp(-tt * 150) * 0.4

    for bar in range(BARS):
        fill = bar % 4 == 3
        for step in [0, 6, 8] + ([] if fill else [11]):
            put(track, kick() * (1.0 if step in (0, 8) else 0.7), bar * BAR + step * t16)
        for step in [4, 12]:
            if fill and step == 12:
                continue
            put(snares, snare() * (1.0 if step == 12 else 0.85), bar * BAR + step * t16)
        if fill:
            for k, step in enumerate([8, 10, 12, 13, 14, 15]):
                put(track, tom(150 - 14 * k) * (0.6 + 0.08 * k), bar * BAR + step * t16)
        if bar % 2 == 1:
            put(track, metal() * 0.35, bar * BAR + 14 * t16)
    track = track / np.abs(track).max() + hall(snares / np.abs(snares).max(), 2.2, 0.5, 5000) * 0.75
    return np.tanh(track * 1.3) / np.tanh(1.3)


def tension():
    track = np.zeros(N)
    brass = np.zeros(N)
    t16 = BEAT / 4
    # Струнное остинато: корень, квинта, октава, терция — шестнадцатыми, каждая нота с мягкой атакой смычка
    for bar in range(BARS):
        root, kind = chord_at(bar)
        notes = chord_notes(root + 12, kind)
        pattern = [notes[0], notes[2], notes[0] + 12, notes[1]]
        for s in range(16):
            note = pattern[s % 4]
            n = int(t16 * 1.3 * SR)
            tt = np.arange(n) / SR
            x = saw(hz(note), n, rng.random()) + saw(hz(note) * 1.006, n, rng.random()) * 0.7
            put(track, x * adsr(n, 0.012, 0.08, 0.6, 0.05, t16) * (1.0 if s % 4 == 0 else 0.75), bar * BAR + s * t16)
    track = bandpass(track, 250, 4500)
    # Медь: на смене аккорда — корень и квинта в малой октаве, фильтр раскрывается и закрывается
    for c in range(len(CHORDS)):
        root, kind = CHORDS[c]
        n = int(1.4 * SR)
        tt = np.arange(n) / SR
        x = np.zeros(n)
        for note in (root - 12, root - 5, root):
            for cents in (-6, 5):
                x += saw(hz(note) * 2 ** (cents / 1200), n, rng.random())
        x = sweep_lowpass(x, 300 + 2600 * np.exp(-tt * 3.5) * np.clip(tt / 0.06, 0, 1))
        put(brass, x * adsr(n, 0.05, 0.4, 0.5, 0.4, 1.0), c * 2 * BAR)
    track = track / np.abs(track).max() + brass / np.abs(brass).max() * 0.8
    return hall(track, 2.6, 0.35, 4500)


def boss():
    track = np.zeros(N)
    # Хор на «а»: аккорд «пил» через форманты гласной, вибрато
    for c in range(len(CHORDS)):
        root, kind = CHORDS[c]
        n = int((2 * BAR + 1.2) * SR)
        tt = np.arange(n) / SR
        x = np.zeros(n)
        for note in chord_notes(root, kind) + [root + 12]:
            vib = 1 + 0.006 * np.sin(2 * np.pi * 5.2 * tt + rng.random() * 6)
            for cents in (-12, 0, 11):
                x += saw(hz(note) * 2 ** (cents / 1200) * vib, n, rng.random())
        x = resonators(x, (730, 1090, 2440), (110, 130, 180), (1.0, 0.6, 0.3))
        put(track, x * adsr(n, 0.8, 1.0, 1.0, 1.2, 2 * BAR), c * 2 * BAR)
    track /= np.abs(track).max()
    # Сирена: тон медленно поднимается и опускается раз в четыре такта
    t = np.arange(N) / SR
    f = 330 + 220 * (0.5 - 0.5 * np.cos(2 * np.pi * t / (4 * BAR)))
    siren = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.3 * np.sin(4 * np.pi * np.cumsum(f) / SR)
    track += siren * 0.12
    return hall(track, 3.5, 0.5, 3500)


def clear_stinger():
    # Развязка: ре мажор нарастает за полсекунды, колокол сверху, долгий хвост
    seconds = 6.0
    n = int(seconds * SR)
    t = np.arange(n) / SR
    x = np.zeros(n)
    for note in (38, 50, 54, 57, 62, 66):
        for cents in (-7, 0, 7):
            x += saw(hz(note) * 2 ** (cents / 1200), n, rng.random())
    x = lowpass(x, 1800) * adsr(n, 0.5, 1.5, 0.7, 2.5, 2.0)
    bell = np.sin(2 * np.pi * hz(74) * t + np.sin(2 * np.pi * hz(74) * 3.5 * t) * 2 * np.exp(-t * 3)) * np.exp(-t * 1.2)
    x = x / np.abs(x).max() + bell * 0.3
    return hall(np.concatenate([x, np.zeros(int(2.5 * SR))]), 3.5, 0.45, 3500, loop=False)


def write(name, x, gain):
    os.makedirs(OUT, exist_ok=True)
    wavfile.write(os.path.join(OUT, name + ".wav"), SR, (np.clip(x * gain, -1, 1) * 32767).astype(np.int16))
    print("saved", name, round(len(x) / SR, 2), "s")


if __name__ == "__main__":
    # Сведение: громкость каждого слоя в общем миксе
    layers = {
        "Music_Calm": calm() * 0.5,
        "Music_Pulse": pulse() * 0.45,
        "Music_Drums": drums() * 0.7,
        "Music_Tension": tension() * 0.4,
        "Music_Boss": boss() * 0.35,
    }
    # Нормировка по сумме всех слоёв: в самом громком месте полного микса — −1 дБ
    peak = np.abs(sum(layers.values())).max()
    gain = 10 ** (-1 / 20) / peak
    for name, x in layers.items():
        write(name, x - x.mean(), gain)
    stinger = clear_stinger()
    write("Music_Clear", stinger / np.abs(stinger).max() * 0.6, 1.0)
