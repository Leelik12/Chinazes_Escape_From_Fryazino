# Текстуры бункера стартового меню: бетон с отпечатками опалубки, бетонный пол, стены под масляной краской,
# краска по металлу (зелёная, красная, кремовая, жёлтая), тёмная и оцинкованная сталь, мешковина и крашеное дерево ящиков. Всё процедурное, без сторонних лицензий.
# Запуск из корня проекта: python Tools/Textures/generate_bunker_textures.py (нужны numpy и Pillow).
# Формат тот же, что у generate_building_textures.py (оттуда берутся шум, сохранение и карты нормалей):
# <имя>.png — цвет, в альфе гладкость; <имя>_Normal.png — нормали OpenGL. Плитка в метрах указана у каждой текстуры
# и совпадает с TILE в Tools/Blender/bunker/build_models.py.
import importlib.util
import os

import numpy as np

_spec = importlib.util.spec_from_file_location(
    "bt", os.path.join(os.path.dirname(os.path.abspath(__file__)), "generate_building_textures.py"))
bt = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bt)
bt.OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Content", "Bunker", "Textures")
fbm, smoothstep, mix, rgb, save, grime = bt.fbm, bt.smoothstep, bt.mix, bt.rgb, bt.save, bt.grime
S, xx, yy = bt.S, bt.xx, bt.yy
rng = np.random.default_rng(1983)


def ridges(cycles, width=0.04):
    # Тонкие извилистые линии (трещины, царапины): места, где шум проходит через ноль
    n = fbm(cycles, 2.2)
    return 1 - smoothstep(0.0, width, np.abs(n) / (np.abs(n).max() + 1e-9) * 4)


def disc_grid(nx, ny, r, jitter=0.0):
    # Круги по сетке (отверстия стяжек опалубки, заклёпки); r — радиус в долях плитки; бесшовно
    out = np.zeros((S, S))
    for i in range(nx):
        for j in range(ny):
            cx = (i + 0.5) / nx + rng.uniform(-jitter, jitter)
            cy = (j + 0.5) / ny + rng.uniform(-jitter, jitter)
            dx = (xx - cx + 0.5) % 1.0 - 0.5
            dy = (yy - cy + 0.5) % 1.0 - 0.5
            out = np.maximum(out, 1 - smoothstep(r * 0.7, r, np.sqrt(dx * dx + dy * dy)))
    return out


def concrete_formwork():
    # Монолитный бетон свода и стен: доски опалубки по 20 см с отпечатком волокон, швы между досками,
    # отверстия стяжек, раковины, высолы и потёки; плитка 2 м
    boards = 10
    v = yy * boards
    row = np.floor(v)
    seam = smoothstep(0.92, 1.0, np.abs(np.cos(np.pi * v)) ** 40)
    grain = fbm(30, 1.8, aniso=(0.06, 1.0))
    # Каждая доска чуть смещена по высоте и тону
    tone = np.sin(row * 12.9898) * 0.5
    base = mix(rgb(118, 116, 110), rgb(152, 149, 141), np.clip(0.5 + fbm(5, 2.0) * 0.25 + tone * 0.15, 0, 1))
    pores = smoothstep(1.4, 2.4, fbm(220, 0.4))
    holes = disc_grid(4, 2, 0.012, jitter=0.0)
    salt = smoothstep(0.6, 2.0, fbm(6, 2.0, aniso=(1.0, 0.15))) * 0.5
    color = base * (0.93 + 0.05 * grain + 0.0)[..., None]
    color = mix(color, color * 0.62, np.clip(seam * 0.8 + pores * 0.7 + holes, 0, 1))
    color = mix(color, rgb(196, 194, 184), salt * 0.45)
    color = grime(color, 0.45)
    height = grain * 0.035 + tone * 0.03 - seam * 0.12 - pores * 0.08 - holes * 0.25 + fbm(60, 1.5) * 0.02
    save("BK_Concrete", color, height, 0.08 + 0.05 * salt, strength=5.0)


def concrete_floor():
    # Затёртый бетонный пол: пятна масла, затоптанные дорожки, трещины, мелкие сколы; плитка 2 м
    base = mix(rgb(92, 90, 86), rgb(124, 121, 115), np.clip(0.5 + fbm(4, 2.2) * 0.3, 0, 1))
    trowel = fbm(12, 1.6, aniso=(1.0, 0.7)) * 0.04
    oil = smoothstep(1.0, 2.2, fbm(3, 2.4))
    crack = ridges(6, 0.05) * smoothstep(-0.2, 0.8, fbm(3, 2.0))
    chips = smoothstep(1.8, 2.4, fbm(40, 2.0))
    color = base * (0.94 + trowel * 2 + 0.04 * fbm(180, 0.6))[..., None]
    color = mix(color, rgb(40, 36, 32), oil * 0.6)
    color = mix(color, color * 0.45, np.clip(crack + chips * 0.6, 0, 1))
    color = grime(color, 0.3)
    height = trowel - crack * 0.15 - chips * 0.1 + fbm(150, 0.8) * 0.01
    save("BK_Floor", color, height, 0.18 + 0.4 * oil, strength=5.0)


