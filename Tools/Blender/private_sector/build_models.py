# Модели частного сектора: деревянные и кирпичный дома, сарай, туалет, гараж, заборы и ворота.
# Запуск в Blender 5.1 (вкладка Scripting или через MCP): строит всё с нуля в отдельной сцене «PrivateSector»
# и экспортирует каждую модель в Assets/Models/PrivateSector/<имя>.fbx. Сцена пользователя не трогается.
# Модели в метрах, низ — на z = 0, перед смотрит в −Y (в Unity это −Z, импортёр FBX увеличивает модели в 2 раза под масштаб мира).
# Окна и двери не прорезаны: рамы, стёкла и наличники лежат поверх стен, так дешевле для VR.
# Развёртка — проекция каждой грани на её плоскость в метрах, делённых на размер плитки материала,
# поэтому бесшовные текстуры из Tools/Textures/generate_building_textures.py ложатся без растяжений.
import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix

REPO = os.environ.get("RACING_REPO", r"D:\GitRepos\RacingProject")
OUT = os.path.join(REPO, "Assets", "Models", "PrivateSector")

# Размер плитки текстуры в метрах для каждого материала
TILE = {"PS_Logs": 2.0, "PS_Planks_Green": 2.0, "PS_Planks_Blue": 2.0, "PS_Planks_Raw": 2.0, "PS_Fence": 2.0,
        "PS_Brick": 1.0, "PS_Slate": 2.0, "PS_MetalRoof": 2.0, "PS_MetalGreen": 2.0, "PS_Plaster": 2.0,
        "PS_Trim": 1.0, "PS_Glass": 1.0}
PREVIEW = {"PS_Logs": (0.45, 0.36, 0.27), "PS_Planks_Green": (0.27, 0.42, 0.3), "PS_Planks_Blue": (0.3, 0.42, 0.55),
           "PS_Planks_Raw": (0.45, 0.42, 0.38), "PS_Fence": (0.45, 0.42, 0.38), "PS_Brick": (0.55, 0.23, 0.16),
           "PS_Slate": (0.52, 0.53, 0.5), "PS_MetalRoof": (0.55, 0.55, 0.55), "PS_MetalGreen": (0.25, 0.36, 0.26),
           "PS_Plaster": (0.62, 0.6, 0.56), "PS_Trim": (0.85, 0.86, 0.83), "PS_Glass": (0.12, 0.15, 0.17)}


