# Генерация бесшовных текстур для частного сектора: бревно, доски, кирпич, шифер, профлист, штукатурка,
# наличники, стекло и крашеный металл. Всё процедурное, поэтому у текстур нет сторонних лицензий.
# Запуск из корня проекта: python Tools/Textures/generate_building_textures.py (нужны numpy и Pillow).
# На каждую текстуру пишутся два файла: <имя>.png — цвет, в альфе гладкость (Smoothness из Albedo Alpha в URP Lit),
# и <имя>_Normal.png — карта нормалей (OpenGL, Y вверх), посчитанная из карты высот.
# Размер плитки в метрах указан у каждой текстуры: под него в Blender строится развёртка (Tools/Blender/private_sector).
import os
import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "PrivateSector", "Textures")
S = 1024
rng = np.random.default_rng(1961)
yy, xx = np.mgrid[0:S, 0:S] / S  # 0..1, y вниз


def fbm(cycles=8.0, beta=2.0, aniso=(1.0, 1.0)):
    # Бесшовный шум с убывающим спектром: периодичен по построению, так как строится через БПФ.
    # cycles — частота, ниже которой спектр плоский; aniso растягивает шум по осям (волокна дерева)
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


def save(name, color, height, smooth, strength=4.0):
    os.makedirs(OUT, exist_ok=True)
    col = np.clip(color, 0, 1)
    a = np.clip(np.broadcast_to(smooth, (S, S)), 0, 1)
    img = np.concatenate([col, a[..., None]], axis=2)
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"))
    # Нормали: производные высоты с переносом через край (текстура бесшовная)
    dx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * strength
    dy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * strength
    n = np.stack([-dx, dy, np.ones_like(dx)], axis=2)  # y изображения вниз, а в OpenGL-нормалях Y вверх
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, name + "_Normal.png"))
    print("saved", name)


def grime(base, amount=0.35):
    # Грязь и потёки: крупные пятна темнее, потёки тянутся вниз
    stains = smoothstep(0.3, 1.8, fbm(3, 2.2))
    streaks = smoothstep(0.5, 2.0, fbm(4, 2.0, aniso=(1.0, 0.08)))
    k = 1 - amount * np.clip(stains * 0.7 + streaks * 0.5, 0, 1)
    return base * k[..., None]


def logs():
    # Сруб: 8 брёвен на плитку 2 м (диаметр 0.25 м), торцы не нужны — только боковая поверхность
    rows = 8
    v = (yy * rows) % 1.0
    profile = np.sqrt(np.clip(1 - (2 * v - 1) ** 2, 0, 1))  # полукруг бревна
    gap = smoothstep(0.0, 0.07, v) * smoothstep(0.0, 0.07, 1 - v)  # тёмный паз с паклей между брёвнами
    grain = fbm(30, 1.5, aniso=(0.04, 1.0))
    knots = smoothstep(2.6, 3.4, fbm(20, 2.5))
    cracks = smoothstep(2.2, 2.8, np.abs(fbm(40, 1.2, aniso=(0.02, 1.0))))
    wood = mix(rgb(118, 96, 74), rgb(150, 132, 112), np.clip(0.5 + grain * 0.25, 0, 1))
    wood = mix(wood, rgb(95, 92, 88), smoothstep(-0.5, 1.5, fbm(5, 2.0)) * 0.6)  # серое выветривание
    wood *= (0.55 + 0.45 * profile)[..., None]
    wood = mix(wood, rgb(60, 45, 32), knots * 0.8)
    wood = mix(wood, rgb(35, 28, 22), cracks * 0.7)
    oakum = mix(rgb(70, 62, 50), rgb(40, 34, 28), np.clip(0.5 + fbm(60, 1.0) * 0.3, 0, 1))
    color = mix(oakum, wood, gap)
    color = grime(color, 0.25)
    height = profile * gap * 0.9 + grain * 0.02 - cracks * 0.15
    save("PS_Logs", color, height, 0.12 + 0.05 * profile, strength=6.0)


def planks(name, paint, peel=1.0, vertical=True):
    # Обшивка доской 0.15 м (плитка 2 м — 13 досок) с облезающей краской.
    # peel — порог в сигмах шума: 1.0 — облезло ~16% краски, 0.5 — ~30%, -3 — краски нет совсем (голое дерево)
    boards = 13
    u = (xx if vertical else yy) * boards
    idx = np.floor(u)
    v = u % 1.0
    seam = smoothstep(0.0, 0.04, v) * smoothstep(0.0, 0.04, 1 - v)
    jitter = (np.sin(idx * 12.9898) * 43758.5453) % 1.0  # своя яркость у каждой доски
    grain = fbm(25, 1.4, aniso=(1.0, 0.04) if vertical else (0.04, 1.0))
    bare = mix(rgb(112, 104, 94), rgb(140, 130, 118), np.clip(0.5 + grain * 0.3, 0, 1))
    chips = smoothstep(peel - 0.15, peel + 0.15, fbm(12, 2.4, aniso=(1.0, 0.35) if vertical else (0.35, 1.0)) + grain * 0.1)
    p = np.array(paint) * (0.92 + 0.12 * jitter)[..., None]
    p = mix(p, p * 0.8, np.clip(fbm(6, 2.0) * 0.3 + 0.3, 0, 1))  # краска выгорела пятнами
    color = mix(p, bare, chips)
    color *= (0.35 + 0.65 * seam)[..., None]
    color = grime(color, 0.3)
    height = seam * 0.5 + (1 - chips) * 0.06 + grain * 0.02
    save(name, color, height, 0.18 + 0.15 * (1 - chips), strength=5.0)