def wall_paint():
    # Масляная краска по бетону (нижняя часть стен): глянец, облупленные пятна до бетона, наплывы краски; плитка 2 м
    paint = rgb(64, 92, 72)
    n = fbm(10, 2.3) + fbm(40, 1.6) * 0.25
    peel = smoothstep(1.9, 2.05, n)
    edge = smoothstep(1.7, 1.9, n) - peel
    drips = smoothstep(1.2, 2.2, fbm(14, 2.0, aniso=(1.0, 0.05))) * 0.5
    concrete = mix(rgb(120, 118, 110), rgb(150, 146, 138), np.clip(0.5 + fbm(30, 1.5) * 0.3, 0, 1))
    coat = paint * (0.9 + 0.1 * np.clip(0.5 + fbm(6, 2.0) * 0.4, 0, 1))[..., None]
    color = mix(coat, coat * 0.75, drips)
    color = mix(color, rgb(170, 175, 160), np.clip(edge, 0, 1) * 0.5)
    color = mix(color, concrete, peel)
    color = grime(color, 0.3)
    height = (1 - peel) * 0.08 + drips * 0.04 + fbm(90, 1.0) * 0.01
    save("BK_WallPaint", color, height, 0.55 * (1 - peel) + 0.08, strength=5.0)


def army_paint(name, paint):
    # Краска по стали (пульт, дверь, шкаф, огнетушитель): «апельсиновая корка», царапины, сколы до грунта и металла; плитка 1 м
    peel_n = fbm(16, 2.4)
    chips = smoothstep(2.0, 2.2, peel_n)
    primer = smoothstep(1.8, 2.0, peel_n) - chips
    scratch = ridges(10, 0.02) * smoothstep(0.5, 1.5, fbm(4, 2.0)) * 0.7
    orange = fbm(260, 0.5)
    coat = paint * (0.9 + 0.1 * np.clip(0.5 + fbm(5, 2.0) * 0.4, 0, 1))[..., None]
    color = mix(coat, rgb(130, 132, 128), np.clip(scratch, 0, 1) * 0.6)
    color = mix(color, rgb(120, 70, 50), np.clip(primer, 0, 1))
    color = mix(color, rgb(150, 150, 148), chips)
    color = grime(color, 0.2)
    height = orange * 0.012 - chips * 0.05 - primer * 0.02 - scratch * 0.03
    save(name, color, height, 0.42 * (1 - chips) + 0.3 * chips, strength=4.0)


def dark_steel():
    # Тёмная воронёная сталь (рукояти, штурвал, приборы): шлифовка вдоль, потёртости; плитка 1 м
    brushed = fbm(300, 0.8, aniso=(0.02, 1.0))
    wear = smoothstep(0.8, 2.0, fbm(8, 2.0))
    color = mix(rgb(46, 48, 50), rgb(105, 106, 104), wear * 0.6)
    color = color * (0.95 + 0.05 * brushed)[..., None]
    save("BK_Steel", color, brushed * 0.01, 0.55 + 0.15 * wear, strength=3.0)


def galvanized():
    # Оцинковка воздуховодов: светлый металл с кристаллами цинка («цветы»), белёсые окислы; плитка 1 м
    cells = fbm(70, 0.3)
    spangle = np.floor((cells - cells.min()) / (np.ptp(cells) + 1e-9) * 6) / 6
    ox = smoothstep(0.6, 2.0, fbm(6, 2.0))
    color = mix(rgb(132, 136, 138), rgb(168, 172, 174), spangle)
    color = mix(color, rgb(190, 192, 186), ox * 0.5)
    color = grime(color, 0.25)
    save("BK_Galv", color, spangle * 0.01, 0.5 - 0.3 * ox, strength=3.0)


def burlap():
    # Мешковина мешков с песком: полотняное плетение, ворс, пыль; плитка 0,5 м
    n = 90
    wx = np.sin(2 * np.pi * n * xx)
    wy = np.sin(2 * np.pi * n * yy)
    checker = np.sign(np.sin(np.pi * n * xx) * np.sin(np.pi * n * yy))
    weave = np.where(checker > 0, np.abs(wy), np.abs(wx))
    thread = fbm(40, 1.4) * 0.3
    base = mix(rgb(118, 98, 68), rgb(160, 138, 98), np.clip(0.5 + fbm(4, 2.0) * 0.3 + thread * 0.3, 0, 1))
    color = base * (0.72 + 0.28 * weave)[..., None]
    color = mix(color, rgb(150, 142, 128), smoothstep(0.5, 2.0, fbm(5, 2.0)) * 0.4)
    save("BK_Burlap", color, weave * 0.2 + thread * 0.05, 0.05, strength=6.0)


def crate_wood():
    # Доски армейских ящиков под выцветшей зелёной краской, на рёбрах и пятнами краска стёрта до дерева; плитка 1 м
    planks = 8
    v = yy * planks
    gap = smoothstep(0.9, 1.0, np.abs(np.cos(np.pi * v)) ** 30)
    grain = fbm(50, 1.6, aniso=(0.03, 1.0))
    wood = mix(rgb(120, 92, 60), rgb(168, 134, 92), np.clip(0.5 + grain * 0.35, 0, 1))
    worn = smoothstep(1.6, 2.1, fbm(9, 2.2) + grain * 0.3)
    paint = rgb(86, 92, 58) * (0.88 + 0.12 * np.clip(0.5 + fbm(6, 2.0) * 0.4, 0, 1))[..., None]
    color = mix(paint, wood, worn)
    color = mix(color, color * 0.35, gap)
    color = grime(color, 0.3)
    save("BK_Wood", color, grain * 0.03 - gap * 0.25 - (1 - worn) * 0.0, 0.12, strength=5.0)


if __name__ == "__main__":
    concrete_formwork()
    concrete_floor()
    wall_paint()
    army_paint("BK_Paint", rgb(78, 94, 70))
    army_paint("BK_PaintRed", rgb(150, 38, 30))
    army_paint("BK_PaintCream", rgb(196, 188, 160))
    army_paint("BK_PaintYellow", rgb(196, 156, 44))
    galvanized()
    dark_steel()
    burlap()
    crate_wood()