class Builder:
    # Собирает один меш через bmesh; m — текущая матрица размещения (для окон на разных стенах)
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []
        self.m = Matrix.Identity(4)

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def face(self, pts, mat):
        # Точки задаются в правой системе координат; зеркальная матрица (at() — левая тройка) вывернула бы грань
        # внутрь, поэтому обход вершин в ней разворачивается
        if self.m.to_3x3().determinant() < 0:
            pts = list(reversed(pts))
        vs = [self.bm.verts.new(self.m @ Vector(p)) for p in pts]
        f = self.bm.faces.new(vs)
        f.material_index = self.mat(mat)
        return f

    def box(self, mn, mx, mat, skip=()):
        x0, y0, z0 = mn
        x1, y1, z1 = mx
        sides = {
            "-y": [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)],
            "+y": [(x1, y1, z0), (x0, y1, z0), (x0, y1, z1), (x1, y1, z1)],
            "-x": [(x0, y1, z0), (x0, y0, z0), (x0, y0, z1), (x0, y1, z1)],
            "+x": [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)],
            "+z": [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)],
            "-z": [(x0, y1, z0), (x1, y1, z0), (x1, y0, z0), (x0, y0, z0)],
        }
        for k, pts in sides.items():
            if k not in skip:
                self.face(pts, mat)

    def slab(self, a, b, c, d, t, mat, edge_mat=None):
        # Пластина толщиной t вверх по нормали из четырёхугольника abcd (против часовой, если смотреть сверху)
        a, b, c, d = Vector(a), Vector(b), Vector(c), Vector(d)
        n = (b - a).cross(d - a).normalized() * t
        top = [a + n, b + n, c + n, d + n]
        self.face(top, mat)
        self.face([d, c, b, a], mat)
        em = edge_mat or mat
        bot = [a, b, c, d]
        for i in range(4):
            j = (i + 1) % 4
            self.face([bot[i], bot[j], top[j], top[i]], em)

    def prism(self, base, apex_h, depth, mat):
        # Треугольная призма: основание base=(x0, x1, z0) в плоскости XZ, вершина над центром, толщина depth по −Y
        x0, x1, z0 = base
        xm = (x0 + x1) / 2
        front = [(x0, -depth, z0), (x1, -depth, z0), (xm, -depth, z0 + apex_h)]
        back = [(x1, 0, z0), (x0, 0, z0), (xm, 0, z0 + apex_h)]
        self.face(front, mat)
        self.face(back, mat)
        self.face([(x0, 0, z0), (x0, -depth, z0), (xm, -depth, z0 + apex_h), (xm, 0, z0 + apex_h)], mat)
        self.face([(x1, -depth, z0), (x1, 0, z0), (xm, 0, z0 + apex_h), (xm, -depth, z0 + apex_h)], mat)

    def cylinder(self, c, axis, r, length, mat, segs=8):
        # Цилиндр (торец бревна, столб) вдоль оси 'x' или 'y' с центром основания в c
        c = Vector(c)
        # Базис правый (u × v = w), иначе бока или торцы смотрели бы внутрь
        if axis == "x":
            u, v, w = Vector((0, 1, 0)), Vector((0, 0, 1)), Vector((1, 0, 0))
        else:
            u, v, w = Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((0, 1, 0))
        ring0 = [c + (u * math.cos(2 * math.pi * i / segs) + v * math.sin(2 * math.pi * i / segs)) * r for i in range(segs)]
        ring1 = [p + w * length for p in ring0]
        for i in range(segs):
            j = (i + 1) % segs
            self.face([ring0[i], ring0[j], ring1[j], ring1[i]], mat)
        self.face(ring1, mat)
        self.face(list(reversed(ring0)), mat)

    def at(self, origin, normal):
        # Локальная система для деталей на стене: x — вдоль стены, y — наружу по нормали, z — вверх
        y = Vector(normal).normalized()
        x = Vector((0, 0, 1)).cross(y).normalized()
        z = y.cross(x)
        self.m = Matrix((
            (x.x, y.x, z.x, origin[0]),
            (x.y, y.y, z.y, origin[1]),
            (x.z, y.z, z.z, origin[2]),
            (0, 0, 0, 1)))

    def reset(self):
        self.m = Matrix.Identity(4)

    def finish(self, collection):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=0.0005)
        uv = self.bm.loops.layers.uv.new("UVMap")
        for f in self.bm.faces:
            n = f.normal
            tile = TILE[self.mats[f.material_index]]
            if abs(n.z) > 0.99:
                t = Vector((1, 0, 0))
            else:
                t = Vector((0, 0, 1)).cross(n).normalized()
            b = n.cross(t)
            for loop in f.loops:
                p = loop.vert.co
                loop[uv].uv = (p.dot(t) / tile, p.dot(b) / tile)
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for mname in self.mats:
            mesh.materials.append(get_material(mname))
        obj = bpy.data.objects.new(self.name, mesh)
        collection.objects.link(obj)
        return obj


