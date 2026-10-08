# Модели бункера-командного пункта для стартового меню: бетонная комната со сводом и рёбрами, трубы с фланцами
# и вентилем, фильтровентиляционная установка с воздуховодом, кабельный лоток, щиток и рубильник, гермодверь
# с кремальерами и штурвалом, полукруглый пульт с экранами меню, приборами, тумблерами и лампами,
# радиостанция, полевой телефон, настольная лампа, подвесные лампы в решётке и реквизит.
# Запуск в Blender 5.1 (вкладка Scripting или через MCP): строит всё с нуля в отдельной сцене «Bunker»
# и экспортирует каждую модель в Assets/Models/Bunker/<имя>.fbx. Сцена пользователя не трогается.
# Развёртка и материалы — построитель из Tools/Blender/private_sector/build_models.py; поверх него здесь
# скругления (модификатор Bevel со сглаживанием нормалей), тела вращения, торы, трубы по ломаной и профили.
# Текстуры BK_* — Tools/Textures/generate_bunker_textures.py, материалы собирает Tools/Unity/BuildBunker.cs.
# Все детали строятся в общих координатах комнаты: начало — пол под глазами сидящего оператора,
# взгляд вдоль +Y Blender, высота глаз 1,36 м. В Unity все префабы ставятся в ноль общего корня.
import bpy
import bmesh
import importlib.util
import math
import os
import random

from mathutils import Matrix, Vector

REPO = os.environ.get("RACING_REPO", r"D:\GitRepos\RacingProject")
OUT = os.path.join(REPO, "Assets", "Models", "Bunker")

_spec = importlib.util.spec_from_file_location(
    "ps_build", os.path.join(REPO, "Tools", "Blender", "private_sector", "build_models.py"))
ps = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ps)
Builder = ps.Builder

# Размер плитки текстуры (м) и цвет для предпросмотра в Blender
MATS = {
    "BK_Concrete": (2.0, (0.55, 0.54, 0.51)), "BK_Floor": (2.0, (0.42, 0.41, 0.39)),
    "BK_WallPaint": (2.0, (0.25, 0.36, 0.28)), "BK_Paint": (1.0, (0.3, 0.37, 0.27)),
    "BK_PaintRed": (1.0, (0.58, 0.15, 0.12)), "BK_PaintCream": (1.0, (0.77, 0.74, 0.63)),
    "BK_PaintYellow": (1.0, (0.77, 0.61, 0.17)), "BK_Steel": (1.0, (0.2, 0.2, 0.21)),
    "BK_Galv": (1.0, (0.58, 0.6, 0.61)), "BK_Burlap": (0.5, (0.55, 0.46, 0.32)),
    "BK_Wood": (1.0, (0.34, 0.36, 0.23)), "BK_Rubber": (1.0, (0.05, 0.05, 0.05)),
    "BK_Screen": (1.0, (0.05, 0.12, 0.08)), "BK_Bulb": (1.0, (1.0, 0.85, 0.6)),
    "BK_Glass": (1.0, (0.15, 0.17, 0.17)), "BK_LampRed": (1.0, (0.9, 0.1, 0.05)),
    "BK_LampGreen": (1.0, (0.2, 0.9, 0.3)), "BK_LampAmber": (1.0, (1.0, 0.6, 0.1)),
    "PS_Rust": (2.0, (0.45, 0.25, 0.14)),
}
for _n, (_t, _c) in MATS.items():
    ps.TILE[_n] = _t
    ps.PREVIEW[_n] = _c

# Размеры комнаты: стены по x и y, высота стен и подъём свода
X0, X1 = -2.3, 1.7
Y0, Y1 = -3.4, 1.6
WALL_H = 2.2
RISE = 0.75
WALL_T = 0.25
PAINT_H = 1.25  # нижняя часть стен выкрашена масляной краской

# Пульт: секции по дуге вокруг оператора; угол 0 — прямо, плюс — влево
DESK_R0, DESK_R1, DESK_Z = 0.55, 1.5, 0.76
SECTION = 55.0
# Экраны: передняя плоскость рамки SCREEN_Y, стекло на 4,5 см глубже (холст меню Unity кладёт на 5 мм перед стеклом)
SCREEN_Y = 1.0
GLASS_Y = SCREEN_Y + 0.045
SCREEN_Z = 1.2
MAIN_SCREEN = (0.84, 0.84 * 301.25 / 628.5)
SETTINGS_SCREEN = (0.84, 0.84 * 371.25 / 628.5)
# Плакат с картой района над главным экраном и листок с советами на правой стене (их ставит Unity)
MAP_CENTER = (0.0, Y1, 2.08)
MAP_SIZE = 0.8
TIPS_CENTER = (X1, 0.35, 1.5)
TIPS_SIZE = (0.9, 0.9 * 301.25 / 628.5)
LAMP_POS = (-0.3, -0.7)       # x, y подвесных ламп; высота — по своду
LAMP2_POS = (-0.3, -2.6)
DESK_LAMP = (32.0, 1.2)        # угол и радиус настольной лампы на пульте (Unity ставит в абажур прожектор)
BULKHEAD = (X0, -1.25, 2.05)   # настенный светильник у двери
DOOR_Y = -2.1                  # центр гермодвери на левой стене
FVU_X = -0.45                  # фильтровентиляционная установка у задней стены
DUCT_X = 0.7                   # воздуховод под сводом


def vault_z(x):
    # Высота свода над точкой x (дуга окружности через верх обеих стен с подъёмом RISE)
    half = (X1 - X0) / 2
    r = (half * half + RISE * RISE) / (2 * RISE)
    cx = (X0 + X1) / 2
    return WALL_H + RISE - r + math.sqrt(max(0.0, r * r - (x - cx) ** 2))


# ---------------------------------------------------------------- примитивы поверх Builder

def basis(axis):
    # Правая тройка (u, v, w) вокруг оси w: v = w × u
    w = Vector(axis).normalized()
    u = w.cross(Vector((0, 0, 1)) if abs(w.z) < 0.9 else Vector((1, 0, 0))).normalized()
    return u, w.cross(u), w


def ring(c, u, v, r, segs, rv=None, phase=0.0):
    rv = r if rv is None else rv
    return [c + u * (math.cos(phase + 2 * math.pi * i / segs) * r) + v * (math.sin(phase + 2 * math.pi * i / segs) * rv)
            for i in range(segs)]


def bridge(b, r0, r1, mat):
    n = len(r0)
    for i in range(n):
        j = (i + 1) % n
        b.face([r0[i], r0[j], r1[j], r1[i]], mat)


def tube(b, c, axis, r, length, mat, segs=16, rv=None, caps=True):
    # Цилиндр (или эллиптический при rv) от точки c вдоль оси на длину length
    u, v, w = basis(axis)
    c = Vector(c)
    r0 = ring(c, u, v, r, segs, rv)
    r1 = [p + w * length for p in r0]
    bridge(b, r0, r1, mat)
    if caps:
        b.face(r1, mat)
        b.face(list(reversed(r0)), mat)


def lathe(b, c, prof, mat, axis=(0, 0, 1), segs=24, closed=False):
    # Тело вращения: prof — точки (радиус, высота) против часовой в плоскости (r вправо, h вверх),
    # обычно снизу от оси по внешней стороне вверх к оси. Концы на оси (r = 0) стягиваются в полюс;
    # closed — профиль замкнут (кольцо, абажур с толщиной стенки)
    u, v, w = basis(axis)
    c = Vector(c)
    rings = []
    for r, h in prof:
        rings.append([c + w * h] * segs if r <= 1e-6 else ring(c + w * h, u, v, r, segs))
    pairs = list(zip(range(len(rings) - 1), range(1, len(rings))))
    if closed:
        pairs.append((len(rings) - 1, 0))
    for a, bb in pairs:
        ra, rb = rings[a], rings[bb]
        for i in range(segs):
            j = (i + 1) % segs
            pts = [ra[i], ra[j], rb[j], rb[i]]
            # Вырожденные стороны у полюса превращаются в треугольники
            uniq = []
            for p in pts:
                if not any((p - q).length < 1e-7 for q in uniq):
                    uniq.append(p)
            if len(uniq) >= 3:
                b.face(uniq, mat)


def torus(b, c, axis, R, r, mat, segs=24, tsegs=8, arc=2 * math.pi, phase=0.0):
    # Тор (обод штурвала, кольцо решётки); arc < 2π — дуга от угла phase (ручка, оголовье)
    u, v, w = basis(axis)
    c = Vector(c)
    full = arc >= 2 * math.pi - 1e-6
    n = segs if full else segs + 1
    rings = []
    for i in range(n):
        a = phase + arc * i / segs
        d = u * math.cos(a) + v * math.sin(a)
        rings.append(ring(c + d * R, d, -w, r, tsegs))  # −w: нормали наружу при обходе по дуге
    for i in range(segs):
        bridge(b, rings[i], rings[(i + 1) % n], mat)
    if not full:
        b.face(list(reversed(rings[0])), mat)
        b.face(rings[-1], mat)


def sweep(b, pts, r, mat, segs=10, caps=True):
    # Труба по ломаной (кабели, гнутые трубки стула, ручки); кольца ориентированы переносом без скручивания
    pts = [Vector(p) for p in pts]
    tans = []
    for i in range(len(pts)):
        a = pts[max(i - 1, 0)]
        c = pts[min(i + 1, len(pts) - 1)]
        tans.append((c - a).normalized())
    u, v, w = basis(tans[0])
    rings = []
    for i, p in enumerate(pts):
        t = tans[i]
        u = (u - t * u.dot(t)).normalized()
        v = t.cross(u)
        rings.append(ring(p, u, v, r, segs))
    for i in range(len(rings) - 1):
        bridge(b, rings[i], rings[i + 1], mat)
    if caps:
        b.face(list(reversed(rings[0])), mat)
        b.face(rings[-1], mat)


