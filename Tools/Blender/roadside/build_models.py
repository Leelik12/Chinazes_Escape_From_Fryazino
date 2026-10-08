# Модели гаражного кооператива и придорожных объектов: ряды гаражей, остановка, ларёк, будка блокпоста,
# опора ЛЭП, фонарь и бетонный блок.
# Запуск в Blender 5.1 (вкладка Scripting или через MCP): строит всё с нуля в отдельной сцене «Roadside»
# и экспортирует каждую модель в Assets/Models/Roadside/<имя>.fbx. Сцена пользователя не трогается.
# Построитель мешей, развёртка и экспорт — из Tools/Blender/private_sector/build_models.py (те же соглашения:
# метры, низ на z = 0, перед смотрит в −Y, материалы PS_* из Assets/Content/PrivateSector).
import bpy
import importlib.util
import math
import os
import random

REPO = os.environ.get("RACING_REPO", r"D:\GitRepos\RacingProject")
OUT = os.path.join(REPO, "Assets", "Models", "Roadside")

_spec = importlib.util.spec_from_file_location(
    "ps_build", os.path.join(REPO, "Tools", "Blender", "private_sector", "build_models.py"))
ps = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ps)
Builder = ps.Builder

# Новые материалы (текстуры — generate_building_textures.py)
ps.TILE.update({"PS_Concrete": 2.0, "PS_PaintBlue": 2.0, "PS_PaintYellow": 2.0, "PS_Rust": 2.0})
ps.PREVIEW.update({"PS_Concrete": (0.55, 0.54, 0.51), "PS_PaintBlue": (0.24, 0.38, 0.55),
                   "PS_PaintYellow": (0.77, 0.63, 0.24), "PS_Rust": (0.45, 0.25, 0.14)})

GATES = ["PS_MetalGreen", "PS_PaintBlue", "PS_Rust", "PS_MetalGreen", "PS_PaintYellow", "PS_Rust"]


def garage_row(col, name, units, seed, w=3.6, d=6.2, h=2.6):
    # Ряд кирпичных гаражей под общей плоской крышей; ворота разного цвета, часть открыта (тёмный бокс внутри)
    rnd = random.Random(seed)
    b = Builder(name)
    length = units * w
    x0, x1, y0, y1 = -length / 2, length / 2, -d / 2, d / 2
    b.box((x0, y0, -0.3), (x1, y1, h), "PS_Brick", skip=("-z", "+z"))
    # Крыша — бетонная плита с рубероидом, свес над воротами; бордюр по краю
    b.slab((x0 - 0.1, y0 - 0.35, h), (x1 + 0.1, y0 - 0.35, h), (x1 + 0.1, y1 + 0.1, h - 0.15), (x0 - 0.1, y1 + 0.1, h - 0.15),
           0.18, "PS_Slate", "PS_Concrete")
    gw, gh = 2.7, 2.2
    for i in range(units):
        cx = x0 + w * (i + 0.5)
        b.at((cx, y0, 0), (0, -1, 0))
        if i > 0:
            # Пилястра между боксами
            b.box((-w / 2 - 0.12, 0, -0.3), (-w / 2 + 0.12, 0.12, h), "PS_Brick", skip=("-y",))
        b.box((-gw / 2 - 0.08, 0, 0), (gw / 2 + 0.08, 0.04, gh + 0.08), "PS_Rust", skip=("-y",))  # рама ворот
        mat = rnd.choice(GATES)
        if rnd.random() < 0.25:
            # Открытые ворота: створки распахнуты наружу, проём — тёмная панель (стена не прорезана)
            b.box((-gw / 2, 0.04, 0.02), (gw / 2, 0.05, gh), "PS_Glass", skip=("-y",))
            for s in (-1, 1):
                hx = s * gw / 2
                b.box((min(hx, hx + s * 0.05), 0.04, 0.02), (max(hx, hx + s * 0.05), 0.04 + gw / 2, gh), mat)
        else:
            for s in (-1, 1):
                b.box((min(0, s * gw / 2) + 0.01, 0.04, 0.02), (max(0, s * gw / 2) - 0.01, 0.08, gh), mat, skip=("-y",))
            b.box((-0.12, 0.08, 1.0), (-0.04, 0.14, 1.15), "PS_Rust")  # засов
        b.reset()
    return b.finish(col)


