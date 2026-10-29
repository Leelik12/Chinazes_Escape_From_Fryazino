# Модели наполнения карты: дороги и обочины (отбойник, знаки, люк, лужа, брошенные «Жигули» и «Москвич», покрышки,
# плиты, агитщиты, мусор), дворы (гараж-«ракушка», горка-ракета, качели, сушилка для белья, лавка, мусорный бак,
# песочница, антенна на крышу), промзона (труба, башенный кран, резервуар, контейнеры, вагон, рельсы, эстакада труб,
# мачта прожекторов, газовые баллоны и шкаф для них, бензовоз), частный сектор (поленница, колодец, теплица, будка,
# бездомная собака — тело и нога отдельно, деревянный столб), пустырь (мешки с песком,
# сгоревшие БТР и грузовик, ёж, колючая проволока, блиндаж), ориентиры (водонапорная башня, телевышка, труба ГРЭС,
# Дом культуры), озеро (причал, лодка, обрушенный мост, камыш, валуны), «Проезд закрыт», трамплины, ящик, баррикада
# и баллон закиси азота.
# Запуск в Blender 5.1 (вкладка Scripting или через MCP): строит всё с нуля в отдельной сцене «LevelProps»
# и экспортирует каждую модель в Assets/Models/LevelProps/<имя>.fbx. Сцена пользователя не трогается.
# Построитель мешей, развёртка и экспорт — из Tools/Blender/private_sector/build_models.py (метры, низ на z = 0,
# перед смотрит в −Y, материалы PS_* из Assets/Content/PrivateSector и LP_* из Assets/Content/LevelProps;
# текстуры LP_* — Tools/Textures/generate_level_props_textures.py).
# Грани материалов из FIT (знаки, щиты, лужи) получают развёртку на всю текстуру, а не плиткой.
import bpy
import bmesh
import importlib.util
import math
import os
import random
from mathutils import Vector

REPO = os.environ.get("RACING_REPO", r"D:\GitRepos\RacingProject")
OUT = os.path.join(REPO, "Assets", "Models", "LevelProps")

_spec = importlib.util.spec_from_file_location(
    "ps_build", os.path.join(REPO, "Tools", "Blender", "private_sector", "build_models.py"))
ps = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ps)
Builder = ps.Builder

ps.TILE.update({"PS_Concrete": 2.0, "PS_PaintBlue": 2.0, "PS_PaintYellow": 2.0, "PS_Rust": 2.0})
ps.PREVIEW.update({"PS_Concrete": (0.55, 0.54, 0.51), "PS_PaintBlue": (0.24, 0.38, 0.55),
                   "PS_PaintYellow": (0.77, 0.63, 0.24), "PS_Rust": (0.45, 0.25, 0.14)})
LP = {  # имя: (плитка в метрах, цвет превью)
    "LP_PaintRed": (2.0, (0.6, 0.15, 0.12)), "LP_PaintWhite": (2.0, (0.8, 0.8, 0.78)),
    "LP_PaintGreen": (2.0, (0.27, 0.36, 0.24)), "LP_Rubber": (0.5, (0.12, 0.12, 0.12)),
    "LP_Sandbag": (1.0, (0.5, 0.45, 0.34)), "LP_Burnt": (2.0, (0.15, 0.12, 0.1)), "LP_Reed": (1.5, (0.4, 0.42, 0.22)),
    "LP_Stone": (2.0, (0.45, 0.44, 0.42)), "LP_Dirt": (2.0, (0.33, 0.28, 0.21)), "LP_Trash": (2.0, (0.3, 0.3, 0.3)),
    "LP_Puddle": (1.0, (0.08, 0.08, 0.09)), "LP_Stripes": (1.0, (0.8, 0.4, 0.4)),
    "LP_Sign_NoEntry": (1.0, (0.75, 0.15, 0.12)), "LP_Sign_Danger": (1.0, (0.85, 0.85, 0.82)),
    "LP_Sign_City": (1.0, (0.12, 0.27, 0.58)), "LP_Sign_Closed": (1.0, (0.9, 0.9, 0.86)),
    "LP_Sign_DK": (1.0, (0.55, 0.12, 0.1)), "LP_Slogan_0": (1.0, (0.62, 0.1, 0.08)),
    "LP_Slogan_1": (1.0, (0.62, 0.1, 0.08)), "LP_Slogan_2": (1.0, (0.62, 0.1, 0.08)),
}
for k, (tile, color) in LP.items():
    ps.TILE[k] = tile
    ps.PREVIEW[k] = color
FIT = {"LP_Sign_NoEntry", "LP_Sign_Danger", "LP_Sign_City", "LP_Sign_Closed", "LP_Sign_DK",
       "LP_Slogan_0", "LP_Slogan_1", "LP_Slogan_2", "LP_Puddle"}

V = Vector


# --- Примитивы поверх Builder ---

def loft(b, rings, mat, cap0=True, cap1=True, cap_mat=None):
    # Боковая поверхность между кольцами одинаковой длины; каждое кольцо обходится против часовой стрелки,
    # если смотреть с конца (по направлению от первого кольца к последнему), — тогда нормали наружу
    n = len(rings[0])
    for r0, r1 in zip(rings, rings[1:]):
        for i in range(n):
            j = (i + 1) % n
            b.face([r0[i], r0[j], r1[j], r1[i]], mat)
    if cap0:
        b.face(list(reversed(rings[0])), cap_mat or mat)
    if cap1:
        b.face(list(rings[-1]), cap_mat or mat)


def basis(d):
    d = V(d).normalized()
    u = V((0, 0, 1)).cross(d)
    if u.length < 1e-4:
        u = V((1, 0, 0))
    u.normalize()
    return d, u, d.cross(u)


def ring(c, d, r, segs, phase=0.0, rv=None):
    # Кольцо вокруг оси d с центром c; rv — второй радиус (эллипс)
    d, u, v = basis(d)
    rv = r if rv is None else rv
    return [V(c) + u * (r * math.cos(phase + 2 * math.pi * i / segs)) + v * (rv * math.sin(phase + 2 * math.pi * i / segs))
            for i in range(segs)]


def tube(b, p0, p1, r0, mat, r1=None, segs=8, caps=True, cap_mat=None):
    # Цилиндр или усечённый конус от p0 до p1 (столбы, трубы, балки круглого сечения); от 6 сегментов
    # их число умножается на ps.SEG_SCALE (тонкие стебли камыша остаются четырёхгранными)
    if segs >= 6:
        segs = min(segs * ps.SEG_SCALE, 40)
    r1 = r0 if r1 is None else r1
    d = V(p1) - V(p0)
    loft(b, [ring(p0, d, r0, segs), ring(p1, d, r1, segs)], mat, caps, caps, cap_mat)


def bar(b, p0, p1, w, mat, h=None):
    # Брус квадратного сечения w × h от p0 до p1 (решётчатые фермы, перекладины, балки)
    h = w if h is None else h
    d = V(p1) - V(p0)
    rings = []
    for p in (p0, p1):
        _, u, v = basis(d)
        rings.append([V(p) + u * (sx * w / 2) + v * (sy * h / 2) for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1))])
    loft(b, rings, mat)


def extrude_x(b, prof, x0, x1, mats, cap_mat=None):
    # Выпуклый профиль prof [(y, z)] против часовой (вид с +X), вытянутый по X от x0 до x1;
    # mats — материал всей боковины или список по рёбрам профиля
    n = len(prof)
    for i in range(n):
        j = (i + 1) % n
        (ya, za), (yb, zb) = prof[i], prof[j]
        m = mats[i] if isinstance(mats, (list, tuple)) else mats
        b.face([(x0, ya, za), (x0, yb, zb), (x1, yb, zb), (x1, ya, za)], m)
    cm = cap_mat or (mats if isinstance(mats, str) else mats[0])
    b.face([(x1, y, z) for y, z in prof], cm)
    b.face([(x0, y, z) for y, z in reversed(prof)], cm)


def lattice(b, base, top, w0, w1, sections, mat, w=0.12, mats=None):
    # Решётчатая квадратная мачта от base до top (z), ширина w0 внизу и w1 вверху: стойки, пояса и раскосы
    pts = []
    for k in range(sections + 1):
        t = k / sections
        z = base[2] + (top - base[2]) * t
        hw = (w0 + (w1 - w0) * t) / 2
        pts.append([V((base[0] + sx * hw, base[1] + sy * hw, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))])
    for k in range(sections):
        m = mats[k % len(mats)] if mats else mat
        for i in range(4):
            j = (i + 1) % 4
            bar(b, pts[k][i], pts[k + 1][i], w * 1.5, m)
            bar(b, pts[k + 1][i], pts[k + 1][j], w, m)
            bar(b, pts[k][i] if k % 2 == 0 else pts[k][j], pts[k + 1][j] if k % 2 == 0 else pts[k + 1][i], w * 0.8, m)
    return pts


def rock_into(b, c, size, seed, mat, flat=0.35, subdiv=2):
    # Валун: икосфера с шумом, низ срезан, ось z сплющена; грани переносятся в построитель
    rnd = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    offs = [V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) for _ in range(5)]
    for vert in bm.verts:
        p = vert.co.normalized()
        k = 1.0 + sum(0.18 * math.sin(3.1 * p.dot(o) + i) for i, o in enumerate(offs))
        vert.co = V((p.x * size[0] * k, p.y * size[1] * k, max(p.z * size[2] * k, -flat * size[2])))
    for f in bm.faces:
        b.face([V(c) + vt.co for vt in f.verts], mat)
    bm.free()


def two_sided(b, pts, mat, eps=0.003):
    # Двусторонняя грань: обратная сторона сдвинута на eps, иначе remove_doubles в Builder.finish склеит их в одну
    pts = [V(p) for p in pts]
    n = (pts[1] - pts[0]).cross(pts[-1] - pts[0]).normalized()
    b.face(pts, mat)
    b.face([p - n * eps for p in reversed(pts)], mat)


def inward_box(b, mn, mx, mat, skip=()):
    # Ящик с гранями внутрь (стенки бака, кузова): та же раскладка граней, что у Builder.box, обход развёрнут
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
            b.face(list(reversed(pts)), mat)


def fit_uv(obj):
    # Знаки и щиты: каждая грань материала из FIT растягивается на всю текстуру (u вправо, v вверх при взгляде на грань)
    me = obj.data
    uv = me.uv_layers[0].data
    for poly in me.polygons:
        if me.materials[poly.material_index].name not in FIT:
            continue
        n = poly.normal
        t = V((1, 0, 0)) if abs(n.z) > 0.99 else V((0, 0, 1)).cross(n).normalized()
        bb = n.cross(t)
        coords = [(me.vertices[me.loops[li].vertex_index].co.dot(t), me.vertices[me.loops[li].vertex_index].co.dot(bb))
                  for li in poly.loop_indices]
        u0, u1 = min(c[0] for c in coords), max(c[0] for c in coords)
        v0, v1 = min(c[1] for c in coords), max(c[1] for c in coords)
        for li, (cu, cv) in zip(poly.loop_indices, coords):
            uv[li].uv = ((cu - u0) / max(u1 - u0, 1e-6), (cv - v0) / max(v1 - v0, 1e-6))


def finish(b, col):
    obj = b.finish(col)
    fit_uv(obj)
    return obj


# --- Дороги и обочины ---

def guardrail(col, name, length=4.0):
    # Секция дорожного отбойника: волнистая полоса на стойках через 2 м
    b = Builder(name)
    for k in range(int(length / 2) + 1):
        x = -length / 2 + 2 * k
        b.box((x - 0.06, -0.05, 0), (x + 0.06, 0.05, 0.8), "PS_Rust", skip=("-z",))
    b.box((-length / 2, -0.22, 0.5), (length / 2, -0.12, 0.62), "PS_MetalRoof")
    b.box((-length / 2, -0.24, 0.62), (length / 2, -0.14, 0.72), "PS_MetalRoof")
    b.box((-length / 2, -0.12, 0.55), (length / 2, -0.05, 0.68), "PS_Rust")  # кронштейн
    return finish(b, col)


def sign(col, name, face_mat, shape, size=0.7, posts=1, h=2.2):
    # Дорожный знак на трубчатой стойке: круг, треугольник или прямоугольник; лицо смотрит в −Y
    b = Builder(name)
    w, hh = (size, size) if shape != "rect" else size
    xs = [0.0] if posts == 1 else [-w / 2 + 0.15, w / 2 - 0.15]
    for x in xs:
        tube(b, (x, 0.05, 0), (x, 0.05, h + hh), 0.035, "PS_MetalRoof", segs=6)
    cz = h + hh / 2
    if shape == "circle":
        pts = [(math.cos(2 * math.pi * i / 16) * w / 2, math.sin(2 * math.pi * i / 16) * w / 2) for i in range(16)]
    elif shape == "tri":
        pts = [(-w / 2, -w * 0.43), (w / 2, -w * 0.43), (0, w * 0.43)]
    else:
        pts = [(-w / 2, -hh / 2), (w / 2, -hh / 2), (w / 2, hh / 2), (-w / 2, hh / 2)]
    b.face([(x, -0.0, cz + z) for x, z in pts], face_mat)
    b.face([(x, 0.02, cz + z) for x, z in reversed(pts)], "PS_MetalRoof")
    if shape == "rect" or shape == "tri":
        # Края таблички (у круга торец не виден)
        n = len(pts)
        for i in range(n):
            (xa, za), (xb, zb) = pts[i], pts[(i + 1) % n]
            b.face([(xa, 0.02, cz + za), (xb, 0.02, cz + zb), (xb, 0.0, cz + zb), (xa, 0.0, cz + za)], "PS_MetalRoof")
    return finish(b, col)