def arc_pts(c, u, v, R, a0, a1, n):
    c, u, v = Vector(c), Vector(u), Vector(v)
    return [c + (u * math.cos(a0 + (a1 - a0) * i / n) + v * math.sin(a0 + (a1 - a0) * i / n)) * R for i in range(n + 1)]


def ellipsoid(b, c, radii, mat, segs=12, rings=8, noise=0.0, rnd=None, squash_top=0.0):
    # Эллипсоид с шумом (мешки с песком, подушки); squash_top приплющивает верх и низ
    c = Vector(c)
    rows = []
    for k in range(1, rings):
        th = math.pi * k / rings
        row = []
        for i in range(segs):
            ph = 2 * math.pi * i / segs
            n = 1.0 + (rnd.uniform(-noise, noise) if rnd else 0.0)
            z = math.cos(th)
            z = math.copysign(abs(z) ** (1.0 - squash_top), z)
            row.append(c + Vector((math.sin(th) * math.cos(ph) * radii[0] * n,
                                   math.sin(th) * math.sin(ph) * radii[1] * n, z * radii[2])))
        rows.append(row)
    top, bot = c + Vector((0, 0, radii[2])), c - Vector((0, 0, radii[2]))
    for i in range(segs):
        j = (i + 1) % segs
        b.face([rows[0][j], rows[0][i], top], mat)
        b.face([rows[-1][i], rows[-1][j], bot], mat)
    for k in range(len(rows) - 1):
        for i in range(segs):
            j = (i + 1) % segs
            b.face([rows[k][i], rows[k + 1][i], rows[k + 1][j], rows[k][j]], mat)


def rrect(x0, x1, z0, z1, r, n=5):
    # Скруглённый прямоугольник против часовой (x вправо, z вверх)
    r = min(r, (x1 - x0) / 2 - 1e-4, (z1 - z0) / 2 - 1e-4)
    pts = []
    for cx, cz, a0 in ((x1 - r, z0 + r, -90), (x1 - r, z1 - r, 0), (x0 + r, z1 - r, 90), (x0 + r, z0 + r, 180)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + math.cos(a) * r, cz + math.sin(a) * r))
    return pts


def prism(b, prof, plane, d0, d1, mat, cap_mat=None):
    # Призма из выпуклого профиля: plane 'xz' — профиль (x, z), выдавлен по y от d0 до d1;
    # 'yz' — профиль (y, z), выдавлен по x; 'xy' — профиль (x, y), выдавлен по z. Грани ориентируются наружу
    def P(p, d):
        if plane == "xz":
            return Vector((p[0], d, p[1]))
        if plane == "yz":
            return Vector((d, p[0], p[1]))
        return Vector((p[0], p[1], d))
    axis = {"xz": Vector((0, 1, 0)), "yz": Vector((1, 0, 0)), "xy": Vector((0, 0, 1))}[plane]
    cap_mat = cap_mat or mat
    ctr = sum((P(p, (d0 + d1) / 2) for p in prof), Vector()) / len(prof)
    a = [P(p, d0) for p in prof]
    c = [P(p, d1) for p in prof]
    face_out(b, a, cap_mat, -axis)
    face_out(b, c, cap_mat, axis)
    n = len(prof)
    for i in range(n):
        j = (i + 1) % n
        q = [a[i], a[j], c[j], c[i]]
        mid = sum(q, Vector()) / 4
        face_out(b, q, mat, mid - ctr)


def frame(b, outer, inner, plane, d0, d1, mat):
    # Рамка между двумя профилями с одинаковым числом точек (рамка экрана, двери), выдавлена от d0 до d1
    def P(p, d):
        if plane == "xz":
            return Vector((p[0], d, p[1]))
        return Vector((d, p[0], p[1]))
    axis = Vector((0, 1, 0)) if plane == "xz" else Vector((1, 0, 0))
    n = len(outer)
    oc = sum((P(p, 0) for p in outer), Vector()) / n
    for i in range(n):
        j = (i + 1) % n
        face_out(b, [P(outer[i], d0), P(outer[j], d0), P(inner[j], d0), P(inner[i], d0)], mat, -axis)
        face_out(b, [P(outer[i], d1), P(outer[j], d1), P(inner[j], d1), P(inner[i], d1)], mat, axis)
        q = [P(outer[i], d0), P(outer[j], d0), P(outer[j], d1), P(outer[i], d1)]
        face_out(b, q, mat, sum(q, Vector()) / 4 - oc - axis * ((d0 + d1) / 2))
        q = [P(inner[i], d0), P(inner[j], d0), P(inner[j], d1), P(inner[i], d1)]
        face_out(b, q, mat, oc + axis * ((d0 + d1) / 2) - sum(q, Vector()) / 4)


def face_out(b, pts, mat, out):
    # Грань с нормалью в сторону out (в локальных координатах построителя)
    pts = [Vector(p) for p in pts]
    n = Vector()
    for i in range(len(pts)):
        p, q = pts[i], pts[(i + 1) % len(pts)]
        n += Vector(((p.y - q.y) * (p.z + q.z), (p.z - q.z) * (p.x + q.x), (p.x - q.x) * (p.y + q.y)))
    if n.dot(Vector(out)) < 0:
        pts.reverse()
    b.face(pts, mat)


def cube(b, mn, mx, mat):
    # Закрытый ящик (в отличие от Builder.box без пропусков граней)
    b.box(mn, mx, mat)


def wall_frame(origin, normal):
    # Правая система для деталей на стене: x вдоль стены, y — от стены в комнату, z — вверх
    y = Vector(normal).normalized()
    x = y.cross(Vector((0, 0, 1))).normalized()
    z = x.cross(y)
    return Matrix(((x.x, y.x, z.x, origin[0]), (x.y, y.y, z.y, origin[1]), (x.z, y.z, z.z, origin[2]), (0, 0, 0, 1)))


def rot_z(deg, origin=(0, 0, 0)):
    return Matrix.Translation(Vector(origin)) @ Matrix.Rotation(math.radians(deg), 4, "Z")


def finish(b, col, bevel=0.005, segs=2, angle=40.0, recalc=True):
    # Сварка вершин и развёртка — в Builder.finish; затем нормали наружу (кроме комнаты, у неё грани смотрят внутрь),
    # гладкое затенение с острыми рёбрами по углу и модификатор Bevel с выравниванием нормалей (применяется при экспорте)
    if recalc:
        bmesh.ops.remove_doubles(b.bm, verts=b.bm.verts, dist=0.0005)
        bmesh.ops.recalc_face_normals(b.bm, faces=b.bm.faces)
    obj = b.finish(col)
    me = obj.data
    for p in me.polygons:
        p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(angle))
    if bevel > 0:
        m = obj.modifiers.new("Bevel", "BEVEL")
        m.width = bevel
        m.segments = segs
        m.limit_method = "ANGLE"
        m.angle_limit = math.radians(angle)
        m.harden_normals = True
        m.use_clamp_overlap = True
    return obj


# ---------------------------------------------------------------- комната

