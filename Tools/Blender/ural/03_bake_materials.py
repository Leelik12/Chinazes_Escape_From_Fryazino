# Урал-4320: процедурные материалы зон для запекания в текстуры.
# У каждой зоны свой чистый базовый цвет. Со старого атласа берётся только мелкая детализация (Ural_Detail —
# атлас минус его размытая копия: решётки, протектор, швы панелей) — она идёт в цвет наложением и в рельеф.
# Тент и фары берут цвет со старого атласа целиком: там он нарисован хорошо.
# Поверх: потёртости на рёбрах, ржавчина мелкими точками у рёбер и внизу, грязь снизу, пыль на верхних гранях,
# затенение в щелях (AO). Скругление рёбер (Bevel) уходит в карту нормалей. Работает только в Cycles.
import bpy

obj = bpy.data.objects["ural4320"]
SRC = bpy.data.images.get("Ural_Source")
DETAIL_PATH = r"C:/Users/fatya/AppData/Local/Temp/claude/D--GitRepos-RacingProject/31ed6994-585d-4795-ba6e-f388c07afa7a/scratchpad/ural/textures/Ural_Detail.png"
DETAIL = bpy.data.images.get("Ural_Detail") or bpy.data.images.load(DETAIL_PATH, check_existing=True)
DETAIL.name = "Ural_Detail"
DETAIL.colorspace_settings.name = 'Non-Color'
bpy.context.scene.render.engine = 'CYCLES'


def srgb(r, g, b):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), 1.0)


# Параметры зон: base — цвет (None — со старого атласа), detail — сила наложения детализации,
# relief — сила рельефа из детализации, rough — шероховатость, wear/rust/dust/dirt — сила эффектов
ZONES = {
    "Paint":     dict(base=srgb(56, 62, 38), detail=0.55, relief=0.35, rough=0.6, wear=1.0, rust=1.3, dust=0.35, dirt=0.9),
    "DarkMetal": dict(base=srgb(38, 37, 35), detail=0.5, relief=0.35, rough=0.6, wear=0.4, rust=0.6, dust=0.3, dirt=0.8),
    "Rubber":    dict(base=srgb(30, 30, 30), detail=0.7, relief=0.6, rough=0.9, wear=0.0, rust=0.0, dust=0.1, dirt=0.35),
    "Canvas":    dict(base=None, tint=srgb(150, 146, 118), detail=0.0, relief=0.25, rough=0.92, wear=0.0, rust=0.0, dust=0.15, dirt=0.4),
    "Glass":     dict(base=srgb(70, 74, 72), detail=0.0, relief=0.0, rough=0.08, wear=0.0, rust=0.0, dust=0.15, dirt=0.0),
    "Lights":    dict(base=None, detail=0.0, relief=0.0, rough=0.25, wear=0.0, rust=0.0, dust=0.0, dirt=0.0),
}
RUST = srgb(92, 46, 24)
BARE = srgb(62, 58, 52)
MUD = srgb(72, 60, 44)
DUST = srgb(122, 108, 88)
BLEACH = srgb(110, 112, 84)