def bus_stop(col, name):
    # Советская бетонная остановка: задняя стена и боковины из плит, плита-навес, деревянная лавка
    b = Builder(name)
    w, d, h = 4.6, 1.9, 2.7
    t = 0.15
    b.box((-w / 2, d / 2 - t, 0), (w / 2, d / 2, h), "PS_Concrete")  # задняя стена
    for s in (-1, 1):
        b.box((s * w / 2 - (t if s > 0 else 0), -d / 2 + 0.3, 0), (s * w / 2 + (0 if s > 0 else t), d / 2, h), "PS_Concrete")
    b.slab((-w / 2 - 0.3, -d / 2 - 0.4, h), (w / 2 + 0.3, -d / 2 - 0.4, h), (w / 2 + 0.3, d / 2 + 0.1, h),
           (-w / 2 - 0.3, d / 2 + 0.1, h), 0.2, "PS_Concrete")
    # Лавка: доски на двух бетонных опорах
    for x in (-1.4, 1.4):
        b.box((x - 0.1, d / 2 - 0.65, 0), (x + 0.1, d / 2 - 0.25, 0.42), "PS_Concrete", skip=("-z",))
    for k in range(3):
        y = d / 2 - 0.68 + k * 0.15
        b.box((-1.9, y, 0.42), (1.9, y + 0.12, 0.47), "PS_Planks_Raw")
    # Табличка на столбе сбоку
    b.box((w / 2 + 0.6, -d / 2 - 0.05, 0), (w / 2 + 0.68, -d / 2 + 0.03, 2.6), "PS_Rust", skip=("-z",))
    b.box((w / 2 + 0.35, -d / 2 - 0.06, 2.0), (w / 2 + 0.93, -d / 2 - 0.02, 2.5), "PS_PaintYellow")
    # Отмостка
    b.box((-w / 2 - 0.4, -d / 2 - 0.6, -0.3), (w / 2 + 1.0, d / 2 + 0.2, 0.08), "PS_Concrete", skip=("-z",))
    return b.finish(col)


def kiosk(col, name, wall="PS_PaintBlue", trim="PS_PaintYellow"):
    # Металлический ларёк: окна-витрины спереди и с боков, навес над окнами, плоская крыша
    b = Builder(name)
    w, d, h = 3.2, 2.4, 2.5
    b.box((-w / 2, -d / 2, 0), (w / 2, d / 2, h), wall, skip=("-z",))
    b.box((-w / 2 - 0.1, -d / 2 - 0.1, h), (w / 2 + 0.1, d / 2 + 0.1, h + 0.25), trim, skip=("-z",))
    for origin, normal, ww in (((0, -d / 2, 0), (0, -1, 0), w - 0.5), ((-w / 2, 0, 0), (-1, 0, 0), d - 0.6),
                               ((w / 2, 0, 0), (1, 0, 0), d - 0.6)):
        b.at(origin, normal)
        b.box((-ww / 2, 0, 0.95), (ww / 2, 0.02, 2.0), "PS_Glass", skip=("-y",))
        b.box((-ww / 2 - 0.05, 0, 0.9), (ww / 2 + 0.05, 0.25, 0.95), trim)  # прилавок
        for k in range(4):
            x = -ww / 2 + ww * k / 3
            b.box((x - 0.03, 0.02, 0.95), (x + 0.03, 0.05, 2.0), "PS_Rust", skip=("-y",))  # решётка
        b.reset()
    b.at((0, -d / 2, 0), (0, -1, 0))
    b.slab((-w / 2, 0, 2.15), (w / 2, 0, 2.15), (w / 2, 0.6, 2.0), (-w / 2, 0.6, 2.0), 0.03, trim)  # козырёк
    b.reset()
    b.box((-w / 2 + 0.2, d / 2, 0.05), (-w / 2 + 1.0, d / 2 + 0.03, 2.0), "PS_Rust", skip=("-y",))  # дверь сзади
    return b.finish(col)