def room(col, name):
    b = Builder(name)
    t = WALL_T
    # Пол и стены — коробки толщиной t: внутрь комнаты смотрят их внешние грани
    b.box((X0 - t, Y0 - t, -0.2), (X1 + t, Y1 + t, 0.0), "BK_Floor", skip=("-z",))
    for z0, z1, mat in ((0.0, PAINT_H, "BK_WallPaint"), (PAINT_H, WALL_H, "BK_Concrete")):
        b.box((X0 - t, Y0, z0), (X0, Y1, z1), mat, skip=("-x", "-y", "+y", "+z", "-z"))
        b.box((X1, Y0, z0), (X1 + t, Y1, z1), mat, skip=("+x", "-y", "+y", "+z", "-z"))
        b.box((X0 - t, Y0 - t, z0), (X1 + t, Y0, z1), mat, skip=("-y", "-x", "+x", "+z", "-z"))
        b.box((X0 - t, Y1, z0), (X1 + t, Y1 + t, z1), mat, skip=("+y", "-x", "+x", "+z", "-z"))
    # Свод: внутренняя поверхность из полос вдоль y и торцевые люнеты
    n = 32
    xs = [X0 + (X1 - X0) * i / n for i in range(n + 1)]
    for i in range(n):
        p0, p1 = (xs[i], Y0, vault_z(xs[i])), (xs[i + 1], Y0, vault_z(xs[i + 1]))
        p2, p3 = (xs[i + 1], Y1, vault_z(xs[i + 1])), (xs[i], Y1, vault_z(xs[i]))
        b.face([p0, p3, p2, p1], "BK_Concrete")
    b.face([(x, Y0, vault_z(x)) for x in xs], "BK_Concrete")
    b.face([(x, Y1, vault_z(x)) for x in reversed(xs)], "BK_Concrete")
    # Цоколь по периметру и полоса-разделитель между краской и побелкой
    for (x0, y0, x1, y1) in ((X0, Y0, X0 + 0.03, Y1), (X1 - 0.03, Y0, X1, Y1), (X0, Y0, X1, Y0 + 0.03), (X0, Y1 - 0.03, X1, Y1)):
        cube(b, (x0, y0, 0.0), (x1, y1, 0.12), "BK_Paint")
        cube(b, (x0, y0, PAINT_H - 0.015), (x1, y1, PAINT_H + 0.015), "BK_Paint")
    # Рёбра свода через 1,25 м с пилястрами
    y = Y0 + 0.6
    while y < Y1 - 0.3:
        prev = None
        for i in range(n + 1):
            x = xs[i]
            z = vault_z(x)
            # Нормаль свода внутрь: от центра окружности
            half = (X1 - X0) / 2
            rr = (half * half + RISE * RISE) / (2 * RISE)
            cz = WALL_H + RISE - rr
            d = Vector((x - (X0 + X1) / 2, 0, z - cz)).normalized()
            inner = Vector((x, 0, z)) - d * 0.1
            outer = Vector((x, 0, z))
            cur = (outer, inner)
            if prev:
                q = [Vector((prev[0].x, y, prev[0].z)), Vector((prev[1].x, y, prev[1].z)),
                     Vector((cur[1].x, y, cur[1].z)), Vector((cur[0].x, y, cur[0].z))]
                face_out(b, q, "BK_Concrete", (0, -1, 0))
                face_out(b, [p + Vector((0, 0.2, 0)) for p in q], "BK_Concrete", (0, 1, 0))
                face_out(b, [Vector((prev[1].x, y, prev[1].z)), Vector((cur[1].x, y, cur[1].z)),
                             Vector((cur[1].x, y + 0.2, cur[1].z)), Vector((prev[1].x, y + 0.2, prev[1].z))],
                         "BK_Concrete", -d)
            prev = cur
        for x, s in ((X0, 1), (X1, -1)):
            busy = (DOOR_Y, 0.8) if s > 0 else (TIPS_CENTER[1], 0.6)
            if abs(y + 0.1 - busy[0]) < busy[1] + 0.1:
                continue
            xa, xb = sorted((x, x + s * 0.1))
            b.box((xa, y, 0.12), (xb, y + 0.2, WALL_H), "BK_Concrete", skip=("-x" if s > 0 else "+x",))
        y += 1.25

    # Трубы справа под сводом: с фланцами, хомутами на тягах и коленом в переднюю стену
    for px, pz, r, mat in ((X1 - 0.2, WALL_H - 0.12, 0.075, "BK_Paint"), (X1 - 0.42, WALL_H - 0.04, 0.05, "PS_Rust"),
                           (X1 - 0.17, 0.28, 0.06, "BK_PaintCream")):
        bend = 0.25
        path = [Vector((px, Y0, pz)), Vector((px, Y1 - bend - 0.1, pz))]
        path += arc_pts((px, Y1 - bend - 0.1, pz + bend), (0, 0, -1), (0, 1, 0), bend, 0, math.pi / 2, 6)[1:]
        path += [Vector((px, Y1 + 0.05, pz + bend + 0.2))]
        sweep(b, path, r, mat, segs=16, caps=False)
        y = Y0 + 0.4
        while y < Y1 - 0.5:
            tube(b, (px, y, pz), (0, 1, 0), r + 0.022, 0.035, mat, segs=16)  # фланец
            for k in range(6):
                a = 2 * math.pi * k / 6
                tube(b, (px + math.cos(a) * (r + 0.012), y - 0.01, pz + math.sin(a) * (r + 0.012)), (0, 1, 0), 0.007, 0.055,
                     "BK_Steel", segs=6)
            yc = y + 0.7
            if pz > 1.0:
                torus(b, (px, yc, pz), (0, 1, 0), r + 0.012, 0.01, "BK_Steel", segs=16, tsegs=6)
                tube(b, (px, yc, pz + r + 0.02), (0, 0, 1), 0.008, vault_z(px) - pz - r - 0.02 + 0.05, "BK_Steel", segs=6)
            else:
                # Нижняя труба на стойках-кронштейнах от стены
                cube(b, (px - 0.01, yc - 0.02, 0.0), (px + 0.01, yc + 0.02, pz - r), "BK_Steel")
                torus(b, (px, yc, pz), (0, 1, 0), r + 0.012, 0.01, "BK_Steel", segs=16, tsegs=6)
            y += 1.6
    # Вентиль на верхней трубе: корпус, шток и красный маховик
    vx, vy, vz = X1 - 0.2, -1.6, WALL_H - 0.12
    lathe(b, (vx, vy - 0.12, vz), [(0.0, 0.0), (0.09, 0.0), (0.11, 0.06), (0.11, 0.18), (0.09, 0.24), (0.0, 0.24)],
          "BK_Paint", axis=(0, 1, 0), segs=16)
    tube(b, (vx - 0.1, vy, vz), (-1, 0, 0), 0.018, 0.16, "BK_Steel", segs=10)
    torus(b, (vx - 0.27, vy, vz), (1, 0, 0), 0.11, 0.012, "BK_PaintRed", segs=24, tsegs=8)
    for k in range(4):
        a = math.pi * k / 2 + math.pi / 4
        d = Vector((0, math.cos(a), math.sin(a)))
        sweep(b, [Vector((vx - 0.27, vy, vz)), Vector((vx - 0.27, vy, vz)) + d * 0.105], 0.008, "BK_PaintRed", segs=6)
    lathe(b, (vx - 0.29, vy, vz), [(0.0, 0.0), (0.025, 0.0), (0.025, 0.04), (0.0, 0.04)], "BK_PaintRed", axis=(1, 0, 0), segs=12)

    # Фильтровентиляционная установка у задней стены слева: рама, два фильтра, вентилятор с ручным приводом
    fx, fy = FVU_X, Y0
    M = wall_frame((fx, fy, 0.0), (0, 1, 0))
    b.m = M
    for x in (-0.55, 0.55):
        for y in (0.08, 0.62):
            cube(b, (x - 0.025, y - 0.025, 0.0), (x + 0.025, y + 0.025, 0.35), "BK_Paint")
    cube(b, (-0.6, 0.05, 0.35), (0.6, 0.65, 0.39), "BK_Paint")
    for x in (-0.3, 0.3):
        lathe(b, (x, 0.35, 0.39), [(0.0, 0.0), (0.2, 0.0), (0.21, 0.03), (0.21, 0.62), (0.2, 0.66), (0.08, 0.7),
                                    (0.06, 0.78), (0.0, 0.78)], "BK_Paint", segs=24)
        for h in (0.15, 0.35, 0.55):
            torus(b, (x, 0.35, 0.39 + h), (0, 0, 1), 0.212, 0.008, "BK_Steel", segs=24, tsegs=6)
    # Вентилятор: улитка на раме, вал и рукоятка
    lathe(b, (0.0, 0.25, 1.45), [(0.0, 0.0), (0.22, 0.0), (0.24, 0.02), (0.24, 0.2), (0.22, 0.22), (0.0, 0.22)],
          "BK_Paint", axis=(0, 1, 0), segs=24)
    cube(b, (-0.08, 0.05, 0.39), (0.08, 0.45, 1.25), "BK_Paint")
    tube(b, (0.0, 0.47, 1.45), (0, 1, 0), 0.02, 0.12, "BK_Steel", segs=10)
    sweep(b, [Vector((0.0, 0.59, 1.45)), Vector((0.2, 0.59, 1.45)), Vector((0.2, 0.71, 1.45))], 0.013, "BK_Steel", segs=8)
    lathe(b, (0.2, 0.71, 1.45), [(0.0, 0.0), (0.022, 0.0), (0.025, 0.05), (0.02, 0.1), (0.0, 0.1)], "BK_Wood",
          axis=(0, 1, 0), segs=12)
    for x in (-0.3, 0.3):
        sweep(b, [Vector((x, 0.35, 1.17)), Vector((x, 0.35, 1.3)), Vector((x * 0.4, 0.3, 1.4)), Vector((0.0, 0.25, 1.45))],
              0.05, "BK_Galv", segs=12)
    b.reset()
    # Воздуховод вдоль свода от установки к передней стене с решётками
    dx, dz = DUCT_X, vault_z(DUCT_X) - 0.32
    sweep(b, [Vector((fx, Y0 + 0.36, 1.67)), Vector((fx, Y0 + 0.36, 1.95)),
              Vector((fx + 0.3, Y0 + 0.36, dz - 0.05)), Vector((dx, Y0 + 0.36, dz)), Vector((dx, Y1 + 0.05, dz))],
          0.12, "BK_Galv", segs=16, caps=False)
    y = Y0 + 1.0
    while y < Y1 - 0.3:
        tube(b, (dx, y, dz), (0, 1, 0), 0.127, 0.03, "BK_Galv", segs=16)  # бандаж
        tube(b, (dx, y + 0.015, dz + 0.12), (0, 0, 1), 0.006, vault_z(dx) - dz - 0.1, "BK_Steel", segs=6)
        y += 1.0
    for y in (-1.6, 0.6):
        cube(b, (dx - 0.1, y - 0.15, dz - 0.16), (dx + 0.1, y + 0.15, dz - 0.1), "BK_Galv")
        for k in range(6):
            yy = y - 0.12 + k * 0.048
            cube(b, (dx - 0.09, yy, dz - 0.17), (dx + 0.09, yy + 0.012, dz - 0.155), "BK_Steel")

    # Кабельный лоток по левой стене, пучок кабелей и спуск к пульту
    tz, ty0 = 2.02, -1.05
    cube(b, (X0 + 0.03, ty0, tz), (X0 + 0.25, Y1 - 0.2, tz + 0.01), "BK_Galv")
    cube(b, (X0 + 0.24, ty0, tz), (X0 + 0.25, Y1 - 0.2, tz + 0.06), "BK_Galv")
    y = ty0 + 0.1
    while y < Y1 - 0.2:
        cube(b, (X0, y, tz - 0.06), (X0 + 0.26, y + 0.03, tz), "BK_Steel")
        y += 0.9
    rnd = random.Random(7)
    for k, (r, mat) in enumerate(((0.022, "BK_Rubber"), (0.016, "BK_Rubber"), (0.02, "BK_Paint"), (0.014, "BK_Rubber"),
                                  (0.018, "BK_Rubber"))):
        x = X0 + 0.06 + k * 0.038
        # Кабели выходят из свода над началом лотка
        pts = [Vector((x + 0.15, ty0 - 0.1, vault_z(x + 0.15) + 0.02)), Vector((x + 0.02, ty0 - 0.05, tz + 0.08)),
               Vector((x, ty0 + 0.05, tz + 0.01 + r))]
        y = ty0 + 0.05
        while y < Y1 - 0.5:
            y += 0.45
            pts.append(Vector((x + rnd.uniform(-0.008, 0.008), y, tz + 0.01 + r + rnd.uniform(0, 0.01))))
        pts.append(Vector((x, Y1 - 0.25, tz + 0.01 + r)))
        pts += [Vector((x + 0.05, Y1 - 0.12, tz - 0.02)), Vector((-1.0 + k * 0.04, Y1 - 0.1, 1.75)),
                Vector((-0.6 + k * 0.03, 1.48, 1.45))]
        sweep(b, pts, r, mat, segs=8)

    # Щиток с автоматами и рубильник на левой стене у пульта
    b.m = wall_frame((X0, 0.6, 0.0), (1, 0, 0))
    cube(b, (-0.3, 0.0, 1.3), (0.3, 0.16, 1.95), "BK_Paint")
    cube(b, (-0.28, 0.16, 1.32), (0.28, 0.175, 1.93), "BK_Paint")  # дверца
    for z in (1.45, 1.8):
        tube(b, (0.31, 0.12, z - 0.05), (0, 0, 1), 0.012, 0.1, "BK_Steel", segs=8)
    lathe(b, (-0.22, 0.175, 1.62), [(0.0, 0.0), (0.02, 0.0), (0.018, 0.03), (0.0, 0.03)], "BK_Steel", axis=(0, 1, 0), segs=12)
    cube(b, (-0.1, 0.175, 1.82), (0.12, 0.18, 1.88), "BK_PaintYellow")  # знак «молния»
    # Рубильник: основание, ножи и рукоять вверх
    cube(b, (-0.87, 0.0, 1.35), (-0.63, 0.05, 1.75), "BK_Paint")
    for x in (-0.81, -0.75, -0.69):
        cube(b, (x - 0.012, 0.05, 1.42), (x + 0.012, 0.08, 1.46), "BK_Steel")
        cube(b, (x - 0.006, 0.07, 1.44), (x + 0.006, 0.09, 1.68), "BK_Steel")
    cube(b, (-0.83, 0.08, 1.64), (-0.67, 0.1, 1.66), "BK_Rubber")
    sweep(b, [Vector((-0.75, 0.09, 1.65)), Vector((-0.75, 0.18, 1.72))], 0.008, "BK_Steel", segs=8)
    lathe(b, (-0.75, 0.18, 1.72), [(0.0, 0.0), (0.02, 0.0), (0.022, 0.06), (0.018, 0.1), (0.0, 0.1)], "BK_PaintRed",
          axis=(0, 1, 0.6), segs=12)
    # Кабелепровод от щитка вниз к полу
    sweep(b, [Vector((0.0, 0.05, 1.3)), Vector((0.0, 0.05, 0.12))], 0.018, "BK_Galv", segs=10)
    b.reset()

    # Настенный светильник у двери: основание, стеклянный колпак и решётка
    bx, by, bz = BULKHEAD
    b.m = wall_frame((bx, by, bz), (1, 0, 0))
    lathe(b, (0, 0, 0), [(0.0, 0.0), (0.09, 0.0), (0.09, 0.04), (0.075, 0.05), (0.0, 0.05)], "BK_Paint",
          axis=(0, 1, 0), segs=20)
    lathe(b, (0, 0.05, 0), [(0.065, 0.0), (0.066, 0.03), (0.055, 0.07), (0.03, 0.09), (0.0, 0.095)], "BK_Bulb",
          axis=(0, 1, 0), segs=20)
    for k in range(4):
        a = math.pi * k / 4
        d = Vector((math.cos(a), 0, math.sin(a)))
        sweep(b, [d * 0.072 + Vector((0, 0.05, 0)) - d * 0.0 + d * 0.0, Vector((0, 0.06, 0)) + d * 0.072,
                  Vector((0, 0.1, 0)) + d * 0.04, Vector((0, 0.11, 0)) - d * 0.04, Vector((0, 0.06, 0)) - d * 0.072,
                  Vector((0, 0.05, 0)) - d * 0.072], 0.004, "BK_Paint", segs=5)
    b.reset()

    # Табличка «Укрытие» на правой стене (текст ставит Unity)
    b.m = wall_frame((X1, -2.1, 0.0), (-1, 0, 0))
    cube(b, (-0.3, 0.0, 1.5), (0.3, 0.015, 1.72), "BK_PaintCream")
    b.reset()
    # Рамка карты района над пультом и пробковая доска под листок с советами
    cx, _, cz = MAP_CENTER
    h = MAP_SIZE / 2
    b.m = wall_frame((cx, Y1, 0.0), (0, -1, 0))
    frame(b, rrect(-h - 0.05, h + 0.05, cz - h - 0.05, cz + h + 0.05, 0.01, 1),
          rrect(-h, h, cz - h, cz + h, 0.005, 1), "xz", 0.0, 0.03, "BK_Wood")
    b.reset()
    tx, ty, tz2 = TIPS_CENTER
    w, hh = TIPS_SIZE[0] / 2 + 0.08, TIPS_SIZE[1] / 2 + 0.08
    b.m = wall_frame((X1, ty, 0.0), (-1, 0, 0))
    cube(b, (-w, 0.0, tz2 - hh), (w, 0.02, tz2 + hh), "BK_Wood")
    frame(b, rrect(-w - 0.03, w + 0.03, tz2 - hh - 0.03, tz2 + hh + 0.03, 0.005, 1),
          rrect(-w, w, tz2 - hh, tz2 + hh, 0.003, 1), "xz", 0.0, 0.035, "BK_Paint")
    b.reset()
    # Часы на передней стене
    b.m = wall_frame((-1.25, Y1, 1.95), (0, -1, 0))
    lathe(b, (0, 0, 0), [(0.0, 0.0), (0.16, 0.0), (0.165, 0.03), (0.15, 0.05), (0.0, 0.05)], "BK_Steel", axis=(0, 1, 0),
          segs=32)
    lathe(b, (0, 0.04, 0), [(0.0, 0.0), (0.14, 0.0), (0.14, 0.012), (0.0, 0.012)], "BK_PaintCream", axis=(0, 1, 0), segs=32)
    for k in range(12):
        a = math.pi * 2 * k / 12
        cube(b, (math.cos(a) * 0.12 - 0.004, 0.052, math.sin(a) * 0.12 - 0.004),
             (math.cos(a) * 0.12 + 0.004, 0.056, math.sin(a) * 0.12 + 0.004), "BK_Rubber")
    cube(b, (-0.004, 0.056, -0.005), (0.004, 0.06, 0.08), "BK_Rubber")
    cube(b, (-0.005, 0.06, -0.004), (0.11, 0.064, 0.004), "BK_Rubber")
    b.reset()
    # Сливной трап в полу
    cube(b, (-0.25, -1.55, 0.0), (0.05, -1.25, 0.012), "BK_Steel")
    for k in range(7):
        x = -0.23 + k * 0.04
        cube(b, (x, -1.53, 0.012), (x + 0.012, -1.27, 0.018), "BK_Steel")
    return finish(b, col, bevel=0.012, segs=2, recalc=False)