def brick():
    # Кирпич 250x65 мм со швом 10 мм, перевязка в полкирпича; плитка 1 м: 4 кирпича на 13 рядов
    rows, cols = 13, 4
    r = np.floor(yy * rows)
    u = (xx * cols + 0.5 * (r % 2)) % 1.0
    v = (yy * rows) % 1.0
    mortar_u, mortar_v = 0.012 / 0.25, 0.012 / 0.077
    body = smoothstep(0, mortar_u, u) * smoothstep(0, mortar_u, 1 - u) * smoothstep(0, mortar_v, v) * smoothstep(0, mortar_v, 1 - v)
    bid = np.floor(xx * cols + 0.5 * (r % 2)) + r * 7.0
    tone = (np.sin(bid * 78.233) * 43758.5453) % 1.0
    base = mix(rgb(128, 52, 38), rgb(168, 78, 52), tone)
    base = mix(base, rgb(95, 40, 32), smoothstep(0.85, 1.0, tone))  # пережжённые кирпичи
    base *= (0.85 + 0.15 * np.clip(0.5 + fbm(80, 1.0) * 0.4, 0, 1))[..., None]
    mortar = mix(rgb(150, 145, 135), rgb(110, 106, 100), np.clip(0.5 + fbm(50, 1.2) * 0.4, 0, 1))
    color = mix(mortar, base, body)
    color = grime(color, 0.3)
    height = body * 0.6 + fbm(90, 1.0) * 0.03
    save("PS_Brick", color, height, 0.1 + 0.05 * body, strength=5.0)


def slate():
    # Шифер: волна 0.15 м поперёк ската, листы 1.0 м внахлёст; плитка 2 м
    waves = 2.0 / 0.15
    wave = 0.5 + 0.5 * np.cos(2 * np.pi * np.round(waves) * xx)
    sheet = (yy * 2.0) % 1.0
    lap = smoothstep(0.0, 0.03, sheet)  # тень от нахлёста верхнего листа
    base = mix(rgb(120, 124, 118), rgb(150, 152, 145), np.clip(0.5 + fbm(10, 2.0) * 0.3, 0, 1))
    lichen = smoothstep(1.0, 2.0, fbm(14, 2.4))
    base = mix(base, rgb(105, 110, 70), lichen * 0.7)  # мох и лишайник
    color = base * (0.7 + 0.3 * wave)[..., None] * (0.6 + 0.4 * lap)[..., None]
    color = grime(color, 0.35)
    height = wave * 0.6 + lap * 0.3
    save("PS_Slate", color, height, 0.15 + 0.1 * wave, strength=3.0)


def metal_roof(name, paint=None):
    # Профлист / кровельное железо: волна 0.1 м, ржавчина пятнами и потёками; плитка 2 м
    waves = np.round(2.0 / 0.1)
    wave = 0.5 + 0.5 * np.cos(2 * np.pi * waves * xx)
    rust_mask = smoothstep(0.9, 1.8, fbm(6, 2.3) + smoothstep(0.3, 2.0, fbm(5, 2.0, aniso=(1.0, 0.1))) * 0.8)
    steel = rgb(140, 142, 140) if paint is None else np.array(paint)
    steel = mix(steel, steel * 0.85, np.clip(fbm(8, 2.0) * 0.3 + 0.3, 0, 1))
    rust = mix(rgb(110, 55, 30), rgb(150, 85, 45), np.clip(0.5 + fbm(60, 1.2) * 0.4, 0, 1))
    color = mix(steel, rust, rust_mask) * (0.75 + 0.25 * wave)[..., None]
    color = grime(color, 0.2)
    height = wave * 0.5 + rust_mask * fbm(100, 1.0) * 0.05
    save(name, color, height, 0.45 * (1 - rust_mask) + 0.08, strength=3.0)


