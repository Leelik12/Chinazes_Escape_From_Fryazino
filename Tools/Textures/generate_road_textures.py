# Генерация бесшовных текстур дорог: городской асфальт, разбитый асфальт трассы, гравий обочины,
# тротуарная плитка, бетон бордюра и маска стёртой краски разметки. Всё процедурное, сторонних лицензий нет.
# Запуск из корня проекта: python Tools/Textures/generate_road_textures.py (нужны numpy и Pillow).
# На каждую текстуру пишутся <имя>.png — цвет, в альфе гладкость (Smoothness из Albedo Alpha в URP Lit) —
# и <имя>_Normal.png — карта нормалей (OpenGL, Y вверх). У маски разметки в альфе не гладкость, а наличие краски.
# Размер плитки в метрах указан у каждой текстуры: под него Tools/Unity/BuildRoads.cs считает развёртку.
import os
import numpy as np
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "Roads", "Textures")
S = 1024
rng = np.random.default_rng(1977)
yy, xx = np.mgrid[0:S, 0:S] / S  # 0..1, y вниз


def fbm(cycles=8.0, beta=2.0, aniso=(1.0, 1.0)):
    # Бесшовный шум с убывающим спектром (через БПФ, поэтому периодичен по построению)
    f = np.fft.fftfreq(S) * S
    fx, fy = np.meshgrid(f * aniso[0], f * aniso[1])
    r = np.sqrt(fx ** 2 + fy ** 2)
    amp = 1.0 / np.maximum(r, cycles) ** (beta / 2 + 0.5)
    amp[0, 0] = 0
    spec = (rng.standard_normal((S, S)) + 1j * rng.standard_normal((S, S))) * amp
    n = np.real(np.fft.ifft2(spec))
    return (n - n.mean()) / (n.std() + 1e-9)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    t = t[..., None] if np.ndim(t) == 2 else t
    return a * (1 - t) + b * t


def rgb(r, g, b):
    return np.array([r, g, b], dtype=float) / 255.0


def save(name, color, height, alpha, strength=4.0):
    os.makedirs(OUT, exist_ok=True)
    col = np.clip(color, 0, 1)
    a = np.clip(np.broadcast_to(alpha, (S, S)), 0, 1)
    img = np.concatenate([col, a[..., None]], axis=2)
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * strength
    n = np.stack([-dx, dy, np.ones_like(dx)], axis=2)  # y изображения вниз, а в OpenGL-нормалях Y вверх
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, name + "_Normal.png"))
    print("saved", name)


def grain(scale=1.0):
    # Щебень в асфальте: мелкие светлые и тёмные зёрна поверх шума
    g = rng.random((S, S))
    stones = smoothstep(0.93, 0.99, g) - smoothstep(0.0, 0.05, 1 - g) * 0.0
    dark = smoothstep(0.0, 0.06, 0.06 - g)
    return stones * scale, dark * scale


def wrapped_lines(count, length, step, jitter, width_range, seed):
    # Сеть трещин: ломаные, нарисованные со сдвигами на ±S, поэтому шов тайла не виден
    r = np.random.default_rng(seed)
    im = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(im)
    for _ in range(count):
        x, y = r.uniform(0, S), r.uniform(0, S)
        ang = r.uniform(0, 2 * np.pi)
        pts = [(x, y)]
        for _ in range(r.integers(length[0], length[1])):
            ang += r.normal(0, jitter)
            x += np.cos(ang) * r.uniform(*step)
            y += np.sin(ang) * r.uniform(*step)
            pts.append((x, y))
        w = int(r.integers(width_range[0], width_range[1] + 1))
        for ox in (-S, 0, S):
            for oy in (-S, 0, S):
                d.line([(px + ox, py + oy) for px, py in pts], fill=255, width=w)
    return np.asarray(im) / 255.0


def wrapped_blobs(count, radius, seed, squash=0.75):
    # Пятна (выбоины, заплатки) с переносом через край; возвращает поле 0..1 (1 — центр пятна) и маску
    r = np.random.default_rng(seed)
    field = np.zeros((S, S))
    for _ in range(count):
        cx, cy = r.uniform(0, 1), r.uniform(0, 1)
        rad = r.uniform(*radius)
        ang = r.uniform(0, np.pi)
        dx = (xx - cx + 0.5) % 1 - 0.5
        dy = (yy - cy + 0.5) % 1 - 0.5
        u = dx * np.cos(ang) + dy * np.sin(ang)
        v = (-dx * np.sin(ang) + dy * np.cos(ang)) / squash
        field = np.maximum(field, 1 - np.sqrt(u * u + v * v) / rad)
    return field