# ---------------------------------------------------------------- гермодверь

def door(col, name):
    # Гермодверь в левой стене: рама со скруглёнными углами, полотно с рёбрами, кремальеры, петли, штурвал, глазок
    b = Builder(name)
    b.m = wall_frame((X0, DOOR_Y, 0.0), (1, 0, 0))
    ow, oh = 0.62, 2.0
    frame(b, rrect(-ow - 0.12, ow + 0.12, 0.0, oh + 0.12, 0.2, 6), rrect(-0.5, 0.5, 0.22, 1.98, 0.14, 6),
          "xz", 0.0, 0.1, "BK_Paint")
    # Полотно: плита, выпуклая филёнка и горизонтальные рёбра
    prism(b, rrect(-0.56, 0.56, 0.18, 2.02, 0.16, 6), "xz", 0.1, 0.2, "BK_Paint")
    frame(b, rrect(-0.5, 0.5, 0.24, 1.96, 0.12, 6), rrect(-0.44, 0.44, 0.3, 1.9, 0.08, 6), "xz", 0.2, 0.225, "BK_Paint")
    for z in (0.62, 1.55):
        cube(b, (-0.44, 0.2, z - 0.03), (0.44, 0.235, z + 0.03), "BK_Paint")
    frame(b, rrect(-0.565, 0.565, 0.175, 2.025, 0.165, 6), rrect(-0.55, 0.55, 0.19, 2.01, 0.15, 6), "xz", 0.09, 0.105,
          "BK_Rubber")  # уплотнитель
    # Петли со стороны +x
    for z in (0.5, 1.7):
        lathe(b, (0.66, 0.14, z - 0.15), [(0.0, 0.0), (0.045, 0.0), (0.05, 0.02), (0.05, 0.28), (0.045, 0.3), (0.0, 0.3)],
              "BK_Paint", segs=16)
        cube(b, (0.45, 0.17, z - 0.1), (0.66, 0.215, z + 0.1), "BK_Paint")
        lathe(b, (0.66, 0.14, z + 0.15), [(0.0, 0.0), (0.02, 0.0), (0.0, 0.03)], "BK_Steel", segs=10)
    # Кремальеры со стороны ручки: ось на раме, рычаг поперёк щели и шар на конце
    for z in (0.45, 1.1, 1.75):
        lathe(b, (-0.62, 0.1, z), [(0.0, 0.0), (0.035, 0.0), (0.035, 0.08), (0.025, 0.1), (0.0, 0.1)], "BK_Steel",
              axis=(0, 1, 0), segs=12)
        sweep(b, [Vector((-0.62, 0.2, z)), Vector((-0.5, 0.25, z + 0.02)), Vector((-0.36, 0.27, z + 0.05))], 0.016,
              "BK_Steel", segs=8)
        lathe(b, (-0.36, 0.27, z + 0.05), [(0.0, -0.03), (0.022, -0.02), (0.03, 0.0), (0.022, 0.02), (0.0, 0.03)],
              "BK_PaintRed", axis=(0, 1, 0), segs=12)
    # Штурвал на ступице
    wc = Vector((0.05, 0.3, 1.1))
    lathe(b, wc - Vector((0, 0.08, 0)), [(0.0, 0.0), (0.07, 0.0), (0.07, 0.04), (0.05, 0.07), (0.035, 0.11), (0.0, 0.11)],
          "BK_Steel", axis=(0, 1, 0), segs=20)
    torus(b, wc + Vector((0, 0.02, 0)), (0, 1, 0), 0.24, 0.02, "BK_Steel", segs=32, tsegs=10)
    for k in range(4):
        a = math.pi * k / 2 + math.pi / 4
        d = Vector((math.cos(a), 0, math.sin(a)))
        sweep(b, [wc + Vector((0, 0.02, 0)) + d * 0.04, wc + Vector((0, 0.03, 0)) + d * 0.12,
                  wc + Vector((0, 0.02, 0)) + d * 0.225], 0.013, "BK_Steel", segs=8)
    cube(b, (-0.24, 0.2, 1.68), (0.24, 0.21, 1.82), "BK_PaintCream")  # табличка «ВЫХОД» (текст ставит Unity)
    b.reset()
    return finish(b, col, bevel=0.008, segs=2)


