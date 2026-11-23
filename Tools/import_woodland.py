"""Extract trees, shrubs, grass and ground textures from the three woodland scenes. Run with Blender's Python.

The scenes are used as libraries of models. Every plant is exported with its pivot on the ground so the
island generator can replant it on the terrain.
"""

import subprocess
import sys
from pathlib import Path
from zipfile import ZipFile

import bmesh
import bpy
import numpy as np
from mathutils import Vector

sys.path.append(str(Path(__file__).resolve().parent))
import import_forest as forest

PROJECT = forest.PROJECT
TEXTURES = forest.OUTPUT / "Textures"


def open_scene(folder):
    forest.open_pack(PROJECT / folder / "source")
    return [obj for obj in bpy.data.objects if obj.type == "MESH"]


def subset(source, slot, key, pivot):
    """Like import_forest.subset, and keeps faces outward when the instance was mirrored."""
    part = forest_subset(source, slot, key, pivot)
    if source.matrix_world.determinant() < 0:
        part.data.flip_normals()
    return part


forest_subset = forest.subset
forest.subset = subset


def with_triangles(meshes, count, slots=None):
    found = [obj for obj in meshes if forest.triangles(obj.data) == count and
             (slots is None or len(obj.data.materials) == slots)]
    if not found:
        raise ValueError(f"No model with {count} triangles")
    return found


def uv_start(obj):
    layer = obj.data.uv_layers.active.data
    return min(item.uv.x for item in layer), min(item.uv.y for item in layer)


def tallest(candidates):
    return max(candidates, key=lambda obj: obj.dimensions.z)


def texture(material, key, size):
    forest.save_image(forest.image_of(material), key, size)


def export_plant(name, source, keys, budgets=None):
    used = {face.material_index for face in source.data.polygons}
    parts = [(source, slot, key) for slot, key in enumerate(keys) if slot in used]
    forest.export(name, parts, forest.pivot_at_base(source), budgets)


