"""Prepare the supplied forest packs for Unity. Run with Blender's Python."""

import math
import random
import tempfile
from pathlib import Path, PurePosixPath
from zipfile import ZipFile

import bmesh
import bpy
from mathutils import Vector


PROJECT = Path(__file__).resolve().parents[1]
OUTPUT = PROJECT / "Assets/JurassicPix/Models/Forest"
PACKS = {
    "pine-forest-at-sunset": "pines",
    "ps1-trees-and-bushes": "plants",
    "PSX_Forest_Level_byStarkCrafts": "stark",
    "PSX_Daysky_HDRI": "day",
    "PSX_Nightsky_HDRI": "night",
}


def unpack(workdir):
    for archive, label in PACKS.items():
        folder = workdir / label
        folder.mkdir()
        with ZipFile(PROJECT / (archive + ".zip")) as source:
            for entry in source.infolist():
                if not entry.is_dir():
                    name = PurePosixPath(entry.filename).name
                    (folder / name).write_bytes(source.read(entry))


def open_pack(folder):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    model = next(path for path in folder.iterdir() if path.suffix in (".fbx", ".glb"))
    if model.suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(model))
    else:
        bpy.ops.import_scene.gltf(filepath=str(model))


def image_of(material):
    shader = next(node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    pending = [link.from_node for link in shader.inputs["Base Color"].links]
    while pending:
        node = pending.pop(0)
        if node.type == "TEX_IMAGE" and node.image:
            return node.image
        pending.extend(link.from_node for socket in node.inputs for link in socket.links)
    raise ValueError(f"No base color texture in {material.name}")


def save_image(image, name, size):
    path = OUTPUT / "Textures" / (name + ".png")
    width, height = image.size
    factor = min(1, size / max(width, height))
    if factor < 1:
        image.scale(max(1, round(width * factor)), max(1, round(height * factor)))
    image.file_format = "PNG"
    image.filepath_raw = str(path)
    image.save()


def triangles(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def prune_cards(mesh, budget, keep_trunk=False):
    """Keep whole disconnected leaf cards and distribute them throughout the crown."""
    edit = bmesh.new()
    edit.from_mesh(mesh)
    edit.verts.ensure_lookup_table()
    seen = set()
    groups = []
    for vertex in edit.verts:
        if vertex in seen:
            continue
        stack = [vertex]
        seen.add(vertex)
        members = []
        while stack:
            current = stack.pop()
            members.append(current)
            for edge in current.link_edges:
                neighbor = edge.other_vert(current)
                if neighbor not in seen:
                    seen.add(neighbor)
                    stack.append(neighbor)
        faces = {face for item in members for face in item.link_faces}
        if faces:
            center = sum((item.co for item in members), Vector()) / len(members)
            weight = sum(face.calc_area() for face in faces)
            cost = sum(len(face.verts) - 2 for face in faces)
            groups.append((center, weight, cost, members))

    if not groups or max(group[2] for group in groups) > budget:
        edit.free()
        return False

    # Rank locally, then take from each cell in turn to preserve the silhouette.
    bounds = [(min(group[0][i] for group in groups), max(group[0][i] for group in groups)) for i in range(3)]
    rng = random.Random(417)
    cells = {}
    for center, area, cost, members in groups:
        key = tuple(int(3.99 * (center[i] - lo) / max(hi - lo, 0.001)) for i, (lo, hi) in enumerate(bounds))
        priority = -math.log(max(rng.random(), .0001)) / max(area, .0001) ** .55
        cells.setdefault(key, []).append((priority, cost, members))
    bins = [sorted(items, key=lambda item: item[0]) for items in cells.values()]
    keep = set()
    if keep_trunk:
        trunk = max(groups, key=lambda group: max(v.co.z for v in group[3]) - min(v.co.z for v in group[3]))
        keep.update(trunk[3])
        budget -= trunk[2]
    for rank in range(max(map(len, bins))):
        for items in bins:
            if rank < len(items):
                _, cost, members = items[rank]
                if cost <= budget and members[0] not in keep:
                    keep.update(members)
                    budget -= cost
    if not keep:
        edit.free()
        return False
    bmesh.ops.delete(edit, geom=[vertex for vertex in edit.verts if vertex not in keep], context="VERTS")
    edit.to_mesh(mesh)
    edit.free()
    return True


def subset(source, slot, key, pivot):
    data = source.data.copy()
    if len(source.data.materials) > 1:
        edit = bmesh.new()
        edit.from_mesh(data)
        bmesh.ops.delete(edit, geom=[face for face in edit.faces if face.material_index != slot], context="FACES")
        edit.to_mesh(data)
        edit.free()
    data.transform(source.matrix_world)
    for vertex in data.vertices:
        vertex.co -= pivot
    data.materials.clear()
    part = bpy.data.objects.new(key, data)
    bpy.context.collection.objects.link(part)
    return part


def export(name, parts, pivot, budgets=None):
    root = bpy.data.objects.new("ForestAsset", None)
    bpy.context.collection.objects.link(root)
    prepared = []
    count = 0
    original = sum(triangles(obj.data) for obj in {part[0] for part in parts})
    for source, slot, key in parts:
        obj = subset(source, slot, key, pivot)
        obj.parent = root
        prepared.append(obj)
        limit = budgets.get(key, 0) if budgets else 0
        initial = triangles(obj.data)
        if 0 < limit < initial:
            if not key.startswith("Leaf") or not prune_cards(obj.data, limit):
                bpy.context.view_layer.objects.active = obj
                modifier = obj.modifiers.new("Low-poly geometry", "DECIMATE")
                modifier.ratio = limit / initial
                modifier.use_collapse_triangulate = True
                bpy.ops.object.modifier_apply(modifier=modifier.name)
            if key == "PineBark" and triangles(obj.data) > limit:
                prune_cards(obj.data, limit, keep_trunk=True)
        count += triangles(obj.data)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root] + prepared:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT / (name + ".fbx")), use_selection=True,
                             object_types={"EMPTY", "MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_anim=False)
    print(f"FOREST {name} {original} -> {count} triangles", flush=True)
    for obj in reversed([root] + prepared):
        bpy.data.objects.remove(obj, do_unlink=True)


def pivot_at_base(source):
    bounds = [source.matrix_world @ Vector(point) for point in source.bound_box]
    return Vector(((min(p.x for p in bounds) + max(p.x for p in bounds)) * .5,
                   (min(p.y for p in bounds) + max(p.y for p in bounds)) * .5,
                   min(p.z for p in bounds)))


def pines(folder):
    open_pack(folder)
    objects = bpy.data.objects
    trunk = objects["FlorasynthTree_branches"]
    leaves = objects["FlorasynthTree_foliage"]
    save_image(bpy.data.images.load(str(folder / "ponderosa-bark-old_1.jpeg")), "PineBark", 256)
    save_image(bpy.data.images.load(str(folder / "ponderosa-foliage_2.png")), "LeafPine", 512)
    terrain = objects["terrain"]
    save_image(image_of(terrain.data.materials[0]), "Terrain", 512)
    moss = bpy.data.images.load(str(folder / "mossy_rock_diff_1k.jpg"))
    save_image(moss, "MossRock", 256)
    export("Terrain", [(terrain, 0, "Terrain")], Vector())
    for suffix, label in (("", "Ponderosa"), (".001", "PonderosaTall"), (".002", "PonderosaWide")):
        base = "FlorasynthTree" + suffix
        parts = [(objects[base + "_branches"], 0, "PineBark"), (objects[base + "_foliage"], 0, "LeafPine")]
        pivot = objects[base].location.copy()
        export(label, parts, pivot, {"PineBark": 850, "LeafPine": 850})
        export(label + "Far", parts, pivot, {"PineBark": 200, "LeafPine": 225})
    source = objects["rock.003"]
    export("MossBoulder", [(source, 0, "MossRock")], pivot_at_base(source), {"MossRock": 140})


def stark(folder):
    open_pack(folder)
    source = {obj.name.strip(): obj for obj in bpy.data.objects}
    names = {
        "PSX_Tree1": "StarkPine", "PSX_Tree2": "StarkFir", "PSX_Tree3": "StarkGiant",
        "PSX_Tree4": "StarkCedar", "PSX_Rock": "StarkRock", "PSX_Treetrunk": "ForestLog",
        "PSX_Grass": "ForestGrass", "PSX_Reed": "ForestReeds", "PSX_Lavender": "ForestStems",
        "PSX_PineConre": "PineCone", "PSX_Dandelion": "ForestFlower",
    }
    materials = {}
    for original, target in names.items():
        model = source[original]
        parts = []
        for index, material in enumerate(model.data.materials):
            image = image_of(material)
            if image.name in materials:
                key = materials[image.name]
            else:
                cutout = material.name.startswith("TreeC") or material.name in ("Dandelion", "Grass", "Grass2", "PIne")
                key = "LeafCone" if material.name == "PIne" else ("Leaf" if cutout else "") + material.name
                save_image(image, key, 128)
                materials[image.name] = key
            parts.append((model, index, key))
        export(target, parts, pivot_at_base(model))
    for original, name in (("PSX_Seamless_ForestEarthGround_128px.png", "TrailEarth"),
                           ("PSX_Seamless_ForestWildGround_128px.png", "ForestFloor"),
                           ("PSX_Seamless_ForestGravel_Ground_128px.png", "TrailGravel")):
        (OUTPUT / "Textures" / (name + ".png")).write_bytes((folder / original).read_bytes())


def plants(folder):
    open_pack(folder)
    names = {
        "текстура ствола дерева 1.png": "PS1Bark",
        "текстура веток.png": "LeafBranches",
        "текстура листьев дерева 1.png": "LeafPS1A",
        "текстура листьев 2.png": "LeafPS1B",
        "кусты 1.png": "LeafShrubA",
        "кусты 2.png": "LeafShrubB",
        "кусты 3.png": "LeafShrubC",
    }
    for original, key in names.items():
        image = bpy.data.images.load(str(folder / original))
        save_image(image, key, 256)
    for index in (1, 2, 3):
        trunk = bpy.data.objects[f"Дерево {index} ствол"]
        crown = bpy.data.objects[f"Дерево {index} листья"]
        parts = [(obj, slot, names[Path(image_of(material).filepath).name])
                 for obj in (trunk, crown) for slot, material in enumerate(obj.data.materials)
                 if any(face.material_index == slot for face in obj.data.polygons)]
        export(f"PS1Tree{index}", parts, pivot_at_base(trunk))
        shrub = bpy.data.objects[f"Куст {index}"]
        key = names[f"кусты {index}.png"]
        export(f"PS1Shrub{index}", [(shrub, 0, key)], pivot_at_base(shrub))


if __name__ == "__main__":
    for folder in (OUTPUT / "Textures", OUTPUT / "Skies"):
        folder.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="jurassic-forest-") as temp:
        staging = Path(temp)
        unpack(staging)
        pines(staging / "pines")
        stark(staging / "stark")
        plants(staging / "plants")
        for label, source, asset in (("day", "DAYSKY.hdr", "Day.hdr"), ("night", "PSX_NIGHTSKY.hdr", "Night.hdr")):
            (OUTPUT / "Skies" / asset).write_bytes((staging / label / source).read_bytes())