# ---------------------------------------------------------------- пульт

def section_m(deg):
    return rot_z(deg)


def desk_section(b, deg):
    # Сегмент пульта: столешница-трапеция с резиновым подлокотником, фартук, задняя стенка, перегородка справа
    a0, a1 = math.radians(deg - SECTION / 2), math.radians(deg + SECTION / 2)

    def p(r, a, z=0.0):
        return Vector((-math.sin(a) * r, math.cos(a) * r, z))

    top = [p(DESK_R0, a0), p(DESK_R1, a0), p(DESK_R1, a1), p(DESK_R0, a1)]
    prism(b, [(q.x, q.y) for q in top], "xy", DESK_Z, DESK_Z + 0.035, "BK_Paint")
    sweep(b, [p(DESK_R0 - 0.005, a0, DESK_Z + 0.02), p(DESK_R0 - 0.005, a1, DESK_Z + 0.02)], 0.022, "BK_Rubber", segs=10)
    # Фартук под передним краем и задняя стенка
    m = (a0 + a1) / 2
    for r, z0, z1 in ((DESK_R0 + 0.06, 0.62, DESK_Z), (1.0, 0.0, DESK_Z)):
        w = math.tan(SECTION / 2 * math.pi / 180) * r * 0.98
        b.m = rot_z(math.degrees(m))
        cube(b, (-w, r, z0), (w, r + 0.025, z1), "BK_Paint")
        b.reset()
    b.m = rot_z(math.degrees(a0))
    cube(b, (-0.025, DESK_R0 + 0.05, 0.0), (0.025, DESK_R1 - 0.02, DESK_Z), "BK_Paint")
    cube(b, (-0.03, DESK_R0 + 0.04, 0.0), (0.03, DESK_R1, 0.08), "BK_Rubber")
    b.reset()


def strip_frame(deg):
    # Наклонная панель перед экраном: x — вдоль, y — вверх по скату, z — нормаль панели
    y0, y1, z0, z1 = 0.6, 0.93, DESK_Z + 0.035, DESK_Z + 0.135
    slope = math.atan2(z1 - z0, y1 - y0)
    return rot_z(deg) @ Matrix.Translation((0, y0, z0)) @ Matrix.Rotation(slope, 4, "X"), math.hypot(y1 - y0, z1 - z0)


def gauge(b, x, y, r, mat_face="BK_PaintCream"):
    # Стрелочный прибор: обод, шкала с рисками, стрелка, стекло
    lathe(b, (x, y, 0), [(0.0, 0.0), (r + 0.008, 0.0), (r + 0.01, 0.012), (r + 0.004, 0.022), (r - 0.002, 0.02),
                         (0.0, 0.02)], "BK_Steel", segs=24)
    lathe(b, (x, y, 0.004), [(0.0, 0.0), (r - 0.002, 0.0), (r - 0.002, 0.0125), (0.0, 0.0125)], mat_face, segs=24)
    for k in range(9):
        a = math.radians(210 - 240 * k / 8)
        cube(b, (x + math.cos(a) * r * 0.78 - 0.0015, y + math.sin(a) * r * 0.78 - 0.0015, 0.0165),
             (x + math.cos(a) * r * 0.78 + 0.0015, y + math.sin(a) * r * 0.78 + 0.0015, 0.0175), "BK_Rubber")
    a = math.radians(random.Random(int(x * 1000 + y * 100)).uniform(40, 150))
    sweep(b, [Vector((x, y, 0.018)), Vector((x + math.cos(a) * r * 0.75, y + math.sin(a) * r * 0.75, 0.018))], 0.0018,
          "BK_PaintRed", segs=4)


def knob(b, x, y, r, mat="BK_Rubber"):
    lathe(b, (x, y, 0), [(0.0, 0.0), (r * 1.25, 0.0), (r * 1.25, 0.006), (r, 0.01), (r * 0.95, 0.035), (r * 0.7, 0.04),
                         (0.0, 0.04)], mat, segs=16)
    cube(b, (x - 0.002, y + r * 0.3, 0.04), (x + 0.002, y + r * 0.9, 0.042), "BK_PaintCream")


def toggle(b, x, y, up=True):
    lathe(b, (x, y, 0), [(0.0, 0.0), (0.012, 0.0), (0.012, 0.008), (0.008, 0.012), (0.0, 0.012)], "BK_Steel", segs=12)
    tip = Vector((x, y + (0.012 if up else -0.012), 0.04))
    sweep(b, [Vector((x, y, 0.01)), tip], 0.003, "BK_Steel", segs=6)
    lathe(b, tip, [(0.0, -0.003), (0.0045, 0.0), (0.0035, 0.006), (0.0, 0.007)], "BK_Steel", segs=8)


def lamp_light(b, x, y, mat):
    lathe(b, (x, y, 0), [(0.0, 0.0), (0.014, 0.0), (0.014, 0.008), (0.011, 0.01), (0.0, 0.01)], "BK_Steel", segs=12)
    lathe(b, (x, y, 0.009), [(0.0, 0.0), (0.0095, 0.0), (0.009, 0.006), (0.005, 0.011), (0.0, 0.012)], mat, segs=12)


def control_strip(b, deg, layout):
    M, L = strip_frame(deg)
    # Клин-подставка под панелью и сама панель
    b.m = rot_z(deg)
    prism(b, [(0.6, DESK_Z + 0.035), (0.93, DESK_Z + 0.035), (0.93, DESK_Z + 0.135)], "yz", -0.43, 0.43, "BK_Paint")
    b.m = M
    cube(b, (-0.43, 0.0, -0.012), (0.43, L, 0.0), "BK_Paint")
    cube(b, (-0.41, 0.02, 0.0), (0.41, L - 0.02, 0.003), "BK_PaintCream")  # лицевая пластина
    rnd = random.Random(int(deg) + 11)
    if layout == "main":
        gauge(b, -0.33, 0.2, 0.05)
        gauge(b, 0.33, 0.2, 0.05)
        for i in range(6):
            toggle(b, -0.2 + i * 0.08, 0.24, up=rnd.random() < 0.5)
            cube(b, (-0.2 + i * 0.08 - 0.018, 0.27, 0.003), (-0.2 + i * 0.08 + 0.018, 0.282, 0.0045), "BK_Rubber")
        for i, m in enumerate(("BK_LampGreen", "BK_LampGreen", "BK_LampAmber", "BK_LampRed", "BK_LampGreen")):
            lamp_light(b, -0.16 + i * 0.08, 0.15, m)
        # Клавишный блок
        for i in range(10):
            for j in range(3):
                x, y = -0.2 + i * 0.044, 0.035 + j * 0.034
                prism(b, rrect(x - 0.016, x + 0.016, y - 0.012, y + 0.012, 0.004, 2), "xy", 0.003, 0.016,
                      "BK_PaintCream" if (i + j) % 5 else "BK_Rubber")
        knob(b, -0.33, 0.06, 0.018)
        knob(b, 0.33, 0.06, 0.018)
    else:
        gauge(b, -0.3, 0.19, 0.06)
        for i in range(4):
            knob(b, -0.16 + i * 0.09, 0.2, 0.02)
        for i in range(8):
            toggle(b, -0.2 + i * 0.06, 0.08, up=rnd.random() < 0.5)
        for i, m in enumerate(("BK_LampRed", "BK_LampAmber", "BK_LampGreen")):
            lamp_light(b, 0.24 + i * 0.05, 0.22, m)
        lamp_light(b, 0.32, 0.12, "BK_LampGreen")
    b.reset()


