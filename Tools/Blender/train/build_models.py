# Электричка в духе ЭД4М и железная дорога: головной вагон с кабиной (ED4M_Head), моторный с токоприёмником
# (ED4M_Motor) и прицепной (ED4M_Trailer); опора контактной сети (CatenaryMast), балластная призма под звено пути
# (RailBed, под RailTrack из Tools/Blender/level_props), переезд: настил (CrossingDeck), стойка шлагбаума
# (CrossingBarrier) и его стрела (CrossingArm, ось вращения — начало координат), светофор с андреевским крестом
# (CrossingSignal) и его фонарь (CrossingLamp — отдельно, чтобы мигать).
# Окраска ЭД4М: светло-серый кузов, красные кабина, нижний пояс и полоса под окнами, тёмные стёкла и тележки.
# Запуск в Blender 5.1 (вкладка Scripting или через MCP): строит всё с нуля в отдельной сцене «Train»
# и экспортирует каждую модель в Assets/Models/Train/<имя>.fbx. Сцена пользователя не трогается.
# Построитель мешей, развёртка и экспорт — из Tools/Blender/private_sector/build_models.py, примитивы (tube, bar,
# closed_part) — из Tools/Blender/level_props/build_models.py. Метры, низ на z = 0 (подошва шпал), перед смотрит в −Y,
# вагоны лежат вдоль Y; головка рельса — на z = RAIL. Материалы TR_* — Tools/Textures/generate_train_textures.py.
import bpy
import importlib.util
import math
import os
import random
from mathutils import Vector

REPO = os.environ.get("RACING_REPO", r"D:\GitRepos\RacingProject")
OUT = os.path.join(REPO, "Assets", "Models", "Train")