def plaster():
    # Штукатурка и бетон фундамента: шероховатость, трещины, сырость снизу; плитка 2 м
    base = mix(rgb(150, 146, 138), rgb(175, 170, 160), np.clip(0.5 + fbm(8, 2.0) * 0.3, 0, 1))
    pits = fbm(150, 0.8)
    cracks = smoothstep(2.6, 3.0, np.abs(fbm(12, 1.0)))
    damp = smoothstep(0.55, 1.0, yy + fbm(6, 2.0) * 0.08)  # низ темнее от сырости
    color = base * (0.92 + 0.08 * pits)[..., None]
    color = mix(color, rgb(80, 76, 70), cracks * 0.8)
    color = mix(color, color * 0.6, damp * 0.6)
    color = grime(color, 0.3)
    save("PS_Plaster", color, pits * 0.05 - cracks * 0.2, 0.12, strength=4.0)


def trim():
    # Наличники и рамы: белая краска по дереву, облупленная по краям; плитка 1 м
    grain = fbm(30, 1.4, aniso=(1.0, 0.05))
    chips = smoothstep(1.2, 1.45, fbm(16, 2.4))
    paint = mix(rgb(220, 222, 215), rgb(195, 198, 190), np.clip(fbm(6, 2.0) * 0.3 + 0.3, 0, 1))
    bare = mix(rgb(105, 98, 88), rgb(135, 125, 112), np.clip(0.5 + grain * 0.3, 0, 1))
    color = grime(mix(paint, bare, chips), 0.25)
    save("PS_Trim", color, (1 - chips) * 0.08 + grain * 0.02, 0.25 * (1 - chips) + 0.1, strength=4.0)


def glass():
    # Старое стекло: тёмное с отражением неба, пыль и разводы; плитка 1 м
    dust = smoothstep(-0.5, 1.5, fbm(5, 2.0)) * 0.5 + smoothstep(0.4, 1.0, yy) * 0.3
    color = mix(rgb(30, 38, 44), rgb(95, 95, 88), np.clip(dust, 0, 1))
    save("PS_Glass", color, np.zeros((S, S)), 0.9 - 0.6 * np.clip(dust, 0, 1), strength=1.0)


def concrete():
    # Бетон опор, плит и павильонов: поры, швы опалубки через 0.5 м, сколы и потёки; плитка 2 м
    base = mix(rgb(128, 126, 120), rgb(160, 157, 150), np.clip(0.5 + fbm(6, 2.0) * 0.3, 0, 1))
    pores = fbm(200, 0.6)
    seams = smoothstep(0.985, 1.0, np.cos(2 * np.pi * 4 * yy) * 0.5 + 0.5)
    chips = smoothstep(1.6, 2.0, fbm(20, 2.2))
    color = base * (0.9 + 0.1 * pores)[..., None]
    color = mix(color, color * 0.7, np.clip(seams + chips * 0.6, 0, 1))
    color = grime(color, 0.4)
    save("PS_Concrete", color, pores * 0.04 - seams * 0.1 - chips * 0.15, 0.1, strength=4.0)


def painted_metal(name, paint, rust=1.0):
    # Гладкий листовой металл (ларьки, остановки, ворота) под краской: сколы, ржавчина от краёв пятнами; плитка 2 м
    # rust — порог в сигмах шума: чем больше, тем меньше ржавчины
    rust_mask = smoothstep(rust, rust + 0.6, fbm(5, 2.3) + smoothstep(0.3, 2.0, fbm(4, 2.0, aniso=(1.0, 0.1))) * 0.7)
    chips = smoothstep(1.9, 2.2, fbm(18, 2.4)) * (1 - rust_mask)
    coat = np.array(paint) * (0.88 + 0.12 * np.clip(fbm(8, 2.0) * 0.4 + 0.5, 0, 1))[..., None]
    rusty = mix(rgb(105, 52, 28), rgb(150, 86, 46), np.clip(0.5 + fbm(60, 1.2) * 0.4, 0, 1))
    color = mix(mix(coat, rgb(120, 122, 120), chips), rusty, rust_mask)
    color = grime(color, 0.25)
    height = rust_mask * fbm(120, 1.0) * 0.06 - chips * 0.03
    save(name, color, height, 0.4 * (1 - rust_mask) + 0.06, strength=3.0)


if __name__ == "__main__":
    logs()
    planks("PS_Planks_Green", rgb(70, 112, 78), peel=0.8)
    planks("PS_Planks_Blue", rgb(78, 108, 140), peel=1.0)
    planks("PS_Planks_Raw", rgb(120, 112, 100), peel=-3.0)
    planks("PS_Fence", rgb(118, 108, 96), peel=-3.0)
    brick()
    slate()
    metal_roof("PS_MetalRoof")
    metal_roof("PS_MetalGreen", paint=rgb(62, 92, 66))
    plaster()
    trim()
    glass()
    concrete()
    painted_metal("PS_PaintBlue", rgb(60, 98, 140), rust=1.1)
    painted_metal("PS_PaintYellow", rgb(196, 160, 62), rust=1.2)
    painted_metal("PS_Rust", rgb(90, 80, 70), rust=-0.6)