def screen_housing(b, deg, size):
    # Корпус экрана: скошенный короб, рамка со скруглённым проёмом, утопленное стекло BK_Screen,
    # козырёк, вентиляционные прорези, табличка, ручки регулировки, винты
    b.m = rot_z(deg)
    sw, sh = size
    hw = 0.5
    z0 = DESK_Z + 0.035
    z1 = SCREEN_Z + sh / 2 + 0.14
    yb = SCREEN_Y + 0.06
    prism(b, [(yb, z0), (1.4, z0), (1.4, z1 - 0.06), (1.28, z1), (yb, z1)], "yz", -hw, hw, "BK_Paint")
    frame(b, rrect(-hw, hw, z0, z1, 0.03, 4),
          rrect(-sw / 2, sw / 2, SCREEN_Z - sh / 2, SCREEN_Z + sh / 2, 0.035, 4), "xz", SCREEN_Y, yb, "BK_Paint")
    prism(b, rrect(-sw / 2 - 0.01, sw / 2 + 0.01, SCREEN_Z - sh / 2 - 0.01, SCREEN_Z + sh / 2 + 0.01, 0.04, 4), "xz",
          GLASS_Y, yb, "BK_Screen")
    # Козырёк
    prism(b, [(SCREEN_Y - 0.13, z1 - 0.005), (yb + 0.05, z1 - 0.005), (yb + 0.05, z1 + 0.02), (SCREEN_Y - 0.13, z1 + 0.0)],
          "yz", -hw - 0.01, hw + 0.01, "BK_Paint")
    for i in range(9):
        x = -0.36 + i * 0.09
        cube(b, (x - 0.03, 1.12, z1 - 0.004), (x + 0.03, 1.24, z1 + 0.002), "BK_Rubber")
    # Табличка и ручки под экраном
    zb = (z0 + SCREEN_Z - sh / 2) / 2
    cube(b, (-0.15, SCREEN_Y - 0.004, zb - 0.025), (0.15, SCREEN_Y, zb + 0.025), "BK_PaintCream")
    for x in (0.3, 0.38):
        lathe(b, (x, SCREEN_Y, zb), [(0.0, 0.0), (0.018, 0.0), (0.017, 0.02), (0.012, 0.028), (0.0, 0.028)], "BK_Rubber",
              axis=(0, -1, 0), segs=16)
    lamp_light_wall(b, -0.36, zb, "BK_LampGreen")
    for x in (-hw + 0.035, hw - 0.035):
        for z in (z0 + 0.03, z1 - 0.03):
            lathe(b, (x, SCREEN_Y, z), [(0.0, 0.0), (0.008, 0.0), (0.006, 0.003), (0.0, 0.004)], "BK_Steel",
                  axis=(0, -1, 0), segs=10)
    b.reset()


def lamp_light_wall(b, x, z, mat):
    lathe(b, (x, SCREEN_Y, z), [(0.0, 0.0), (0.014, 0.0), (0.014, 0.008), (0.011, 0.01), (0.0, 0.01)], "BK_Steel",
          axis=(0, -1, 0), segs=12)
    lathe(b, (x, SCREEN_Y - 0.009, z), [(0.0, 0.0), (0.0095, 0.0), (0.009, 0.006), (0.005, 0.011), (0.0, 0.012)], mat,
          axis=(0, -1, 0), segs=12)


def radio(b):
    # Радиостанция на правой секции: корпус, лицевая панель с шкалой, приборами, ручками и гнёздами, гарнитура
    b.m = rot_z(-SECTION) @ Matrix.Translation((0.0, 1.1, DESK_Z + 0.035)) @ Matrix.Rotation(math.radians(8), 4, "Z")
    cube(b, (-0.4, -0.16, 0.0), (0.4, 0.2, 0.34), "BK_Paint")
    cube(b, (-0.385, -0.165, 0.015), (0.385, -0.16, 0.325), "BK_PaintCream")
    frame(b, rrect(-0.33, -0.02, 0.2, 0.3, 0.012, 3), rrect(-0.32, -0.03, 0.21, 0.29, 0.008, 3), "xz", -0.18, -0.165,
          "BK_Steel")
    prism(b, rrect(-0.32, -0.03, 0.21, 0.29, 0.008, 3), "xz", -0.17, -0.165, "BK_LampAmber")  # шкала с подсветкой
    for k in range(13):
        x = -0.31 + k * 0.0225
        cube(b, (x - 0.001, -0.172, 0.215), (x + 0.001, -0.171, 0.24 if k % 2 else 0.25), "BK_Rubber")
    # Дальше элементы на лицевой плоскости: x вправо, y вверх, z — наружу (−y корпуса)
    b.m = b.m @ Matrix(((1, 0, 0, 0), (0, 0, -1, -0.165), (0, 1, 0, 0), (0, 0, 0, 1)))
    for x in (0.14, 0.24):
        gauge(b, x, 0.25, 0.04)
    for i in range(5):
        knob(b, -0.3 + i * 0.075, 0.1, 0.02 if i % 2 else 0.026)
    for i in range(4):
        toggle(b, 0.08 + i * 0.05, 0.1, up=i % 2 == 0)
    for i in range(3):
        lathe(b, (0.3 + 0.0, 0.06 + i * 0.05, 0), [(0.0, 0.0), (0.012, 0.0), (0.012, 0.012), (0.006, 0.014), (0.0, 0.014)],
              "BK_Steel", segs=10)
    b.reset()
    # Ручка на крышке
    b.m = rot_z(-SECTION) @ Matrix.Translation((0.0, 1.1, DESK_Z + 0.035)) @ Matrix.Rotation(math.radians(8), 4, "Z")
    sweep(b, [Vector((-0.2, 0.02, 0.34)), Vector((-0.2, 0.02, 0.38)), Vector((0.2, 0.02, 0.38)), Vector((0.2, 0.02, 0.34))],
          0.01, "BK_Steel", segs=8)
    # Гарнитура на крышке: оголовье и два наушника
    hx, hy = 0.0, -0.05
    torus(b, (hx, hy, 0.345), (0, 1, 0), 0.1, 0.01, "BK_Rubber", segs=16, tsegs=6, arc=math.pi, phase=math.pi)
    for s in (-1, 1):
        lathe(b, (hx + s * 0.1, hy, 0.34), [(0.0, 0.0), (0.04, 0.0), (0.045, 0.02), (0.035, 0.045), (0.0, 0.05)],
              "BK_Rubber", axis=(s, 0, 0), segs=16)
    b.reset()


def field_phone(b):
    # Полевой телефон: корпус со скруглениями, трубка на рычагах, индукторная рукоятка
    b.m = rot_z(-SECTION - 4) @ Matrix.Translation((-0.08, 0.74, DESK_Z + 0.035))
    prism(b, rrect(-0.12, 0.12, -0.09, 0.09, 0.03, 4), "xy", 0.0, 0.11, "BK_Paint")
    for x in (-0.07, 0.07):
        cube(b, (x - 0.008, -0.01, 0.11), (x + 0.008, 0.01, 0.135), "BK_Rubber")
    sweep(b, [Vector((-0.1, 0.0, 0.15)), Vector((-0.06, 0.0, 0.135)), Vector((0.06, 0.0, 0.135)), Vector((0.1, 0.0, 0.15))],
          0.012, "BK_Rubber", segs=10)
    for s in (-1, 1):
        lathe(b, (s * 0.11, 0.0, 0.15), [(0.0, 0.0), (0.03, 0.0), (0.032, 0.015), (0.024, 0.03), (0.0, 0.032)],
              "BK_Rubber", axis=(0, 0, -1), segs=14)
    sweep(b, [Vector((0.12, 0.03, 0.06)), Vector((0.15, 0.03, 0.06)), Vector((0.15, 0.03, 0.0)), Vector((0.17, 0.03, 0.0))],
          0.007, "BK_Steel", segs=6)
    lathe(b, (0.17, 0.03, 0.0), [(0.0, 0.0), (0.012, 0.0), (0.012, 0.05), (0.0, 0.05)], "BK_Wood", axis=(1, 0, 0), segs=10)
    # Шнур от трубки к корпусу
    sweep(b, [Vector((-0.12, 0.0, 0.14)), Vector((-0.16, 0.04, 0.08)), Vector((-0.14, 0.08, 0.02)),
              Vector((-0.1, 0.09, 0.05))], 0.005, "BK_Rubber", segs=6)
    b.reset()


def desk_lamp(b):
    # Настольная лампа на шарнирном кронштейне между главной секцией и секцией настроек
    a = math.radians(DESK_LAMP[0])
    c = Vector((-math.sin(a) * DESK_LAMP[1], math.cos(a) * DESK_LAMP[1], DESK_Z + 0.035))
    lathe(b, c, [(0.0, 0.0), (0.08, 0.0), (0.08, 0.015), (0.06, 0.03), (0.02, 0.035), (0.0, 0.035)], "BK_Paint", segs=24)
    j1 = c + Vector((0, 0, 0.05))
    j2 = j1 + Vector((0.0, 0.1, 0.3))
    j3 = j2 + Vector((math.sin(a) * 0.05, -math.cos(a) * 0.32, 0.02))
    for s in (-0.012, 0.012):
        off = Vector((s, 0, 0))
        sweep(b, [j1 + off, j2 + off], 0.006, "BK_Steel", segs=8)
        sweep(b, [j2 + off, j3 + off], 0.006, "BK_Steel", segs=8)
    for j in (j1, j2):
        tube(b, j - Vector((0.02, 0, 0)), (1, 0, 0), 0.012, 0.04, "BK_Steel", segs=10)
    # Абажур: конус со стенкой (замкнутый профиль), смотрит вниз-вперёд
    lathe(b, j3, [(0.0, 0.02), (0.03, 0.02), (0.075, -0.07), (0.08, -0.075), (0.035, 0.03), (0.0, 0.035)], "BK_Paint",
          axis=(0, 0.35, 1), segs=24)
    lathe(b, j3 + Vector((0, 0.01, -0.03)), [(0.0, -0.03), (0.02, -0.025), (0.022, 0.0), (0.0, 0.0)], "BK_Bulb",
          axis=(0, 0.35, 1), segs=12)


def papers(b):
    # Бумаги, планшет, кружка и карандаш на главной секции
    b.m = rot_z(-12) @ Matrix.Translation((0.25, 0.58, DESK_Z + 0.035))
    cube(b, (-0.11, -0.15, 0.0), (0.11, 0.15, 0.006), "BK_Wood")
    cube(b, (-0.1, -0.14, 0.006), (0.1, 0.13, 0.009), "BK_PaintCream")
    cube(b, (-0.04, 0.12, 0.009), (0.04, 0.15, 0.02), "BK_Steel")
    b.m = rot_z(9) @ Matrix.Translation((-0.32, 0.6, DESK_Z + 0.035))
    cube(b, (-0.1, -0.14, 0.0), (0.1, 0.14, 0.004), "BK_PaintCream")
    sweep(b, [Vector((0.06, -0.12, 0.009)), Vector((0.1, 0.02, 0.009))], 0.004, "BK_PaintYellow", segs=6)
    b.reset()
    c = rot_z(SECTION) @ Vector((0.3, 0.75, DESK_Z + 0.035))
    lathe(b, c, [(0.0, 0.0), (0.038, 0.0), (0.04, 0.005), (0.04, 0.09), (0.036, 0.092), (0.034, 0.012), (0.0, 0.012)],
          "BK_PaintCream", segs=20, closed=False)
    torus(b, c + Vector((0.045, 0, 0.05)), (0, 1, 0), 0.022, 0.006, "BK_PaintCream", segs=12, tsegs=6, arc=math.pi)


