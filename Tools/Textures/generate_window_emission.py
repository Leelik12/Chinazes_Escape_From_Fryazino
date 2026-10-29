# Карты свечения окон панельных домов (russian_buildings): на текстуре фасада ищутся тёмные стёкла
# (тёмные малонасыщенные пятна размером со стекло), соседние стёкла склеиваются в окно, и случайная часть окон
# «зажигается»: тёплый свет ламп, реже холодный от телевизора. Три варианта на текстуру с разным набором окон —
# BuildNightLighting.cs раздаёт их домам, чтобы соседние дома не светились одинаково.
# Результат: Assets/Content/Buildings/NightWindows/<текстура>_win<k>.png (чёрный фон, RGB).
# Запуск из корня проекта: python Tools/Textures/generate_window_emission.py (нужны numpy, scipy и Pillow).
import glob
import os
import re
import zlib
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
SRC = os.path.join(ROOT, "Assets", "Content", "Buildings", "russian_buildings", "textures")
OUT = os.path.join(ROOT, "Assets", "Content", "Buildings", "NightWindows")
VARIANTS = 3
LIT_SHARE = 0.28
# Тёплые лампы (накаливания, абажуры) и холодный свет телевизора
WARM = [(1.0, 0.72, 0.38), (1.0, 0.62, 0.3), (1.0, 0.82, 0.55), (0.95, 0.55, 0.25)]
COLD = [(0.55, 0.7, 1.0), (0.7, 0.8, 1.0)]


def process(path):
    name = os.path.splitext(os.path.basename(path))[0]
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(float) / 255
    h, w, _ = rgb.shape
    scale = w / 1024
    lum = rgb @ np.array([0.299, 0.587, 0.114])
    sat = rgb.max(2) - rgb.min(2)
    glass = (lum < 0.42) & (sat < 0.16)
    # На LOD-текстурах окна по 10–12 пикселей и стёкла в 2–3 пикселя: их не чистим и порог размера ниже
    fine = "LOD" in name
    if not fine:
        glass = ndimage.binary_opening(glass, iterations=1)
    panes, n = ndimage.label(glass)
    if n == 0:
        return 0
    sizes = ndimage.sum(np.ones_like(lum), panes, range(1, n + 1))
    boxes = ndimage.find_objects(panes)
    keep = np.zeros(n + 1, bool)
    for i, (box, size) in enumerate(zip(boxes, sizes), start=1):
        bh = box[0].stop - box[0].start
        bw = box[1].stop - box[1].start
        # Стекло — небольшое и плотное пятно; полосы швов, тени и крупные тёмные поля — нет
        # (швы, грязь и детали крыши — мелкие или тонкие пятна)
        small = (min(bh, bw) < 2 or size < 5) if fine else (min(bh, bw) < max(3, 5 * scale) or size < 50 * scale * scale)
        if not small and size <= 2600 * scale * scale and size > 0.6 * bh * bw and bh < 90 * scale and bw < 90 * scale                 and 0.2 < bw / bh < 5:
            keep[i] = True
    glass = keep[panes]
    # У окна вокруг стекла светлая рама; полосы на крыше и решётки вентиляции окружены тёмным
    panes, n = ndimage.label(glass)
    if n == 0:
        return 0
    ring = ndimage.binary_dilation(glass, iterations=max(2, int(3 * scale))) & ~glass
    ring_lum = ndimage.mean(lum, ndimage.grey_dilation(panes, size=2 * max(2, int(3 * scale)) + 1) * ring, range(1, n + 1))
    glass = np.concatenate([[False], np.asarray(ring_lum) > 0.5])[panes]
    # Стёкла одного окна (через переплёт) — одно окно
    windows, m = ndimage.label(ndimage.binary_dilation(glass, iterations=max(2, int(5 * scale))))
    count = 0
    for k in range(VARIANTS):
        rng = np.random.default_rng(zlib.crc32(("%s_%d" % (name, k)).encode()))
        color = np.zeros((m + 1, 3))
        for i in range(1, m + 1):
            if rng.random() < LIT_SHARE:
                base = COLD[rng.integers(len(COLD))] if rng.random() < 0.15 else WARM[rng.integers(len(WARM))]
                color[i] = np.array(base) * rng.uniform(0.45, 1.0)
        em = color[windows] * glass[..., None]
        # Свет за стеклом неровный: ярче к центру окна, переплёт и рама тёмные
        em *= (0.75 + 0.25 * ndimage.gaussian_filter(glass.astype(float), 2 * scale))[..., None]
        os.makedirs(OUT, exist_ok=True)
        Image.fromarray((np.clip(em, 0, 1) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, "%s_win%d.png" % (name, k)))
        count += 1
    return m


if __name__ == "__main__":
    for path in sorted(glob.glob(os.path.join(SRC, "rus_build_*.png"))):
        if re.search(r"_(h|n|s)\.png$", path) or "dec" in os.path.basename(path):
            continue
        print(os.path.basename(path), "windows:", process(path))
