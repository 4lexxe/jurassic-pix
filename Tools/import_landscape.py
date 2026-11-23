"""Convert the three supplied landscapes to compact, textured Unity FBXs."""

import math
import os
import shutil
import subprocess
import sys
import tempfile
from collections import deque
from pathlib import Path, PurePosixPath
from zipfile import ZipFile

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

sys.path.insert(0, str(Path(__file__).resolve().parent))
from import_forest import OUTPUT, PROJECT, image_of, pivot_at_base, save_image, triangles


def extract(archive, member, to):
    with archive.open(member) as source, to.open("wb") as target:
        shutil.copyfileobj(source, target)


def unpack_range(folder):
    with ZipFile(PROJECT / "mountain-range-01.zip") as outer:
        archive = folder / "range.rar"
        extract(outer, "source/Mountain_Range01_solararchitect.rar", archive)
        extract(outer, "textures/color_range01.jpeg", folder / "Range.jpeg")
    program = shutil.which("7z") or str(Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "7-Zip/7z.exe")
    model = folder / "Range.obj"
    with model.open("wb") as target:
        subprocess.run([program, "x", "-so", str(archive), "Mountain_Range01_solararchitect/mesh_range01_05K_OBJ.obj"],
                       stdout=target, check=True)
    return model, folder / "Range.jpeg"


def unpack_peak(folder):
    with ZipFile(PROJECT / "peak-mountain-hiking-trail.zip") as outer:
        nested = folder / "peak.zip"
        extract(outer, "source/20260124_007_OUTPUT_LOD03.zip", nested)
    with ZipFile(nested) as archive:
        for filename in archive.namelist():
            if PurePosixPath(filename).suffix.lower() in {".obj", ".mtl", ".png"}:
                extract(archive, filename, folder / PurePosixPath(filename).name)
    return folder / "20260124_007_RC_LOD0.obj", folder / "20260124_007_RC_LOD0_u0_v0_diffuse.png"


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def prepare(obj, name, pivot):
    mesh = obj.data.copy()
    mesh.transform(obj.matrix_world)
    for vertex in mesh.vertices:
        vertex.co -= pivot
    mesh.materials.clear()
    part = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(part)
    return part


def export(name, parts):
    root = bpy.data.objects.new("LandscapeAsset", None)
    bpy.context.collection.objects.link(root)
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for part in parts:
        part.parent = root
        part.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT / (name + ".fbx")), use_selection=True, object_types={"EMPTY", "MESH"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_anim=False)


def continuous_relief(source, name, budget):
    data = source.data
    data.calc_loop_triangles()
    faces = list(data.loop_triangles)
    points = [vertex.co.copy() for vertex in data.vertices]
    uv_layer = data.uv_layers.active.data
    surface = BVHTree.FromPolygons(points, [tuple(face.vertices) for face in faces], all_triangles=True)
    side = int(math.sqrt(budget / 2)) + 1
    limits = [(min(p[i] for p in points), max(p[i] for p in points)) for i in range(3)]
    height = limits[2][1] + 1
    coords, uvs = [], []
    sources = [-1] * (side * side)
    pending = deque()
    down = Vector((0, 0, -1))

    for row in range(side):
        y = limits[1][0] + (limits[1][1] - limits[1][0]) * row / (side - 1)
        for col in range(side):
            x = limits[0][0] + (limits[0][1] - limits[0][0]) * col / (side - 1)
            start = Vector((x, y, height))
            hit, _, index, _ = surface.ray_cast(start, down, height - limits[2][0] + 1)
            point = Vector((x, y, 0))
            uv = Vector((0, 0, 0))
            if hit is not None:
                triangle = faces[index]
                original_uvs = [Vector((*uv_layer[loop].uv, 0)) for loop in triangle.loops]
                uv = barycentric_transform(hit, *(points[v] for v in triangle.vertices), *original_uvs)
                point.z = hit.z
                sources[len(coords)] = len(coords)
                pending.append(len(coords))
            coords.append(point)
            uvs.append(uv)
    if not pending:
        raise ValueError(f"No surface samples for {name}")

    # Fill scan holes from the nearest valid sample and blend the perimeter into the valley.
    distances = [0] * len(coords)
    while pending:
        index = pending.popleft()
        row, col = divmod(index, side)
        for y, x in ((row - 1, col), (row + 1, col), (row, col - 1), (row, col + 1)):
            if not (0 <= y < side and 0 <= x < side):
                continue
            neighbor = y * side + x
            if sources[neighbor] != -1:
                continue
            donor = sources[index]
            sources[neighbor] = donor
            distances[neighbor] = distances[index] + 1
            coords[neighbor].z = coords[donor].z * max(0, 1 - distances[neighbor] / (side * .13))
            uvs[neighbor] = uvs[donor]
            pending.append(neighbor)

    result = []
    for index, point in enumerate(coords):
        row, col = divmod(index, side)
        edge = min(row, col, side - row - 1, side - col - 1)
        edge_width = .20 if name == "HikingPeak" else .095
        blend = min(1, edge / (side * edge_width))
        point.z *= blend * blend * (3 - 2 * blend)
        if row < side - 1 and col < side - 1:
            result.extend(((index, index + 1, index + side),
                           (index + 1, index + side + 1, index + side)))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(coords, [], result)
    mesh.update()
    texture = mesh.uv_layers.new(name="UVMap").data
    for poly in mesh.polygons:
        poly.use_smooth = True
        for loop in poly.loop_indices:
            uv = uvs[mesh.loops[loop].vertex_index]
            texture[loop].uv = (uv.x, uv.y)
    part = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(part)
    return part


