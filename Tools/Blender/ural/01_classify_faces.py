# Урал-4320: разбивает единый меш на зоны материалов по цвету старой атласной текстуры под каждым полигоном.
# Запуск в открытом Blender (Scripting или MCP): exec(open(путь).read())
import bpy
import numpy as np
import colorsys

OBJ = "ural4320"
obj = bpy.data.objects[OBJ]
mesh = obj.data
img = bpy.data.images[0]
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[:, :, :3]

uv = mesh.uv_layers.active.data


def sample(u, v):
    x = int((u % 1.0) * (w - 1))
    y = int((v % 1.0) * (h - 1))
    return px[y, x]


def face_color(poly):
    # Среднее по центру и по точкам между центром и вершинами: одна точка шумит на нарисованных деталях
    uvs = [uv[i].uv for i in poly.loop_indices]
    cu = sum(p.x for p in uvs) / len(uvs)
    cv = sum(p.y for p in uvs) / len(uvs)
    pts = [(cu, cv)] + [((cu + p.x) * 0.5, (cv + p.y) * 0.5) for p in uvs]
    return np.mean([sample(a, b) for a, b in pts], axis=0)


ZONES = ["Paint", "Canvas", "DarkMetal", "Rubber", "Glass", "Lights"]


def classify(rgb, poly):
    r, g, b = [float(c) for c in rgb]
    hh, ss, vv = colorsys.rgb_to_hsv(r, g, b)
    # Пороги сняты с атласа: тент светлый (яркость ~0.53), краска ~0.33, шины серые без насыщенности
    if r > 0.45 and r > g * 1.4 and r > b * 1.4:
        return "Lights"
    if vv < 0.16:
        return "DarkMetal"
    if vv > 0.45 and ss < 0.3:
        return "Canvas"
    if ss < 0.08:
        return "Rubber"
    return "Paint"


# Голосование по связным кускам сетки: нарисованные детали не дробят деталь на разные материалы
import bmesh
bm = bmesh.new()
bm.from_mesh(mesh)
bm.faces.ensure_lookup_table()
votes = [classify(face_color(p), p) for p in mesh.polygons]
result_faces = list(votes)
seen = set()
for f in bm.faces:
    if f.index in seen:
        continue
    stack, island = [f], []
    seen.add(f.index)
    while stack:
        cur = stack.pop()
        island.append(cur.index)
        for e in cur.edges:
            for nb in e.link_faces:
                if nb.index not in seen:
                    seen.add(nb.index)
                    stack.append(nb)
    tally = {}
    for i in island:
        tally[votes[i]] = tally.get(votes[i], 0) + mesh.polygons[i].area
    best = max(tally, key=tally.get)
    # Крупные куски (кабина, тент) содержат разные зоны — для них оставляем пополигонное решение
    if len(island) <= 60:
        for i in island:
            result_faces[i] = best
bm.free()

counts = {}
for z in result_faces:
    counts[z] = counts.get(z, 0) + 1

# Колёса: всё, что ниже оси и у колёсных арок, с тёмным цветом — резина. Колёса ищем по связным частям позже
for name in ZONES:
    mat = bpy.data.materials.get("Ural_" + name) or bpy.data.materials.new("Ural_" + name)
    mat.use_nodes = True
    if mat.name not in [s.material.name for s in obj.material_slots if s.material]:
        obj.data.materials.append(mat)

index = {s.material.name: i for i, s in enumerate(obj.material_slots) if s.material}
colors = {"Paint": (0.25, 0.3, 0.15, 1), "Canvas": (0.6, 0.55, 0.42, 1), "DarkMetal": (0.05, 0.05, 0.05, 1),
          "Rubber": (0.02, 0.02, 0.02, 1), "Glass": (0.2, 0.3, 0.35, 1), "Lights": (0.9, 0.3, 0.1, 1)}
for name, c in colors.items():
    m = bpy.data.materials["Ural_" + name]
    m.diffuse_color = c
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = c

for p, z in zip(mesh.polygons, result_faces):
    p.material_index = index["Ural_" + z]

print("ZONES", counts)