def asphalt_city():
    # Городской асфальт, плитка 8 м: ровный, серый, с зерном щебня, пятнами масла и лёгкими старыми заплатками
    base = rgb(82, 83, 82)
    n = fbm(6, 2.2)
    stones, dark = grain()
    col = base * (1 + 0.06 * n)[..., None]
    col = mix(col, rgb(120, 119, 114), stones * 0.6)
    col = mix(col, rgb(40, 40, 40), dark * 0.5)
    oil = smoothstep(1.8, 3.0, fbm(14, 2.0))
    col = mix(col, rgb(52, 52, 53), oil * 0.35)
    cracks = wrapped_lines(6, (4, 10), (15, 45), 0.5, (1, 1), 12)
    col = mix(col, rgb(35, 35, 35), cracks * 0.7)
    height = n * 0.15 + stones * 0.4 - dark * 0.3 - cracks * 0.6
    smooth = 0.18 + oil * 0.2 - stones * 0.05
    save("Road_Asphalt", col, height, smooth, strength=3.0)


def asphalt_worn():
    # Разбитый асфальт трассы, плитка 16 м: тёмные и светлые заплатки, сеть трещин, выбоины со щебнем и водой
    base = rgb(68, 69, 68)
    n = fbm(5, 2.3)
    stones, dark = grain(1.2)
    col = base * (1 + 0.09 * n)[..., None]
    col = mix(col, rgb(115, 113, 106), stones * 0.7)
    col = mix(col, rgb(38, 38, 37), dark * 0.6)
    # Выцветший верхний слой: участки светлее и суше
    bleach = smoothstep(0.2, 1.6, fbm(4, 2.0))
    col = mix(col, rgb(104, 103, 98), bleach * 0.45)
    # Заплатки: прямоугольники разных лет (тёмный свежий битум и серый старый)
    r = np.random.default_rng(21)
    height = n * 0.2 + stones * 0.5 - dark * 0.3
    for _ in range(5):
        cx, cy, w, h = r.uniform(0, 1), r.uniform(0, 1), r.uniform(0.08, 0.3), r.uniform(0.05, 0.18)
        dx = np.abs((xx - cx + 0.5) % 1 - 0.5)
        dy = np.abs((yy - cy + 0.5) % 1 - 0.5)
        # Край заплатки неровный: граница прямоугольника сдвинута шумом
        dist = np.maximum(dx - w / 2, dy - h / 2) + n * 0.004
        m = smoothstep(0.004, 0.0, dist)
        tone = rgb(56, 57, 57) if r.random() < 0.6 else rgb(82, 82, 79)
        col = mix(col, tone * (1 + 0.05 * n)[..., None], m * 0.7)
        edge = smoothstep(0.003, 0.0, np.abs(dist))
        col = mix(col, rgb(34, 34, 34), edge * 0.35)
        height = height + m * 0.15
    # Трещины: крупная сеть и мелкая «крокодиловая кожа»
    big = wrapped_lines(22, (6, 16), (18, 50), 0.55, (2, 3), 31)
    small = wrapped_lines(140, (3, 7), (6, 16), 0.9, (1, 1), 32) * smoothstep(0.3, 1.2, fbm(4, 2.0))
    cr = np.clip(big + small * 0.8, 0, 1)
    col = mix(col, rgb(26, 26, 25), cr * 0.85)
    height = height - cr * 0.9
    # Выбоины: углубление со щебнем, в середине вода
    pit = wrapped_blobs(9, (0.012, 0.035), 41)
    hole = smoothstep(0.0, 0.08, pit)
    water = smoothstep(0.45, 0.6, pit)
    gravel = rng.random((S, S))
    col = mix(col, rgb(52, 51, 48) * (0.7 + 0.6 * gravel)[..., None], hole)
    col = mix(col, rgb(40, 45, 48), water * 0.85)
    height = height - hole * 2.5 + gravel * hole * 0.3
    # Трава в больших трещинах
    weeds = big * smoothstep(0.6, 1.4, fbm(8, 2.0))
    col = mix(col, rgb(78, 92, 46), weeds * 0.8)
    smooth = 0.14 + water * 0.75 + bleach * -0.04
    save("Road_AsphaltWorn", col, height, smooth, strength=3.5)