def manhole(col, name):
    # Канализационный люк чуть выше асфальта
    b = Builder(name)
    tube(b, (0, 0, -0.05), (0, 0, 0.03), 0.42, "PS_Rust", segs=14)
    tube(b, (0, 0, 0.03), (0, 0, 0.045), 0.3, "PS_Rust", segs=14)
    return finish(b, col)


def puddle(col, name, seed, rx=2.4, ry=1.6):
    # Лужа или яма с водой: плоский неровный многоугольник, вода в середине текстуры, грязь по краю
    rnd = random.Random(seed)
    b = Builder(name)
    pts = []
    for i in range(14):
        a = 2 * math.pi * i / 14
        k = rnd.uniform(0.8, 1.1)
        pts.append((math.cos(a) * rx * k, math.sin(a) * ry * k, 0.03))
    b.face(pts, "LP_Puddle")
    return finish(b, col)


def rrect(cx, cy, hx, hy, r0, r1=None, n=3):
    # Скруглённый прямоугольник против часовой (вид с +третьей оси): углы r0 снизу и r1 сверху по второй оси;
    # n сегментов на угол — у всех колец одинаковое число точек, их можно лофтить
    r1 = r0 if r1 is None else r1
    pts = []
    for (sx, sy, r) in ((1, -1, r0), (1, 1, r1), (-1, 1, r1), (-1, -1, r0)):
        ccx, ccy = cx + sx * (hx - r), cy + sy * (hy - r)
        a0 = math.atan2(sy, sx) - math.pi / 4
        for k in range(n + 1):
            a = a0 + (math.pi / 2) * k / n
            pts.append((ccx + math.cos(a) * r, ccy + math.sin(a) * r))
    return pts


def closed_part(b, build):
    # Замкнутая деталь: нормали её граней пересчитываются наружу, как бы ни шёл обход колец
    before = set(b.bm.faces)
    build()
    faces = [f for f in b.bm.faces if f not in before]
    # Builder.face создаёт вершины на каждую грань: без склейки грани не связаны и пересчёт нормалей случаен
    verts = list({v for f in faces for v in f.verts})
    bmesh.ops.remove_doubles(b.bm, verts=verts, dist=0.0005)
    faces = [f for f in b.bm.faces if f.is_valid and f not in before]
    bmesh.ops.recalc_face_normals(b.bm, faces=faces)


def lathe_x(b, x0, x1, c, prof, mat, segs=16):
    # Тело вращения вокруг оси X: prof — [(смещение по X от x0 к x1 в долях, радиус)], замкнутый контур
    rings = [ring(V((x0 + (x1 - x0) * t, c[1], c[2])), V((1, 0, 0)), r, segs) for t, r in prof]
    loft(b, rings + [rings[0]], mat, cap0=False, cap1=False)


def car_wheel(b, x_in, x_out, y, z, r, tyre, rim, burnt):
    # Колесо: шина с закруглёнными боковинами, диск с колпаком; у сгоревшей — голый ржавый диск
    s = 1 if x_out > x_in else -1
    w = abs(x_out - x_in)
    if not burnt:
        closed_part(b, lambda: lathe_x(b, x_in, x_out, (0, y, z), [(0.0, r * 0.66), (0.0, r * 0.88), (0.15, r * 0.98), (0.5, r),
                                                                  (0.85, r * 0.98), (1.0, r * 0.88), (1.0, r * 0.66)], tyre))
    rr = r * (0.66 if not burnt else 0.8)
    closed_part(b, lambda: tube(b, (x_in + s * w * 0.1, y, z), (x_out - s * 0.005, y, z), rr, rim, segs=10))
    # Колпак и гайки
    closed_part(b, lambda: tube(b, (x_out - s * 0.005, y, z), (x_out + s * 0.025, y, z), rr * 0.45, rim, r1=rr * 0.35, segs=10))
    if not burnt:
        for k in range(4):
            a = k * math.pi / 2 + 0.4
            p = V((x_out, y + math.cos(a) * rr * 0.65, z + math.sin(a) * rr * 0.65))
            b.box(tuple(p - V((0.012 * (1 - s), 0.02, 0.02))), tuple(p + V((0.012 * (1 + s), 0.02, 0.02))), "LP_Rubber")


def car_wreck(col, name, paint, length, wagon, seed, burnt=False):
    # Брошенная легковушка («Жигули» — седан с двойными круглыми фарами, «Москвич» — комби с прямоугольными):
    # кузов — лофт сечений со скруглёнными кромками и вырезами арок, кабина — лофт горизонтальных сечений
    # со стойками и стёклами, колёса с дисками и колпаками, хромированные бампера, решётка, номера, ручки, зеркала,
    # дворники. У сгоревшей стёкол и резины нет, всё горелое, кузов сел на диски. Перед — в −Y
    rnd = random.Random(seed)
    b = Builder(name)
    body = "LP_Burnt" if burnt else paint
    glass = "LP_Burnt" if burnt else "PS_Glass"
    chrome = "PS_Rust" if burnt else "PS_MetalRoof"
    dark = "LP_Burnt" if burnt else "LP_Rubber"
    hw, L = 0.81, length / 2
    sag = 0.12 if burnt else 0.0
    wheel_r = 0.3
    axles = (-L + 0.82, L - 0.95)

    # Кузов: сечения вдоль Y; у арок низ поднимается по окружности над колесом
    def zb_at(y):
        for ya in axles:
            dy = y - ya
            if abs(dy) < 0.4:
                return 0.31 + math.sqrt(max(0.0, 0.4 ** 2 - dy ** 2)) * 0.95 - sag
        return 0.34 - sag

    deck_front, deck_rear = 0.9, 0.96 if not wagon else 0.93
    stations = [(-L, 0.74, 0.44, 0.76), (-L + 0.06, 0.79, 0.37, 0.86), (-L + 0.3, 0.81, None, 0.89)]
    for ya in axles:
        for k in range(-4, 5):
            stations.append((ya + k * 0.1, 0.81, None, None))
    stations += [(L - 0.28, 0.81, None, None), (L - 0.06, 0.8, 0.38, 0.93), (L, 0.75, 0.45, 0.86)]
    stations.sort(key=lambda s: s[0])
    rings = []
    for y, w, zb, zt in stations:
        zb = zb_at(y) if zb is None else zb - sag
        if zt is None:
            t = (y + L) / length
            zt = deck_front + (deck_rear - deck_front) * t
        else:
            zt -= sag
        hz = (zt - zb) / 2
        sec = rrect(0, (zb + zt) / 2, w, hz, min(0.05, hz * 0.3), min(0.12, hz * 0.65), n=3)
        rings.append([V((x, y, z)) for x, z in sec])
    closed_part(b, lambda: loft(b, rings, body))
    # Пороги и молдинг вдоль борта
    for s in (-1, 1):
        b.box((s * hw - 0.01, -L + 1.25, 0.36 - sag), (s * (hw + 0.012), L - 1.4, 0.42 - sag), dark)
        b.box((s * (hw + 0.006) - 0.004, -L + 0.1, 0.66 - sag), (s * (hw + 0.012), L - 0.1, 0.685 - sag), chrome)

    # Кабина: горизонтальные сечения от линии стёкол до крыши, углы — стойки, бока — стёкла и средняя стойка
    f0 = -L + 1.38
    r0 = (L - 0.2) if wagon else (L - 1.05)
    zbelt = deck_front + 0.03 + (deck_rear - deck_front) * 0.5 - sag
    roof = 1.42 - sag

    def cabin_ring(z, ya, yb, w, r):
        return [V((x, y, z)) for x, y in rrect(0, (ya + yb) / 2, w, (yb - ya) / 2, r, r, n=2)]

    def kinds(pts):
        # Ребро стекла — на прямых участках; боковые прямые делятся средней стойкой
        out = []
        n = len(pts)
        for i in range(n):
            a, c = pts[i], pts[(i + 1) % n]
            straight_side = abs(a.x - c.x) < 1e-4 and abs(a.y - c.y) > 0.2
            straight_end = abs(a.y - c.y) < 1e-4 and abs(a.x - c.x) > 0.2
            out.append("glass" if straight_side or straight_end else "pillar")
        return out

    base = cabin_ring(zbelt, f0, r0, 0.77, 0.12)
    low = cabin_ring(zbelt + 0.04, f0 + 0.02, r0 - 0.02, 0.76, 0.12)
    top = cabin_ring(roof, f0 + 0.62, r0 - (0.08 if wagon else 0.42), 0.66, 0.1)
    crown = cabin_ring(roof + 0.04, f0 + 0.68, r0 - (0.12 if wagon else 0.48), 0.62, 0.08)

    def cabin():
        n = len(base)
        kk = kinds(low)
        for i in range(n):
            j = (i + 1) % n
            b.face([base[i], base[j], low[j], low[i]], body)
            b.face([low[i], low[j], top[j], top[i]], glass if kk[i] == "glass" else body)
            b.face([top[i], top[j], crown[j], crown[i]], body)
        b.face(list(crown), body)
        b.face(list(reversed(base)), body)
    closed_part(b, cabin)
    # Средняя стойка поверх бокового стекла
    ym0 = f0 + 0.02 + (r0 - f0) * (0.5 if not wagon else 0.4)
    ym1 = f0 + 0.62 + ((r0 - (0.08 if wagon else 0.42)) - (f0 + 0.62)) * (0.5 if not wagon else 0.4)
    for s in (-1, 1):
        b.face([(s * 0.773, ym0 - 0.05, zbelt + 0.04), (s * 0.773, ym0 + 0.05, zbelt + 0.04),
                (s * 0.673, ym1 + 0.05, roof), (s * 0.673, ym1 - 0.05, roof)][::s], body)
        if wagon:
            ym2 = ym0 + (r0 - ym0) * 0.62
            ym3 = ym1 + ((r0 - 0.08) - ym1) * 0.62
            b.face([(s * 0.773, ym2 - 0.04, zbelt + 0.04), (s * 0.773, ym2 + 0.04, zbelt + 0.04),
                    (s * 0.673, ym3 + 0.04, roof), (s * 0.673, ym3 - 0.04, roof)][::s], body)

    # Колёса
    for y in axles:
        for s in (-1, 1):
            car_wheel(b, s * (hw - 0.27), s * (hw - 0.04), y, wheel_r - sag * 0.6,
                      wheel_r if not burnt else 0.27, "LP_Rubber", chrome if not burnt else "LP_Burnt", burnt)
    # Тёмная ниша арки, чтобы не просвечивал пол
    for y in axles:
        b.box((-hw + 0.02, y - 0.36, 0.55 - sag), (hw - 0.02, y + 0.36, 0.66 - sag), dark, skip=("+z",))

    # Бампера: хромированные трубы овального сечения с загибом на бока
    def bumper(y, d):
        # Концы загибаются назад, к кузову
        for x0, x1, dy in ((-0.62, 0.62, 0.0), (0.62, 0.8, 0.1), (-0.62, -0.8, 0.1)):
            p0 = V((x0, y, 0.46 - sag))
            p1 = V((x1, y - d * dy, 0.46 - sag))
            closed_part(b, lambda p0=p0, p1=p1: loft(b, [ring(p0, p1 - p0, 0.055, 8, rv=0.03), ring(p1, p1 - p0, 0.055, 8, rv=0.03)], chrome))
        for x in (-0.42, 0.42):  # клыки
            ya, yb = sorted((y + d * 0.06, y - d * 0.02))
            b.box((x - 0.03, ya, 0.42 - sag), (x + 0.03, yb, 0.6 - sag), dark)
    bumper(-L - 0.05, -1)
    bumper(L + 0.05, 1)

    # Перед: решётка, фары, номер; зад: фонари, номер
    zf = 0.72 - sag
    b.box((-0.48, -L - 0.035, zf - 0.11), (0.48, -L + 0.02, zf + 0.09), dark)
    for k in range(5):
        z = zf - 0.09 + k * 0.04
        b.box((-0.47, -L - 0.045, z), (0.47, -L - 0.03, z + 0.012), chrome)
    if not wagon:
        for s in (-1, 1):
            for x in (0.56, 0.7):
                closed_part(b, lambda x=x, s=s: tube(b, (s * x, -L + 0.0, zf), (s * x, -L - 0.05, zf), 0.072, chrome, segs=10))
                b.face([(s * x + math.cos(a) * 0.06, -L - 0.052, zf + math.sin(a) * 0.06) for a in
                        [2 * math.pi * k / 16 for k in range(16)]], glass)
    else:
        for s in (-1, 1):
            xa, xb = sorted((s * 0.5, s * 0.78))
            b.box((xa, -L - 0.04, zf - 0.08), (xb, -L + 0.02, zf + 0.07), chrome)
            b.box((xa + 0.02, -L - 0.05, zf - 0.06), (xb - 0.02, -L - 0.035, zf + 0.05), glass)
    for s in (-1, 1):
        b.box((min(s * 0.5, s * 0.77), L - 0.02, 0.7 - sag), (max(s * 0.5, s * 0.77), L + 0.035, 0.84 - sag),
              "LP_PaintRed" if not burnt else "LP_Burnt")
    for y, d in ((-L - 0.1, -1), (L + 0.1, 1)):
        b.box((-0.26, min(y, y - d * 0.02), 0.5 - sag + (0.02 if d < 0 else 0.12)),
              (0.26, max(y, y - d * 0.02), 0.62 - sag + (0.02 if d < 0 else 0.12)), "LP_PaintWhite" if not burnt else "PS_Rust")

    # Дверные ручки, зеркала, дворники, антенна
    for s in (-1, 1):
        for y in (f0 + 0.55, ym0 + 0.6):
            b.box((min(s * (hw + 0.005), s * (hw + 0.03)), y - 0.07, 0.8 - sag), (max(s * (hw + 0.005), s * (hw + 0.03)), y + 0.07, 0.83 - sag), chrome)
        b.box((min(s * 0.78, s * 0.95), f0 + 0.08, zbelt + 0.05), (max(s * 0.78, s * 0.95), f0 + 0.13, zbelt + 0.16), dark)
    for x in (-0.3, 0.2):
        b.box((x - 0.25, f0 + 0.1, zbelt + 0.06), (x + 0.25, f0 + 0.13, zbelt + 0.08), dark)
    if not burnt:
        tube(b, (0.7, L - 0.55, deck_rear - 0.02), (0.72, L - 0.55, deck_rear + 0.75), 0.006, chrome, segs=4, caps=False)
    if rnd.random() < 0.5 and not burnt:
        # Багажник на крыше с хламом
        for x in (-0.55, 0.55):
            b.box((x - 0.02, f0 + 0.9, roof + 0.04), (x + 0.02, r0 - 0.6, roof + 0.1), chrome)
        for y in (f0 + 0.95, (f0 + r0) / 2 - 0.3, r0 - 0.65):
            b.box((-0.57, y - 0.02, roof + 0.08), (0.57, y + 0.02, roof + 0.11), chrome)
        b.box((-0.4, f0 + 1.0, roof + 0.1), (0.3, r0 - 0.8, roof + 0.36), "LP_Sandbag")
    return polish(finish(b, col), width=0.008, segments=1, angle=32.0)


