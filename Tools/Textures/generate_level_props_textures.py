# Текстуры для пропсов наполнения карты (Tools/Blender/level_props): красная и белая краска, горелый металл,
# резина, мешковина, камыш, камень, грязь, мусор, вода в луже, полосы шлагбаума, дорожные знаки и щиты с лозунгами.
# Шум, сохранение (цвет + гладкость в альфе, карта нормалей) и часть рецептов — из generate_building_textures.py.
# Запуск из корня проекта: python Tools/Textures/generate_level_props_textures.py (нужны numpy и Pillow).
# Знаки и щиты не плиточные: в Blender их грань растягивается на всю текстуру (fit-развёртка), текст рисуется
# системным шрифтом Arial (C:/Windows/Fonts) — в проект попадает только картинка.
import importlib.util
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

_spec = importlib.util.spec_from_file_location(
    "bt", os.path.join(os.path.dirname(os.path.abspath(__file__)), "generate_building_textures.py"))
bt = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bt)
bt.OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Content", "LevelProps", "Textures")
bt.rng = np.random.default_rng(1980)
S, fbm, smoothstep, mix, rgb, save, grime = bt.S, bt.fbm, bt.smoothstep, bt.mix, bt.rgb, bt.save, bt.grime
yy, xx = bt.yy, bt.xx
FONT = "C:/Windows/Fonts/arialbd.ttf"
C = 1024  # холст рисунка знаков в пикселях: координаты рисунков заданы под него, потом он масштабируется до S


def rubber():
    # Резина покрышек: почти чёрная, протектор — поперечные канавки через 4 см, пыль; плитка 0.5 м
    grooves = smoothstep(0.75, 0.95, np.abs(np.sin(np.pi * 12 * xx + np.sin(np.pi * 2 * yy) * 0.6)))
    base = mix(rgb(28, 28, 28), rgb(48, 46, 44), np.clip(0.5 + fbm(10, 2.0) * 0.3, 0, 1))
    dust = smoothstep(0.5, 2.0, fbm(6, 2.0))
    color = mix(base * (1 - 0.5 * grooves)[..., None], rgb(95, 88, 78), dust * 0.4)
    save("LP_Rubber", color, -grooves * 0.4, 0.25 * (1 - dust), strength=4.0)


def sandbag():
    # Мешки с песком: грубая мешковина (полотняное плетение), швы между мешками через 0.5 м, грязь; плитка 1 м
    weave = (np.sin(np.pi * 160 * xx) * np.sin(np.pi * 160 * yy)) * 0.5 + 0.5
    v = (yy * 4) % 1.0
    u = (xx * 2 + 0.5 * (np.floor(yy * 4) % 2)) % 1.0
    bag = np.sqrt(np.clip(1 - (2 * v - 1) ** 2, 0, 1)) * np.sqrt(np.clip(1 - (2 * u - 1) ** 4, 0, 1))
    base = mix(rgb(120, 108, 80), rgb(150, 136, 104), np.clip(0.5 + fbm(8, 2.0) * 0.3, 0, 1))
    color = base * (0.85 + 0.15 * weave)[..., None] * (0.45 + 0.55 * bag)[..., None]
    color = grime(color, 0.35)
    save("LP_Sandbag", color, bag * 0.8 + weave * 0.05, 0.05, strength=5.0)


def burnt():
    # Горелый металл: сажа, окалина, пятна рыжей ржавчины и следы облезшей краски; плитка 2 м
    rust = smoothstep(0.2, 1.4, fbm(5, 2.2))
    soot = mix(rgb(22, 20, 19), rgb(52, 46, 42), np.clip(0.5 + fbm(40, 1.2) * 0.4, 0, 1))
    rusty = mix(rgb(95, 45, 22), rgb(140, 70, 35), np.clip(0.5 + fbm(60, 1.0) * 0.4, 0, 1))
    paint = smoothstep(1.6, 2.2, fbm(7, 2.4))
    color = mix(mix(soot, rusty, rust * 0.8), rgb(70, 78, 60), paint * 0.6)
    save("LP_Burnt", color, rust * fbm(120, 1.0) * 0.08, 0.08, strength=3.0)


