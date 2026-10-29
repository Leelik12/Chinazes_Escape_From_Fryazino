# Генерация бесшовной текстуры капель дождя на лобовом стекле (1024×1024): RainDrops.png (RGBA — капли светлые
# по краю и с бликом, прозрачные между собой; рисунок в альфе) и RainDrops_Normal.png (выпуклость капель, в ней
# преломляется свет фар и фонарей). Капли разного размера, крупные слегка вытянуты вниз, есть подтёки.
# Слой показывает Assets/Scripts/Car/WindshieldRain.cs, дворники стирают его.
# Запуск из корня проекта: python Tools/Textures/generate_rain_drops.py (нужны numpy и Pillow).
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "Content", "Effects")
S = 1024
rng = np.random.default_rng(2024)

height = np.zeros((S, S), np.float32)
yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)


def drop(cx, cy, r, stretch):
    # Капля — полусфера, крупная вытянута вниз (stretch); рисуется с переносом через края, чтобы плитка была бесшовной
    global height
    x0, x1 = int(cx - r * 1.5), int(cx + r * 1.5) + 1
    y0, y1 = int(cy - r * 1.5 * stretch), int(cy + r * 1.5 * stretch) + 1
    for oy in range(y0, y1):
        py = oy % S
        dy = (oy - cy) / (r * stretch)
        for ox in range(x0, x1):
            dx = (ox - cx) / r
            d2 = dx * dx + dy * dy
            if d2 < 1.0:
                px = ox % S
                height[py, px] = max(height[py, px], np.sqrt(1.0 - d2) * r)


# Мелкая морось, средние капли и несколько крупных с подтёками
for count, rmin, rmax in ((2600, 1.2, 3.0), (650, 3.0, 7.0), (90, 7.0, 13.0)):
    for _ in range(count):
        cx, cy = rng.uniform(0, S), rng.uniform(0, S)
        r = rng.uniform(rmin, rmax)
        stretch = 1.0 + (r / 13.0) * rng.uniform(0.1, 0.5)
        drop(cx, cy, r, stretch)
        if r > 8 and rng.random() < 0.5:
            # Подтёк: цепочка мелких капель над крупной
            for k in range(1, int(rng.integers(4, 9))):
                drop(cx + rng.normal(0, 0.8), cy - k * r * 0.55, r * max(0.18, 0.45 - k * 0.04), 1.2)

mask = (height > 0).astype(np.float32)
h = height / max(1e-3, height.max())
# Нормаль по градиенту высоты (Unity, OpenGL: зелёный вверх)
gy, gx = np.gradient(height)
nx, ny, nz = -gx, gy, np.ones_like(gx) * 1.6
length = np.sqrt(nx * nx + ny * ny + nz * nz)
normal = np.stack([(nx / length) * 0.5 + 0.5, (ny / length) * 0.5 + 0.5, (nz / length) * 0.5 + 0.5], -1)
normal[mask == 0] = (0.5, 0.5, 1.0)

# Цвет: край капли светлее (преломлённый свет), середина почти прозрачная, блик сверху слева
edge = mask * np.clip(1.0 - h * 2.2, 0, 1)
gloss = mask * np.clip((-gx - gy) * 0.35, 0, 1)
alpha = np.clip(0.18 * mask + 0.55 * edge + 0.6 * gloss, 0, 1)
shade = np.clip(0.75 + 0.25 * gloss - 0.15 * h, 0, 1)
rgba = np.stack([shade, shade, shade * 1.02, alpha], -1)

os.makedirs(OUT, exist_ok=True)
Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "RainDrops.png"))
Image.fromarray((np.clip(normal, 0, 1) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, "RainDrops_Normal.png"))
print("saved", OUT)