def tire_stack(col, name, count=4, seed=1):
    # Стопка старых покрышек, верхние сдвинуты
    rnd = random.Random(seed)
    b = Builder(name)
    for k in range(count):
        x, y = rnd.uniform(-0.08, 0.08), rnd.uniform(-0.08, 0.08)
        z = k * 0.2
        tube(b, (x, y, z), (x, y, z + 0.2), 0.33, "LP_Rubber", segs=12, caps=False)
        tube(b, (x, y, z + 0.2), (x, y, z), 0.18, "LP_Rubber", segs=12, caps=False)  # внутренняя стенка
        loft(b, [ring((x, y, z + 0.2), (0, 0, 1), 0.18, 12), ring((x, y, z + 0.2), (0, 0, 1), 0.33, 12)],
             "LP_Rubber", False, False)
    return finish(b, col)


def slab(col, name, l=6.0, w=1.75, t=0.22):
    # Дорожная плита ПД
    b = Builder(name)
    b.box((-l / 2, -w / 2, 0), (l / 2, w / 2, t), "PS_Concrete", skip=("-z",))
    for x in (-l / 2 + 0.4, l / 2 - 0.4):
        b.box((x - 0.1, -0.03, t), (x + 0.1, 0.03, t + 0.08), "PS_Rust", skip=("-z",))  # монтажные петли
    return finish(b, col)


def billboard(col, name, face):
    # Агитационный щит 6 × 3 м на двух двутавровых стойках
    b = Builder(name)
    for x in (-2.2, 2.2):
        b.box((x - 0.12, 0.05, 0), (x + 0.12, 0.25, 6.0), "PS_Rust", skip=("-z",))
    b.box((-3.0, 0.0, 2.8), (3.0, 0.05, 5.8), "PS_Rust", skip=("-y",))
    b.face([(-3.0, 0.0, 2.8), (3.0, 0.0, 2.8), (3.0, 0.0, 5.8), (-3.0, 0.0, 5.8)], face)
    b.box((-3.1, -0.06, 2.72), (3.1, 0.06, 2.8), "PS_Rust")
    b.box((-3.1, -0.06, 5.8), (3.1, 0.06, 5.88), "PS_Rust")
    return finish(b, col)


def trash_pile(col, name, seed):
    # Куча мусора: мешки и тряпьё, пара досок и ящик
    rnd = random.Random(seed)
    b = Builder(name)
    for k in range(6):
        c = (rnd.uniform(-1.2, 1.2), rnd.uniform(-0.9, 0.9), 0)
        rock_into(b, c, (rnd.uniform(0.4, 0.8), rnd.uniform(0.35, 0.7), rnd.uniform(0.3, 0.55)), seed * 10 + k,
                  "LP_Trash", flat=0.1, subdiv=2)
    b.box((-0.8, 0.6, 0), (0.0, 1.2, 0.5), "PS_Planks_Raw")
    bar(b, (-1.5, -0.5, 0.05), (1.4, 0.2, 0.45), 0.14, "PS_Planks_Raw", h=0.03)
    return finish(b, col)


# --- Дворы ---

def shell_garage(col, name, paint):
    # Гараж-«ракушка»: полукруглый кожух с выштампованными поперечными рёбрами, плоские торцы, по низу — ржавая
    # рама-юбка; спереди двустворчатая дверь на петлях с ручками, проушинами и навесным замком, сзади —
    # вентиляционная решётка. Габариты прежние (r 1.6, длина 5.4): по ним расставлены дворы
    b = Builder(name)
    r, L, segs, k = 1.6, 5.4, 16, 1.1
    yf, yb = -L / 2, L / 2

    def arc(y, rr=r):
        return [V((math.cos(math.pi * i / segs) * rr, y, math.sin(math.pi * i / segs) * rr * k)) for i in range(segs + 1)]

    rings = [arc(yf), arc(yb)]
    for i in range(segs):
        b.face([rings[1][i], rings[1][i + 1], rings[0][i + 1], rings[0][i]], paint)
    b.face(list(rings[0]), paint)
    b.face(list(reversed(rings[1])), paint)
    # Рёбра жёсткости поперёк кожуха
    for n in range(7):
        a = arc(yf + 0.45 + n * (L - 0.9) / 6, r + 0.02)
        for i in range(segs):
            bar(b, a[i], a[i + 1], 0.09, paint, h=0.045)
    # Юбка: уголок вдоль низа и порог спереди
    for s in (-1, 1):
        x0, x1 = sorted((s * (r - 0.03), s * (r + 0.07)))
        b.box((x0, yf - 0.03, 0), (x1, yb + 0.03, 0.16), "PS_Rust")
    b.box((-r + 0.05, yf - 0.07, 0), (r - 0.05, yf, 0.09), "PS_Rust")
    # Створки с ржавой кромкой, петли, ручки
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.012, s * 0.92))
        b.box((x0, yf - 0.035, 0.1), (x1, yf, 1.38), paint, skip=("+y",))
        b.box((x0 + 0.03, yf - 0.042, 0.16), (x1 - 0.03, yf - 0.035, 0.2), "PS_Rust", skip=("+y",))
        for z in (0.32, 1.1):
            tube(b, (s * 0.95, yf - 0.035, z), (s * 0.95, yf - 0.035, z + 0.16), 0.028, "PS_Rust", segs=6)
        bar(b, (s * 0.22, yf - 0.07, 0.58), (s * 0.22, yf - 0.07, 0.92), 0.025, "PS_MetalRoof")
        for z in (0.6, 0.9):
            bar(b, (s * 0.22, yf - 0.035, z), (s * 0.22, yf - 0.07, z), 0.02, "PS_MetalRoof")
    b.box((-0.012, yf - 0.03, 0.1), (0.012, yf, 1.38), "LP_Rubber", skip=("+y",))
    # Проушины и навесной замок
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.03, s * 0.09))
        b.box((x0, yf - 0.1, 0.74), (x1, yf - 0.035, 0.8), "PS_Rust")
    b.box((-0.045, yf - 0.13, 0.6), (0.045, yf - 0.09, 0.7), "PS_MetalRoof")
    bar(b, (0, yf - 0.11, 0.7), (0, yf - 0.11, 0.78), 0.015, "PS_MetalRoof")
    # Вентиляционная решётка сзади
    b.box((-0.32, yb, 1.18), (0.32, yb + 0.02, 1.44), "PS_Rust", skip=("-y",))
    for z in (1.24, 1.31, 1.38):
        b.box((-0.28, yb + 0.02, z - 0.015), (0.28, yb + 0.03, z + 0.015), "LP_Rubber", skip=("-y",))
    return finish(b, col)


def rocket_slide(col, name):
    # Детская горка-ракета: труба с окнами, конус, стабилизаторы, лестница и скат
    b = Builder(name)
    for k, (z0, z1) in enumerate(((0.0, 1.2), (1.2, 2.4), (2.4, 3.6), (3.6, 4.4))):
        tube(b, (0, 0, z0), (0, 0, z1), 0.9, "LP_PaintRed" if k % 2 == 0 else "LP_PaintWhite", segs=12, caps=False)
    tube(b, (0, 0, 4.4), (0, 0, 6.4), 0.9, "LP_PaintRed", r1=0.05, segs=12, caps=False)
    for a in range(3):
        ang = 2 * math.pi * a / 3 + 0.3
        d = V((math.cos(ang), math.sin(ang), 0))
        p = d * 0.9
        b.face([p + V((0, 0, 0)), p + d * 1.0 + V((0, 0, -0.0)), p + d * 0.9 + V((0, 0, 0.6)), p + V((0, 0, 1.8))], "LP_PaintWhite")
        b.face([p + V((0, 0, 1.8)), p + d * 0.9 + V((0, 0, 0.6)), p + d * 1.0, p], "LP_PaintWhite")
    # Площадка на 2.4 м, лестница сзади (+Y), скат вперёд (−Y)
    for s in (-1, 1):
        bar(b, (s * 0.3, 2.6, 0), (s * 0.3, 0.85, 2.4), 0.06, "PS_Rust")
    for k in range(7):
        t = (k + 0.5) / 7
        bar(b, (-0.3, 2.6 - 1.75 * t, 2.4 * t), (0.3, 2.6 - 1.75 * t, 2.4 * t), 0.04, "PS_Rust")
    b.slab((-0.35, -0.9, 2.3), (0.35, -0.9, 2.3), (0.35, -3.8, 0.3), (-0.35, -3.8, 0.3), 0.04, "PS_MetalRoof")
    for s in (-1, 1):
        bar(b, (s * 0.38, -0.9, 2.45), (s * 0.38, -3.8, 0.45), 0.04, "PS_Rust")
        bar(b, (s * 0.35, -3.6, 0), (s * 0.35, -3.6, 0.3), 0.06, "PS_Rust")
    return finish(b, col)


def swings(col, name):
    # Качели: А-образные стойки из труб, перекладина, два сиденья на прутах
    b = Builder(name)
    h, w = 2.4, 3.2
    for x in (-w / 2, w / 2):
        bar(b, (x, -0.8, 0), (x, 0, h), 0.08, "PS_PaintBlue")
        bar(b, (x, 0.8, 0), (x, 0, h), 0.08, "PS_PaintBlue")
    bar(b, (-w / 2, 0, h), (w / 2, 0, h), 0.09, "PS_PaintBlue")
    for x, swing in ((-0.7, 0.0), (0.7, 0.35)):
        for s in (-1, 1):
            bar(b, (x + s * 0.22, 0, h), (x + s * 0.22, -swing, 0.55), 0.03, "PS_Rust")
        b.box((x - 0.28, -swing - 0.15, 0.5), (x + 0.28, -swing + 0.15, 0.55), "PS_Planks_Green")
    return finish(b, col)


def clothesline(col, name, seed):
    # Сушилка для белья: две Т-образные стойки, три верёвки и простыни
    rnd = random.Random(seed)
    b = Builder(name)
    L, h = 7.0, 2.2
    for x in (-L / 2, L / 2):
        bar(b, (x, 0, 0), (x, 0, h), 0.07, "PS_Rust")
        bar(b, (x, -0.6, h - 0.05), (x, 0.6, h - 0.05), 0.06, "PS_Rust")
    for y in (-0.5, 0.0, 0.5):
        bar(b, (-L / 2, y, h), (L / 2, y, h - 0.05), 0.012, "PS_Trim")
    mats = ["LP_PaintWhite", "PS_PaintBlue", "PS_Trim", "LP_PaintRed"]
    for k in range(5):
        y = rnd.choice((-0.5, 0.0, 0.5))
        x = rnd.uniform(-2.6, 2.0)
        w, d = rnd.uniform(0.6, 1.4), rnd.uniform(0.5, 1.2)
        m = rnd.choice(mats)
        two_sided(b, [(x, y, h - d), (x + w, y, h - d), (x + w, y, h - 0.02), (x, y, h - 0.02)], m)
    return finish(b, col)