def guard_booth(col, name):
    # Будка блокпоста: стены из листа, окна на все стороны, односкатная крыша
    b = Builder(name)
    w, d, h0, h1 = 2.2, 2.2, 2.4, 2.7
    b.box((-w / 2, -d / 2, 0), (w / 2, d / 2, h0), "PS_PaintYellow", skip=("-z", "+z"))
    b.slab((-w / 2 - 0.2, -d / 2 - 0.3, h0 + 0.3), (w / 2 + 0.2, -d / 2 - 0.3, h0 + 0.3), (w / 2 + 0.2, d / 2 + 0.2, h0),
           (-w / 2 - 0.2, d / 2 + 0.2, h0), 0.06, "PS_MetalRoof")
    for origin, normal in (((0, -d / 2, 0), (0, -1, 0)), ((-w / 2, 0, 0), (-1, 0, 0)), ((w / 2, 0, 0), (1, 0, 0))):
        b.at(origin, normal)
        b.box((-0.7, 0, 1.1), (0.7, 0.02, 2.0), "PS_Glass", skip=("-y",))
        b.box((-0.75, 0, 1.05), (0.75, 0.06, 1.1), "PS_Trim")
        b.reset()
    b.at((0, d / 2, 0), (0, 1, 0))
    b.box((-0.45, 0, 0.05), (0.45, 0.03, 2.1), "PS_Rust", skip=("-y",))
    b.reset()
    b.box((-w / 2 - 0.1, -d / 2 - 0.1, -0.3), (w / 2 + 0.1, d / 2 + 0.1, 0.05), "PS_Concrete", skip=("-z",))
    return b.finish(col)


def power_pole(col, name, h=9.5):
    # Бетонная опора ЛЭП с деревянной траверсой и тремя изоляторами; провода тянет Unity (BuildRoadside.cs)
    b = Builder(name)
    b.box((-0.13, -0.1, 0), (0.13, 0.1, h), "PS_Concrete", skip=("-z",))  # ствол
    b.box((-0.8, -0.08, h - 0.6), (0.8, 0.08, h - 0.45), "PS_Planks_Raw")
    for x in (-0.7, 0.0, 0.7):
        z = h - 0.45 if x != 0 else h
        b.cylinder((x, 0, z), "y", 0.06, 0.01, "PS_Glass", segs=6)
        b.box((x - 0.04, -0.04, z), (x + 0.04, 0.04, z + 0.18), "PS_Trim")
    return b.finish(col)


def street_lamp(col, name, h=8.0):
    # Фонарь «кобра»: бетонная стойка, стальной кронштейн над дорогой (в −Y) и плафон
    b = Builder(name)
    b.box((-0.12, -0.09, 0), (0.12, 0.09, h), "PS_Concrete", skip=("-z",))
    b.box((-0.04, -1.8, h - 0.3), (0.04, 0.0, h - 0.22), "PS_Rust")
    b.box((-0.04, -1.8, h - 0.3), (0.04, -1.72, h), "PS_Rust")
    b.box((-0.18, -2.4, h - 0.12), (0.18, -1.6, h + 0.05), "PS_MetalRoof")
    b.box((-0.14, -2.35, h - 0.16), (0.14, -1.65, h - 0.12), "PS_Glass", skip=("+z",))
    return b.finish(col)


def concrete_block(col, name):
    # Фундаментный блок ФБС 2.4 × 0.6 × 0.6 — для заграждений блокпоста
    b = Builder(name)
    b.box((-1.2, -0.3, 0), (1.2, 0.3, 0.58), "PS_Concrete", skip=("-z",))
    return b.finish(col)


def build_all():
    scene = bpy.data.scenes.get("Roadside") or bpy.data.scenes.new("Roadside")
    col = bpy.data.collections.get("RS_Models")
    if col is None:
        col = bpy.data.collections.new("RS_Models")
        scene.collection.children.link(col)
    for o in list(col.objects):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            bpy.data.meshes.remove(data)
    objs = [
        garage_row(col, "GarageRow_6", 6, seed=11),
        garage_row(col, "GarageRow_6b", 6, seed=23),
        garage_row(col, "GarageRow_4", 4, seed=37),
        bus_stop(col, "BusStop"),
        kiosk(col, "Kiosk"),
        kiosk(col, "Kiosk_Yellow", wall="PS_PaintYellow", trim="PS_PaintBlue"),
        guard_booth(col, "GuardBooth"),
        power_pole(col, "PowerPole"),
        street_lamp(col, "StreetLamp"),
        concrete_block(col, "ConcreteBlock"),
    ]
    x = 0.0
    for o in objs:
        dims = o.dimensions
        o.location = (x + dims.x / 2, 0, 0)
        x += dims.x + 3.0
    return scene, objs


if __name__ == "__main__":
    ps.OUT = OUT
    sc, models = build_all()
    ps.export(sc, models)