def console(col, name):
    b = Builder(name)
    for deg in (SECTION, 0.0, -SECTION):
        desk_section(b, deg)
    b.m = rot_z(SECTION * 1.5)
    cube(b, (-0.025, DESK_R0 + 0.05, 0.0), (0.025, DESK_R1 - 0.02, DESK_Z), "BK_Paint")
    b.reset()
    screen_housing(b, 0.0, MAIN_SCREEN)
    screen_housing(b, SECTION, SETTINGS_SCREEN)
    control_strip(b, 0.0, "main")
    control_strip(b, SECTION, "settings")
    radio(b)
    field_phone(b)
    desk_lamp(b)
    papers(b)
    # Пучок кабелей за пультом к стене
    for k in range(4):
        sweep(b, [Vector((-0.6 + k * 0.03, 1.48, 1.45)), Vector((-0.5 + k * 0.03, 1.42, 0.9)),
                  Vector((-0.3 + k * 0.05, 1.3, 0.8))], 0.012 + 0.003 * (k % 2), "BK_Rubber", segs=8)
    return finish(b, col, bevel=0.004, segs=2)


# ---------------------------------------------------------------- подвесная лампа

def lamp(col, name):
    # Подвесная лампа: розетка на своде, трубка подвеса, патрон, эмалированный абажур, колба и решётка
    b = Builder(name)
    lathe(b, (0, 0, -0.04), [(0.0, 0.0), (0.03, 0.0), (0.06, 0.03), (0.065, 0.04), (0.0, 0.04)], "BK_Paint", segs=24)
    tube(b, (0, 0, -0.04), (0, 0, -1), 0.012, 0.3, "BK_Steel", segs=12)
    lathe(b, (0, 0, -0.42), [(0.0, 0.0), (0.03, 0.0), (0.032, 0.04), (0.028, 0.08), (0.02, 0.09), (0.0, 0.09)],
          "BK_Steel", segs=20)
    lathe(b, (0, 0, -0.42), [(0.0, 0.03), (0.03, 0.03), (0.16, -0.06), (0.17, -0.068), (0.17, -0.06), (0.032, 0.045),
                             (0.0, 0.05)], "BK_PaintCream", segs=32)
    lathe(b, (0, 0, -0.42), [(0.0, -0.12), (0.028, -0.115), (0.04, -0.08), (0.03, -0.04), (0.02, -0.01), (0.0, 0.0)],
          "BK_Bulb", segs=20)
    # Решётка: кольца и дуги вниз под колбой
    for z, r in ((-0.48, 0.15), (-0.54, 0.1)):
        torus(b, (0, 0, z), (0, 0, 1), r, 0.005, "BK_Steel", segs=32, tsegs=6)
    for k in range(6):
        a = 2 * math.pi * k / 6
        d = Vector((math.cos(a), math.sin(a), 0))
        pts = [Vector((0, 0, -0.48)) + d * 0.15, Vector((0, 0, -0.52)) + d * 0.13, Vector((0, 0, -0.56)) + d * 0.08,
               Vector((0, 0, -0.58)) + d * 0.02]
        sweep(b, pts, 0.004, "BK_Steel", segs=5)
    lathe(b, (0, 0, -0.59), [(0.0, 0.0), (0.02, 0.0), (0.02, 0.012), (0.0, 0.012)], "BK_Steel", segs=12)
    return finish(b, col, bevel=0.002, segs=2)


# ---------------------------------------------------------------- реквизит

def crate(b, c, size, yaw=0.0, lid=True):
    # Армейский ящик: доски с зазорами, обвязка по рёбрам, металлические уголки и верёвочные ручки
    prev = b.m
    b.m = prev @ rot_z(yaw, c)
    sx, sy, sz = size
    t = 0.022
    cube(b, (-sx / 2 + t, -sy / 2 + t, t), (sx / 2 - t, sy / 2 - t, sz - t), "BK_Wood")  # внутренний объём
    n = max(2, int(sz / 0.11))
    for side in (-1, 1):
        for i in range(n):
            z0, z1 = sz * i / n + 0.003, sz * (i + 1) / n - 0.003
            cube(b, (-sx / 2, side * sy / 2 - (t if side > 0 else 0), z0), (sx / 2, side * sy / 2 + (0 if side > 0 else t), z1),
                 "BK_Wood")
            cube(b, (side * sx / 2 - (t if side > 0 else 0), -sy / 2 + t, z0), (side * sx / 2 + (0 if side > 0 else t), sy / 2 - t,
                                                                                 z1), "BK_Wood")
    m = max(2, int(sx / 0.13))
    for i in range(m):
        x0, x1 = -sx / 2 + sx * i / m + 0.003, -sx / 2 + sx * (i + 1) / m - 0.003
        cube(b, (x0, -sy / 2, sz - t), (x1, sy / 2, sz), "BK_Wood")
    # Обвязка: брусья по вертикальным рёбрам и металлические уголки сверху и снизу
    def span(s, half, inside, outside):
        return (half - inside, half + outside) if s > 0 else (-half - outside, -half + inside)
    for xs in (-1, 1):
        for ys in (-1, 1):
            ax, ay = span(xs, sx / 2, 0.04, 0.012), span(ys, sy / 2, 0.04, 0.012)
            cube(b, (ax[0], ay[0], 0.0), (ax[1], ay[1], sz), "BK_Wood")
            ax, ay = span(xs, sx / 2, 0.05, 0.016), span(ys, sy / 2, 0.05, 0.016)
            for z in (0.0, sz - 0.05):
                cube(b, (ax[0], ay[0], z), (ax[1], ay[1], z + 0.05), "BK_Steel")
    for xs in (-1, 1):
        torus(b, (xs * (sx / 2 + 0.03), 0, sz * 0.6), (1, 0, 0), 0.05, 0.008, "BK_Burlap", segs=12, tsegs=6, arc=math.pi)
    b.m = prev


def ammo_box(b, c, yaw):
    # Металлический ящик-цинк с крышкой на защёлке и ручкой
    prev = b.m
    b.m = prev @ rot_z(yaw, c)
    cube(b, (-0.17, -0.09, 0.0), (0.17, 0.09, 0.2), "BK_Paint")
    cube(b, (-0.175, -0.095, 0.2), (0.175, 0.095, 0.225), "BK_Paint")
    for x in (-0.12, 0.0, 0.12):
        cube(b, (x - 0.015, -0.096, 0.03), (x + 0.015, 0.096, 0.17), "BK_Paint")
    cube(b, (-0.03, -0.11, 0.17), (0.03, -0.095, 0.23), "BK_Steel")
    sweep(b, [Vector((-0.06, 0.0, 0.225)), Vector((-0.06, 0.0, 0.25)), Vector((0.06, 0.0, 0.25)), Vector((0.06, 0.0, 0.225))],
          0.006, "BK_Steel", segs=6)
    b.m = prev


def sandbag(b, c, yaw, rnd, length=0.5):
    # Мешок с песком: шумный приплюснутый эллипсоид и завязанный «хвост»
    prev = b.m
    b.m = prev @ rot_z(yaw, c)
    ellipsoid(b, (0, 0, 0.08), (length / 2, 0.17, 0.085), "BK_Burlap", segs=14, rings=8, noise=0.06, rnd=rnd,
              squash_top=0.45)
    lathe(b, (length / 2 - 0.02, 0, 0.08), [(0.0, 0.0), (0.035, 0.0), (0.02, 0.04), (0.03, 0.07), (0.0, 0.075)],
          "BK_Burlap", axis=(1, 0, 0), segs=8)
    b.m = prev