def yard_bench(col, name):
    # Дворовая лавка со спинкой на бетонных ножках
    b = Builder(name)
    for x in (-0.8, 0.8):
        b.box((x - 0.08, -0.25, 0), (x + 0.08, 0.2, 0.42), "PS_Concrete", skip=("-z",))
        bar(b, (x, 0.2, 0.42), (x, 0.3, 0.95), 0.06, "PS_Rust")
    for k in range(3):
        y = -0.24 + k * 0.16
        b.box((-1.0, y, 0.42), (1.0, y + 0.13, 0.47), "PS_Planks_Green")
    for k in range(2):
        z = 0.6 + k * 0.2
        b.box((-1.0, 0.24 + k * 0.03, z), (1.0, 0.28 + k * 0.03, z + 0.13), "PS_Planks_Green")
    return finish(b, col)


def trash_bin(col, name):
    # Мусорный контейнер без крышки, внутри мусор
    b = Builder(name)
    w, d, h = 1.4, 0.9, 1.15
    b.box((-w / 2, -d / 2, 0.12), (w / 2, d / 2, h), "LP_PaintGreen", skip=("+z",))
    inward_box(b, (-w / 2 + 0.03, -d / 2 + 0.03, 0.12), (w / 2 - 0.03, d / 2 - 0.03, h), "LP_PaintGreen", skip=("+z", "-z"))
    b.face([(-w / 2 + 0.03, -d / 2 + 0.03, h - 0.15), (w / 2 - 0.03, -d / 2 + 0.03, h - 0.15),
            (w / 2 - 0.03, d / 2 - 0.03, h - 0.15), (-w / 2 + 0.03, d / 2 - 0.03, h - 0.15)], "LP_Trash")
    for s in (-1, 1):
        tube(b, (s * 0.5, -0.3, 0.12), (s * 0.5, -0.3, 0.0), 0.07, "LP_Rubber", segs=6)
        tube(b, (s * 0.5, 0.3, 0.12), (s * 0.5, 0.3, 0.0), 0.07, "LP_Rubber", segs=6)
    return finish(b, col)


def sandbox(col, name):
    # Песочница: дощатый борт и грибок-навес
    b = Builder(name)
    s = 1.6
    for (x0, y0, x1, y1) in ((-s, -s, s, -s + 0.08), (-s, s - 0.08, s, s), (-s, -s, -s + 0.08, s), (s - 0.08, -s, s, s)):
        b.box((x0, y0, 0), (x1, y1, 0.3), "PS_Planks_Green", skip=("-z",))
    b.face([(-s + 0.08, -s + 0.08, 0.12), (s - 0.08, -s + 0.08, 0.12), (s - 0.08, s - 0.08, 0.12), (-s + 0.08, s - 0.08, 0.12)],
           "LP_Dirt")
    tube(b, (0, 0, 0), (0, 0, 2.2), 0.06, "PS_Planks_Raw", segs=6)
    tube(b, (0, 0, 2.0), (0, 0, 2.6), 1.3, "LP_PaintRed", r1=0.05, segs=10)
    return finish(b, col)


def tv_antenna(col, name):
    # Антенна «волновой канал» на мачте с растяжками — на крыши панелек
    b = Builder(name)
    tube(b, (0, 0, 0), (0, 0, 4.0), 0.03, "PS_MetalRoof", segs=6)
    bar(b, (0, -1.1, 3.6), (0, 1.1, 3.6), 0.03, "PS_MetalRoof")
    for k in range(7):
        y = -1.0 + k * 0.33
        hw = 0.5 - k * 0.04
        bar(b, (-hw, y, 3.6), (hw, y, 3.6), 0.012, "PS_MetalRoof")
    for a in (0, 2.1, 4.2):
        bar(b, (0, 0, 3.0), (math.cos(a) * 1.8, math.sin(a) * 1.8, 0), 0.008, "PS_Rust")
    return finish(b, col)


# --- Промзона ---

def factory_chimney(col, name, h=45.0):
    # Кирпичная заводская труба с поясами и скобами
    b = Builder(name)
    tube(b, (0, 0, 0), (0, 0, h), 2.6, "PS_Brick", r1=1.5, segs=14, caps=False)
    tube(b, (0, 0, h), (0, 0, h - 1.0), 1.35, "LP_Burnt", r1=1.4, segs=14, caps=False)  # копоть внутри
    loft(b, [ring((0, 0, h), (0, 0, 1), 1.35, 14), ring((0, 0, h), (0, 0, 1), 1.5, 14)], "PS_Brick", False, False)
    for z in (8, 18, 28, 38):
        r = 2.6 + (1.5 - 2.6) * z / h
        tube(b, (0, 0, z), (0, 0, z + 0.3), r + 0.08, "PS_Rust", segs=14)
    tube(b, (0, 0, -0.5), (0, 0, 1.5), 3.4, "PS_Concrete", r1=3.0, segs=14)
    return finish(b, col)


def tower_crane(col, name, h=36.0):
    # Башенный кран: решётчатая башня, стрела вперёд (−Y), противовесная консоль, кабина, оголовок с тягами
    b = Builder(name)
    lattice(b, (0, 0, 0), h, 1.8, 1.8, 18, "PS_PaintYellow", w=0.1)
    jib = 30.0
    for x in (-0.7, 0.7):
        bar(b, (x, 1.0, h), (x, -jib, h), 0.12, "PS_PaintYellow")  # нижние пояса стрелы
    bar(b, (0, 1.0, h + 1.6), (0, -jib, h + 0.2), 0.12, "PS_PaintYellow")  # верхний пояс
    for k in range(15):
        y = -k * 2.0
        bar(b, (-0.7, y, h), (0.7, y, h), 0.07, "PS_PaintYellow")
        bar(b, (-0.7, y, h), (0, y - 1.0, h + 1.6 * (1 - (k + 0.5) / 15)), 0.06, "PS_PaintYellow")
        bar(b, (0.7, y, h), (0, y - 1.0, h + 1.6 * (1 - (k + 0.5) / 15)), 0.06, "PS_PaintYellow")
    bar(b, (-0.6, 0.5, h), (-0.6, 9.0, h), 0.15, "PS_PaintYellow")
    bar(b, (0.6, 0.5, h), (0.6, 9.0, h), 0.15, "PS_PaintYellow")
    b.box((-1.2, 6.0, h - 1.8), (1.2, 9.0, h), "PS_Concrete")  # противовес
    lattice(b, (0, 0, h), h + 6.0, 1.4, 0.3, 3, "PS_PaintYellow", w=0.08)
    bar(b, (0, 0, h + 6.0), (0, -jib * 0.65, h + 0.1), 0.04, "PS_Rust")
    bar(b, (0, 0, h + 6.0), (0, 8.5, h + 0.1), 0.04, "PS_Rust")
    b.box((0.95, -1.2, h - 2.4), (2.4, 0.6, h - 0.2), "PS_PaintYellow")  # кабина
    b.box((0.94, -1.21, h - 1.6), (2.41, -1.2, h - 0.5), "PS_Glass", skip=("+y",))
    bar(b, (0, -18.0, h - 0.1), (0, -18.0, 6.0), 0.02, "PS_Rust")  # трос с крюком
    b.box((-0.25, -18.25, 5.4), (0.25, -17.75, 6.0), "PS_PaintYellow")
    b.box((-1.6, -1.6, 0), (1.6, 1.6, 0.8), "PS_Concrete", skip=("-z",))
    return finish(b, col)


def fuel_tank(col, name, r=5.0, h=8.0):
    # Вертикальный резервуар с кольцевыми швами, винтовой лестницей (упрощённо — прямая по боку) и ограждением крыши
    b = Builder(name)
    tube(b, (0, 0, 0), (0, 0, h), r, "LP_PaintWhite", segs=20, caps=False)
    tube(b, (0, 0, h), (0, 0, h + 0.9), r, "LP_PaintWhite", r1=0.6, segs=20, caps=False)
    for z in (2, 4, 6):
        tube(b, (0, 0, z), (0, 0, z + 0.06), r + 0.03, "PS_Rust", segs=20, caps=False)
    for k in range(12):
        z = h * (k + 0.5) / 12
        a = -0.25 + k * 0.05
        p = V((math.cos(a) * (r + 0.4), math.sin(a) * (r + 0.4), z))
        bar(b, p, p + V((0, 0, 0.05)) + V((-math.sin(a), math.cos(a), 0)) * 0.7, 0.06, "PS_Rust", h=0.05)
    bar(b, (r + 0.4, -1.4, 0), (r + 0.4, 1.4, h), 0.08, "PS_Rust")
    tube(b, (0, 0, -0.3), (0, 0, 0.1), r + 0.6, "PS_Concrete", segs=20)
    return finish(b, col)


def gas_cylinder_at(b, c, paint="LP_PaintRed"):
    # Пропановый баллон 50 л: башмак, корпус с белой полосой, плечо, горловина и колпак вентиля
    x, y, z = c
    tube(b, (x, y, z), (x, y, z + 0.1), 0.14, "PS_Rust", segs=12)
    tube(b, (x, y, z + 0.1), (x, y, z + 0.82), 0.15, paint, segs=12)
    tube(b, (x, y, z + 0.58), (x, y, z + 0.66), 0.153, "LP_PaintWhite", segs=12, caps=False)
    tube(b, (x, y, z + 0.82), (x, y, z + 0.92), 0.15, paint, r1=0.07, segs=12)
    tube(b, (x, y, z + 0.92), (x, y, z + 0.96), 0.045, "PS_MetalRoof", segs=8)
    tube(b, (x, y, z + 0.96), (x, y, z + 1.06), 0.065, paint, r1=0.055, segs=8)


def gas_cylinder(col, name):
    # Одиночный газовый баллон у дома (плиты в частном секторе — на баллонном газе); взрывается
    b = Builder(name)
    gas_cylinder_at(b, (0, 0, 0))
    return finish(b, col)


def gas_cylinder_rack(col, name):
    # Шкаф-клетка на четыре баллона: каркас из уголка, решётка спереди, глухая задняя стенка, крыша, знак
    b = Builder(name)
    w, d, h = 1.5, 0.45, 1.22
    frame = "PS_MetalGreen"
    for x in (-w / 2, w / 2):
        for y in (-d / 2, d / 2):
            b.box((x - 0.025, y - 0.025, 0), (x + 0.025, y + 0.025, h), frame)
    for z in (0.04, 0.62, h - 0.03):
        bar(b, (-w / 2, -d / 2, z), (w / 2, -d / 2, z), 0.04, frame)
        for x in (-w / 2, w / 2):
            bar(b, (x, -d / 2, z), (x, d / 2, z), 0.04, frame)
    for k in range(1, 10):
        x = -w / 2 + k * w / 10
        bar(b, (x, -d / 2, 0.04), (x, -d / 2, h - 0.03), 0.018, frame)
    b.box((-w / 2, d / 2 - 0.02, 0), (w / 2, d / 2, h), frame)
    b.box((-w / 2 - 0.06, -d / 2 - 0.08, h), (w / 2 + 0.06, d / 2 + 0.04, h + 0.04), "PS_MetalRoof")
    b.box((-w / 2 + 0.02, -d / 2 + 0.02, 0), (w / 2 - 0.02, d / 2 - 0.02, 0.03), "PS_Rust")
    b.box((-0.16, -d / 2 - 0.04, 0.82), (0.16, -d / 2 - 0.03, 1.1), "LP_Sign_Danger", skip=("+y",))
    for x in (-0.54, -0.18, 0.18, 0.54):
        gas_cylinder_at(b, (x, 0.02, 0.03))
    return finish(b, col)