def get_material(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.diffuse_color = PREVIEW[name] + (1.0,)
    return m


def window(b, origin, normal, w=0.9, h=1.25, shutters=None, carved=True):
    # Окно с рамой, крестовиной, наличником (с «кокошником» сверху) и подоконным отливом; shutters — материал ставен
    b.at(origin, normal)
    hw, hh = w / 2, h / 2
    b.face([(-hw, 0.01, hh), (hw, 0.01, hh), (hw, 0.01, -hh), (-hw, 0.01, -hh)], "PS_Glass")
    f = 0.06
    b.box((-hw - f, 0, -hh - f), (-hw, 0.07, hh + f), "PS_Trim", skip=("-y",))
    b.box((hw, 0, -hh - f), (hw + f, 0.07, hh + f), "PS_Trim", skip=("-y",))
    b.box((-hw, 0, hh), (hw, 0.07, hh + f), "PS_Trim", skip=("-y",))
    b.box((-hw, 0, -hh - f), (hw, 0.07, -hh), "PS_Trim", skip=("-y",))
    b.box((-0.02, 0, -hh), (0.02, 0.05, hh), "PS_Trim", skip=("-y",))
    b.box((-hw, 0, hh * 0.35 - 0.02), (hw, 0.05, hh * 0.35 + 0.02), "PS_Trim", skip=("-y",))
    # Наличник: широкие доски вокруг рамы
    t = 0.12
    b.box((-hw - f - t, 0, -hh - f - 0.05), (-hw - f, 0.09, hh + f + 0.05), "PS_Trim", skip=("-y",))
    b.box((hw + f, 0, -hh - f - 0.05), (hw + f + t, 0.09, hh + f + 0.05), "PS_Trim", skip=("-y",))
    b.box((-hw - f - t - 0.08, 0, hh + f), (hw + f + t + 0.08, 0.11, hh + f + 0.16), "PS_Trim", skip=("-y",))
    if carved:
        b.m = b.m @ Matrix.Translation((0, 0.11, 0))
        b.prism((-hw - f - t, hw + f + t, hh + f + 0.16), 0.28, 0.06, "PS_Trim")
        b.m = b.m @ Matrix.Translation((0, -0.11, 0))
    # Отлив под окном
    b.box((-hw - f - t - 0.04, 0, -hh - f - 0.12), (hw + f + t + 0.04, 0.16, -hh - f - 0.05), "PS_Trim", skip=("-y",))
    if shutters:
        for s in (-1, 1):
            x0 = s * (hw + f + t + 0.02)
            x1 = x0 + s * hw
            b.box((min(x0, x1), 0.02, -hh - f), (max(x0, x1), 0.06, hh + f), shutters, skip=("-y",))
    b.reset()


def door(b, origin, normal, mat, w=0.9, h=2.0):
    b.at(origin, normal)
    b.box((-w / 2, 0, 0), (w / 2, 0.05, h), mat, skip=("-y",))
    b.box((-w / 2 - 0.08, 0, 0), (-w / 2, 0.08, h + 0.08), "PS_Trim", skip=("-y",))
    b.box((w / 2, 0, 0), (w / 2 + 0.08, 0.08, h + 0.08), "PS_Trim", skip=("-y",))
    b.box((-w / 2 - 0.08, 0, h), (w / 2 + 0.08, 0.08, h + 0.08), "PS_Trim", skip=("-y",))
    b.box((w / 2 - 0.15, 0.05, h * 0.48), (w / 2 - 0.08, 0.1, h * 0.52), "PS_MetalRoof")  # ручка
    b.reset()


def gable_roof(b, x0, x1, y0, y1, zw, pitch, roof_mat, gable_mat, over_side=0.5, over_gable=0.4, t=0.08):
    # Двускатная крыша, конёк вдоль Y; фронтоны — треугольники стен под скатами
    hw = (x1 - x0) / 2
    xm = (x0 + x1) / 2
    k = math.tan(math.radians(pitch))
    zr = zw + hw * k
    ya, yb = y0 - over_gable, y1 + over_gable
    ze = zw - over_side * k
    b.slab((x0 - over_side, ya, ze), (xm, ya, zr), (xm, yb, zr), (x0 - over_side, yb, ze), t, roof_mat)
    b.slab((xm, ya, zr), (x1 + over_side, ya, ze), (x1 + over_side, yb, ze), (xm, yb, zr), t, roof_mat)
    b.box((xm - 0.12, ya, zr + t * 0.6), (xm + 0.12, yb, zr + t + 0.08), "PS_MetalRoof")  # конёк
    b.face([(x0, y0, zw), (x1, y0, zw), (xm, y0, zr)], gable_mat)
    b.face([(x1, y1, zw), (x0, y1, zw), (xm, y1, zr)], gable_mat)
    # Лобовые доски по краю фронтона
    for y in (ya, yb - 0.04):
        b.slab((x0 - over_side, y, ze - 0.2), (xm, y, zr - 0.2), (xm, y + 0.04, zr - 0.2), (x0 - over_side, y + 0.04, ze - 0.2), 0.2, "PS_Trim")
        b.slab((xm, y, zr - 0.2), (x1 + over_side, y, ze - 0.2), (x1 + over_side, y + 0.04, ze - 0.2), (xm, y + 0.04, zr - 0.2), 0.2, "PS_Trim")
    return zr


def house(col, name, w, d, wall, roof, gable, shutters, wall_h=2.7, base_h=0.5, pitch=38, log_corners=False,
          front_windows=3, side_windows=2, veranda=False, chimney=True):
    b = Builder(name)
    x0, x1, y0, y1 = -w / 2, w / 2, -d / 2, d / 2
    zw = base_h + wall_h
    # Цоколь чуть шире стен и уходит в землю, чтобы на неровном рельефе не было щели
    b.box((x0 - 0.05, y0 - 0.05, -0.4), (x1 + 0.05, y1 + 0.05, base_h), "PS_Plaster")
    b.box((x0, y0, base_h), (x1, y1, zw), wall, skip=("-z",))
    if log_corners:
        # Торцы брёвен «в обло» на углах: ряды вдоль X и вдоль Y со сдвигом в полбревна
        r = 0.13
        rows = int(wall_h / 0.25)
        for i in range(rows):
            z = base_h + 0.125 + i * 0.25
            for cx in (x0, x1):
                for cy in (y0, y1):
                    b.cylinder((x0 - 0.3, cy, z), "x", r, 0.3, wall) if cx == x0 else b.cylinder((x1, cy, z), "x", r, 0.3, wall)
                    if z + 0.125 < zw:
                        zz = z + 0.125
                        b.cylinder((cx, y0 - 0.3, zz), "y", r, 0.3, wall) if cy == y0 else b.cylinder((cx, y1, zz), "y", r, 0.3, wall)
    zr = gable_roof(b, x0, x1, y0, y1, zw, pitch, roof, gable, over_side=0.55 if log_corners else 0.45)
    zc = base_h + wall_h * 0.45
    # Окна по фасаду (−Y), по бокам и одно сзади
    for i in range(front_windows):
        x = x0 + w * (i + 1) / (front_windows + 1)
        window(b, (x, y0, zc), (0, -1, 0), shutters=shutters)
    for i in range(side_windows):
        y = y0 + d * (i + 1) / (side_windows + 1)
        window(b, (x0, y, zc), (-1, 0, 0), shutters=shutters)
        if i > 0:  # первое окно справа заняло бы место крыльца
            window(b, (x1, y, zc), (1, 0, 0), shutters=shutters)
    window(b, (0, y1, zc), (0, 1, 0), w=0.7, h=1.0, carved=False)
    # Слуховое окно во фронтоне
    window(b, (0, y0, zw + (zr - zw) * 0.38), (0, -1, 0), w=0.55, h=0.75, carved=False)
    # Вход сбоку (+X) с крыльцом и козырьком на двух столбах
    dy = y0 + 1.3
    door(b, (x1, dy, base_h), (1, 0, 0), wall if wall.startswith("PS_Planks") else "PS_Planks_Raw")
    b.box((x1, dy - 0.9, -0.3), (x1 + 1.3, dy + 0.9, base_h), "PS_Planks_Raw", skip=("-z", "-x"))
    b.box((x1 + 1.3, dy - 0.6, -0.3), (x1 + 1.65, dy + 0.6, base_h * 0.5), "PS_Planks_Raw", skip=("-z", "-x"))
    for sy in (-0.85, 0.85):
        b.box((x1 + 1.18, dy + sy - 0.06, base_h), (x1 + 1.3, dy + sy + 0.06, base_h + 2.35), "PS_Trim")
    b.slab((x1, dy - 1.05, base_h + 2.55), (x1, dy + 1.05, base_h + 2.55), (x1 + 1.5, dy + 1.05, base_h + 2.3),
           (x1 + 1.5, dy - 1.05, base_h + 2.3), 0.06, roof)
    if veranda:
        # Застеклённая веранда во всю заднюю часть левого бока: пояс мелких окон
        vx0, vy0, vy1, vh = x0 - 2.2, y0 + d * 0.35, y1 - 0.3, wall_h - 0.2
        b.box((vx0, vy0, -0.4), (x0, vy1, base_h), "PS_Plaster", skip=("+x",))
        b.box((vx0, vy0, base_h), (x0, vy1, base_h + 0.9), wall, skip=("+x", "-z"))
        b.box((vx0, vy0, base_h + 0.9), (x0, vy1, base_h + vh), "PS_Glass", skip=("+x", "-z", "+z"))
        n = int((vy1 - vy0) / 0.6)
        for i in range(n + 1):
            y = vy0 + (vy1 - vy0) * i / n
            b.box((vx0 - 0.05, y - 0.03, base_h + 0.9), (vx0 + 0.02, y + 0.03, base_h + vh), "PS_Trim")
        for x in (vx0 + 0.6, vx0 + 1.2, vx0 + 1.8):
            b.box((x - 0.03, vy0 - 0.05, base_h + 0.9), (x + 0.03, vy0 + 0.02, base_h + vh), "PS_Trim")
        b.box((vx0 - 0.05, vy0 - 0.05, base_h + 0.9), (x0, vy1 + 0.05, base_h + 0.97), "PS_Trim")
        b.box((vx0, vy0, base_h + vh), (x0, vy1, base_h + vh + 0.1), wall)
        b.slab((vx0 - 0.4, vy0 - 0.3, base_h + vh + 0.05), (x0, vy0 - 0.3, base_h + vh + 0.55),
               (x0, vy1 + 0.3, base_h + vh + 0.55), (vx0 - 0.4, vy1 + 0.3, base_h + vh + 0.05), 0.06, roof)
    if chimney:
        cy = y0 + d * 0.62
        b.box((-0.32, cy - 0.32, zw), (0.32, cy + 0.32, zr + 0.9), "PS_Brick")
        b.box((-0.38, cy - 0.38, zr + 0.9), (0.38, cy + 0.38, zr + 1.0), "PS_Plaster")
    return b.finish(col)


def shed(col, name, w=3.5, d=2.6, h0=2.2, h1=2.7, wall="PS_Planks_Raw", roof="PS_MetalRoof", door_w=0.9):
    # Сарай с односкатной крышей: передняя стена ниже задней
    b = Builder(name)
    x0, x1, y0, y1 = -w / 2, w / 2, -d / 2, d / 2
    b.box((x0, y0, -0.2), (x1, y1, 0.0), "PS_Plaster")
    b.face([(x0, y0, 0), (x1, y0, 0), (x1, y0, h0), (x0, y0, h0)], wall)
    b.face([(x1, y1, 0), (x0, y1, 0), (x0, y1, h1), (x1, y1, h1)], wall)
    b.face([(x0, y1, 0), (x0, y0, 0), (x0, y0, h0), (x0, y1, h1)], wall)
    b.face([(x1, y0, 0), (x1, y1, 0), (x1, y1, h1), (x1, y0, h0)], wall)
    k = (h1 - h0) / d
    b.slab((x0 - 0.25, y0 - 0.3, h0 - 0.3 * k), (x1 + 0.25, y0 - 0.3, h0 - 0.3 * k), (x1 + 0.25, y1 + 0.3, h1 + 0.3 * k),
           (x0 - 0.25, y1 + 0.3, h1 + 0.3 * k), 0.05, roof)
    door(b, (x0 + w * 0.3, y0, 0), (0, -1, 0), wall, w=door_w, h=min(1.9, h0 - 0.15))
    return b.finish(col)


def garage(col, name, w=3.6, d=6.2, h=2.6):
    # Кирпичный гараж с плоской крышей и зелёными металлическими воротами; ставятся рядами вплотную
    b = Builder(name)
    x0, x1, y0, y1 = -w / 2, w / 2, -d / 2, d / 2
    b.box((x0, y0, -0.3), (x1, y1, h), "PS_Brick", skip=("-z", "+z"))
    b.slab((x0 - 0.05, y0 - 0.25, h), (x1 + 0.05, y0 - 0.25, h), (x1 + 0.05, y1 + 0.1, h - 0.12), (x0 - 0.05, y1 + 0.1, h - 0.12), 0.15, "PS_Plaster")
    b.at((0, y0, 0), (0, -1, 0))
    gw, gh = 2.7, 2.2
    b.box((-gw / 2 - 0.08, 0, 0), (gw / 2 + 0.08, 0.04, gh + 0.08), "PS_MetalRoof", skip=("-y",))
    for s in (-1, 1):
        b.box((min(0, s * gw / 2) + 0.01, 0.04, 0.02), (max(0, s * gw / 2) - 0.01, 0.08, gh), "PS_MetalGreen", skip=("-y",))
    b.box((gw * 0.12, 0.08, 0.1), (gw * 0.4, 0.1, 1.9), "PS_MetalGreen", skip=("-y",))  # калитка в воротах
    b.box((-0.12, 0.08, 1.0), (-0.04, 0.14, 1.15), "PS_MetalRoof")  # засов
    b.reset()
    return b.finish(col)


def outhouse(col, name):
    return shed(col, name, w=1.2, d=1.2, h0=2.0, h1=2.3, door_w=0.7)


def fence_picket(col, name, length=3.0, h=1.5):
    # Штакетник: два столба, две прожилины, острые штакетины; секция стыкуется с соседней по столбу
    b = Builder(name)
    for x in (-length / 2, length / 2):
        b.box((x - 0.05, -0.05, -0.3), (x + 0.05, 0.05, h + 0.1), "PS_Fence")
    for z in (0.35, h - 0.3):
        b.box((-length / 2, 0.05, z - 0.04), (length / 2, 0.1, z + 0.04), "PS_Fence")
    n = int(length / 0.13)
    for i in range(n):
        x = -length / 2 + 0.1 + (length - 0.2) * i / (n - 1)
        hh = h - 0.12 + 0.05 * math.sin(i * 1.7)  # чуть вразнобой, как у старого забора
        b.box((x - 0.04, 0.1, 0.08), (x + 0.04, 0.12, hh), "PS_Fence", skip=("+z",))
        b.face([(x - 0.04, 0.12, hh), (x + 0.04, 0.12, hh), (x, 0.12, hh + 0.12)], "PS_Fence")
        b.face([(x + 0.04, 0.1, hh), (x - 0.04, 0.1, hh), (x, 0.1, hh + 0.12)], "PS_Fence")
    return b.finish(col)


def fence_solid(col, name, length=3.0, h=1.9, mat="PS_Fence"):
    # Глухой забор из досок или профлиста
    b = Builder(name)
    for x in (-length / 2, length / 2):
        b.box((x - 0.06, -0.06, -0.3), (x + 0.06, 0.06, h + 0.1), "PS_Fence")
    b.box((-length / 2, 0.06, 0.05), (length / 2, 0.09, h), mat)
    return b.finish(col)


def gate(col, name, h=1.9, mat="PS_Planks_Green"):
    # Ворота 3.4 м и калитка 1 м между тремя толстыми столбами, сверху — доска-«козырёк»
    b = Builder(name)
    posts = (-2.3, -1.2, 2.3)
    for x in posts:
        b.box((x - 0.09, -0.09, -0.4), (x + 0.09, 0.09, h + 0.35), "PS_Fence")
    b.box((-2.4, -0.15, h + 0.35), (2.4, 0.15, h + 0.42), "PS_Fence")
    b.box((-1.11, 0.0, 0.08), (0.53, 0.05, h), mat)
    b.box((0.57, 0.0, 0.08), (2.21, 0.05, h), mat)
    b.box((-2.21, 0.0, 0.08), (-1.29, 0.05, h), mat)
    for x0, x1 in ((-1.11, 0.53), (0.57, 2.21), (-2.21, -1.29)):  # Z-образные связки на воротах
        b.box((x0, 0.05, 0.3), (x1, 0.08, 0.42), "PS_Fence")
        b.box((x0, 0.05, h - 0.42), (x1, 0.08, h - 0.3), "PS_Fence")
    b.box((-1.38, 0.05, 1.0), (-1.32, 0.11, 1.12), "PS_MetalRoof")
    return b.finish(col)


def build_all():
    scene = bpy.data.scenes.get("PrivateSector") or bpy.data.scenes.new("PrivateSector")
    col = bpy.data.collections.get("PS_Models")
    if col is None:
        col = bpy.data.collections.new("PS_Models")
        scene.collection.children.link(col)
    for o in list(col.objects):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            bpy.data.meshes.remove(data)
    objs = [
        house(col, "House_Wood_A", 7.5, 8.0, "PS_Logs", "PS_Slate", "PS_Planks_Raw", "PS_Planks_Blue", log_corners=True),
        house(col, "House_Wood_B", 6.5, 7.5, "PS_Planks_Green", "PS_MetalRoof", "PS_Planks_Green", None,
              front_windows=2, veranda=True, pitch=42),
        house(col, "House_Wood_C", 7.0, 7.0, "PS_Planks_Blue", "PS_Slate", "PS_Planks_Blue", "PS_Trim", front_windows=3,
              side_windows=2, pitch=35),
        house(col, "House_Brick", 8.5, 9.0, "PS_Brick", "PS_MetalGreen", "PS_Brick", None, wall_h=2.9, base_h=0.6,
              front_windows=3, side_windows=3, pitch=30),
        shed(col, "Shed"),
        outhouse(col, "Outhouse"),
        garage(col, "Garage"),
        fence_picket(col, "Fence_Picket"),
        fence_solid(col, "Fence_Solid"),
        fence_solid(col, "Fence_Metal", mat="PS_MetalGreen"),
        gate(col, "Gate"),
    ]
    # Раскладываем в ряд для осмотра в Blender (на экспорт не влияет: каждая модель выгружается из нуля)
    x = 0.0
    for o in objs:
        dims = o.dimensions
        o.location = (x + dims.x / 2, 0, 0)
        x += dims.x + 3.0
    return scene, objs


def export(scene, objs):
    os.makedirs(OUT, exist_ok=True)
    vl = scene.view_layers[0]
    for o in objs:
        loc = o.location.copy()
        o.location = (0, 0, 0)
        for other in scene.objects:
            other.select_set(False, view_layer=vl)
        o.select_set(True, view_layer=vl)
        vl.objects.active = o
        path = os.path.join(OUT, o.name + ".fbx")
        # Выделение передаём явно: иначе экспортёр берёт выделенное в сцене, открытой в окне
        with bpy.context.temp_override(scene=scene, view_layer=vl, selected_objects=[o], active_object=o, object=o):
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                                     apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                                     bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=True,
                                     add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
        o.location = loc
        print("exported", path, len(o.data.polygons), "faces")


if __name__ == "__main__":
    sc, models = build_all()
    export(sc, models)
