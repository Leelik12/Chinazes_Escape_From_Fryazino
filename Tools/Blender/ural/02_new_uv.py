# Урал-4320: чистка зон, применение трансформа и новая развёртка UV_Bake без перекрытий.
# Старая развёртка UVMap остаётся: с неё запекается исходный рисунок на новую.
import bpy
import math

obj = bpy.data.objects["ural4320"]
mesh = obj.data
slots = {s.material.name: i for i, s in enumerate(obj.material_slots) if s.material}

# Масштаб 0.01 и поворот на 90° — наследие экспорта из игры; применяем, чтобы меш был в метрах и Z вверх
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# Резина бывает только на колёсах: серые детали выше колёс — тёмный металл
for p in mesh.polygons:
    if p.material_index == slots["Ural_Rubber"] and p.center.z > 1.3:
        p.material_index = slots["Ural_DarkMetal"]

# Старый общий материал больше не нужен ни одной грани
if "Ural" in slots:
    used = any(p.material_index == slots["Ural"] for p in mesh.polygons)
    print("old material used:", used)

if "UV_Bake" not in mesh.uv_layers:
    mesh.uv_layers.new(name="UV_Bake")
mesh.uv_layers.active = mesh.uv_layers["UV_Bake"]

bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.002, area_weight=0.0,
                         correct_aspect=True, scale_to_bounds=False)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(udim_source='CLOSEST_UDIM', rotate=True, margin=0.003)
bpy.ops.object.mode_set(mode='OBJECT')

# Плотность текселей при атласе 4096
area3d = sum(p.area for p in mesh.polygons)
uvl = mesh.uv_layers["UV_Bake"].data
area_uv = 0.0
for p in mesh.polygons:
    pts = [uvl[i].uv for i in p.loop_indices]
    s = 0.0
    for k in range(1, len(pts) - 1):
        a, b, c = pts[0], pts[k], pts[k + 1]
        s += abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5
    area_uv += s
print("TEXEL px/m at 4096:", round(4096 * math.sqrt(area_uv / area3d), 1), "uv coverage:", round(area_uv, 3), "surface m2:", round(area3d, 1))
