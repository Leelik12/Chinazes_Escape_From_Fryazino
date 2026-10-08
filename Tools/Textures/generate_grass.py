# Генерация текстур травы для деталей террейна: пучки травинок на прозрачном фоне.
# Всё рисуется процедурно, поэтому у текстур нет сторонних лицензий.
# Запуск из корня проекта: python Tools/Textures/generate_grass.py (нужны numpy и Pillow).
# Травинки светлые и почти серые: оттенок задают цвета Healthy/Dry у прототипа деталей в Unity.
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Content", "Nature", "Textures")
SIZE = 256
SS = 4  # суперсэмплинг: рисуем в 4 раза крупнее и уменьшаем, чтобы края травинок были гладкими
rng = np.random.default_rng(1986)


def blade(draw, x0, h, lean, width, color):
    # Травинка — изогнутый клин от основания к острому кончику
    n = 12
    pts_l, pts_r = [], []
    for i in range(n + 1):
        t = i / n
        x = x0 + lean * t ** 1.8
        y = SIZE * SS - t * h
        w = width * (1.0 - t) ** 0.8
        pts_l.append((x - w / 2, y))
        pts_r.append((x + w / 2, y))
    draw.polygon(pts_l + pts_r[::-1], fill=color)


def tuft(name, blades, height, width, base_rgb, spread, seed_heads=0):
    img = Image.new("RGBA", (SIZE * SS, SIZE * SS), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    for _ in range(blades):
        x0 = SIZE * SS * (0.5 + rng.normal(0, spread))
        h = SIZE * SS * height * rng.uniform(0.45, 1.0)
        lean = SIZE * SS * rng.normal(0, 0.12)
        shade = rng.uniform(0.7, 1.05)
        c = tuple(int(min(255, v * shade)) for v in base_rgb) + (255,)
        blade(draw, x0, h, lean, SS * width * rng.uniform(0.7, 1.3), c)
    for _ in range(seed_heads):
        # Метёлки сорняков: тонкий стебель с вытянутой головкой
        x0 = SIZE * SS * (0.5 + rng.normal(0, spread * 0.8))
        h = SIZE * SS * rng.uniform(0.75, 0.97)
        lean = SIZE * SS * rng.normal(0, 0.06)
        top = (x0 + lean, SIZE * SS - h)
        draw.line([(x0, SIZE * SS), top], fill=(150, 140, 110, 255), width=SS)
        draw.ellipse([top[0] - 3 * SS, top[1] - 1 * SS, top[0] + 3 * SS, top[1] + 16 * SS], fill=(170, 150, 105, 255))
    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    # Темнее к основанию — трава затеняет сама себя
    a = np.asarray(img).astype(float)
    grad = np.linspace(1.0, 0.55, SIZE)[::-1][:, None]
    a[..., :3] *= grad[..., None]
    img = Image.fromarray(a.clip(0, 255).astype(np.uint8), "RGBA")
    # Прозрачные пиксели берут цвет соседей, иначе при мипмапах по краям появляется тёмная кайма
    rgb = img.convert("RGB").filter(ImageFilter.MaxFilter(5))
    alpha = img.split()[3]
    out = Image.composite(img.convert("RGB"), rgb, alpha)
    out.putalpha(alpha)
    os.makedirs(OUT, exist_ok=True)
    out.save(os.path.join(OUT, name + ".png"))
    print("saved", name)


if __name__ == "__main__":
    tuft("Grass_Tuft", blades=70, height=0.9, width=7, base_rgb=(205, 215, 185), spread=0.12)
    tuft("Grass_Dry", blades=55, height=0.75, width=6, base_rgb=(225, 210, 165), spread=0.14)
    tuft("Weeds", blades=25, height=0.6, width=9, base_rgb=(190, 205, 170), spread=0.1, seed_heads=6)