def mountain(filename, texture, name, budget):
    reset()
    bpy.ops.wm.obj_import(filepath=str(filename))
    source = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
    reference = prepare(source, "Source relief", pivot_at_base(source))
    before = triangles(reference.data)
    bpy.context.view_layer.objects.active = reference
    decimate = reference.modifiers.new("Terrain simplification", "DECIMATE")
    decimate.ratio = min(1, budget * 3 / before)
    decimate.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    part = continuous_relief(reference, name, budget)
    save_image(bpy.data.images.load(str(texture), check_existing=True), name, 1024)
    export(name, [part])
    print(f"LANDSCAPE {name}: {before} -> {triangles(part.data)} triangles", flush=True)


def join_parts(group, key):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in group:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = group[0]
    if len(group) > 1:
        bpy.ops.object.join()
    group[0].name = key
    return group[0]


def forest_landscape():
    reset()
    bpy.ops.import_scene.gltf(filepath=str(PROJECT / "landscape_forest__mountains.glb"))
    materials = {
        "Material.001": "LandFloor",
        "Material.002": "LeafLandGrass",
        "Material.003": "LeafLandPlants",
        "Material.004": "LeafLandPine",
        "Material.005": "LeafLandFir",
        "Material.006": "LeafLandTree",
        "Material.007": "LandSlope",
        "Material.008": "LandRidge",
        "Material.009": "LandCrest",
    }
    for material, key in materials.items():
        save_image(image_of(bpy.data.materials[material]), key, 512)

    groups = {}
    source = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.data.materials[0].name in materials]
    pivot = Vector((-1.6, 19.8, -1.22))
    for obj in source:
        key = materials[obj.data.materials[0].name]
        lower = min((obj.matrix_world @ Vector(vertex)).z for vertex in obj.bound_box)
        # The far-above-ground atlas samples and the pack's sky dome are authoring aids.
        if lower >= 30:
            continue
        if key == "LeafLandGrass":
            # Keep two thirds of the repeated small grass cards.
            number = obj.name.split("_")[0].split(".")[-1]
            if number.isdigit() and int(number) % 3 == 0:
                continue
        part = prepare(obj, key, pivot)
        groups.setdefault(key, []).append(part)
    ground, plants = [], []
    for key, items in groups.items():
        part = join_parts(items, key)
        (plants if key.startswith("Leaf") else ground).append(part)
    export("LandscapeTerrain", ground)
    export("LandscapePlants", plants)
    count = sum(triangles(obj.data) for obj in ground + plants)
    print(f"LANDSCAPE planted terrain: {count} triangles in {len(ground) + len(plants)} meshes", flush=True)


if __name__ == "__main__":
    with tempfile.TemporaryDirectory(prefix="jurassic-landscape-import-") as scratch:
        folder = Path(scratch)
        mountain(*unpack_range(folder), "MountainRange", 14000)
        mountain(*unpack_peak(folder), "HikingPeak", 22000)
        forest_landscape()
