# Урал-4320: экспорт в FBX для Unity.
# Рабочий объект не трогаем: экспортируется копия с одной развёрткой UV_Bake, одним материалом,
# и шесть колёс отдельными объектами с центром на оси (их крутит скрипт машины).
import bpy
import bmesh
from mathutils import Matrix, Vector

SRC = "ural4320"
OUT = globals().get("OUT", r"D:/GitRepos/RacingProject/Assets/Models/Ural/Ural4320.fbx")
WHEEL_Z = 0.595
WHEEL_R = 0.62
WHEEL_X = 1.19
WHEEL_Y = [-2.77, 0.775, 2.16]

src = bpy.data.objects[SRC]
for o in list(bpy.data.objects):
    if o.name.startswith("Export_") or o.name == "Ural4320" or o.name.startswith("Wheel_"):
        bpy.data.objects.remove(o, do_unlink=True)
# Меши прошлого экспорта: иначе новые получат имена с суффиксом .001
for m in list(bpy.data.meshes):
    if m.users == 0 and (m.name.startswith("Wheel_") or m.name.startswith("Ural4320_Body")):
        bpy.data.meshes.remove(m)

body = src.copy()
body.data = src.data.copy()
body.name = "Export_Body"
bpy.context.collection.objects.link(body)
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
bpy.context.view_layer.objects.active = body
for m in list(body.modifiers):
    bpy.ops.object.modifier_apply(modifier=m.name)

me = body.data
me.uv_layers.remove(me.uv_layers["UVMap"])
me.uv_layers["UV_Bake"].name = "UVMap"
mat = bpy.data.materials.get("Ural_Body") or bpy.data.materials.new("Ural_Body")
me.materials.clear()
me.materials.append(mat)
for p in me.polygons:
    p.material_index = 0


def wheel_of(verts):
    # Деталь относится к колесу, если целиком лежит в его цилиндре снаружи рамы
    xs = [v.co.x for v in verts]
    ys = [v.co.y for v in verts]
    zs = [v.co.z for v in verts]
    # Ось-полоса из пары граней может тянуться через всю ширину без вершин посередине — отсекаем по ширине
    if min(abs(x) for x in xs) < 0.94 or max(xs) - min(xs) > 0.7 or max(zs) > WHEEL_Z + WHEEL_R or min(zs) < -0.05:
        return None
    side = 1 if xs[0] > 0 else -1
    for i, yc in enumerate(WHEEL_Y):
        if min(ys) >= yc - WHEEL_R and max(ys) <= yc + WHEEL_R:
            return (i, side)
    return None


bm = bmesh.new()
bm.from_mesh(me)
bm.faces.ensure_lookup_table()
groups = {}
seen = set()
for f in bm.faces:
    if f.index in seen:
        continue
    stack, island = [f], []
    seen.add(f.index)
    while stack:
        c = stack.pop()
        island.append(c)
        for e in c.edges:
            for nb in e.link_faces:
                if nb.index not in seen:
                    seen.add(nb.index)
                    stack.append(nb)
    key = wheel_of({v for fa in island for v in fa.verts})
    if key:
        groups.setdefault(key, []).extend(fa.index for fa in island)
bm.free()

names = {0: "Front", 1: "Middle", 2: "Rear"}


def keep_faces(mesh, keep):
    # Оставляет в меше только перечисленные грани (индексы исходного меша кузова)
    b = bmesh.new()
    b.from_mesh(mesh)
    b.faces.ensure_lookup_table()
    bmesh.ops.delete(b, geom=[f for f in b.faces if f.index not in keep], context='FACES')
    b.to_mesh(mesh)
    b.free()


export = [body]
all_wheel_faces = set()
for (i, side), faces in sorted(groups.items()):
    # Грузовик в Blender смотрит в -Y, поэтому его левый борт — +X
    name = "Wheel_%s_%s" % (names[i], "L" if side > 0 else "R")
    w = body.copy()
    w.data = me.copy()
    w.name = "Export_" + name
    w.data.name = name
    bpy.context.collection.objects.link(w)
    keep_faces(w.data, set(faces))
    all_wheel_faces.update(faces)
    # Центр колеса — на оси
    center = Vector((WHEEL_X * side, WHEEL_Y[i], WHEEL_Z))
    w.data.transform(Matrix.Translation(-center))
    w.location = center
    w.parent = body
    export.append(w)

keep_faces(me, set(range(len(me.polygons))) - all_wheel_faces)
body.data.name = "Ural4320_Body"
# Имена в FBX — без служебной приставки
body.name = "Ural4320"
for w in export[1:]:
    w.name = w.data.name

bpy.ops.object.select_all(action='DESELECT')
for o in export:
    o.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.export_scene.fbx(filepath=OUT, use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', use_tspace=True,
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
counts = [len(o.data.polygons) for o in export[1:]]
for o in export:
    data = o.data
    bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.meshes.remove(data)
print("wheel faces", counts)
print("exported", OUT, "wheels", len(counts))