def reed():
    # Камыш: вертикальные стебли и листья, зелёные снизу и сухие жёлтые к верху; плитка 1 м (в высоту — весь лист)
    stalks = fbm(40, 1.0, aniso=(1.0, 0.02))
    base = mix(rgb(70, 88, 45), rgb(150, 138, 80), smoothstep(0.9, 0.1, yy))
    color = base * (0.75 + 0.25 * np.clip(0.5 + stalks * 0.4, 0, 1))[..., None]
    save("LP_Reed", color, stalks * 0.1, 0.2, strength=2.0)


def stone():
    # Камень валунов: серый гранит с вкраплениями, лишайник; плитка 2 м
    base = mix(rgb(96, 94, 90), rgb(140, 136, 128), np.clip(0.5 + fbm(6, 2.0) * 0.35, 0, 1))
    grains = fbm(300, 0.4)
    lichen = smoothstep(1.0, 1.8, fbm(10, 2.4))
    color = mix(base * (0.85 + 0.15 * grains)[..., None], rgb(110, 118, 80), lichen * 0.6)
    save("LP_Stone", color, fbm(20, 2.0) * 0.3 + grains * 0.03, 0.12, strength=4.0)


def dirt():
    # Грязь и земля (бруствер, обочина ямы): комья, лужицы и следы; плитка 2 м
    base = mix(rgb(70, 58, 44), rgb(102, 86, 66), np.clip(0.5 + fbm(8, 2.0) * 0.35, 0, 1))
    clods = fbm(80, 1.0)
    wet = smoothstep(1.0, 1.6, fbm(5, 2.2))
    color = mix(base * (0.85 + 0.15 * clods)[..., None], rgb(40, 34, 28), wet * 0.7)
    save("LP_Dirt", color, clods * 0.2 - wet * 0.2, 0.08 + 0.6 * wet, strength=4.0)


def trash():
    # Мусор: пакеты, бумага, бутылки — пёстрые пятна на грязи; плитка 2 м
    cells = fbm(5, 3.0)
    k = (np.floor((cells + 3) * 3) % 5)
    palette = [rgb(40, 40, 42), rgb(160, 150, 130), rgb(70, 90, 120), rgb(110, 60, 40), rgb(60, 80, 50)]
    color = np.zeros((S, S, 3))
    for i, c in enumerate(palette):
        color += (k == i)[..., None] * c
    color = grime(color * (0.8 + 0.2 * np.clip(0.5 + fbm(90, 1.0) * 0.4, 0, 1))[..., None], 0.4)
    save("LP_Trash", color, cells * 0.15, 0.35, strength=3.0)


def puddle():
    # Вода в луже: почти чёрная гладкая, по краю грязь (край — около границы UV через fit-развёртку)
    r = np.sqrt((xx - 0.5) ** 2 + (yy - 0.5) ** 2) * 2 + fbm(6, 2.0) * 0.08
    water = smoothstep(0.98, 0.82, r)
    color = mix(rgb(70, 60, 48), rgb(34, 38, 42), water)
    save("LP_Puddle", color, -water * 0.3, 0.1 + 0.85 * water, strength=2.0)


def stripes():
    # Красно-белые полосы шлагбаума и заграждения: по 0.5 м вдоль u; плитка 1 м
    red = (xx % 1.0) < 0.5
    paint = np.where(red[..., None], rgb(180, 36, 30), rgb(225, 225, 218))
    chips = smoothstep(1.7, 2.1, fbm(16, 2.4))
    color = grime(mix(paint, rgb(110, 70, 50), chips), 0.25)
    save("LP_Stripes", color, -chips * 0.05, 0.4 * (1 - chips) + 0.05, strength=3.0)


def text_sign(name, bg, draw_fn, rust=1.4, smooth=0.35):
    # Табличка: рисунок PIL поверх краски, затем сколы, ржавчина от краёв и грязь
    img = Image.new("RGB", (C, C), tuple(int(c * 255) for c in bg))
    draw_fn(ImageDraw.Draw(img))
    color = np.asarray(img.resize((S, S), Image.LANCZOS)).astype(float) / 255.0
    edge = np.maximum(np.abs(xx - 0.5), np.abs(yy - 0.5)) * 2
    rust_mask = smoothstep(rust, rust + 0.5, fbm(6, 2.2) + smoothstep(0.85, 1.0, edge) * 1.5)
    chips = smoothstep(1.9, 2.2, fbm(18, 2.4))
    color = mix(mix(color, rgb(120, 122, 120), chips), rgb(120, 66, 36), rust_mask)
    color = grime(color, 0.3)
    save(name, color, rust_mask * 0.05 - chips * 0.03, smooth * (1 - rust_mask) + 0.05, strength=2.0)