def fuel_truck(col, name):
    # Бензовоз на шасси ГАЗ-53: капот с решёткой и фарами, кабина со стёклами и зеркалами, рама, эллиптическая
    # цистерна с красной полосой, горловиной, поручнями и лестницей, ложементы, ящики, колёса (сзади спаренные)
    b = Builder(name)
    cab, tank = "LP_PaintGreen", "PS_PaintYellow"
    extrude_x(b, [(-3.2, 0.85), (-2.0, 0.85), (-2.0, 1.5), (-2.9, 1.5), (-3.2, 1.3)], -0.8, 0.8, cab)
    extrude_x(b, [(-2.0, 0.85), (-0.6, 0.85), (-0.6, 2.4), (-1.7, 2.4), (-2.0, 1.6)], -1.05, 1.05, cab)
    # Стёкла: лобовое по наклону кабины, боковые в дверях
    e = V((0, 0.3, 0.8)).normalized()
    off = V((0, -0.8, 0.3)).normalized() * 0.01
    lo, hi = V((0, -2.0, 1.6)) + e * 0.1 + off, V((0, -2.0, 1.6)) + e * 0.75 + off
    two_sided(b, [lo + V((-0.9, 0, 0)), lo + V((0.9, 0, 0)), hi + V((0.9, 0, 0)), hi + V((-0.9, 0, 0))], "PS_Glass")
    for s in (-1, 1):
        x = s * 1.06
        two_sided(b, [(x, -1.55, 1.7), (x, -0.8, 1.7), (x, -0.8, 2.25), (x, -1.35, 2.25)], "PS_Glass")
        bar(b, (s * 1.05, -1.75, 1.9), (s * 1.28, -1.75, 1.9), 0.03, "LP_Rubber")  # зеркало
        b.box((s * 1.28 - 0.02, -1.8, 1.8), (s * 1.28 + 0.02, -1.7, 2.0), "LP_Rubber")
    b.box((-0.7, -3.23, 0.9), (0.7, -3.2, 1.25), "PS_Rust", skip=("+y",))  # решётка
    for s in (-1, 1):
        tube(b, (s * 0.62, -3.15, 1.05), (s * 0.62, -3.26, 1.05), 0.09, "PS_Trim", segs=8)
        x0, x1 = sorted((s * 0.78, s * 1.15))
        b.box((x0, -3.05, 0.95), (x1, -1.95, 1.02), cab)  # крыло
    b.box((-1.1, -3.38, 0.55), (1.1, -3.2, 0.75), "PS_MetalRoof")
    b.box((-0.5, -3.2, 0.55), (0.5, 3.25, 0.8), "LP_Rubber")  # рама
    # Цистерна: эллипс со скруглёнными днищами
    tz = 1.75
    st = [(-0.45, 0.82), (-0.38, 0.96), (-0.28, 1.0), (2.95, 1.0), (3.05, 0.96), (3.12, 0.82)]
    closed_part(b, lambda: loft(b, [ring(V((0, y, tz)), V((0, 1, 0)), 1.05 * k, 16, rv=0.7 * k) for y, k in st], tank))
    loft(b, [ring(V((0, y, tz)), V((0, 1, 0)), 1.06, 16, rv=0.71) for y in (1.0, 1.35)], "LP_PaintRed", cap0=False, cap1=False)
    tube(b, (0, 1.3, tz + 0.6), (0, 1.3, tz + 0.82), 0.25, "PS_MetalRoof", segs=10)
    tube(b, (0, 1.3, tz + 0.82), (0, 1.3, tz + 0.86), 0.28, "PS_MetalRoof", segs=10)
    for s in (-1, 1):
        bar(b, (s * 0.4, -0.1, tz + 0.95), (s * 0.4, 2.8, tz + 0.95), 0.035, "PS_Rust")
        for y in (-0.1, 1.35, 2.8):
            bar(b, (s * 0.4, y, tz + 0.6), (s * 0.4, y, tz + 0.95), 0.03, "PS_Rust")
        bar(b, (s * 0.25, 3.2, 0.7), (s * 0.25, 3.0, tz + 0.65), 0.035, "PS_Rust")  # лестница
    for k in range(6):
        t = (k + 0.5) / 6
        y, z = 3.2 - 0.2 * t, 0.7 + (tz + 0.65 - 0.7) * t
        bar(b, (-0.25, y, z), (0.25, y, z), 0.03, "PS_Rust")
    for y in (-0.1, 1.4, 2.8):
        b.box((-0.85, y - 0.1, 0.8), (0.85, y + 0.1, 1.15), "LP_Rubber")  # ложементы
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.55, s * 0.98))
        b.box((x0, 0.0, 0.6), (x1, 0.9, 0.98), "PS_MetalRoof")  # ящик
        x0, x1 = sorted((s * 0.6, s * 1.22))
        b.box((x0, 1.35, 0.98), (x1, 2.7, 1.02), "LP_Rubber")  # брызговик
    b.box((-1.0, 3.1, 0.45), (1.0, 3.3, 0.7), "PS_Rust")
    tube(b, (0.85, -0.55, 0.85), (0.85, -0.55, 2.55), 0.05, "LP_Rubber", segs=6)  # выхлоп за кабиной
    for s in (-1, 1):
        car_wheel(b, s * 0.8, s * 1.1, -2.5, 0.48, 0.48, "LP_Rubber", "PS_MetalRoof", False)
        car_wheel(b, s * 0.6, s * 0.88, 2.0, 0.48, 0.48, "LP_Rubber", "PS_MetalRoof", False)
        car_wheel(b, s * 0.9, s * 1.18, 2.0, 0.48, 0.48, "LP_Rubber", "PS_MetalRoof", False)
    return finish(b, col)


def container(col, name, paint):
    # Морской контейнер 20 футов: гофра — PS_MetalGreen/PS_PaintBlue/PS_Rust, двери сзади, угловые фитинги
    b = Builder(name)
    L, w, h = 6.06, 2.44, 2.59
    b.box((-L / 2, -w / 2, 0), (L / 2, w / 2, h), paint)
    for x in (-L / 2, L / 2):
        for y in (-w / 2, w / 2):
            b.box((x - 0.08, y - 0.08, 0), (x + 0.08, y + 0.08, h + 0.02), "PS_Rust")
    for s in (-1, 1):
        b.box((L / 2, s * w / 4 - 0.6, 0.1), (L / 2 + 0.03, s * w / 4 + 0.58, h - 0.1), paint, skip=("-x",))
        for y in (-0.3, 0.3):
            bar(b, (L / 2 + 0.06, s * w / 4 + y, 0.15), (L / 2 + 0.06, s * w / 4 + y, h - 0.15), 0.04, "PS_Rust")
    return finish(b, col)


def wagon(col, name):
    # Крытый товарный вагон на двух тележках
    b = Builder(name)
    L, w, h = 14.0, 3.0, 3.6
    z0 = 1.1
    b.box((-w / 2, -L / 2, z0), (w / 2, L / 2, z0 + h), "PS_Rust")
    b.slab((-w / 2 - 0.1, -L / 2 - 0.05, z0 + h), (w / 2 + 0.1, -L / 2 - 0.05, z0 + h), (w / 2 + 0.1, L / 2 + 0.05, z0 + h),
           (-w / 2 - 0.1, L / 2 + 0.05, z0 + h), 0.15, "PS_MetalRoof")
    for s in (-1, 1):
        b.box((s * w / 2, -1.0, z0 + 0.1), (s * (w / 2 + 0.04), 1.0, z0 + 2.6), "LP_PaintRed")  # дверь
        bar(b, (s * (w / 2 + 0.06), -1.6, z0 + 2.7), (s * (w / 2 + 0.06), 1.6, z0 + 2.7), 0.06, "PS_Rust")
    b.box((-w / 2 - 0.05, -L / 2 - 0.3, z0 - 0.3), (w / 2 + 0.05, L / 2 + 0.3, z0), "LP_Burnt")  # рама
    for y in (-L / 2 + 2.2, L / 2 - 2.2):
        b.box((-1.0, y - 1.3, 0.45), (1.0, y + 1.3, z0 - 0.3), "LP_Burnt")
        for dy in (-0.9, 0.9):
            for s in (-1, 1):
                tube(b, (s * 0.76, y + dy, 0.45), (s * 0.86, y + dy, 0.45), 0.45, "PS_Rust", segs=10)
    for y in (-L / 2 - 0.3, L / 2 + 0.3):
        for x in (-0.9, 0.9):
            tube(b, (x, y, z0 - 0.1), (x, y + (0.4 if y > 0 else -0.4), z0 - 0.1), 0.15, "PS_Rust", segs=6)  # буфера
    return finish(b, col)


def rail_track(col, name, length=12.0):
    # Звено пути: шпалы через 0.55 м и два рельса (колея 1.52 м); лежит по Y
    b = Builder(name)
    n = int(length / 0.55)
    for k in range(n):
        y = -length / 2 + (k + 0.5) * length / n
        b.box((-1.35, y - 0.12, 0), (1.35, y + 0.12, 0.16), "LP_Burnt", skip=("-z",))
    for x in (-0.76, 0.76):
        b.box((x - 0.07, -length / 2, 0.16), (x + 0.07, length / 2, 0.19), "PS_Rust", skip=("-z",))
        b.box((x - 0.02, -length / 2, 0.19), (x + 0.02, length / 2, 0.3), "PS_Rust", skip=("-z",))
        b.box((x - 0.04, -length / 2, 0.3), (x + 0.04, length / 2, 0.34), "PS_MetalRoof")
    return finish(b, col)


def pipe_rack(col, name, length=12.0):
    # Эстакада трубопроводов: П-образные опоры через 6 м и три трубы
    b = Builder(name)
    for k in range(int(length / 6) + 1):
        y = -length / 2 + 6 * k
        for x in (-1.2, 1.2):
            b.box((x - 0.12, y - 0.12, 0), (x + 0.12, y + 0.12, 4.6), "PS_Concrete", skip=("-z",))
        b.box((-1.4, y - 0.15, 4.6), (1.4, y + 0.15, 4.9), "PS_Concrete")
    for x, r, m in ((-0.8, 0.3, "PS_Rust"), (0.0, 0.25, "LP_PaintWhite"), (0.75, 0.35, "PS_MetalRoof")):
        tube(b, (x, -length / 2, 4.9 + r), (x, length / 2, 4.9 + r), r, m, segs=10)
    return finish(b, col)


def floodlight_mast(col, name, h=20.0):
    # Мачта освещения: решётчатая опора и площадка с четырьмя прожекторами, смотрящими вперёд (−Y) и вниз
    b = Builder(name)
    lattice(b, (0, 0, 0), h, 1.6, 0.6, 8, "PS_MetalRoof", w=0.07)
    b.box((-1.2, -1.2, h), (1.2, 1.2, h + 0.1), "PS_Rust")
    for x in (-0.8, -0.27, 0.27, 0.8):
        b.box((x - 0.22, -1.1, h + 0.2), (x + 0.22, -0.6, h + 0.65), "PS_MetalRoof")
        b.box((x - 0.18, -1.12, h + 0.24), (x + 0.18, -1.1, h + 0.61), "PS_Glass", skip=("+y",))
    b.box((-1.0, -1.0, -0.4), (1.0, 1.0, 0.3), "PS_Concrete", skip=("-z",))
    return finish(b, col)


# --- Частный сектор ---

def woodpile(col, name, seed):
    # Поленница: ряды поленьев торцами наружу под навесом из листа
    rnd = random.Random(seed)
    b = Builder(name)
    L, rows = 2.8, 6
    for r_ in range(rows):
        z = 0.11 + r_ * 0.2
        x = -L / 2 + 0.11 + (0.1 if r_ % 2 else 0)
        while x < L / 2 - 0.1:
            rr = rnd.uniform(0.08, 0.11)
            tube(b, (x, -0.5, z), (x, 0.5, z), rr, "PS_Logs", segs=6, cap_mat="PS_Planks_Raw")
            x += 0.21
    for x in (-L / 2 - 0.05, L / 2 + 0.05):
        bar(b, (x, 0, 0), (x, 0, 1.6), 0.08, "PS_Planks_Raw")
    b.slab((-L / 2 - 0.3, -0.75, 1.65), (L / 2 + 0.3, -0.75, 1.65), (L / 2 + 0.3, 0.75, 1.8), (-L / 2 - 0.3, 0.75, 1.8),
           0.03, "PS_MetalRoof")
    return finish(b, col)


def well(col, name):
    # Колодец: сруб, ворот с ручкой, двускатная крышка на столбах, ведро
    b = Builder(name)
    s = 0.6
    for k in range(5):
        z = k * 0.18
        for (p0, p1) in (((-s, -s), (s, -s)), ((s, -s), (s, s)), ((s, s), (-s, s)), ((-s, s), (-s, -s))):
            bar(b, (p0[0], p0[1], z + 0.09), (p1[0], p1[1], z + 0.09), 0.18, "PS_Logs")
    for x in (-s, s):
        bar(b, (x, 0, 0.9), (x, 0, 1.9), 0.1, "PS_Planks_Raw")
    tube(b, (-s, 0, 1.3), (s, 0, 1.3), 0.1, "PS_Logs", segs=8)
    bar(b, (s + 0.05, 0, 1.3), (s + 0.05, -0.3, 1.3), 0.03, "PS_Rust")
    b.box((-s - 0.15, -0.5, 1.9), (s + 0.15, 0.0, 1.92), "PS_Planks_Raw")
    b.slab((-s - 0.15, -0.55, 1.88), (s + 0.15, -0.55, 1.88), (s + 0.15, 0, 2.2), (-s - 0.15, 0, 2.2), 0.03, "PS_Planks_Raw")
    b.slab((s + 0.15, 0.55, 1.88), (-s - 0.15, 0.55, 1.88), (-s - 0.15, 0, 2.2), (s + 0.15, 0, 2.2), 0.03, "PS_Planks_Raw")
    tube(b, (0.3, -0.75, 0), (0.3, -0.75, 0.3), 0.14, "PS_MetalRoof", r1=0.16, segs=8)
    return finish(b, col)


def greenhouse(col, name, seed):
    # Теплица: арочный каркас, плёнка/стекло, часть стёкол выбита (нет граней)
    rnd = random.Random(seed)
    b = Builder(name)
    L, r, segs = 6.0, 1.6, 6
    arc = [(math.cos(math.pi * i / segs) * r, math.sin(math.pi * i / segs) * r * 1.2 + 0.4) for i in range(segs + 1)]
    ys = [-L / 2 + L * k / 6 for k in range(7)]
    for y in ys:
        for i in range(segs):
            bar(b, (arc[i][0], y, arc[i][1]), (arc[i + 1][0], y, arc[i + 1][1]), 0.05, "PS_Planks_Raw")
        for x in (-r, r):
            bar(b, (x, y, 0), (x, y, 0.4), 0.05, "PS_Planks_Raw")
    for k in range(6):
        for i in range(segs):
            if rnd.random() < 0.18:
                continue
            p = [(arc[i][0], ys[k], arc[i][1]), (arc[i + 1][0], ys[k], arc[i + 1][1]),
                 (arc[i + 1][0], ys[k + 1], arc[i + 1][1]), (arc[i][0], ys[k + 1], arc[i][1])]
            two_sided(b, p, "PS_Glass")
    b.box((-r, -L / 2, 0), (r, L / 2, 0.4), "PS_Planks_Raw", skip=("-z", "+z"))
    return finish(b, col)