def seamless(image, box, size, name):
    """Cross-fade a crop with a half-tile shifted copy so it repeats without seams."""
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    pixels = pixels.reshape(height, width, 4)
    left, bottom, side = box
    crop = pixels[bottom:bottom + side, left:left + side, :3]
    shifted = np.roll(crop, (side // 2, side // 2), axis=(0, 1))
    ramp = 1 - np.abs(np.linspace(-1, 1, side, dtype=np.float32))
    weight = np.clip(np.minimum.outer(ramp, ramp) * 2.4, 0, 1)[..., None]
    tile = crop * weight + shifted * (1 - weight)
    rgba = np.concatenate([tile, np.ones((side, side, 1), dtype=np.float32)], axis=2)
    result = bpy.data.images.new(name, side, side, alpha=False)
    result.pixels = rgba.ravel()
    forest.save_image(result, name, size)


def clump(source, radius, budget, key, name):
    """Export the grass cards around the middle of a lawn as one small clump."""
    edit = bmesh.new()
    edit.from_mesh(source.data)
    seen = set()
    islands = []
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
                other = edge.other_vert(current)
                if other not in seen:
                    seen.add(other)
                    stack.append(other)
        faces = {face for item in members for face in item.link_faces}
        if faces:
            center = sum((item.co for item in members), Vector()) / len(members)
            islands.append((center, sum(len(face.verts) - 2 for face in faces), members))
    middle = sum((island[0] for island in islands), Vector()) / len(islands)
    seed = min(islands, key=lambda island: (island[0] - middle).length)[0]
    keep = set()
    for center, cost, members in sorted(islands, key=lambda island: (island[0] - seed).length):
        if (center - seed).length > radius or cost > budget:
            break
        keep.update(members)
        budget -= cost
    bmesh.ops.delete(edit, geom=[vertex for vertex in edit.verts if vertex not in keep], context="VERTS")
    data = bpy.data.meshes.new(name)
    edit.to_mesh(data)
    edit.free()
    piece = bpy.data.objects.new(name, data)
    piece.matrix_world = source.matrix_world
    bpy.context.collection.objects.link(piece)
    bpy.context.view_layer.update()
    forest.export(name, [(piece, 0, key)], forest.pivot_at_base(piece))


def night_forest():
    meshes = open_scene("a-forest-3-with-a-road-at-night-for-game")
    pine = tallest(with_triangles(meshes, 348, 2))
    texture(pine.data.materials[0], "LeafWoodland", 1024)
    texture(pine.data.materials[1], "WoodBark", 256)
    export_plant("WoodPine", pine, ["LeafWoodland", "WoodBark"])
    for name, count in (("WoodFir", 248), ("WoodOak", 152), ("WoodSapling", 176), ("WoodBush", 144)):
        export_plant(name, tallest(with_triangles(meshes, count)), ["LeafWoodland"])
    tufts = with_triangles(meshes, 24)
    export_plant("WoodGrass", tallest([tuft for tuft in tufts if uv_start(tuft)[0] < .25]), ["LeafWoodland"])
    export_plant("WoodReeds", tallest([tuft for tuft in tufts if uv_start(tuft)[0] >= .25]), ["LeafWoodland"])
    ground = with_triangles(meshes, 5202)[0]
    seamless(forest.image_of(ground.data.materials[0]), (100, 1560, 768), 512, "WoodFloor")


def mountain_firs():
    meshes = open_scene("the-landscape-is-a-forest-in-the-mountains")
    models = (
        ("AlpineFir", 45, ["LeafAlpineFir", "LeafAlpineFirTop"]),
        ("AlpinePine", 106, ["WoodBark", "LeafAlpinePine", "LeafAlpineBranch", "LeafAlpineTwig"]),
        ("AlpineSlim", 36, ["LeafAlpineSlim", "LeafAlpineCrown"]),
        ("AlpineBush", 24, ["LeafAlpineOak"]),
    )
    for name, count, keys in models:
        plant = tallest(with_triangles(meshes, count))
        for material, key in zip(plant.data.materials, keys):
            if key != "WoodBark":
                texture(material, key, 512)
        export_plant(name, plant, keys)
    crag = with_triangles(meshes, 6598)[0]
    texture(crag.data.materials[0], "AlpineRock", 512)


def islands(edit):
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
                other = edge.other_vert(current)
                if other not in seen:
                    seen.add(other)
                    stack.append(other)
        groups.append(members)
    return groups


def piece(source, name, keep):
    """World-space copy of the islands chosen by keep(groups)."""
    data = source.data.copy()
    data.transform(source.matrix_world)
    edit = bmesh.new()
    edit.from_mesh(data)
    chosen = {vertex for group in keep(islands(edit)) for vertex in group}
    bmesh.ops.delete(edit, geom=[vertex for vertex in edit.verts if vertex not in chosen], context="VERTS")
    edit.to_mesh(data)
    edit.free()
    data.materials.clear()
    result = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(result)
    return result


def simplify(obj, ratio):
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new("Low-poly geometry", "DECIMATE")
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def height(group):
    return max(vertex.co.z for vertex in group) - min(vertex.co.z for vertex in group)


def ponderosa(name, trunk_source, leaf_source, limbs, trunk_ratio, limb_ratio, cards):
    """A ponderosa with its whole trunk, the main limbs and enlarged needle cards, standing on its base."""
    trunk = piece(trunk_source, "PineBark", lambda groups: [max(groups, key=height)])
    base = min(vertex.co.z for vertex in trunk.data.vertices)
    foot = [vertex.co for vertex in trunk.data.vertices if vertex.co.z < base + .3]
    pivot = Vector((sum(co.x for co in foot) / len(foot), sum(co.y for co in foot) / len(foot), base + .08))
    simplify(trunk, trunk_ratio)
    boughs = piece(trunk_source, "PineBark", lambda groups: sorted(
        (group for group in groups if group is not max(groups, key=height)), key=len, reverse=True)[:limbs])
    simplify(boughs, limb_ratio)
    needles = piece(leaf_source, "LeafPine", lambda groups: groups)
    total = len(needles.data.polygons)
    forest.prune_cards(needles.data, cards)
    edit = bmesh.new()
    edit.from_mesh(needles.data)
    growth = min(2.3, (total / max(1, len(edit.faces))) ** .5)
    for group in islands(edit):
        center = sum((vertex.co for vertex in group), Vector()) / len(group)
        for vertex in group:
            vertex.co = center + (vertex.co - center) * growth
    edit.to_mesh(needles.data)
    edit.free()
    forest.export(name, [(trunk, 0, "PineBark"), (boughs, 0, "PineBark"), (needles, 0, "LeafPine")], pivot)
    for obj in (trunk, boughs, needles):
        bpy.data.objects.remove(obj, do_unlink=True)


def ponderosa_stand():
    open_scene("pine-forest-at-sunset")
    objects = bpy.data.objects
    grown = (("Ponderosa", "FlorasynthTree"), ("PonderosaTall", "FlorasynthTree.001"), ("PonderosaWide", "FlorasynthTree.002"))
    for name, base in grown:
        trunk, leaves = objects[base + "_branches"], objects[base + "_foliage"]
        ponderosa(name, trunk, leaves, 14, .6, .25, 900)
        ponderosa(name + "Far", trunk, leaves, 5, .3, .13, 230)
    for name, index in (("PonderosaYoung", "012"), ("PonderosaSapling", "003")):
        base = f"Ponderosa Pine seedling.{index}"
        ponderosa(name, objects[base + "_branches"], objects[base + "_foliage"], 8, .5, .25, 420)
    texture(objects["grass_lush"].data.materials[0], "LeafPineGrass", 256)
    texture(objects["grass_fine"].data.materials[0], "LeafPineReeds", 256)
    clump(objects["grass_lush"], 1.7, 90, "LeafPineGrass", "PineGrass")
    clump(objects["grass_fine"], 1.7, 90, "LeafPineReeds", "PineReeds")
    log = objects["log.003"]
    forest.export("PineLog", [(log, 0, "PineBark")], forest.pivot_at_base(log))
    rock = objects["rock.002"]
    forest.export("AlpineBoulder", [(rock, 0, "AlpineRock")], forest.pivot_at_base(rock), {"AlpineRock": 140})
    meadow = bpy.data.images.load(str(PROJECT / "pine-forest-at-sunset/textures/grass_color.jpg"))
    forest.save_image(meadow, "Meadow", 512)


def cutout(color_file, opacity_file, key, size):
    """Join a colour map and its separate opacity map into one cut-out texture."""
    color = bpy.data.images.load(str(color_file))
    opacity = bpy.data.images.load(str(opacity_file))
    width, height = color.size
    if tuple(opacity.size) != (width, height):
        opacity.scale(width, height)
    pixels = np.empty(width * height * 4, dtype=np.float32)
    mask = np.empty(width * height * 4, dtype=np.float32)
    color.pixels.foreach_get(pixels)
    opacity.pixels.foreach_get(mask)
    pixels = pixels.reshape(-1, 4)
    pixels[:, 3] = mask.reshape(-1, 4)[:, 0]
    result = bpy.data.images.new(key, width, height, alpha=True)
    result.pixels.foreach_set(pixels.ravel())
    forest.save_image(result, key, size)


def tree_pack():
    folder = PROJECT / "low-poly-forest-tree-pack"
    pack = folder / "source" / "FOREST_TREE_PACK"
    if not pack.exists():
        with ZipFile(PROJECT / "low-poly-forest-tree-pack.zip") as archive:
            archive.extractall(folder)
        subprocess.run(["tar", "-xf", "LOW POLY FOREST TREE PACK.rar"], cwd=folder / "source", check=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(pack / "SOURCE" / "Tree_Pack.fbx"))
    objects = bpy.data.objects
    art = pack / "TEXTURES"
    cutout(art / "TREES_LOW_POLY/Background_Tree_Atlas_Diffuse.png", art / "TREES_LOW_POLY/Background_Tree_Atlas_Opacity.png",
           "LeafGrove", 1024)
    for index, key in ((1, "LeafGroveFir"), (2, "LeafGroveBirch")):
        cutout(art / f"TREES_HIGH_POLY/Tree_Branches_{index}_Diffuse.png", art / f"TREES_HIGH_POLY/Tree_Branches_{index}_Opacity.png",
               key, 512)
    for source, key in (("TREES_HIGH_POLY/Tree_Trunk_01_Diffuse.png", "GroveBark"),
                        ("TREES_HIGH_POLY/Tree_Trunk_02_Diffuse.png", "GroveBirchBark"), ("ROCKS/ROCKS_DIFFUSE.png", "GroveRock")):
        forest.save_image(bpy.data.images.load(str(art / source)), key, 512)

    detailed = (("GroveFir", "Tree_Trunk_01.001", "GroveBark", "LeafGroveFir"), ("GroveSpruce", "Tree_Trunk_01.002", "GroveBark", "LeafGroveFir"),
                ("GroveYoungFir", "Tree_Trunk_01", "GroveBark", "LeafGroveFir"), ("GroveBirch", "Tree_Trunk_02", "GroveBirchBark", "LeafGroveBirch"))
    crowns = [obj for obj in objects if obj.name.startswith("Tree_Branches")]
    for name, trunk_name, bark, leaves in detailed:
        trunk = objects[trunk_name]
        place = trunk.matrix_world.translation.xy
        crown = min(crowns, key=lambda obj: (obj.matrix_world.translation.xy - place).length)
        forest.export(name, [(trunk, 0, bark), (crown, 0, leaves)], forest.pivot_at_base(trunk))
    atlas = ["Background_Tree_Atlas"] + [f"Background_Tree_Atlas.{index:03d}" for index in range(1, 13)]
    names = [f"GroveTree{index}" for index in range(1, 9)] + ["GroveGrass", "GroveShrub", "GroveBush", "GroveWeed", "GroveThicket"]
    for name, source in zip(names, atlas):
        export_plant(name, objects[source], ["LeafGrove"])
    for name, source in (("GroveStone", "Rocks.001"), ("GroveStoneFlat", "Rocks.005"), ("GroveStoneTall", "Rocks.006")):
        export_plant(name, objects[source], ["GroveRock"])


if __name__ == "__main__":
    TEXTURES.mkdir(parents=True, exist_ok=True)
    night_forest()
    mountain_firs()
    ponderosa_stand()
    tree_pack()