def centered(d, text, y, size, fill):
    # Шрифт уменьшается, пока строка не влезет в поле с отступом от рамки
    font = ImageFont.truetype(FONT, size)
    w = d.textlength(text, font=font)
    while w > C - 160:
        size -= 8
        font = ImageFont.truetype(FONT, size)
        w = d.textlength(text, font=font)
    d.text(((C - w) / 2, y), text, font=font, fill=fill)


def signs():
    white, red, black, blue = (232, 232, 226), (190, 30, 28), (25, 25, 25), (30, 70, 150)

    def no_entry(d):  # «Въезд запрещён» — «кирпич»
        d.ellipse((20, 20, C - 20, C - 20), fill=red)
        d.rectangle((150, C / 2 - 90, C - 150, C / 2 + 90), fill=white)
    text_sign("LP_Sign_NoEntry", (0.85, 0.85, 0.82), no_entry)

    def danger(d):  # «Прочие опасности»: красный треугольник, восклицательный знак
        d.polygon([(C / 2, 30), (C - 20, C - 80), (20, C - 80)], fill=red)
        d.polygon([(C / 2, 190), (C - 170, C - 160), (170, C - 160)], fill=white)
        d.rectangle((C / 2 - 30, 380, C / 2 + 30, 700), fill=black)
        d.ellipse((C / 2 - 38, 740, C / 2 + 38, 816), fill=black)
    text_sign("LP_Sign_Danger", (0.85, 0.85, 0.82), danger)

    def city(d):  # Синий указатель направления (под щит 2.4 × 1.2 м — шрифт по высоте растянут)
        d.rectangle((24, 24, C - 24, C - 24), outline=white, width=18)
        centered(d, "ЗАРЕЧЬЕ", 170, 190, white)
        centered(d, "ПРОМЗОНА  →", 560, 150, white)
    text_sign("LP_Sign_City", (0.12, 0.27, 0.58), city)

    def closed(d):  # «Проезд закрыт»
        d.rectangle((24, 24, C - 24, C - 24), outline=red, width=40)
        centered(d, "ПРОЕЗД", 220, 230, red)
        centered(d, "ЗАКРЫТ", 540, 230, red)
    text_sign("LP_Sign_Closed", (0.9, 0.9, 0.86), closed)

    def dk(d):  # Вывеска Дома культуры (щит 12 × 1.5 м — буквы растянуты по горизонтали)
        centered(d, "ДК", 120, 700, (235, 225, 200))
    text_sign("LP_Sign_DK", (0.55, 0.12, 0.1), dk, rust=2.0)


def slogans():
    # Агитационные щиты 6 × 3 м: красное поле, белые буквы в две строки
    for i, (l1, l2) in enumerate((("СЛАВА", "ТРУДУ!"), ("МИРУ —", "МИР!"), ("ПЯТИЛЕТКУ —", "В 4 ГОДА!"))):
        def draw(d, l1=l1, l2=l2):
            d.rectangle((30, 30, C - 30, C - 30), outline=(230, 200, 120), width=22)
            centered(d, l1, 190, 230 if len(l1) < 8 else 150, (240, 236, 225))
            centered(d, l2, 560, 230 if len(l2) < 8 else 160, (240, 236, 225))
        text_sign("LP_Slogan_%d" % i, (0.62, 0.1, 0.08), draw, rust=1.2, smooth=0.2)


if __name__ == "__main__":
    bt.painted_metal("LP_PaintRed", rgb(165, 40, 32), rust=1.0)
    bt.painted_metal("LP_PaintWhite", rgb(210, 210, 202), rust=1.1)
    bt.painted_metal("LP_PaintGreen", rgb(70, 92, 62), rust=0.9)
    rubber()
    sandbag()
    burnt()
    reed()
    stone()
    dirt()
    trash()
    puddle()
    stripes()
    signs()
    slogans()