def gravel():
    # Гравий обочины, плитка 4 м: камешки разной крупности на пыльном грунте
    n = fbm(6, 2.0)
    base = mix(rgb(118, 108, 92), rgb(96, 88, 76), smoothstep(-1, 1, n))
    # Камешки — ячейки Вороного: по точке в клетке сетки, ближайшие ищутся среди 3 × 3 соседних клеток
    cells = 120
    r = np.random.default_rng(51)
    jitter = r.random((cells, cells, 2))
    tones = r.random((cells, cells))
    gx, gy = xx * cells, yy * cells
    cx, cy = np.floor(gx).astype(int), np.floor(gy).astype(int)
    tone = np.zeros((S, S))
    best = np.full((S, S), 9.0)
    second = np.full((S, S), 9.0)
    for ox in (-1, 0, 1):
        for oy in (-1, 0, 1):
            nx, ny = (cx + ox) % cells, (cy + oy) % cells
            px = cx + ox + jitter[ny, nx, 0]
            py = cy + oy + jitter[ny, nx, 1]
            dist = np.sqrt((gx - px) ** 2 + (gy - py) ** 2)
            closer = dist < best
            second = np.where(closer, best, np.minimum(second, dist))
            best = np.where(closer, dist, best)
            tone = np.where(closer, tones[ny, nx], tone)
    # Камень занимает середину ячейки, и не каждая ячейка — камень: между ними пыльный грунт
    edge = smoothstep(0.05, 0.3, second - best) * (tone > 0.35)
    stone = mix(rgb(150, 144, 132), rgb(88, 84, 78), (tone - 0.35) / 0.65)
    col = mix(base, stone * (1 + 0.08 * n)[..., None], edge * 0.9)
    height = edge * 0.8 + n * 0.2
    save("Road_Gravel", col, height, 0.12 + edge * 0.05, strength=5.0)


def sidewalk():
    # Тротуарная плитка, плитка 4 м: квадраты 0,5 м, швы с землёй, сколы и разбитые плитки
    n = fbm(5, 2.0)
    tiles = 8
    tx, ty = (xx * tiles) % 1, (yy * tiles) % 1
    seam = smoothstep(0.035, 0.015, np.minimum(np.minimum(tx, 1 - tx), np.minimum(ty, 1 - ty)))
    ix, iy = np.floor(xx * tiles).astype(int), np.floor(yy * tiles).astype(int)
    r = np.random.default_rng(61)
    tone = r.uniform(0.85, 1.08, (tiles, tiles))[iy, ix]
    base = rgb(148, 145, 136) * tone[..., None] * (1 + 0.05 * n)[..., None]
    dirt = smoothstep(0.3, 1.6, fbm(3, 2.0))
    col = mix(base, rgb(100, 94, 82), dirt * 0.35)
    col = mix(col, rgb(70, 66, 56), seam)
    broken = wrapped_lines(18, (2, 5), (10, 30), 0.8, (1, 2), 62)
    col = mix(col, rgb(80, 77, 70), broken * 0.8)
    weeds = seam * smoothstep(0.7, 1.6, fbm(10, 2.0))
    col = mix(col, rgb(76, 90, 44), weeds)
    height = (1 - seam) * 0.5 - broken * 0.5 + n * 0.1
    save("Road_Sidewalk", col, height, 0.15 - dirt * 0.05, strength=4.0)


def curb():
    # Бетон бордюра, плитка 2 м вдоль: светлый бетон, швы между камнями через 1 м, сколы и грязь снизу
    n = fbm(8, 2.0)
    pores = smoothstep(0.96, 1.0, rng.random((S, S)))
    col = rgb(172, 169, 160) * (1 + 0.06 * n)[..., None]
    col = mix(col, rgb(110, 108, 102), pores * 0.6)
    tx = (xx * 2) % 1
    joint = smoothstep(0.012, 0.004, np.minimum(tx, 1 - tx))
    col = mix(col, rgb(80, 78, 72), joint)
    grime = smoothstep(0.4, 1.0, yy) * (0.5 + 0.5 * smoothstep(-1, 1, fbm(4, 2.0, aniso=(0.2, 1.0))))
    col = mix(col, rgb(96, 90, 78), grime * 0.55)
    chips = wrapped_lines(10, (2, 4), (8, 20), 0.9, (2, 3), 71)
    col = mix(col, rgb(130, 128, 120), chips * 0.7)
    height = n * 0.2 - pores * 0.4 - joint * 0.8 - chips * 0.5
    save("Road_Curb", col, height, 0.12, strength=3.0)


def paint_mask():
    # Краска разметки, плитка 4 м: белый цвет, в альфе — сколько краски осталось.
    # Материал режет альфу порогом: в городе краска почти целая, на трассе от неё остаются клочки
    n = fbm(10, 2.2)
    fine = fbm(60, 1.5)
    wear = 0.5 + 0.22 * n + 0.08 * fine
    col = np.broadcast_to(rgb(226, 224, 214), (S, S, 3)) * (0.95 + 0.05 * fine)[..., None]
    save("Road_Paint", col, n * 0.05, np.clip(wear, 0, 1), strength=1.0)


asphalt_city()
asphalt_worn()
gravel()
sidewalk()
curb()
paint_mask()