def props(col, name):
    b = Builder(name)
    rnd = random.Random(42)
    # Ящики и цинки в правом заднем углу
    crate(b, (X1 - 0.7, Y0 + 0.4, 0.0), (0.8, 0.6, 0.5), 4)
    crate(b, (X1 - 0.68, Y0 + 0.4, 0.5), (0.7, 0.5, 0.42), -7)
    crate(b, (X1 - 0.75, Y0 + 1.05, 0.0), (0.7, 0.5, 0.45), 11)
    ammo_box(b, (X1 - 0.8, Y0 + 1.05, 0.45), 8)
    ammo_box(b, (X1 - 0.7, Y0 + 1.07, 0.675), 14)
    ammo_box(b, (0.35, Y0 + 0.5, 0.0), -15)
    # Мешки с песком перевязкой в левом заднем углу, у двери
    for row in range(4):
        off = 0.26 if row % 2 else 0.0
        for k in range(3 - row // 2):
            x = X0 + 0.32 + off + k * 0.52
            if x > FVU_X - 0.85:
                continue
            sandbag(b, (x, Y0 + 0.24 + rnd.uniform(-0.02, 0.02), row * 0.15), rnd.uniform(-6, 6), rnd)
    # Металлический шкаф у левой стены за спиной: корпус, две створки с щелью, жалюзи, ручки, ножки, коробка сверху
    b.m = wall_frame((X0 + 0.1, -0.25, 0.0), (1, 0, 0))
    cube(b, (-0.46, 0.0, 0.06), (0.46, 0.5, 1.8), "BK_Paint")
    for s in (-1, 1):
        x0, x1 = (0.004, 0.45) if s > 0 else (-0.45, -0.004)
        cube(b, (x0, 0.5, 0.09), (x1, 0.515, 1.77), "BK_Paint")
        for k in range(6):
            z = 1.45 + k * 0.035
            prism(b, [(0.5, z), (0.52, z - 0.008), (0.52, z + 0.012)], "yz", x0 + 0.06, x1 - 0.06, "BK_Paint")
        cube(b, (s * 0.03 - 0.012, 0.515, 0.9), (s * 0.03 + 0.012, 0.535, 1.1), "BK_Steel")
    for x in (-0.42, 0.42):
        for y in (0.04, 0.46):
            cube(b, (x - 0.02, y - 0.02, 0.0), (x + 0.02, y + 0.02, 0.06), "BK_Steel")
    cube(b, (-0.3, 0.05, 1.8), (0.1, 0.4, 1.95), "BK_Wood")
    b.reset()
    # Противогазы на крючках между шкафом и дверью
    b.m = wall_frame((X0, -0.91, 0.0), (1, 0, 0))
    for x in (-0.09, 0.09):
        tube(b, (x, 0.0, 1.62), (0, 1, 0), 0.006, 0.06, "BK_Steel", segs=6)
        ellipsoid(b, (x, 0.07, 1.42), (0.075, 0.05, 0.1), "BK_Rubber", segs=12, rings=8)
        for s in (-1, 1):
            lathe(b, (x + s * 0.03, 0.115, 1.45), [(0.0, 0.0), (0.02, 0.0), (0.02, 0.01), (0.0, 0.01)], "BK_Glass",
                  axis=(0, 1, 0), segs=12)
        lathe(b, (x, 0.1, 1.32), [(0.0, 0.0), (0.045, 0.0), (0.045, 0.09), (0.02, 0.1), (0.0, 0.1)], "BK_Paint",
              axis=(0, 0.6, -1), segs=16)
        sweep(b, [Vector((x - 0.06, 0.06, 1.45)), Vector((x - 0.04, 0.02, 1.6)), Vector((x, 0.01, 1.62)),
                  Vector((x + 0.04, 0.02, 1.6)), Vector((x + 0.06, 0.06, 1.45))], 0.006, "BK_Rubber", segs=6)
    b.reset()
    # Огнетушитель на кронштейне у двери
    b.m = wall_frame((X0, -1.22, 0.0), (1, 0, 0))
    lathe(b, (0.0, 0.12, 0.85), [(0.0, 0.0), (0.075, 0.0), (0.08, 0.02), (0.08, 0.5), (0.06, 0.57), (0.025, 0.6),
                                 (0.02, 0.64), (0.0, 0.64)], "BK_PaintRed", segs=24)
    cube(b, (-0.04, 0.15, 1.47), (0.04, 0.17, 1.5), "BK_Steel")
    sweep(b, [Vector((0.0, 0.13, 1.49)), Vector((-0.04, 0.2, 1.47)), Vector((-0.07, 0.22, 1.2)), Vector((-0.05, 0.2, 0.95))],
          0.009, "BK_Rubber", segs=8)
    torus(b, (0.0, 0.12, 1.15), (0, 0, 1), 0.085, 0.006, "BK_Steel", segs=20, tsegs=6)
    cube(b, (-0.04, 0.0, 1.1), (0.04, 0.04, 1.2), "BK_Steel")
    b.reset()
    # Аптечка на правой стене
    b.m = wall_frame((X1, -2.1, 0.0), (-1, 0, 0))
    b.m = b.m @ Matrix.Translation((0.0, 0.0, -0.45))
    cube(b, (-0.17, 0.0, 1.3), (0.17, 0.11, 1.6), "BK_PaintCream")
    cube(b, (-0.165, 0.11, 1.305), (0.165, 0.12, 1.595), "BK_PaintCream")
    cube(b, (-0.06, 0.12, 1.43), (0.06, 0.125, 1.47), "BK_PaintRed")
    cube(b, (-0.02, 0.12, 1.39), (0.02, 0.125, 1.51), "BK_PaintRed")
    b.reset()
    # Канистра под противогазами: корпус с крестообразными выштамповками, три ручки, горловина
    b.m = rot_z(90, (X0 + 0.25, -0.9, 0.0))
    cube(b, (-0.17, -0.08, 0.0), (0.17, 0.08, 0.44), "BK_Paint")
    for s in (-1, 1):
        y = s * 0.083
        sweep(b, [Vector((-0.13, y, 0.05)), Vector((0.13, y, 0.39))], 0.012, "BK_Paint", segs=8)
        sweep(b, [Vector((-0.13, y, 0.39)), Vector((0.13, y, 0.05))], 0.012, "BK_Paint", segs=8)
    for x in (-0.1, 0.0, 0.1):
        sweep(b, [Vector((x, -0.025, 0.44)), Vector((x, -0.025, 0.48)), Vector((x, 0.025, 0.48)), Vector((x, 0.025, 0.44))],
              0.009, "BK_Paint", segs=6)
    lathe(b, (0.13, 0.0, 0.44), [(0.0, 0.0), (0.03, 0.0), (0.03, 0.04), (0.0, 0.04)], "BK_Paint", segs=14)
    b.reset()
    # Стул оператора: каркас из гнутой трубы, фанерное сиденье с подушкой и спинка
    sz = 0.46
    for s in (-1, 1):
        x = s * 0.2
        sweep(b, [Vector((x, 0.12, 0.0)), Vector((x, 0.1, sz - 0.03)), Vector((x, -0.26, sz - 0.03)),
                  Vector((x, -0.29, 0.0))], 0.012, "BK_Steel", segs=10)
        sweep(b, [Vector((x, -0.24, sz - 0.03)), Vector((x, -0.33, sz + 0.1)), Vector((x, -0.36, sz + 0.5))], 0.012,
              "BK_Steel", segs=10)
    for y in (0.07, -0.22):
        sweep(b, [Vector((-0.2, y, 0.18)), Vector((0.2, y, 0.18))], 0.009, "BK_Steel", segs=8)
    prism(b, rrect(-0.23, 0.23, -0.3, 0.12, 0.04, 4), "xy", sz - 0.02, sz, "BK_Wood")
    prism(b, rrect(-0.21, 0.21, -0.28, 0.1, 0.06, 5), "xy", sz, sz + 0.05, "BK_Paint")
    b.m = Matrix.Translation((0, -0.36, sz + 0.32)) @ Matrix.Rotation(math.radians(-12), 4, "X")
    prism(b, rrect(-0.22, 0.22, -0.13, 0.13, 0.04, 4), "xz", -0.02, 0.0, "BK_Wood")
    b.reset()
    # Стеллаж у правой стены за спиной (нижняя полка над трубой отопления): банки, бутыли, коробки, рулоны карт, цинки
    b.m = wall_frame((X1, -0.88, 0.0), (-1, 0, 0))
    for x in (-0.45, 0.45):
        for y in (0.02, 0.38):
            cube(b, (x - 0.02, y - 0.02, 0.0), (x + 0.02, y - 0.016, 1.8), "BK_Paint")
            cube(b, (x - 0.02, y - 0.02, 0.0), (x - 0.016, y + 0.02, 1.8), "BK_Paint")
    for z in (0.42, 0.85, 1.28, 1.7):
        cube(b, (-0.47, 0.0, z), (0.47, 0.4, z + 0.02), "BK_Paint")
    for i in range(2):
        ammo_box(b, (-0.22 + i * 0.4, 0.2, 0.44), 0)
    for i in range(5):
        lathe(b, (-0.35 + i * 0.09, 0.15 + (i % 2) * 0.12, 0.87), [(0.0, 0.0), (0.04, 0.0), (0.042, 0.005), (0.042, 0.11),
                                                                   (0.04, 0.115), (0.0, 0.115)], "BK_Galv", segs=16)
    for i in range(3):
        lathe(b, (0.12 + i * 0.1, 0.2, 0.87), [(0.0, 0.0), (0.035, 0.0), (0.04, 0.03), (0.04, 0.14), (0.025, 0.17),
                                               (0.025, 0.19), (0.0, 0.19)], "BK_Glass", segs=16)
    for i in range(3):
        cube(b, (-0.4 + i * 0.27, 0.05, 1.3), (-0.18 + i * 0.27, 0.35, 1.3 + 0.12 + 0.05 * (i % 2)), "BK_Wood")
    for i in range(4):
        tube(b, (-0.4, 0.08 + i * 0.07, 1.75), (1, 0, 0), 0.03, 0.75 - i * 0.05, "BK_PaintCream", segs=12)
    b.reset()
    return finish(b, col, bevel=0.004, segs=2)


def build_all():
    scene = bpy.data.scenes.get("Bunker") or bpy.data.scenes.new("Bunker")
    col = bpy.data.collections.get("BK_Models")
    if col is None:
        col = bpy.data.collections.new("BK_Models")
        scene.collection.children.link(col)
    for o in list(col.objects):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            bpy.data.meshes.remove(data)
    objs = [room(col, "Bunker_Room"), door(col, "Bunker_Door"), console(col, "Bunker_Console"),
            lamp(col, "Bunker_Lamp"), props(col, "Bunker_Props")]
    # Лампа строится от точки крепления: для осмотра в Blender подвешиваем её под свод
    objs[3].location = (LAMP_POS[0], LAMP_POS[1], vault_z(LAMP_POS[0]))
    return scene, objs


def export(scene, objs):
    # Экспорт с применёнными модификаторами и нормалями (скругления Bevel); модели уже в координатах комнаты
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
        with bpy.context.temp_override(scene=scene, view_layer=vl, selected_objects=[o], active_object=o, object=o):
            bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                                     apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                                     bake_space_transform=True, use_mesh_modifiers=True, mesh_smooth_type='OFF',
                                     use_tspace=False, add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
        o.location = loc
        print("exported", path)


if __name__ == "__main__":
    sc, models = build_all()
    export(sc, models)
