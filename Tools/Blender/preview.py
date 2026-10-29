# Превью моделей для проверки: рендер выбранных объектов в отдельной сцене «Preview» (Workbench, студийный
# свет, цвета материалов, затенение полостей) с нескольких ракурсов в один PNG-лист.
# Запуск в Blender 5.1 через MCP или Scripting:
#   import importlib.util; spec = importlib.util.spec_from_file_location("pv", r"<repo>\Tools\Blender\preview.py")
#   pv = importlib.util.module_from_spec(spec); spec.loader.exec_module(pv)
#   pv.render(["RpgCrate"], r"C:\temp\crate.png")
# Сцены моделей не меняются: объекты на время рендера связываются со сценой превью и потом отвязываются.
import math
import os

import bpy
from mathutils import Vector

VIEWS = [(35, 25), (145, 20), (-60, 55), (90, 5)]  # (азимут, высота) в градусах


def _scene():
    scene = bpy.data.scenes.get("Preview") or bpy.data.scenes.new("Preview")
    scene.render.engine = 'BLENDER_WORKBENCH'
    shading = scene.display.shading
    shading.light = 'STUDIO'
    shading.color_type = 'MATERIAL'
    shading.show_cavity = True
    shading.cavity_type = 'BOTH'
    shading.show_shadows = True
    shading.shadow_intensity = 0.5
    shading.show_specular_highlight = True
    scene.display.render_aa = '8'
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("PreviewWorld")
    scene.render.image_settings.file_format = 'PNG'
    return scene


def _bounds(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        # matrix_basis, а не matrix_world: мировая матрица обновляется только при пересчёте графа сцены модели
        m = o.matrix_basis if o.parent is None else o.matrix_world
        for c in o.bound_box:
            p = m @ Vector(c)
            lo = Vector((min(lo.x, p.x), min(lo.y, p.y), min(lo.z, p.z)))
            hi = Vector((max(hi.x, p.x), max(hi.y, p.y), max(hi.z, p.z)))
    return lo, hi


def render(names, path, size=640, views=VIEWS):
    scene = _scene()
    objs = [bpy.data.objects[n] for n in names]
    for o in objs:
        if o.name not in scene.collection.objects:
            scene.collection.objects.link(o)
    cam_data = bpy.data.cameras.get("PreviewCam") or bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.get("PreviewCam") or bpy.data.objects.new("PreviewCam", cam_data)
    if cam.name not in scene.collection.objects:
        scene.collection.objects.link(cam)
    scene.camera = cam
    cam_data.lens = 50
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    lo, hi = _bounds(objs)
    center = (lo + hi) / 2
    radius = max((hi - lo).length / 2, 0.1)
    distance = radius / math.tan(math.radians(36) / 2) * 1.08
    tiles = []
    base, ext = os.path.splitext(path)
    for i, (az, el) in enumerate(views):
        a, e = math.radians(az), math.radians(el)
        offset = Vector((math.cos(e) * math.sin(a), -math.cos(e) * math.cos(a), math.sin(e))) * distance
        cam.location = center + offset
        cam.rotation_euler = (center - cam.location).to_track_quat('-Z', 'Y').to_euler()
        tile = "%s_%d%s" % (base, i, ext)
        scene.render.filepath = tile
        with bpy.context.temp_override(scene=scene):
            bpy.ops.render.render(write_still=True, scene=scene.name)
        tiles.append(tile)
    for o in objs:
        if o.name in scene.collection.objects:
            scene.collection.objects.unlink(o)
    _sheet(tiles, path, size)
    return path


def _sheet(tiles, path, size):
    # Лист 2×2 из отдельных ракурсов
    cols = 2
    rows = (len(tiles) + 1) // 2
    sheet = bpy.data.images.new("PreviewSheet", cols * size, rows * size)
    pixels = [0.0] * (cols * size * rows * size * 4)
    for i, tile in enumerate(tiles):
        img = bpy.data.images.load(tile)
        px = list(img.pixels)
        cx, cy = i % cols, rows - 1 - i // cols
        for y in range(size):
            src = y * size * 4
            dst = ((cy * size + y) * cols * size + cx * size) * 4
            pixels[dst:dst + size * 4] = px[src:src + size * 4]
        bpy.data.images.remove(img)
        os.remove(tile)
    sheet.pixels = pixels
    sheet.filepath_raw = path
    sheet.file_format = 'PNG'
    sheet.save()
    bpy.data.images.remove(sheet)
