# Генерация атласа трещин лобового стекла 2×2 (1024×1024, RGBA): в каждой клетке след пули — дырка с
# матовым ореолом, лучевые трещины с изломами и ответвлениями и дуги «паутины» между ними. Цвет белый,
# рисунок — в альфе. Рисуется в двойном разрешении и уменьшается — линии сглажены.
# Запуск из корня проекта: python Tools/Textures/generate_glass_cracks.py (нужны numpy и Pillow).
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "Content", "Effects", "GlassCracks.png")
CELL = 1024  # клетка в двойном разрешении
rng = np.random.default_rng(1990)


def jagged(draw, start, angle, length, width, alpha, depth):
    # Трещина с изломами: идёт отрезками, слегка меняя направление; иногда ветвится
    x, y = start
    travelled = 0.0
    while travelled < length:
        step = rng.uniform(0.03, 0.07) * CELL
        angle += rng.normal(0, 0.18)
        nx, ny = x + math.cos(angle) * step, y + math.sin(angle) * step
        fade = 1.0 - travelled / length
        w = max(1, int(round(width * (0.4 + 0.6 * fade))))
        draw.line([(x, y), (nx, ny)], fill=int(alpha * (0.35 + 0.65 * fade)), width=w)
        if depth < 2 and rng.random() < 0.12:
            jagged(draw, (nx, ny), angle + rng.choice([-1, 1]) * rng.uniform(0.4, 0.9),
                   (length - travelled) * rng.uniform(0.3, 0.6), w * 0.7, alpha * 0.8, depth + 1)
        x, y = nx, ny
        travelled += step


def cell(seed_angle):
    image = Image.new("L", (CELL, CELL), 0)
    draw = ImageDraw.Draw(image)
    c = CELL / 2 + rng.uniform(-0.04, 0.04) * CELL
    center = (c, CELL / 2 + rng.uniform(-0.04, 0.04) * CELL)

    # Лучи
    rays = int(rng.integers(9, 15))
    angles = np.sort(seed_angle + np.linspace(0, 2 * np.pi, rays, endpoint=False) + rng.normal(0, 0.15, rays))
    lengths = rng.uniform(0.28, 0.47, rays) * CELL
    for a, length in zip(angles, lengths):
        jagged(draw, center, a, length, rng.uniform(3, 6), 235, 0)

    # Дуги паутины между соседними лучами на нескольких радиусах
    for radius in sorted(rng.uniform(0.06, 0.3, int(rng.integers(2, 5))) * CELL):
        for i in range(rays):
            if rng.random() < 0.35:
                continue
            a0, a1 = angles[i], angles[(i + 1) % rays] + (2 * np.pi if i == rays - 1 else 0)
            r0 = radius * rng.uniform(0.85, 1.15)
            r1 = radius * rng.uniform(0.85, 1.15)
            points = []
            for k in range(6):
                f = k / 5
                a = a0 + (a1 - a0) * f
                r = r0 + (r1 - r0) * f + rng.normal(0, 0.006 * CELL)
                points.append((center[0] + math.cos(a) * r, center[1] + math.sin(a) * r))
            draw.line(points, fill=int(rng.uniform(140, 210)), width=int(rng.integers(2, 4)))

    # Матовый ореол вокруг дырки и сама дырка с крошкой стекла по краю
    halo = Image.new("L", (CELL, CELL), 0)
    hd = ImageDraw.Draw(halo)
    r = rng.uniform(0.045, 0.07) * CELL
    hd.ellipse([center[0] - r, center[1] - r, center[0] + r, center[1] + r], fill=150)
    halo = halo.filter(ImageFilter.GaussianBlur(CELL * 0.025))
    image = Image.fromarray(np.maximum(np.asarray(image), np.asarray(halo)).astype(np.uint8))
    draw = ImageDraw.Draw(image)
    hole = rng.uniform(0.012, 0.018) * CELL
    draw.ellipse([center[0] - hole * 1.6, center[1] - hole * 1.6, center[0] + hole * 1.6, center[1] + hole * 1.6], fill=245)
    draw.ellipse([center[0] - hole, center[1] - hole, center[0] + hole, center[1] + hole], fill=40)
    return image


def main():
    atlas = Image.new("L", (CELL * 2, CELL * 2), 0)
    for i in range(4):
        atlas.paste(cell(rng.uniform(0, 2 * np.pi)), ((i % 2) * CELL, (i // 2) * CELL))
    alpha = atlas.resize((1024, 1024), Image.LANCZOS)
    a = np.asarray(alpha).astype(np.float32) / 255.0
    rgb = np.full((1024, 1024, 3), 235, np.uint8)
    rgba = np.dstack([rgb, (np.clip(a, 0, 1) * 255).astype(np.uint8)])
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(OUT)
    print("saved", OUT)


if __name__ == "__main__":
    main()
