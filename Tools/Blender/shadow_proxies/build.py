# Упрощённые копии тяжёлых чужих моделей только для теней (кузов и колёса Tahoe, пулемёты).
# Каждая тень (солнце в двух каскадах, фары игрока и врагов) перерисовывает модель заново, а кузов Tahoe — 210 тыс.
# треугольников. Тень от упрощённой копии на глаз та же.
# Порядок: Tools/Unity/BuildShadowProxies.cs (шаг dump) пишет меши в WORK/in как «v x y z» / «f a b c»,
# этот скрипт в Blender (через MCP или blender --background --python) склеивает вершины по швам развёртки,
# прореживает (Decimate Collapse) до TARGET треугольников и пишет WORK/out, затем BuildShadowProxies.cs (шаг build)
# создаёт из них меши Assets/Models/ShadowProxies. Координаты не переводятся между осями Blender и Unity:
# прореживанию это не важно, а порядок вершин в треугольниках сохраняется
import bpy, bmesh, os, tempfile

WORK = os.path.join(tempfile.gettempdir(), "RacingProject_ShadowProxies")
# Сколько треугольников оставить у каждой модели
TARGET = {"Tahoe_Body": 14000, "Tahoe_FL": 1400, "Tahoe_FR": 1400, "Tahoe_RL": 1400, "Tahoe_RR": 1400,
          "MachineGun_NoScope": 2500, "Machine Gun": 2500}


def read(path):
    verts, faces = [], []
    with open(path) as f:
        for line in f:
            p = line.split()
            if not p:
                continue
            if p[0] == "v":
                verts.append((float(p[1]), float(p[2]), float(p[3])))
            elif p[0] == "f":
                faces.append((int(p[1]) - 1, int(p[2]) - 1, int(p[3]) - 1))
    return verts, faces


def simplify(name, verts, faces, target):
    mesh = bpy.data.meshes.new(name + "_Shadow")
    bm = bmesh.new()
    bv = [bm.verts.new(v) for v in verts]
    for f in faces:
        try:
            bm.faces.new((bv[f[0]], bv[f[1]], bv[f[2]]))
        except ValueError:
            pass  # вырожденный или повторный треугольник
    # Швы развёртки и жёсткие рёбра делят вершины: без склейки прореживание рвёт сетку по ним
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    dec = obj.modifiers.new("Decimate", "DECIMATE")
    dec.decimate_type = 'COLLAPSE'
    dec.ratio = min(1.0, target / max(1, len(mesh.polygons)))
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(obj.evaluated_get(dg), depsgraph=dg)
    bpy.data.objects.remove(obj)
    bpy.data.meshes.remove(mesh)
    bm = bmesh.new()
    bm.from_mesh(out)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    lines = ["v %r %r %r" % tuple(v.co) for v in bm.verts]
    bm.verts.index_update()
    lines += ["f %d %d %d" % tuple(v.index + 1 for v in f.verts) for f in bm.faces]
    tris = len(bm.faces)
    bm.free()
    bpy.data.meshes.remove(out)
    return lines, tris


def build_all():
    os.makedirs(os.path.join(WORK, "out"), exist_ok=True)
    report = {}
    for fn in sorted(os.listdir(os.path.join(WORK, "in"))):
        name = fn[:-4]
        if name not in TARGET:
            continue
        verts, faces = read(os.path.join(WORK, "in", fn))
        lines, tris = simplify(name, verts, faces, TARGET[name])
        with open(os.path.join(WORK, "out", fn), "w") as f:
            f.write("\n".join(lines) + "\n")
        report[name] = (len(faces), tris)
    return report


if __name__ == "__main__":
    print(build_all())
