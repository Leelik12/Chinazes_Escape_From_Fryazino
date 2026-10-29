"""Текстура подпалины от взрыва на асфальте: Assets/Resources/Fx/Scorch.png (RGBA 256x256).

Тёмное пятно с рваным краем и лучами от центра; альфа плавно уходит в ноль к краю.
Запуск из корня проекта: python Tools/Textures/generate_scorch.py
"""
import numpy as np
from PIL import Image

N = 256
rng = np.random.default_rng(7)
y, x = np.mgrid[0:N, 0:N] / (N - 1) * 2 - 1
r = np.hypot(x, y)
a = np.arctan2(y, x)


def fbm(octaves=5):
    # Сумма октав сглаженного шума на решётке
    out = np.zeros((N, N))
    for o in range(octaves):
        f = 2 ** (o + 2)
        g = rng.standard_normal((f + 1, f + 1))
        u = (x + 1) / 2 * f
        v = (y + 1) / 2 * f
        i = np.floor(u).astype(int).clip(0, f - 1)
        j = np.floor(v).astype(int).clip(0, f - 1)
        fu = u - i
        fv = v - j
        fu = fu * fu * (3 - 2 * fu)
        fv = fv * fv * (3 - 2 * fv)
        n = (g[j, i] * (1 - fu) * (1 - fv) + g[j, i + 1] * fu * (1 - fv)
             + g[j + 1, i] * (1 - fu) * fv + g[j + 1, i + 1] * fu * fv)
        out += n / 2 ** o
    return out


n = fbm()
rays = 0.5 + 0.5 * np.sin(a * 13 + n * 2) * np.sin(a * 7 - 1.3)
edge = 0.75 + 0.15 * n + 0.1 * rays
alpha = np.clip((edge - r) / 0.35, 0, 1) ** 1.4
alpha *= np.clip(0.85 + 0.25 * fbm(4), 0, 1)
lum = np.clip(0.06 + 0.06 * n + 0.08 * r, 0, 0.3)
rgb = np.stack([lum * 1.05, lum * 0.95, lum * 0.85], -1)
img = np.dstack([rgb, alpha]) * 255
Image.fromarray(img.clip(0, 255).astype(np.uint8), 'RGBA').save('Assets/Resources/Fx/Scorch.png')