def _load(name, path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(REPO, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


lp = _load("lp_build", ("Tools", "Blender", "level_props", "build_models.py"))
ps = lp.ps
Builder = ps.Builder
tube, bar, closed_part = lp.tube, lp.bar, lp.closed_part

TR = {  # имя: (плитка в метрах, цвет превью)
    "TR_BodyGrey": (2.0, (0.74, 0.75, 0.75)), "TR_Red": (2.0, (0.67, 0.12, 0.14)),
    "TR_DarkMetal": (2.0, (0.19, 0.19, 0.2)), "TR_Ballast": (2.0, (0.45, 0.43, 0.4)),
    "TR_LampWhite": (1.0, (1.0, 0.96, 0.87)), "TR_LampRed": (1.0, (0.92, 0.16, 0.11)),
}
for k, (tile, color) in TR.items():
    ps.TILE[k] = tile
    ps.PREVIEW[k] = color

V = Vector
RAIL = 0.34          # головка рельса над подошвой шпал (RailTrack)
L = 21.5             # длина кузова
HW = 1.74            # половина ширины кузова
Z0 = RAIL + 1.25     # низ кузова
ZS = 3.95            # начало скругления крыши
ZT = 5.2             # верх крыши
WIRE = RAIL + 6.2    # высота контактного провода


# --- Кузов ---

def half_width(z):
    # Борт вертикальный до ZS, выше — дуга крыши до плоского верха шириной 2 × 0.95
    if z <= ZS:
        return HW
    t = min(1.0, (z - ZS) / (ZT - ZS))
    return 0.95 + (HW - 0.95) * math.sqrt(max(0.0, 1 - t * t))


def nose_y(z, cab):
    # Перед кузова на высоте z: у кабины — почти отвесный низ и наклонное лобовое стекло, выше — скос к крыше
    y = -L / 2
    if not cab:
        return y
    if z <= 2.45:
        return y + (z - Z0) / (2.45 - Z0) * 0.12
    if z <= 4.3:
        return y + 0.12 + (z - 2.45) / (4.3 - 2.45) * 0.78
    return y + 0.9 + (z - 4.3) * 1.1


def body_slices():
    zs = [Z0, Z0 + 0.32, 2.12, 2.42, 2.45, 3.2, 3.8, ZS]
    zs += [ZS + (ZT - ZS) * k / 6 for k in range(1, 7)]
    return zs


def body(b, cab):
    # Кузов — лофт горизонтальных сечений (скруглённый в плане прямоугольник); материал грани — по высоте и по тому,
    # кабина это или борт: красные нижний пояс, полоса под окнами и вся кабина, серые борта, крыша — металл
    n = 4
    slices = []
    for z in body_slices():
        hw = half_width(z)
        yf, yr = nose_y(z, cab), L / 2
        rf = min(0.75, hw - 0.05) if cab else 0.12
        pts = lp.rrect(0.0, (yf + yr) / 2, hw, (yr - yf) / 2, rf, 0.12, n)
        slices.append([V((x, y, z)) for x, y in pts])
    cab_end = -L / 2 + 1.6

    def material(c):
        if c.z > ZS + 0.05:
            return "PS_MetalRoof"
        if cab and c.y < cab_end:
            return "TR_Red"
        if c.z < Z0 + 0.32 or 2.12 < c.z < 2.42:
            return "TR_Red"
        return "TR_BodyGrey"

    def build():
        count = len(slices[0])
        for r0, r1 in zip(slices, slices[1:]):
            for i in range(count):
                j = (i + 1) % count
                quad = [r0[i], r0[j], r1[j], r1[i]]
                c = sum(quad, V()) / 4
                b.face(quad, material(c))
        b.face(list(reversed(slices[0])), "TR_DarkMetal")
        b.face(list(slices[-1]), "PS_MetalRoof")
    closed_part(b, build)


def plate_x(b, side, y0, y1, z0, z1, mat, out=0.012, t=0.02):
    # Пластина на борту (окно, дверь): чуть наружу от обшивки
    x0 = side * (HW + out)
    x1 = side * (HW + out + t)
    b.box((min(x0, x1), y0, z0), (max(x0, x1), y1, z1), mat)


def side_window(b, side, y0, y1, z0=2.55, z1=3.6):
    plate_x(b, side, y0 - 0.05, y1 + 0.05, z0 - 0.05, z1 + 0.05, "TR_DarkMetal", out=0.004, t=0.012)
    plate_x(b, side, y0, y1, z0, z1, "PS_Glass")


def door(b, side, yc):
    # Двустворчатая дверь: серые створки с окнами, тёмный шов и уплотнитель, подножка снизу
    w = 1.3
    plate_x(b, side, yc - w / 2 - 0.05, yc + w / 2 + 0.05, Z0 - 0.02, 3.75, "TR_DarkMetal", out=0.004, t=0.012)
    for s in (-1, 1):
        a, c = (yc, yc + s * w / 2) if s > 0 else (yc + s * w / 2, yc)
        plate_x(b, side, a + 0.02, c - 0.02, Z0 + 0.02, 3.7, "TR_BodyGrey")
        wa, wc = a + 0.12, c - 0.12
        plate_x(b, side, wa, wc, 2.5, 3.5, "PS_Glass", out=0.034, t=0.008)
    x = side * (HW + 0.03)
    b.box((min(x, side * (HW + 0.25)), yc - w / 2, Z0 - 0.35), (max(x, side * (HW + 0.25)), yc + w / 2, Z0 - 0.28),
          "TR_DarkMetal")


def gangway(b, y_end, s):
    # Переходная площадка: гармошка между вагонами
    y1 = y_end + s * 0.28
    b.box((-0.62, min(y_end, y1), 1.95), (0.62, max(y_end, y1), 4.0), "LP_Rubber")
    b.box((-0.75, min(y_end, y1) - 0.02, Z0 + 0.05), (0.75, max(y_end, y1) + 0.02, Z0 + 0.3), "TR_DarkMetal")


def bogie(b, yc):
    # Тележка: боковины, надрессорная балка, пружины, две колёсные пары
    for s in (-1, 1):
        b.box((s * 0.98 - 0.1, yc - 1.65, 0.6), (s * 0.98 + 0.1, yc + 1.65, 1.08), "TR_DarkMetal")
        b.box((s * 0.98 - 0.13, yc - 0.45, 1.08), (s * 0.98 + 0.13, yc + 0.45, 1.45), "TR_DarkMetal")
        for dy in (-1.3, 1.3):
            tube(b, (s * 0.7, yc + dy, RAIL + 0.525), (s * 0.84, yc + dy, RAIL + 0.525), 0.525, "TR_DarkMetal", segs=12)
            tube(b, (s * 0.84, yc + dy, RAIL + 0.525), (s * 0.95, yc + dy, RAIL + 0.525), 0.16, "PS_Rust", segs=8)
    b.box((-1.05, yc - 0.3, 0.95), (1.05, yc + 0.3, 1.3), "TR_DarkMetal")
    for dy in (-1.3, 1.3):
        tube(b, (-0.7, yc + dy, RAIL + 0.525), (0.7, yc + dy, RAIL + 0.525), 0.09, "PS_Rust", segs=6)


def underframe(b, seed):
    # Подвагонное оборудование между тележками: ящики разной длины
    rnd = random.Random(seed)
    y = -L / 2 + 4.6
    while y < L / 2 - 5.0:
        length = rnd.uniform(1.0, 2.4)
        depth = rnd.uniform(0.45, 0.8)
        for s in (-1, 1):
            if rnd.random() < 0.85:
                b.box((s * 0.25 if s > 0 else -1.35, y, Z0 - depth), (1.35 if s > 0 else -0.25, y + length, Z0), "TR_DarkMetal")
        y += length + rnd.uniform(0.25, 0.7)


def side_windows(b, y0, y1, count):
    # Ряд окон салона между дверями
    step = (y1 - y0) / count
    for side in (-1, 1):
        for k in range(count):
            a = y0 + k * step + 0.18
            side_window(b, side, a, a + step - 0.36)


def cab_front(b):
    # Лобовые стёкла в две половины, прожектор над ними, буферные фонари, сцепка и путеочиститель
    for x0, x1 in ((-0.97, -0.06), (0.06, 0.97)):
        for k, (out, mat) in enumerate(((0.006, "TR_DarkMetal"), (0.02, "PS_Glass"))):
            grow = 0.05 if k == 0 else 0.0
            za, zb = 2.6 - grow, 4.05 + grow
            pts = [(x0 - grow, nose_y(za, True) - out, za), (x1 + grow, nose_y(za, True) - out, za),
                   (x1 + grow, nose_y(zb, True) - out, zb), (x0 - grow, nose_y(zb, True) - out, zb)]
            b.face(pts, mat)
    # Прожектор под козырьком крыши
    zl = 4.4
    yl = nose_y(zl, True)
    b.box((-0.22, yl - 0.08, zl - 0.14), (0.22, yl + 0.02, zl + 0.14), "TR_DarkMetal")
    b.box((-0.16, yl - 0.1, zl - 0.09), (0.16, yl - 0.08, zl + 0.09), "TR_LampWhite")
    # Буферные фонари: белые снаружи, красные хвостовые ближе к оси
    for s in (-1, 1):
        for x, mat in ((s * 0.92, "TR_LampWhite"), (s * 0.55, "TR_LampRed")):
            zc = 2.0
            y = nose_y(zc, True)
            tube(b, (x, y + 0.02, zc), (x, y - 0.07, zc), 0.13, "TR_DarkMetal", segs=10)
            tube(b, (x, y - 0.07, zc), (x, y - 0.085, zc), 0.1, mat, segs=10)
    # Буферный брус, автосцепка, путеочиститель
    yb = -L / 2
    b.box((-1.5, yb - 0.25, Z0 - 0.45), (1.5, yb + 0.4, Z0 + 0.05), "TR_DarkMetal")
    b.box((-0.18, yb - 0.75, RAIL + 0.85), (0.18, yb - 0.25, RAIL + 1.15), "PS_Rust")
    b.slab((-1.45, yb - 0.55, RAIL + 0.12), (1.45, yb - 0.55, RAIL + 0.12), (1.5, yb + 0.1, Z0 - 0.45),
           (-1.5, yb + 0.1, Z0 - 0.45), 0.04, "TR_DarkMetal")
    # Номерной щит под крышей и поручни у двери кабины
    b.box((-0.55, nose_y(4.2, True) - 0.03, 4.12), (0.55, nose_y(4.2, True) + 0.01, 4.27), "TR_DarkMetal")


def roof_equipment(b, pantograph, seed):
    # Крышевое оборудование: блоки вентиляции; у моторного вагона — токоприёмник и ящики резисторов
    rnd = random.Random(seed)
    for y in (-L / 2 + 6.0, 0.0, L / 2 - 5.0):
        b.box((-0.55, y - 0.9, ZT - 0.02), (0.55, y + 0.9, ZT + 0.32), "PS_MetalRoof")
    if not pantograph:
        return
    y0 = -L / 2 + 3.2
    b.box((-0.9, y0 - 0.6, ZT), (0.9, y0 + 0.6, ZT + 0.12), "TR_DarkMetal")
    for x in (-0.75, 0.75):
        for y in (y0 - 0.5, y0 + 0.5):
            tube(b, (x, y, ZT + 0.12), (x, y, ZT + 0.42), 0.07, "LP_Rubber", segs=6)
    base = V((0, y0 + 0.3, ZT + 0.45))
    knee = V((0, y0 + 1.95, ZT + 0.95))
    head = V((0, y0 - 0.05, WIRE - 0.05))
    for x in (-0.32, 0.32):
        bar(b, base + V((x, 0, 0)), knee + V((x * 0.3, 0, 0)), 0.07, "PS_Rust")
    bar(b, knee, head, 0.06, "PS_Rust")
    bar(b, base + V((0, -0.4, 0)), knee + V((0, -0.2, -0.05)), 0.03, "PS_Rust")
    for dy in (-0.16, 0.16):
        bar(b, (-0.85, head.y + dy, head.z), (0.85, head.y + dy, head.z), 0.06, "TR_DarkMetal")
        for s in (-1, 1):
            bar(b, (s * 0.85, head.y + dy, head.z), (s * 1.02, head.y + dy, head.z - 0.16), 0.05, "TR_DarkMetal")
    bar(b, (0, head.y - 0.16, head.z - 0.04), (0, head.y + 0.16, head.z - 0.04), 0.05, "TR_DarkMetal")
    for k in range(3):
        y = y0 + 3.0 + k * 1.6 + rnd.uniform(-0.2, 0.2)
        b.box((-0.7, y, ZT - 0.02), (0.7, y + 1.2, ZT + 0.45), "TR_DarkMetal")


def ed4m_car(col, name, cab, pantograph, seed):
    b = Builder(name)
    body(b, cab)
    if cab:
        cab_front(b)
        d0, d1 = -L / 2 + 3.9, L / 2 - 1.7
        for side in (-1, 1):
            side_window(b, side, -L / 2 + 1.45, -L / 2 + 2.6, 2.6, 3.55)  # окно кабины
            door(b, side, d0)
            door(b, side, d1)
        side_windows(b, d0 + 0.85, d1 - 0.85, 8)
        gangway(b, L / 2, 1)
    else:
        d0, d1 = -L / 2 + 1.7, L / 2 - 1.7
        for side in (-1, 1):
            door(b, side, d0)
            door(b, side, d1)
        side_windows(b, d0 + 0.85, d1 - 0.85, 10)
        gangway(b, -L / 2, -1)
        gangway(b, L / 2, 1)
    for yc in (-7.5, 7.5):
        bogie(b, yc)
    underframe(b, seed)
    roof_equipment(b, pantograph, seed)
    return lp.finish(b, col)


# --- Путь и контактная сеть ---

def rail_bed(col, name, length=12.0):
    # Балластная призма под звено пути: щебень, верх чуть выше подошвы шпал, низ уходит в землю; торцы открыты,
    # чтобы звенья стыковались
    b = Builder(name)
    top, bottom, z1, z0 = 1.75, 2.9, 0.06, -0.7
    y0, y1 = -length / 2, length / 2
    b.face([(-top, y0, z1), (top, y0, z1), (top, y1, z1), (-top, y1, z1)], "TR_Ballast")
    for s in (-1, 1):
        pts = [(s * top, y0, z1), (s * bottom, y0, z0), (s * bottom, y1, z0), (s * top, y1, z1)]
        b.face(pts if s > 0 else list(reversed(pts)), "TR_Ballast")
    return lp.finish(b, col)


def catenary_mast(col, name):
    # Железобетонная опора с консолью: путь — в 3.1 м по +X; несущий трос и контактный провод крепятся над осью пути
    b = Builder(name)
    tube(b, (0, 0, -0.5), (0, 0, 9.4), 0.2, "PS_Concrete", r1=0.14, segs=10)
    b.box((-0.3, -0.3, -0.5), (0.3, 0.3, 0.25), "PS_Concrete")
    tube(b, (0, 0, 7.0), (3.45, 0, 7.15), 0.045, "PS_Rust", segs=6)
    tube(b, (0, 0, 8.7), (3.45, 0, 7.95), 0.035, "PS_Rust", segs=6)
    tube(b, (3.1, 0, 7.15), (3.1, 0, 7.95), 0.03, "PS_Rust", segs=6)
    for p in ((0.25, 0, 7.02), (0.25, 0, 8.65)):
        tube(b, p, (p[0] + 0.35, 0, p[2] + (0.01 if p[2] < 8 else -0.08)), 0.07, "LP_Rubber", segs=6)
    # Фиксатор контактного провода
    bar(b, (2.6, 0, 7.15), (3.1, 0, WIRE + 0.05), 0.03, "PS_Rust")
    tube(b, (3.1, -0.05, WIRE), (3.1, 0.05, WIRE), 0.04, "PS_Rust", segs=6)
    return lp.finish(b, col)


# --- Переезд ---

def crossing_deck(col, name, length=8.0):
    # Настил переезда вдоль пути: бетонные плиты между рельсами и снаружи, верх вровень с головкой рельса
    b = Builder(name)
    y0, y1 = -length / 2, length / 2
    b.box((-0.66, y0, 0.16), (0.66, y1, RAIL), "PS_Concrete", skip=("-z",))
    for s in (-1, 1):
        a, c = (0.86, 2.2) if s > 0 else (-2.2, -0.86)
        b.box((a, y0, 0.0), (c, y1, RAIL), "PS_Concrete", skip=("-z",))
    return lp.finish(b, col)


def crossing_barrier(col, name):
    # Стойка шлагбаума с противовесом; стрела — отдельная модель
    b = Builder(name)
    b.box((-0.3, -0.3, 0), (0.3, 0.3, 0.2), "PS_Concrete")
    tube(b, (0, 0, 0.2), (0, 0, 1.15), 0.12, "LP_PaintWhite", segs=8)
    b.box((-0.22, -0.18, 0.75), (0.22, 0.18, 1.25), "LP_PaintWhite")
    b.box((-0.08, -0.22, 0.95), (0.08, 0.22, 1.1), "TR_DarkMetal")
    return lp.finish(b, col)


def crossing_arm(col, name, length=5.0):
    # Стрела шлагбаума вдоль +X от оси вращения в начале координат; противовес позади оси
    b = Builder(name)
    b.box((0, -0.05, -0.06), (length, 0.05, 0.06), "LP_Stripes")
    b.box((-0.55, -0.07, -0.12), (0, 0.07, 0.12), "TR_DarkMetal")
    b.box((length * 0.6 - 0.05, -0.06, -0.2), (length * 0.6 + 0.05, 0.06, 0.06), "LP_PaintWhite")
    return lp.finish(b, col)


def crossing_signal(col, name):
    # Светофор переезда: стойка, чёрный щит под два фонаря с козырьками, над ним андреевский крест
    b = Builder(name)
    b.box((-0.25, -0.25, 0), (0.25, 0.25, 0.15), "PS_Concrete")
    tube(b, (0, 0, 0.15), (0, 0, 3.4), 0.07, "LP_PaintWhite", segs=8)
    b.box((-0.62, -0.1, 2.15), (0.62, -0.04, 2.65), "TR_DarkMetal")
    for x in (-0.36, 0.36):
        tube(b, (x, -0.1, 2.4), (x, -0.3, 2.4), 0.17, "TR_DarkMetal", segs=10, caps=False)
    for s in (-1, 1):
        bar(b, (-0.62, -0.06, 2.85 + (0.55 if s > 0 else 0)), (0.62, -0.06, 2.85 + (0 if s > 0 else 0.55)), 0.16,
            "LP_Stripes", h=0.04)
    return lp.finish(b, col)


def crossing_lamp(col, name):
    # Стекло фонаря светофора: включается и гаснет из Unity (RailCrossing)
    b = Builder(name)
    tube(b, (0, 0, 0), (0, -0.02, 0), 0.14, "TR_LampRed", segs=10)
    return lp.finish(b, col)


def build_all():
    scene = bpy.data.scenes.get("Train") or bpy.data.scenes.new("Train")
    col = bpy.data.collections.get("TR_Models")
    if col is None:
        col = bpy.data.collections.new("TR_Models")
        scene.collection.children.link(col)
    for o in list(col.objects):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            bpy.data.meshes.remove(data)
    objs = [
        ed4m_car(col, "ED4M_Head", True, False, 1),
        ed4m_car(col, "ED4M_Motor", False, True, 2),
        ed4m_car(col, "ED4M_Trailer", False, False, 3),
        rail_bed(col, "RailBed"),
        catenary_mast(col, "CatenaryMast"),
        crossing_deck(col, "CrossingDeck"),
        crossing_barrier(col, "CrossingBarrier"),
        crossing_arm(col, "CrossingArm"),
        crossing_signal(col, "CrossingSignal"),
        crossing_lamp(col, "CrossingLamp"),
    ]
    x = 0.0
    for o in objs:
        dims = o.dimensions
        o.location = (x + dims.x / 2, 0, 0)
        x += dims.x + 3.0
    return scene, objs


if __name__ == "__main__":
    # TR_ONLY=Имя1,Имя2 — экспортировать только эти модели (и их LOD1), остальные FBX не трогать
    only = {n for n in os.environ.get("TR_ONLY", "").split(",") if n}
    ps.OUT = OUT
    sc, models = build_all()
    if only:
        models = [o for o in models if o.name in only]
    ps.export(sc, models, lod_build=build_all)