def dog_house(col, name):
    # Собачья будка с лазом спереди и двускатной крышей
    b = Builder(name)
    w, d, h = 0.9, 1.1, 0.8
    b.box((-w / 2, -d / 2, 0), (w / 2, d / 2, h), "PS_Planks_Raw", skip=("+z",))
    b.box((-0.2, -d / 2 - 0.01, 0.05), (0.2, -d / 2, 0.55), "PS_Glass", skip=("+y",))
    b.slab((-w / 2 - 0.1, -d / 2 - 0.1, h), (0, -d / 2 - 0.1, h + 0.35), (0, d / 2 + 0.1, h + 0.35), (-w / 2 - 0.1, d / 2 + 0.1, h),
           0.03, "PS_Slate")
    b.slab((0, -d / 2 - 0.1, h + 0.35), (w / 2 + 0.1, -d / 2 - 0.1, h), (w / 2 + 0.1, d / 2 + 0.1, h), (0, d / 2 + 0.1, h + 0.35),
           0.03, "PS_Slate")
    for y in (-d / 2, d / 2):
        b.face([(-w / 2, y, h), (w / 2, y, h), (0, y, h + 0.35)] if y < 0 else [(w / 2, y, h), (-w / 2, y, h), (0, y, h + 0.35)],
               "PS_Planks_Raw")
    tube(b, (0.6, -0.4, 0), (0.6, -0.4, 0.08), 0.15, "PS_MetalRoof", segs=8)  # миска
    return finish(b, col)


def stray_dog_body(col, name):
    # Бездомная дворняга, тело без ног (нога — отдельная модель StrayDog_Leg, четыре копии качает
    # Assets/Scripts/Effects/StrayDogs.cs): туловище — лофт эллиптических сечений, шея, голова с мордой, уши, глаза,
    # нос, хвост. Перед — в −Y; точки крепления ног (±0.07, −0.22, 0.36) и (±0.07, 0.22, 0.36)
    b = Builder(name)
    fur, dark = "LP_Dirt", "LP_Rubber"
    st = [(-0.31, 0.07, 0.08, 0.45), (-0.27, 0.11, 0.14, 0.45), (-0.15, 0.12, 0.15, 0.44), (0.05, 0.11, 0.12, 0.44),
          (0.22, 0.11, 0.12, 0.45), (0.3, 0.07, 0.08, 0.46)]
    closed_part(b, lambda: loft(b, [ring(V((0, y, z)), V((0, 1, 0)), rx, 12, rv=rz) for y, rx, rz, z in st], fur))
    tube(b, (0, -0.22, 0.48), (0, -0.36, 0.62), 0.075, fur, r1=0.06, segs=8)
    hs = [(-0.33, 0.05, 0.05, 0.645), (-0.36, 0.075, 0.075, 0.645), (-0.44, 0.07, 0.07, 0.64), (-0.47, 0.045, 0.045, 0.615),
          (-0.56, 0.038, 0.036, 0.605), (-0.575, 0.025, 0.022, 0.605)]
    closed_part(b, lambda: loft(b, [ring(V((0, y, z)), V((0, 1, 0)), rx, 10, rv=rz) for y, rx, rz, z in hs], fur))
    b.box((-0.02, -0.59, 0.6), (0.02, -0.57, 0.63), dark)
    for s in (-1, 1):
        x0, x1 = sorted((s * 0.03, s * 0.045))
        b.box((x0, -0.47, 0.655), (x1, -0.455, 0.67), dark)
        two_sided(b, [(s * 0.025, -0.38, 0.7), (s * 0.07, -0.36, 0.69), (s * 0.06, -0.39, 0.78)], fur)
    tube(b, (0, 0.28, 0.47), (0, 0.37, 0.56), 0.03, fur, r1=0.022, segs=6)
    tube(b, (0, 0.37, 0.56), (0, 0.43, 0.62), 0.022, fur, r1=0.012, segs=6)
    return finish(b, col)


def stray_dog_leg(col, name):
    # Нога дворняги: верх — в начале координат (точка крепления), лапа на z = −0.36; качается вокруг X
    b = Builder(name)
    tube(b, (0, 0, 0.02), (0, 0.012, -0.2), 0.035, "LP_Dirt", r1=0.026, segs=6)
    tube(b, (0, 0.012, -0.2), (0, 0, -0.34), 0.026, "LP_Dirt", r1=0.022, segs=6)
    b.box((-0.026, -0.05, -0.36), (0.026, 0.02, -0.33), "LP_Dirt")
    return finish(b, col)


def wooden_pole(col, name, h=7.5):
    # Деревянный столб на бетонном пасынке с траверсой и двумя изоляторами (провода тянет Unity)
    b = Builder(name)
    b.box((-0.1, 0.12, 0), (0.1, 0.3, 2.0), "PS_Concrete", skip=("-z",))
    tube(b, (0, 0, 0.2), (0, 0, h), 0.12, "PS_Logs", r1=0.1, segs=8)
    bar(b, (-0.6, 0, h - 0.4), (0.6, 0, h - 0.4), 0.09, "PS_Planks_Raw")
    for x in (-0.5, 0.5):
        tube(b, (x, 0, h - 0.35), (x, 0, h - 0.15), 0.05, "PS_Glass", r1=0.03, segs=6)
    return finish(b, col)


# --- Пустырь и война ---

def sandbag_wall(col, name, length=3.0, rows=3):
    # Бруствер из мешков с песком (мешки — округлые бруски, через ряд со сдвигом)
    b = Builder(name)
    for r_ in range(rows):
        z = r_ * 0.25
        x = -length / 2 + (0.3 if r_ % 2 else 0)
        while x < length / 2 - 0.1:
            x1 = min(x + 0.6, length / 2)
            rock_into(b, ((x + x1) / 2, 0, z), ((x1 - x) / 2 + 0.02, 0.3, 0.17), int(x * 100) + r_ * 7, "LP_Sandbag",
                      flat=0.6, subdiv=1)
            x = x1
    return finish(b, col)


def burnt_btr(col, name):
    # Сгоревший БТР-70: клиновидный корпус на восьми колёсах (часть без резины), башенка со стволом, открытые люки
    b = Builder(name)
    L, w = 7.5, 2.8
    extrude_x(b, [(-L / 2, 1.0), (-L / 2 + 1.0, 0.55), (L / 2 - 0.7, 0.55), (L / 2, 1.1), (L / 2, 1.9), (L / 2 - 1.3, 2.25),
                  (-L / 2 + 2.0, 2.25), (-L / 2 + 0.2, 1.55)], -w / 2, w / 2, "LP_Burnt")
    tube(b, (0.2, -0.6, 2.25), (0.2, -0.6, 2.75), 0.55, "LP_Burnt", r1=0.45, segs=10)
    tube(b, (0.2, -1.0, 2.55), (0.2, -3.0, 2.48), 0.07, "LP_Burnt", segs=6)
    for k, y in enumerate((-2.4, -0.9, 0.9, 2.4)):
        for s in (-1, 1):
            r = 0.6 if (k + s) % 3 else 0.42
            tube(b, (s * w / 2 - s * 0.1, y, 0.6), (s * w / 2 + s * 0.35, y, 0.6), r, "LP_Rubber" if r > 0.5 else "LP_Burnt", segs=10)
    b.box((-0.9, 1.2, 2.25), (-0.2, 1.9, 2.3), "LP_Burnt")
    b.box((-0.9, 1.9, 2.3), (-0.2, 1.95, 3.0), "LP_Burnt")  # распахнутый люк
    return finish(b, col)


def burnt_truck(col, name):
    # Сгоревший грузовик (ЗИЛ-130): кабина, капот, рама с бортовой платформой, колёса-диски
    b = Builder(name)
    w = 2.4
    extrude_x(b, [(-4.0, 0.9), (-2.6, 0.9), (-2.6, 1.6), (-3.6, 1.6), (-4.0, 1.45)], -0.85, 0.85, "LP_Burnt")  # капот
    extrude_x(b, [(-2.6, 0.9), (-1.0, 0.9), (-1.0, 2.6), (-2.2, 2.6), (-2.6, 1.7)], -w / 2 + 0.1, w / 2 - 0.1, "LP_Burnt")
    b.box((-w / 2, -1.0, 0.6), (w / 2, 3.8, 0.9), "LP_Burnt")
    for (x0, x1) in ((-w / 2, -w / 2 + 0.05), (w / 2 - 0.05, w / 2)):
        b.box((x0, -0.9, 0.9), (x1, 3.8, 1.5), "LP_Burnt")
    b.box((-w / 2, 3.75, 0.9), (w / 2, 3.8, 1.5), "LP_Burnt")
    for y in (-3.0, 2.4, 3.3):
        for s in (-1, 1):
            tube(b, (s * 0.9, y, 0.5), (s * 1.15, y, 0.5), 0.45, "LP_Burnt", segs=10)
    return finish(b, col)


def hedgehog(col, name):
    # Противотанковый ёж: три сваренных уголка крест-накрест
    b = Builder(name)
    s = 1.1
    for d in (V((1, 1, 0.9)), V((1, -1, 0.9)), V((0, 1.3, -0.9))):
        d = d.normalized() * s
        c = V((0, 0, 0.8))
        bar(b, c - d, c + d, 0.14, "PS_Rust", h=0.03)
        bar(b, c - d, c + d, 0.03, "PS_Rust", h=0.14)
    return finish(b, col)


def barbed_wire(col, name, length=6.0):
    # Спираль колючей проволоки на X-образных кольях
    b = Builder(name)
    for x in (-length / 2, 0, length / 2):
        bar(b, (x, -0.5, 0), (x, 0.5, 1.1), 0.06, "PS_Planks_Raw")
        bar(b, (x, 0.5, 0), (x, -0.5, 1.1), 0.06, "PS_Planks_Raw")
    turns, segs, r = int(length / 0.35), 10, 0.45
    prev = None
    for i in range(turns * segs + 1):
        t = i / segs
        a = 2 * math.pi * t
        p = V((-length / 2 + length * i / (turns * segs), math.cos(a) * r, 0.5 + math.sin(a) * r))
        if prev is not None:
            bar(b, prev, p, 0.012, "PS_Rust")
        prev = p
    return finish(b, col)


def dugout(col, name):
    # Блиндаж: земляной холм с накатом из брёвен и входом с бревенчатой обвязкой спереди (−Y)
    b = Builder(name)
    rock_into(b, (0, 0.6, 0), (3.2, 3.0, 1.3), 7, "LP_Dirt", flat=0.05)
    for k in range(9):
        x = -2.0 + k * 0.5
        tube(b, (x, -1.8, 1.25), (x, 2.4, 1.25), 0.15, "PS_Logs", segs=6, cap_mat="PS_Planks_Raw")
    b.box((-0.7, -2.6, 0), (0.7, -1.6, 1.2), "LP_Burnt", skip=("-z", "-y"))
    b.face([(-0.6, -2.0, 0), (0.6, -2.0, 0), (0.6, -2.0, 1.1), (-0.6, -2.0, 1.1)], "PS_Glass")  # тёмный проход
    for x in (-0.75, 0.75):
        tube(b, (x, -2.6, 0), (x, -2.6, 1.3), 0.1, "PS_Logs", segs=6)
    tube(b, (-0.9, -2.6, 1.35), (0.9, -2.6, 1.35), 0.12, "PS_Logs", segs=6)
    return finish(b, col)


# --- Ориентиры ---

def water_tower(col, name):
    # Водонапорная башня Рожновского: кирпичный ствол, стальной бак, конус крыши, лестница, авиационный огонь сверху
    b = Builder(name)
    tube(b, (0, 0, 0), (0, 0, 18.0), 3.0, "PS_Brick", r1=2.6, segs=16, caps=False)
    for z in (6.0, 12.0):
        tube(b, (0, 0, z), (0, 0, z + 0.4), 3.05 - z * 0.022, "PS_Plaster", segs=16)
    b.box((-0.7, -3.05, 0), (0.7, -2.7, 2.4), "PS_Rust", skip=("-z",))  # дверь
    tube(b, (0, 0, 18.0), (0, 0, 19.0), 2.6, "PS_Rust", r1=4.6, segs=16)
    tube(b, (0, 0, 19.0), (0, 0, 24.5), 4.6, "LP_PaintWhite", segs=16, caps=False)
    tube(b, (0, 0, 24.5), (0, 0, 26.5), 4.8, "PS_Rust", r1=0.4, segs=16)
    tube(b, (0, 0, 26.5), (0, 0, 28.0), 0.08, "PS_Rust", segs=6)
    for k in range(2):
        bar(b, (4.7 * (1 if k else -1), -0.3, 19.0), (4.7 * (1 if k else -1), -0.3, 24.5), 0.05, "PS_Rust")
    return finish(b, col)


