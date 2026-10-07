# Урал-4320: уточнение зон после 01_classify_faces и 02_new_uv (меш уже в метрах, Z вверх).
# По цвету атласа серыми выходят и шины, и бампер, и ступицы. Резиной оставляем только шины —
# связные куски по 160 граней (шесть колёс и запаска за кабиной); серое вне шин — тёмный металл,
# а ступицы колёс (куски по 80 граней у колёс) — краска, как на настоящем Урале.
import bpy
import bmesh

obj = bpy.data.objects["ural4320"]
me = obj.data
slot = {s.material.name[5:]: i for i, s in enumerate(obj.material_slots) if s.material}

bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
tire, hub, rim = set(), set(), set()
seen = set()
for f in bm.faces:
    if f.index in seen:
        continue
    stack, island = [f], []
    seen.add(f.index)
    while stack:
        c = stack.pop()
        island.append(c.index)
        for e in c.edges:
            for nb in e.link_faces:
                if nb.index not in seen:
                    seen.add(nb.index)
                    stack.append(nb)
    zmax = max(v.co.z for i in island for v in bm.faces[i].verts)
    if len(island) == 160:
        # В куске шины есть и диск: всё ближе 0.33 м к оси — диск (краска), остальное — резина.
        # Ось колеса — самое короткое измерение куска
        vs = {v for i in island for v in bm.faces[i].verts}
        lo = [min(v.co[k] for v in vs) for k in range(3)]
        hi = [max(v.co[k] for v in vs) for k in range(3)]
        axis = min(range(3), key=lambda k: hi[k] - lo[k])
        center = [(lo[k] + hi[k]) * 0.5 for k in range(3)]
        for i in island:
            c = bm.faces[i].calc_center_median()
            r = sum((c[k] - center[k]) ** 2 for k in range(3) if k != axis) ** 0.5
            (tire if r > 0.33 else rim).add(i)
    elif len(island) == 80 and zmax < 1.2:
        hub.update(island)
bm.free()

changed = 0
for p in me.polygons:
    if p.index in rim and p.material_index != slot["Paint"]:
        p.material_index = slot["Paint"]
        changed += 1
    elif p.index in hub and p.material_index in (slot["Rubber"], slot["DarkMetal"]):
        p.material_index = slot["Paint"]
        changed += 1
    elif p.index not in tire and p.material_index == slot["Rubber"]:
        p.material_index = slot["DarkMetal"]
        changed += 1
    elif p.index in tire and p.material_index != slot["Rubber"]:
        p.material_index = slot["Rubber"]
        changed += 1
# Стёкла фар: круглые веера граней на передке. В атласе на них нарисован крест — заменяем зоной стекла
lens = 0
for p in me.polygons:
    # Вся передняя пластина фары, а не только веер стекла: верх пластины разбит на треугольники,
    # которые цветом атласа ушли в тёмный металл, и на фаре получался зубчатый рисунок
    vs = [me.vertices[i].co for i in p.vertices]
    if (all(v.y < -3.67 and 0.74 < abs(v.x) < 1.18 and 1.14 < v.z < 1.56 for v in vs) and p.normal.y < -0.9
            and p.material_index in (slot["DarkMetal"], slot["Glass"])):
        p.material_index = slot["Glass"]
        lens += 1
print("lens faces", lens)
print("tire faces", len(tire), "rim faces", len(rim), "hub faces", len(hub), "changed", changed)