def build(mat, p):
    nt = mat.node_tree
    nt.nodes.clear()
    N, L = nt.nodes, nt.links

    def add(kind, **props):
        n = N.new(kind)
        for k, v in props.items():
            setattr(n, k, v)
        return n

    def link_or_set(value, sock):
        if hasattr(value, "is_output"):
            L.new(value, sock)
        else:
            sock.default_value = value

    def maprange(src, a, b, c=0.0, d=1.0):
        n = add("ShaderNodeMapRange")
        n.inputs[1].default_value, n.inputs[2].default_value = a, b
        n.inputs[3].default_value, n.inputs[4].default_value = c, d
        L.new(src, n.inputs[0])
        return n.outputs[0]

    def math(op, a, b, clamp=True):
        n = add("ShaderNodeMath", operation=op, use_clamp=clamp)
        link_or_set(a, n.inputs[0])
        link_or_set(b, n.inputs[1])
        return n.outputs[0]

    # У узла Mix одноимённые float/vector/color входы, поэтому сокеты берутся по индексам
    def mixc(a, b, fac, blend='MIX'):
        n = add("ShaderNodeMix", data_type='RGBA', blend_type=blend, clamp_result=True)
        link_or_set(fac, n.inputs[0])
        link_or_set(a, n.inputs[6])
        link_or_set(b, n.inputs[7])
        return n.outputs[2]

    def mixf(a, b, fac):
        n = add("ShaderNodeMix", data_type='FLOAT', clamp_factor=True)
        link_or_set(fac, n.inputs[0])
        link_or_set(a, n.inputs[2])
        link_or_set(b, n.inputs[3])
        return n.outputs[0]

    out = add("ShaderNodeOutputMaterial")
    bsdf = add("ShaderNodeBsdfPrincipled")
    L.new(bsdf.outputs[0], out.inputs[0])
    coord = add("ShaderNodeTexCoord")
    geo = add("ShaderNodeNewGeometry")
    uv_old = add("ShaderNodeUVMap", uv_map="UVMap")
    src = add("ShaderNodeTexImage", image=SRC, interpolation='Cubic')
    det = add("ShaderNodeTexImage", image=DETAIL, interpolation='Cubic')
    L.new(uv_old.outputs[0], src.inputs[0])
    L.new(uv_old.outputs[0], det.inputs[0])

    def noise(scale, detail=6.0, rough=0.55):
        n = add("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        n.inputs["Roughness"].default_value = rough
        L.new(coord.outputs["Object"], n.inputs["Vector"])
        return n.outputs["Fac"]

    # Маска рёбер: нормаль со скруглением расходится с геометрической только у рёбер
    bevel = add("ShaderNodeBevel", samples=8)
    bevel.inputs["Radius"].default_value = 0.015
    dot = add("ShaderNodeVectorMath", operation='DOT_PRODUCT')
    L.new(bevel.outputs[0], dot.inputs[0])
    L.new(geo.outputs["Normal"], dot.inputs[1])
    edge = maprange(dot.outputs["Value"], 0.995, 0.94)

    ao = add("ShaderNodeAmbientOcclusion", samples=16)
    ao.inputs["Distance"].default_value = 0.3
    cavity = maprange(ao.outputs["AO"], 0.9, 0.3)  # 1 — глубоко в щели

    sep = add("ShaderNodeSeparateXYZ")
    L.new(coord.outputs["Object"], sep.inputs[0])
    nsep = add("ShaderNodeSeparateXYZ")
    L.new(geo.outputs["Normal"], nsep.inputs[0])

    big = noise(1.2)
    mid = noise(6.0)
    fine = noise(35.0, 8.0, 0.65)

    # --- цвет ---
    if p["base"] is None:
        color = src.outputs["Color"]
        if p.get("tint"):
            # Тент на атласе почти белый и ночью светится: приглушаем и уводим в хаки
            color = mixc(color, p["tint"], 1.0, 'MULTIPLY')
    else:
        # Неравномерность: крупные пятна выцветания ±8 %
        color = mixc(p["base"], maprange(big, 0.3, 0.7, 0.82, 1.12), 1.0, 'MULTIPLY')
        # Потёки: шум, растянутый по вертикали, темнит краску полосами
        stretch = add("ShaderNodeMapping")
        stretch.inputs["Scale"].default_value = (8.0, 8.0, 0.7)
        L.new(coord.outputs["Object"], stretch.inputs["Vector"])
        streak_n = add("ShaderNodeTexNoise")
        streak_n.inputs["Scale"].default_value = 2.5
        streak_n.inputs["Detail"].default_value = 4
        L.new(stretch.outputs[0], streak_n.inputs["Vector"])
        color = mixc(color, srgb(30, 28, 22), maprange(streak_n.outputs["Fac"], 0.52, 0.72, 0.0, 0.35 * min(p["wear"], 1.0)))
        if p["wear"] > 0.5:
            # Верх выгорает на солнце
            color = mixc(color, BLEACH, maprange(nsep.outputs["Z"], 0.5, 1.0, 0.0, 0.12))
    if p["detail"] > 0:
        color = mixc(color, det.outputs["Color"], p["detail"], 'OVERLAY')

    # Потёртости до голого металла: только у рёбер и пятнами
    wear_m = math('MULTIPLY', math('MULTIPLY', edge, maprange(fine, 0.5, 0.62)), p["wear"])
    if p["wear"] > 0:
        color = mixc(color, BARE, wear_m)

    # Ржавчина: мелкие пятна у рёбер и внизу
    rust_zone = math('MAXIMUM', edge, maprange(sep.outputs["Z"], 1.1, 0.4))
    rust_m = math('MULTIPLY', math('MULTIPLY', rust_zone, maprange(mid, 0.56, 0.68)), maprange(fine, 0.45, 0.6))
    rust_m = math('MULTIPLY', rust_m, p["rust"] * 0.9)
    if p["rust"] > 0:
        color = mixc(color, RUST, rust_m)

    # Грязь снизу с рваным краем
    hz = math('ADD', sep.outputs["Z"], math('MULTIPLY', big, 0.5), clamp=False)
    dirt_m = math('MULTIPLY', maprange(hz, 1.25, 0.55), p["dirt"])
    color = mixc(color, MUD, dirt_m)

    # Пыль на верхних гранях и в щелях
    dust_src = math('MAXIMUM', maprange(nsep.outputs["Z"], 0.35, 0.9), math('MULTIPLY', cavity, 0.6))
    dust_m = math('MULTIPLY', math('MULTIPLY', dust_src, maprange(mid, 0.35, 0.7)), p["dust"])
    color = mixc(color, DUST, dust_m)

    # Затенение в щелях
    color = mixc(color, mixf(1.0, ao.outputs["AO"], 0.65), 1.0, 'MULTIPLY')
    L.new(color, bsdf.inputs["Base Color"])

    # --- шероховатость и металличность ---
    rough = maprange(fine, 0.3, 0.7, p["rough"] - 0.08, p["rough"] + 0.08)
    rough = mixf(rough, 0.4, wear_m)
    rough = mixf(rough, 0.85, rust_m)
    rough = mixf(rough, 0.95, dirt_m)
    rough = mixf(rough, 0.9, dust_m)
    L.new(rough, bsdf.inputs["Roughness"])
    metal = math('MULTIPLY', wear_m, 0.8)
    metal.node.name = "MetalOut"
    L.new(metal, bsdf.inputs["Metallic"])

    # --- рельеф: детализация атласа + мелкий шум (ржавчина бугристее) поверх скруглённых рёбер ---
    grain = math('ADD', math('MULTIPLY', fine, 0.05, False),
                 math('MULTIPLY', math('MULTIPLY', rust_m, fine, False), 0.4, False), clamp=False)
    h = math('ADD', math('MULTIPLY', det.outputs["Color"], p["relief"], False), grain, clamp=False)
    bump = add("ShaderNodeBump")
    bump.inputs["Strength"].default_value = 1.0
    bump.inputs["Distance"].default_value = 0.004
    L.new(h, bump.inputs["Height"])
    L.new(bevel.outputs[0], bump.inputs["Normal"])
    L.new(bump.outputs[0], bsdf.inputs["Normal"])

    # Узлы раскладываем сеткой, чтобы материал можно было открыть руками
    for i, n in enumerate(nt.nodes):
        n.location = ((i % 12) * 220 - 2400, -(i // 12) * 260)


for slot in obj.material_slots:
    if slot.material and slot.material.name.startswith("Ural_"):
        build(slot.material, ZONES[slot.material.name[5:]])
print("materials built")