def tv_mast(col, name, h=120.0):
    # Телевышка: решётчатая мачта, красно-белые секции, площадки и антенный шпиль
    b = Builder(name)
    lattice(b, (0, 0, 0), h, 12.0, 2.0, 24, None, w=0.35, mats=["LP_PaintRed", "LP_PaintRed", "LP_PaintWhite", "LP_PaintWhite"])
    for z in (h * 0.33, h * 0.66, h):
        hw = (12.0 + (2.0 - 12.0) * z / h) / 2 + 0.8
        b.box((-hw, -hw, z - 0.3), (hw, hw, z), "PS_Rust")
    tube(b, (0, 0, h), (0, 0, h + 18.0), 0.4, "LP_PaintRed", r1=0.15, segs=8)
    for x in (-6.5, 6.5):
        for y in (-6.5, 6.5):
            b.box((x - 0.8, y - 0.8, -0.5), (x + 0.8, y + 0.8, 0.6), "PS_Concrete", skip=("-z",))
    return finish(b, col)


def power_chimney(col, name, h=110.0):
    # Труба ГРЭС: бетонный ствол, красно-белые пояса в верхней трети, площадки
    b = Builder(name)
    r0, r1 = 7.0, 3.8
    zs = [0.0, h * 0.66] + [h * 0.66 + (h * 0.34) * k / 6 for k in range(1, 7)]
    for k in range(len(zs) - 1):
        za, zb = zs[k], zs[k + 1]
        m = "PS_Concrete" if k == 0 else ("LP_PaintRed" if k % 2 else "LP_PaintWhite")
        tube(b, (0, 0, za), (0, 0, zb), r0 + (r1 - r0) * za / h, m, r1=r0 + (r1 - r0) * zb / h, segs=20, caps=False)
    tube(b, (0, 0, h), (0, 0, h - 2.0), r1 - 0.4, "LP_Burnt", segs=20, caps=False)
    loft(b, [ring((0, 0, h), (0, 0, 1), r1 - 0.4, 20), ring((0, 0, h), (0, 0, 1), r1, 20)], "LP_Burnt", False, False)
    for z in (h * 0.5, h * 0.8):
        r = r0 + (r1 - r0) * z / h
        tube(b, (0, 0, z), (0, 0, z + 0.4), r + 1.0, "PS_Rust", segs=20)
    return finish(b, col)


def culture_house(col, name):
    # Дом культуры: два этажа под штукатуркой, портик из шести колонн с фронтоном, ступени, вывеска, ленточные окна
    b = Builder(name)
    W, D, H = 36.0, 18.0, 11.0
    b.box((-W / 2, -D / 2, 0), (W / 2, D / 2, H), "PS_Plaster", skip=("-z",))
    b.box((-W / 2 - 0.3, -D / 2 - 0.3, H), (W / 2 + 0.3, D / 2 + 0.3, H + 0.8), "PS_Trim")
    for z in (1.4, 6.4):
        for s in (-1, 1):
            x0 = s * 7.5
            x1 = s * (W / 2 - 1.5)
            b.at((0, -D / 2, 0), (0, -1, 0))
            b.box((min(x0, x1), 0, z), (max(x0, x1), 0.03, z + 3.0), "PS_Glass", skip=("-y",))
            b.reset()
        for side in (-1, 1):
            b.at((side * W / 2, 0, 0), (side, 0, 0))
            b.box((-D / 2 + 1.5, 0, z), (D / 2 - 1.5, 0.03, z + 3.0), "PS_Glass", skip=("-y",))
            b.reset()
    # Портик
    PW, PD = 14.0, 5.0
    y0 = -D / 2 - PD
    b.box((-PW / 2 - 1.0, y0 - 2.0, 0), (PW / 2 + 1.0, -D / 2, 0.5), "PS_Concrete", skip=("-z",))
    b.box((-PW / 2 - 1.0, y0 - 3.0, 0), (PW / 2 + 1.0, y0 - 2.0, 0.25), "PS_Concrete", skip=("-z",))
    for k in range(6):
        x = -PW / 2 + 0.6 + k * (PW - 1.2) / 5
        tube(b, (x, y0 + 0.6, 0.5), (x, y0 + 0.6, H - 0.4), 0.45, "PS_Trim", r1=0.4, segs=10)
    b.box((-PW / 2, y0, H - 0.4), (PW / 2, -D / 2, H + 0.8), "PS_Trim")
    b.at((0, y0, H + 0.8), (0, -1, 0))
    b.prism((-PW / 2, PW / 2, 0), 3.0, -PD, "PS_Plaster")
    b.reset()
    b.face([(-5.0, y0 - 0.02, H - 0.3), (5.0, y0 - 0.02, H - 0.3), (5.0, y0 - 0.02, H + 0.7), (-5.0, y0 - 0.02, H + 0.7)],
           "LP_Sign_DK")
    b.box((-1.6, -D / 2 - 0.02, 0.5), (1.6, -D / 2, 4.0), "PS_Glass", skip=("+y",))  # двери
    return finish(b, col)


# --- Озеро ---

def pier(col, name, length=20.0, w=3.0):
    # Деревянный причал: сваи (уходят на 4 м вниз — под воду) и настил с пропавшими досками
    b = Builder(name)
    rnd = random.Random(3)
    for k in range(int(length / 4) + 1):
        y = k * 4.0
        for x in (-w / 2 + 0.2, w / 2 - 0.2):
            tube(b, (x, y, -4.0), (x, y, 0.9), 0.15, "PS_Logs", segs=6)
    for k in range(int(length / 0.3)):
        if rnd.random() < 0.08:
            continue
        y = k * 0.3
        b.box((-w / 2, y, 0.8), (w / 2, y + 0.26, 0.86), "PS_Planks_Raw")
    for x in (-w / 2 + 0.2, w / 2 - 0.2):
        b.box((x - 0.08, 0, 0.65), (x + 0.08, length, 0.8), "PS_Planks_Raw")
    return finish(b, col)


def boat(col, name, paint):
    # Вёсельная лодка: корпус лофтом по поперечным сечениям, две банки внутри
    b = Builder(name)
    secs = [(-2.1, 0.05, 0.62), (-1.6, 0.45, 0.55), (-0.6, 0.7, 0.5), (0.6, 0.7, 0.5), (1.6, 0.6, 0.52), (2.0, 0.5, 0.55)]
    outer, inner = [], []
    for y, hw, h in secs:
        outer.append([V((-hw, y, h)), V((-hw * 0.8, y, 0.1)), V((0, y, 0.0)), V((hw * 0.8, y, 0.1)), V((hw, y, h))])
        inner.append([V((-hw + 0.04, y, h)), V((-hw * 0.8 + 0.04, y, 0.14)), V((0, y, 0.06)), V((hw * 0.8 - 0.04, y, 0.14)),
                      V((hw - 0.04, y, h))])
    for k in range(len(secs) - 1):
        for i in range(4):
            b.face([outer[k][i], outer[k + 1][i], outer[k + 1][i + 1], outer[k][i + 1]], paint)
            b.face([inner[k][i + 1], inner[k + 1][i + 1], inner[k + 1][i], inner[k][i]], "PS_Planks_Raw")
        b.face([outer[k][4], outer[k + 1][4], inner[k + 1][4], inner[k][4]], "PS_Planks_Raw")
        b.face([inner[k][0], inner[k + 1][0], outer[k + 1][0], outer[k][0]], "PS_Planks_Raw")
    b.face(list(reversed(outer[0])), paint)
    b.face(list(outer[-1]), paint)
    for y in (-0.5, 0.8):
        b.box((-0.62, y, 0.38), (0.62, y + 0.25, 0.42), "PS_Planks_Raw")
    return finish(b, col)


def bridge_ruin(col, name):
    # Обрушенный мост: пролёт на двух опорах обрывается над водой, рваный край с арматурой; начало — на берегу (y = 0)
    b = Builder(name)
    W, L = 10.0, 34.0
    for y in (8.0, 22.0):
        b.box((-3.5, y - 1.0, -30.0), (3.5, y + 1.0, -0.8), "PS_Concrete")
        b.box((-W / 2 + 0.5, y - 1.2, -0.8), (W / 2 - 0.5, y + 1.2, 0), "PS_Concrete")
    b.box((-W / 2, 0, 0), (W / 2, L, 1.0), "PS_Concrete")
    b.box((-W / 2, 0, 1.0), (W / 2, L, 1.05), "PS_Concrete")
    for s in (-1, 1):
        b.box((s * W / 2 - (0.3 if s > 0 else 0), 0, 1.0), (s * W / 2 + (0 if s > 0 else 0.3), L, 1.9), "PS_Concrete")
    # Отвалившийся кусок, наклонно уходящий в воду
    b.slab((-W / 2 + 0.5, L + 0.6, 0.0), (W / 2 - 1.0, L + 0.6, -0.4), (W / 2 - 1.5, L + 10.0, -9.0), (-W / 2, L + 10.0, -8.5),
           1.0, "PS_Concrete")
    rnd = random.Random(5)
    for k in range(10):
        x = -W / 2 + 0.6 + k * (W - 1.2) / 9
        bar(b, (x, L, 0.5), (x + rnd.uniform(-0.4, 0.4), L + rnd.uniform(0.6, 1.6), 0.5 + rnd.uniform(-1.0, 0.5)), 0.04, "PS_Rust")
    return finish(b, col)


def reeds(col, name, seed, n=60, radius=1.8):
    # Куртина камыша: двусторонние сужающиеся листья, наклонённые наружу, кое-где метёлки
    rnd = random.Random(seed)
    b = Builder(name)
    for k in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        rr = radius * math.sqrt(rnd.random())
        x, y = math.cos(a) * rr, math.sin(a) * rr
        h = rnd.uniform(1.4, 2.4)
        lean = V((math.cos(a), math.sin(a), 0)) * rnd.uniform(0.1, 0.5) + V((rnd.uniform(-0.2, 0.2), rnd.uniform(-0.2, 0.2), 0))
        yaw = rnd.uniform(0, math.pi)
        side = V((math.cos(yaw), math.sin(yaw), 0)) * 0.03
        base = V((x, y, -0.1))
        top = base + V((0, 0, h)) + lean
        p = [base - side, base + side, top]
        two_sided(b, p, "LP_Reed")
        if rnd.random() < 0.2:
            tube(b, top - V((0, 0, 0.05)), top + V((0, 0, 0.25)), 0.03, "PS_Logs", segs=4, caps=False)
    return finish(b, col)


def boulder(col, name, seed, size):
    b = Builder(name)
    rock_into(b, (0, 0, 0), size, seed, "LP_Stone", flat=0.3, subdiv=2)
    return finish(b, col)


# --- Заграждения и геймплей ---

def road_closed(col, name, w=8.0):
    # «Проезд закрыт»: полосатый шлагбаум на двух опорах и табличка в центре
    b = Builder(name)
    for x in (-w / 2, w / 2):
        b.box((x - 0.12, -0.12, 0), (x + 0.12, 0.12, 1.1), "LP_Stripes", skip=("-z",))
    b.box((-w / 2, -0.08, 0.9), (w / 2, 0.08, 1.06), "LP_Stripes")
    b.box((-w / 2, -0.08, 0.3), (w / 2, 0.08, 0.46), "LP_Stripes")
    b.face([(-0.9, -0.1, 1.06), (0.9, -0.1, 1.06), (0.9, -0.1, 1.96), (-0.9, -0.1, 1.96)], "LP_Sign_Closed")
    b.box((-0.9, -0.09, 1.06), (0.9, -0.05, 1.96), "PS_MetalRoof", skip=("-y",))
    for x in (-0.6, 0.6):
        bar(b, (x, -0.04, 1.0), (x, -0.04, 1.1), 0.04, "PS_Rust")
    return finish(b, col)


def ramp(col, name, l=9.0, w=5.0, h=1.8, mat="PS_Concrete"):
    # Трамплин: клин, въезд в −Y, обрыв в +Y
    b = Builder(name)
    extrude_x(b, [(-l / 2, 0.0), (l / 2, 0.0), (l / 2, h)], -w / 2, w / 2, mat)
    return finish(b, col)


def wood_ramp(col, name, l=8.0, w=4.4, h=1.6):
    # Деревянный трамплин из досок на брусьях — ломается (собирается Unity как LooseProp)
    b = Builder(name)
    b.slab((-w / 2, -l / 2, 0.0), (w / 2, -l / 2, 0.0), (w / 2, l / 2, h), (-w / 2, l / 2, h), 0.08, "PS_Planks_Raw")
    for x in (-w / 2 + 0.2, 0, w / 2 - 0.2):
        bar(b, (x, l / 2 - 0.1, 0), (x, l / 2 - 0.1, h), 0.15, "PS_Planks_Raw")
        bar(b, (x, 0, 0), (x, 0, h / 2), 0.12, "PS_Planks_Raw")
    return finish(b, col)


def crate(col, name):
    # Деревянный ящик 1.1 м
    b = Builder(name)
    s = 0.55
    b.box((-s, -s, 0), (s, s, 2 * s), "PS_Planks_Raw")
    for (o, n) in (((0, -s, 0), (0, -1, 0)), ((0, s, 0), (0, 1, 0)), ((-s, 0, 0), (-1, 0, 0)), ((s, 0, 0), (1, 0, 0))):
        b.at(o, n)
        b.box((-s, 0, 0), (s, 0.03, 0.1), "PS_Planks_Raw")
        b.box((-s, 0, 2 * s - 0.1), (s, 0.03, 2 * s), "PS_Planks_Raw")
        bar(b, (-s + 0.05, 0.02, 0.1), (s - 0.05, 0.02, 2 * s - 0.1), 0.1, "PS_Planks_Raw", h=0.03)
        b.reset()
    return finish(b, col)


