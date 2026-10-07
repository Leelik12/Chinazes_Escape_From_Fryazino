# Урал-4320: запекание текстур на развёртку UV_Bake.
# Перед запуском задать RES и OUT_DIR (по умолчанию 4096 и папка модели в проекте).
import bpy
import os

RES = globals().get("RES", 4096)
OUT_DIR = globals().get("OUT_DIR", r"D:/GitRepos/RacingProject/Assets/Models/Ural/Textures")
PASSES = globals().get("PASSES", ["Albedo", "Roughness", "Normal", "Metallic"])
os.makedirs(OUT_DIR, exist_ok=True)

obj = bpy.data.objects["ural4320"]
mesh = obj.data
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = globals().get("SAMPLES", 32)
scene.render.bake.margin = 8
scene.render.bake.use_selected_to_active = False
mesh.uv_layers.active = mesh.uv_layers["UV_Bake"]

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj

mats = [s.material for s in obj.material_slots if s.material and s.material.name.startswith("Ural_")]


def target(name, non_color):
    # Изображение каждый раз новое: старое ссылается на файл прошлого запекания, которого может уже не быть
    old = bpy.data.images.get("Ural_" + name)
    if old:
        bpy.data.images.remove(old)
    img = bpy.data.images.new("Ural_" + name, RES, RES, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color' if non_color else 'sRGB'
    for m in mats:
        nt = m.node_tree
        n = nt.nodes.get("BakeTarget") or nt.nodes.new("ShaderNodeTexImage")
        n.name = "BakeTarget"
        n.location = (1500, 400)
        n.image = img
        uvn = nt.nodes.get("BakeUV") or nt.nodes.new("ShaderNodeUVMap")
        uvn.name = "BakeUV"
        uvn.uv_map = "UV_Bake"
        uvn.location = (1300, 400)
        if not n.inputs[0].is_linked:
            nt.links.new(uvn.outputs[0], n.inputs[0])
        nt.nodes.active = n
    return img


def save(img, name):
    img.filepath_raw = os.path.join(OUT_DIR, "Ural_" + name + ".png")
    img.file_format = 'PNG'
    img.save()


# Металличность запекается как свечение: временно подменяем выход материала на Emission со значением Metallic
def swap_to_emission(m):
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    out = nt.nodes["Material Output"]
    em = nt.nodes.new("ShaderNodeEmission")
    metal = nt.nodes.get("MetalOut")
    if metal:
        nt.links.new(metal.outputs[0], em.inputs["Color"])
    else:
        v = bsdf.inputs["Metallic"].default_value
        em.inputs["Color"].default_value = (v, v, v, 1)
    old = out.inputs[0].links[0].from_socket
    nt.links.new(em.outputs[0], out.inputs[0])
    return em, old


for p in PASSES:
    if p == "Albedo":
        img = target(p, False)
        bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=True)
    elif p == "Roughness":
        img = target(p, True)
        bpy.ops.object.bake(type='ROUGHNESS', use_clear=True)
    elif p == "Normal":
        img = target(p, True)
        bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', use_clear=True)
    elif p == "Metallic":
        img = target(p, True)
        swaps = [(m,) + swap_to_emission(m) for m in mats]
        bpy.ops.object.bake(type='EMIT', use_clear=True)
        for m, em, old in swaps:
            nt = m.node_tree
            nt.links.new(old, nt.nodes["Material Output"].inputs[0])
            nt.nodes.remove(em)
    save(img, p)
    print("baked", p)