def barricade(col, name, seed):
    # Баррикада: козлы из брусьев, доски и покрышки — ломается от удара
    rnd = random.Random(seed)
    b = Builder(name)
    for x in (-1.6, 1.6):
        bar(b, (x, -0.6, 0), (x, 0.2, 1.3), 0.12, "PS_Planks_Raw")
        bar(b, (x, 0.6, 0), (x, -0.2, 1.3), 0.12, "PS_Planks_Raw")
    for z in (0.5, 0.9, 1.25):
        bar(b, (-2.0, rnd.uniform(-0.1, 0.1), z), (2.0, rnd.uniform(-0.1, 0.1), z + rnd.uniform(-0.15, 0.15)), 0.2,
            "PS_Planks_Raw", h=0.04)
    tube(b, (0.3, -0.7, 0), (0.3, -0.7, 0.25), 0.35, "LP_Rubber", segs=10)
    return finish(b, col)


def nitro_canister(col, name):
    # Баллон закиси азота (подбираемый): синий цилиндр с белой полосой, вентиль и ручка
    b = Builder(name)
    tube(b, (0, 0, 0), (0, 0, 0.75), 0.17, "PS_PaintBlue", segs=12)
    tube(b, (0, 0, 0.45), (0, 0, 0.55), 0.172, "LP_PaintWhite", segs=12, caps=False)
    tube(b, (0, 0, 0.75), (0, 0, 0.85), 0.17, "PS_PaintBlue", r1=0.06, segs=12)
    tube(b, (0, 0, 0.85), (0, 0, 0.95), 0.03, "PS_MetalRoof", segs=6)
    bar(b, (-0.08, 0, 0.96), (0.08, 0, 0.96), 0.03, "LP_PaintRed")
    return finish(b, col)


polish = ps.polish


def pg7_round(b, x0, y, z, length=0.92):
    # Выстрел ПГ-7В лёжа вдоль X: хвостовая трубка с кольцом стабилизатора, двигатель, головная часть
    # с оживальным обтекателем и взрывателем на носу
    seg = 16
    tail = x0 + length * 0.42
    tube(b, (x0, y, z), (x0 + 0.03, y, z), 0.028, "PS_MetalRoof", segs=seg)
    tube(b, (x0 + 0.03, y, z), (tail, y, z), 0.021, "PS_MetalRoof", segs=seg, caps=False)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        p = V((x0 + 0.05, y + math.cos(a) * 0.024, z + math.sin(a) * 0.024))
        bar(b, p, p + V((0.12, 0, 0)), 0.006, "PS_MetalRoof", h=0.018)
    motor = x0 + length * 0.6
    tube(b, (tail, y, z), (motor, y, z), 0.021, "LP_PaintGreen", r1=0.036, segs=seg, caps=False)
    head0 = motor
    head1 = x0 + length * 0.78
    tube(b, (head0, y, z), (head1, y, z), 0.042, "LP_PaintGreen", segs=seg)
    rings = []
    for k in range(7):
        f = k / 6
        r = 0.042 * math.cos(f * math.pi / 2) ** 0.8 + 0.008 * f
        rings.append(ring((head1 + (x0 + length - 0.03 - head1) * f, y, z), (1, 0, 0), r, seg))
    loft(b, rings, "LP_PaintGreen", cap0=False)
    tube(b, (x0 + length - 0.03, y, z), (x0 + length, y, z), 0.012, "PS_MetalRoof", r1=0.006, segs=8)


def rpg_crate(col, name):
    # Ящик выстрелов к РПГ-7 (подбираемый): зелёный дощатый ящик на полозьях, верёвочные ручки, защёлки,
    # крышка откинута назад, внутри на ложементах два выстрела ПГ-7В
    b = Builder(name)
    L, W, H = 1.02, 0.44, 0.3
    hl, hw = L / 2, W / 2
    t = 0.022
    wood = "PS_Planks_Green"
    # Полозья и дно
    for y in (-hw + 0.06, hw - 0.06):
        b.box((-hl, y - 0.03, 0), (hl, y + 0.03, 0.035), "PS_Planks_Raw")
    b.box((-hl, -hw, 0.035), (hl, hw, 0.035 + t), wood)
    # Стенки доска к доске с узким зазором
    z0 = 0.035 + t
    planks = [(z0, z0 + 0.12), (z0 + 0.125, H)]
    for z_a, z_b in planks:
        b.box((-hl, -hw, z_a), (hl, -hw + t, z_b), wood)
        b.box((-hl, hw - t, z_a), (hl, hw, z_b), wood)
        b.box((-hl, -hw + t, z_a), (-hl + t, hw - t, z_b), wood)
        b.box((hl - t, -hw + t, z_a), (hl, hw - t, z_b), wood)
    # Наугольники и обвязка торцов
    for x in (-hl - 0.012, hl - 0.008):
        for y in (-hw - 0.012, hw - 0.008):
            b.box((x, y, z0), (x + 0.02, y + 0.02, H), wood)
    for x, s in ((-hl, -1), (hl, 1)):
        b.box((x + (0 if s > 0 else -0.018), -hw, H - 0.05), (x + (0.018 if s > 0 else 0), hw, H - 0.02), wood)
        # Верёвочная ручка: петля из двух узлов
        pts = [V((x + s * 0.03, -0.09 + 0.18 * k / 10, H - 0.12 - 0.05 * math.sin(math.pi * k / 10))) for k in range(11)]
        for p0, p1 in zip(pts, pts[1:]):
            tube(b, p0, p1, 0.011, "LP_Sandbag", segs=6, caps=False)
        for yy in (-0.09, 0.09):
            tube(b, (x, yy, H - 0.12), (x + s * 0.035, yy, H - 0.12), 0.018, "LP_Sandbag", segs=8)
    # Защёлки спереди
    for x in (-0.3, 0.3):
        b.box((x - 0.03, -hw - 0.012, H - 0.09), (x + 0.03, -hw, H - 0.02), "PS_MetalRoof")
        b.box((x - 0.012, -hw - 0.02, H - 0.07), (x + 0.012, -hw - 0.012, H - 0.03), "PS_MetalRoof")
    # Трафаретная табличка
    two_sided(b, [(-0.16, -hw - 0.002, 0.11), (0.16, -hw - 0.002, 0.11), (0.16, -hw - 0.002, 0.2), (-0.16, -hw - 0.002, 0.2)],
              "LP_PaintWhite")
    # Ложементы и выстрелы
    for x in (-0.3, 0.05, 0.36):
        b.box((x - 0.03, -hw + t, z0), (x + 0.03, hw - t, z0 + 0.06), "PS_Planks_Raw")
    for y in (-0.1, 0.1):
        pg7_round(b, -0.47, y, z0 + 0.105)
    # Крышка откинута на петлях задней кромки на 110°
    lid = Builder("_lid")
    lid.box((-hl, -W, 0), (hl, 0, t), wood)
    for x in (-hl + 0.1, 0.0, hl - 0.1):
        lid.box((x - 0.03, -W, t), (x + 0.03, 0, t + 0.018), wood)
    import mathutils
    rot = mathutils.Matrix.Translation((0, hw, H)) @ mathutils.Matrix.Rotation(math.radians(-110), 4, 'X')
    for f in lid.bm.faces:
        b.face([rot @ v.co for v in f.verts], lid.mats[f.material_index])
    lid.bm.free()
    for x in (-0.35, 0.35):
        tube(b, (x - 0.04, hw + 0.006, H), (x + 0.04, hw + 0.006, H), 0.009, "PS_MetalRoof", segs=8)
    return polish(finish(b, col), width=0.004, segments=2)


def build_all():
    scene = bpy.data.scenes.get("LevelProps") or bpy.data.scenes.new("LevelProps")
    col = bpy.data.collections.get("LP_Models")
    if col is None:
        col = bpy.data.collections.new("LP_Models")
        scene.collection.children.link(col)
    for o in list(col.objects):
        data = o.data
        bpy.data.objects.remove(o, do_unlink=True)
        if data and data.users == 0:
            bpy.data.meshes.remove(data)
    objs = [
        guardrail(col, "Guardrail"),
        sign(col, "Sign_NoEntry", "LP_Sign_NoEntry", "circle"),
        sign(col, "Sign_Danger", "LP_Sign_Danger", "tri", size=0.9),
        sign(col, "Sign_City", "LP_Sign_City", "rect", size=(2.4, 1.2), posts=2, h=1.6),
        manhole(col, "Manhole"),
        puddle(col, "Puddle_0", 1),
        puddle(col, "Puddle_1", 2, rx=3.2, ry=1.4),
        car_wreck(col, "Zhiguli_Red", "LP_PaintRed", 4.1, False, 1),
        car_wreck(col, "Zhiguli_White", "LP_PaintWhite", 4.1, False, 2),
        car_wreck(col, "Moskvich_Blue", "PS_PaintBlue", 4.2, True, 3),
        car_wreck(col, "Moskvich_Yellow", "PS_PaintYellow", 4.2, True, 4),
        car_wreck(col, "Zhiguli_Burnt", "LP_Burnt", 4.1, False, 5, burnt=True),
        tire_stack(col, "TireStack", 4, 1),
        tire_stack(col, "TireStack_Low", 2, 2),
        slab(col, "RoadSlab"),
        billboard(col, "Billboard_0", "LP_Slogan_0"),
        billboard(col, "Billboard_1", "LP_Slogan_1"),
        billboard(col, "Billboard_2", "LP_Slogan_2"),
        trash_pile(col, "TrashPile_0", 1),
        trash_pile(col, "TrashPile_1", 2),
        shell_garage(col, "ShellGarage_Green", "PS_MetalGreen"),
        shell_garage(col, "ShellGarage_Blue", "PS_PaintBlue"),
        rocket_slide(col, "RocketSlide"),
        swings(col, "Swings"),
        clothesline(col, "Clothesline", 1),
        yard_bench(col, "YardBench"),
        trash_bin(col, "TrashBin"),
        sandbox(col, "Sandbox"),
        tv_antenna(col, "TvAntenna"),
        factory_chimney(col, "FactoryChimney"),
        tower_crane(col, "TowerCrane"),
        fuel_tank(col, "FuelTank"),
        gas_cylinder(col, "GasCylinder"),
        gas_cylinder_rack(col, "GasCylinderRack"),
        fuel_truck(col, "FuelTruck"),
        container(col, "Container_Green", "PS_MetalGreen"),
        container(col, "Container_Blue", "PS_PaintBlue"),
        container(col, "Container_Rust", "PS_Rust"),
        wagon(col, "Wagon"),
        rail_track(col, "RailTrack"),
        pipe_rack(col, "PipeRack"),
        floodlight_mast(col, "FloodlightMast"),
        woodpile(col, "Woodpile", 1),
        well(col, "Well"),
        greenhouse(col, "Greenhouse", 1),
        dog_house(col, "DogHouse"),
        stray_dog_body(col, "StrayDog_Body"),
        stray_dog_leg(col, "StrayDog_Leg"),
        wooden_pole(col, "WoodenPole"),
        sandbag_wall(col, "SandbagWall"),
        burnt_btr(col, "BurntBTR"),
        burnt_truck(col, "BurntTruck"),
        hedgehog(col, "Hedgehog"),
        barbed_wire(col, "BarbedWire"),
        dugout(col, "Dugout"),
        water_tower(col, "WaterTower"),
        tv_mast(col, "TvMast"),
        power_chimney(col, "PowerChimney"),
        culture_house(col, "CultureHouse"),
        pier(col, "Pier"),
        boat(col, "Boat_Blue", "PS_PaintBlue"),
        boat(col, "Boat_Green", "LP_PaintGreen"),
        bridge_ruin(col, "BridgeRuin"),
        reeds(col, "Reeds_0", 1),
        reeds(col, "Reeds_1", 2, n=40, radius=1.2),
        boulder(col, "Boulder_0", 1, (1.6, 1.2, 1.0)),
        boulder(col, "Boulder_1", 2, (0.9, 0.8, 0.6)),
        boulder(col, "Boulder_2", 3, (2.6, 1.8, 1.4)),
        road_closed(col, "RoadClosed"),
        ramp(col, "Ramp"),
        wood_ramp(col, "WoodRamp"),
        crate(col, "Crate"),
        barricade(col, "Barricade", 1),
        nitro_canister(col, "NitroCanister"),
        rpg_crate(col, "RpgCrate"),
    ]
    x = 0.0
    for o in objs:
        dims = o.dimensions
        o.location = (x + dims.x / 2, 0, 0)
        x += dims.x + 3.0
    return scene, objs


if __name__ == "__main__":
    # LP_ONLY=Имя1,Имя2 — экспортировать только эти модели (и их LOD1), остальные FBX не трогать
    only = {n for n in os.environ.get("LP_ONLY", "").split(",") if n}
    ps.OUT = OUT
    sc, models = build_all()
    if only:
        models = [o for o in models if o.name in only]
    ps.export(sc, models, lod_build=build_all)
