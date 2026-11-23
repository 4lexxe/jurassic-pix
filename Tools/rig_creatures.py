"""Bake the expedition's generic Unity rigs and motion clips from the supplied FBX meshes.

Run from the project root with Blender 5.1:
    blender -b --factory-startup --python-exit-code 1 --python Tools/rig_creatures.py
"""

import heapq
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector


# Anatomical landmarks in the source models' corrected Blender coordinates.
# Central chains use (forward, height); the bilateral chains use (width, forward, height).
SPECIES = {
    "Argentinosaurus": dict(
        source="ARGENTINOSAURUS", pivot=.16, gait="quadruped",
        spine=[(.04, .207), (.163, .207), (.257, .225), (.333, .296), (.413, .396), (.48, .477), (.547, .482)],
        tail=[(.032, .209), (-.123, .225), (-.272, .186), (-.422, .137), (-.549, .091)],
        hind=[(.083,.052,.221), (.086,.066,.110), (.087,.043,.026), (.086,.080,.011)],
        fore=[(.060,.277,.199), (.056,.299,.105), (.055,.306,.030), (.055,.340,.010)],
        jaw=[(.474,.474), (.553,.461)],
        gait_values=(1.66, 1.08, .061, .086, .033, .055), attack=1.45,
        shut=2, bite=8, sip=96, lie=(.078, .055, .075, 78), crouch=(.024, -1.5), death=2.8, droop=(16, 14),
    ),
    "Triceratops": dict(
        source="TRICERAPTOR", pivot=.035, gait="quadruped",
        spine=[(-.114,.289), (.053,.291), (.202,.299), (.278,.316), (.536,.257)],
        tail=[(-.151,.280), (-.257,.239), (-.403,.156), (-.567,.089)],
        hind=[(.115,-.110,.258), (.120,-.098,.150), (.120,-.142,.048), (.120,-.105,.010)],
        fore=[(.116,.216,.258), (.120,.185,.150), (.120,.222,.050), (.120,.262,.010)],
        jaw=[(.401,.280), (.535,.242)],
        gait_values=(1.27, .74, .108, .169, .061, .103), attack=1.12,
        shut=17, bite=6, lie=(.100, .16, .19, 16), crouch=(.042, -2), death=1.6, droop=(22, 18),
    ),
    "Tyrannosaurus": dict(
        source="TREX", pivot=.16, gait="biped",
        spine=[(.125,.319), (.242,.330), (.337,.356), (.405,.403), (.570,.374)],
        tail=[(.108,.314), (-.067,.312), (-.235,.317), (-.43,.310), (-.579,.266)],
        hind=[(.086,.144,.321), (.100,.224,.210), (.103,.076,.115), (.097,.087,.033), (.098,.194,.015)],
        arms=[(.044,.278,.276), (.058,.314,.243), (.076,.376,.222)],
        jaw=[(.400,.355), (.559,.305)],
        gait_values=(1.12, .67, .111, .174, .070, .113), attack=1.03,
        shut=9, bite=22, crouch=(.056, -7), death=1.8, droop=(20, 16),
    ),
    "Velociraptor": dict(
        source="VELOCIRAPTOR", pivot=.13, gait="biped",
        spine=[(.103,.301), (.224,.289), (.327,.320), (.369,.420), (.451,.456), (.566,.422)],
        tail=[(.082,.303), (-.086,.338), (-.274,.367), (-.440,.398), (-.563,.407)],
        hind=[(.059,.119,.265), (.066,.139,.187), (.067,.062,.089), (.063,.066,.027), (.062,.184,.013)],
        arms=[(.066,.229,.293), (.133,.280,.234), (.216,.355,.143)],
        jaw=[(.454,.433), (.565,.395)],
        gait_values=(.76, .48, .101, .150, .065, .097), attack=.77,
        shut=12, bite=22, crouch=(.048, -7), death=1.1, droop=(22, 16),
    ),
    "Quetzalcoatlus": dict(
        source="Quetzalcoatlus", pivot=-.102, gait="winged",
        spine=[(-.148,.061), (-.094,.085), (-.032,.111), (.037,.140), (.105,.158), (.259,.115)],
        tail=[(-.135,.066), (-.199,.058)],
        hind=[(.035,-.135,.059), (.041,-.162,.040), (.045,-.204,.019), (.048,-.267,.009)],
        wings=[(.046,-.090,.086), (.147,-.113,.105), (.220,-.014,.176), (.398,-.023,.180), (.592,-.017,.176)],
        jaw=[(.097,.146), (.263,.102)],
        gait_values=(1.16, .75, .029, .043, .023, .041), attack=.92,
        shut=3, bite=12, sip=46, lie=(.012, 0, .024, 24), crouch=(.008, -2), death=1.3, droop=(18, 8),
    ),
}
FPS = 30
TAU = math.tau
X, Y, Z = (1, 0, 0), (0, 1, 0), (0, 0, 1)


def smooth(a, b, value):
    t = min(1, max(0, (value - a) / (b - a)))
    return t * t * (3 - 2 * t)


def create_skeleton(name, spec, mesh):
    data = bpy.data.armatures.new(name + " Skeleton")
    arm = bpy.data.objects.new("Skeleton", data)
    bpy.context.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bones = {}

    def point(p):
        return Vector((p[0], p[1] - spec["pivot"], p[2]))

    def chain(names, points, parent=None, connected=True):
        for index, bone_name in enumerate(names):
            head, tail = (point(p) for p in points[index:index + 2])
            bone = data.edit_bones.new(bone_name)
            bone.head, bone.tail = head, tail
            if parent:
                bone.parent = data.edit_bones[parent]
                bone.use_connect = connected and (bone.parent.tail - head).length < .0001
            bones[bone_name] = (head, tail)
            parent = bone_name

    def middle(points):
        return [(0, forward, height) for forward, height in points]

    hip = spec["spine"][0]
    chain(["Motion"], [(0, spec["pivot"], 0), (0, spec["pivot"], .07)])
    spine = ["Hips", "Chest"] + ["Neck.%02d" % i for i in range(1, len(spec["spine"]) - 3)] + ["Head"]
    chain(spine, middle(spec["spine"]), "Motion", False)
    tail = ["Tail.%02d" % i for i in range(1, len(spec["tail"]))]
    chain(tail, middle(spec["tail"]), "Hips", False)
    if "jaw" in spec:
        chain(["Jaw"], middle(spec["jaw"]), "Head", False)

    limbs = {}
    for part in ("hind", "fore", "arms", "wings"):
        if part not in spec:
            continue
        for sign, side in ((-1, "L"), (1, "R")):
            prefix = dict(hind="Hind", fore="Fore", arms="Arm", wings="Wing")[part]
            names = [f"{prefix}.{i + 1:02d}.{side}" for i in range(len(spec[part]) - 1)]
            chain(names, [(sign * width, y, z) for width, y, z in spec[part]],
                  "Hips" if part == "hind" else "Chest", False)
            limbs[(part, sign)] = names
    bpy.ops.object.mode_set(mode="OBJECT")
    arm.data.bones["Motion"].use_deform = False
    arm.show_in_front = True
    mesh.parent = arm
    modifier = mesh.modifiers.new("Skeletal deformation", "ARMATURE")
    modifier.object = arm
    modifier.use_deform_preserve_volume = True
    return arm, bones, limbs, spine, tail


def segment_distance(point, segment):
    start, end = segment
    line = end - start
    t = max(0, min(1, (point - start).dot(line) / line.length_squared))
    return (point - (start + t * line)).length


def leg_profiles(mesh, spec):
    """Measure each leg: where it parts from the belly, its inner face and the length of the foot."""
    points = [(abs(v.co.x), v.co.y + spec["pivot"], v.co.z) for v in mesh.data.vertices]
    profiles = {}
    for part in ("hind", "fore"):
        if part not in spec:
            continue
        ys = [landmark[1] for landmark in spec[part]]
        near, far = min(ys) - .03, max(ys) + .03
        crotch = min(z for x, y, z in points if x < .012 and near < y < far) - .004
        column = [(x, y) for x, y, z in points if crotch * .35 < z < crotch * .8 and near - .05 < y < far + .05 and x > .012]
        inner = min(x for x, _ in column)
        foot = [y for x, y, z in points if z < crotch * .8 and near - .05 < y < far + .05 and x > inner * .6]
        profiles[part] = (crotch, inner, min(foot), max(foot), spec[part][0][2] * 1.1)
    return profiles


def leg_strength(x, y, z, profile):
    crotch, inner, start, end, top = profile
    half = (end - start) * .5
    along = 1 - smooth(half * .5, half * 1.2, max(start - y, y - end, 0))
    free = smooth(.006, .016, x)
    joined = (1 - smooth(crotch, top, z)) * smooth(inner * .45, inner * .95, x)
    return along * (free + (joined - free) * smooth(crotch - .008, crotch + .008, z))


def mandible(mesh, spec):
    """How much each vertex belongs to the lower jaw: the skin nearer to the chin than to the snout along the surface."""
    hinge, tip = spec["jaw"]
    places = [vertex.co for vertex in mesh.data.vertices]
    links = [[] for _ in places]
    for edge in mesh.data.edges:
        a, b = edge.vertices
        length = (places[a] - places[b]).length
        links[a].append((b, length))
        links[b].append((a, length))

    def travel(seed):
        reach = [math.inf] * len(places)
        reach[seed] = 0
        queue = [(0, seed)]
        while queue:
            distance, vertex = heapq.heappop(queue)
            if distance > reach[vertex]:
                continue
            for other, length in links[vertex]:
                if distance + length < reach[other]:
                    reach[other] = distance + length
                    heapq.heappush(queue, (distance + length, other))
        return reach

    front = [index for index, place in enumerate(places)
             if abs(place.x) < .02 and (hinge[0] + tip[0]) * .5 < place.y + spec["pivot"] < tip[0] + .03]
    chin = travel(min(front, key=lambda index: places[index].z))
    snout = travel(max(front, key=lambda index: places[index].z))
    share = [smooth(-.012, .012, snout[index] - chin[index]) if chin[index] < math.inf else None
             for index in range(len(places))]
    head = [index for index, value in enumerate(share) if value is not None and places[index].y + spec["pivot"] > hinge[0] - .05]
    for index, value in enumerate(share):
        if value is None:
            # Loose pieces such as teeth follow the nearest skin.
            nearest = min(head, key=lambda other: (places[other] - places[index]).length_squared) if                 places[index].y + spec["pivot"] > hinge[0] - .05 else None
            share[index] = share[nearest] if nearest is not None else 0
    return share


def bind_mesh(mesh, spec, bones, limbs, spine, tail):
    groups = {name: mesh.vertex_groups.new(name=name) for name in bones if name != "Motion"}
    profiles = leg_profiles(mesh, spec)
    lower_jaw = mandible(mesh, spec) if "jaw" in spec else None

    def distribute(point, names, strength, weights):
        if strength <= 0:
            return
        distances = sorted((segment_distance(point, bones[name]), name) for name in names)[:2]
        values = [1 / (distance + .019)**3 for distance, _ in distances]
        total = sum(values)
        for (_, name), value in zip(distances, values):
            weights[name] = weights.get(name, 0) + strength * value / total

    for vertex in mesh.data.vertices:
        p = vertex.co
        x, y, z = abs(p.x), p.y + spec["pivot"], p.z
        side = -1 if p.x < 0 else 1
        central = spine + tail
        weights = {}
        remaining = 1.0
        if spec["source"] == "TREX":
            terminal = limbs["arms", side][-1]
            hand = smooth(.053, .065, x) * smooth(.315, .346, y) * (1-smooth(.250,.267,z))
            hand *= 1 - smooth(.033, .052, segment_distance(p, bones[terminal]))
            distribute(p, [terminal], hand, weights)
            remaining -= hand

            toes = remaining * (1-smooth(.029,.058,z)) * leg_strength(x, y, z, profiles["hind"])
            distribute(p, [limbs["hind", side][-1]], toes, weights)
            remaining -= toes

        if spec["gait"] == "winged":
            sole = (1-smooth(.015,.037,z)) * (1-smooth(-.191,-.152,y)) * smooth(.018,.030,x)
            distribute(p, [limbs["hind", side][-1]], sole, weights)
            remaining -= sole
            wing = remaining * smooth(.048, .103, x) * (1 - (1-smooth(.028,.076,z)) * (1-smooth(-.152,-.119,y)))
            distribute(p, limbs["wings", side], wing, weights)
            remaining -= wing
        elif "arms" in spec:
            if spec["source"] == "TREX":
                arm = remaining * smooth(.036,.070,x) * smooth(.273,.314,y) * (1-smooth(.380,.409,y)) * (1-smooth(.244,.283,z))
            else:
                arm = smooth(.065,.133,x) * smooth(.153,.225,y) * (1-smooth(.360,.408,y)) * (1-smooth(.291,.337,z))
            distribute(p, limbs["arms", side], arm, weights)
            remaining -= arm

        legs = [part for part in ("hind", "fore") if part in spec]
        leg = min(legs, key=lambda part: min(segment_distance(p, bones[n]) for n in limbs[part, side]))
        segments = limbs[leg, side]
        distance = min(segment_distance(p, bones[n]) for n in segments)
        hip = spec[leg][0][2]
        side_mask = smooth(.016, spec[leg][0][0] * .83, x)
        low = 1 - smooth(hip * .48, hip * 1.08, z)
        outer = smooth(spec[leg][0][0] * .75, spec[leg][0][0] * 1.43, x) * (1 - smooth(hip, hip * 1.28, z))
        reach = .076 if spec["gait"] == "quadruped" else .062
        if spec["gait"] == "winged":
            strength = remaining * side_mask * max(low, outer * .87) * (1 - smooth(reach*.69, reach*1.54, distance))
            strength *= 1 - smooth(-.120, -.100, y)
        else:
            strength = remaining * leg_strength(x, y, z, profiles[leg])
        distribute(p, segments, strength, weights)
        remaining -= strength

        if "jaw" in spec:
            hinge = spec["jaw"][0]
            jaw = remaining * smooth(hinge[0] -.016, hinge[0] +.052, y) * lower_jaw[vertex.index]
            distribute(p, ["Jaw"], jaw, weights)
            remaining -= jaw
        distribute(p, central, remaining, weights)
        strongest = sorted(weights.items(), key=lambda entry: entry[1], reverse=True)[:4]
        normalization = sum(value for _, value in strongest)
        for bone, value in strongest:
            groups[bone].add([vertex.index], value / normalization, "REPLACE")


class Animator:
    def __init__(self, rig, landmarks, limbs, spine, tail):
        self.rig = rig
        self.landmarks = landmarks
        self.limbs = limbs
        self.spine = spine
        self.tail = tail
        self.pose = rig.pose.bones
        self.rest = {bone.name: bone.bone.matrix_local.to_quaternion() for bone in self.pose}

    def rotate(self, name, x=0, y=0, z=0):
        swing = Quaternion(Z, math.radians(z)) @ Quaternion(Y, math.radians(y)) @ Quaternion(X, math.radians(x))
        rest = self.rest[name]
        self.pose[name].rotation_quaternion = rest.inverted() @ swing @ rest

    def shift(self, name, forward=0, height=0, side=0):
        self.pose[name].location = self.rest[name].inverted() @ Vector((side, forward, height))

    def aim(self, name, target):
        bone = self.pose[name]
        current = bone.tail - bone.head
        desired = target - bone.head
        if desired.length < .00001:
            return
        change = current.normalized().rotation_difference(desired.normalized())
        matrix = (change @ bone.matrix.to_quaternion()).to_matrix().to_4x4()
        matrix.translation = bone.head
        local = self.rig.convert_space(pose_bone=bone, matrix=matrix, from_space="POSE", to_space="LOCAL")
        bone.rotation_quaternion = local.to_quaternion()
        bpy.context.view_layer.update()

    def place_leg(self, names, forward, height, swing):
        upper, lower = names[:2]
        foot = names[-1]
        foot_base = self.landmarks[foot][0]
        ankle = foot_base + Vector((0, forward, height))
        target = ankle
        if len(names) == 4:
            hock = self.landmarks[names[2]][0]
            direction = (hock - foot_base).normalized()
            direction = Quaternion(X, swing * .26) @ direction
            target = ankle + direction * self.pose[names[2]].length
        hip = self.pose[upper].head.copy()
        knee = self.landmarks[lower][0]
        rest_foot = self.landmarks[names[2]][0]
        rest_axis = (rest_foot - self.landmarks[upper][0]).normalized()
        bend = knee - self.landmarks[upper][0]
        bend -= rest_axis * bend.dot(rest_axis)
        delta = target - hip
        if delta.length < .00001:
            return
        axis = delta.normalized()
        pole = bend - axis * axis.dot(bend)
        if pole.length < .00001:
            pole = axis.cross(Vector(X))
        length_a, length_b = self.pose[upper].length, self.pose[lower].length
        distance = min(length_a + length_b - .0002, max(abs(length_a - length_b) + .0002, delta.length))
        projection = (distance * distance + length_a * length_a - length_b * length_b) / (2 * distance)
        bend_height = math.sqrt(max(0, length_a * length_a - projection * projection))
        self.aim(upper, hip + axis * projection + pole.normalized() * bend_height)
        self.aim(lower, target)
        if len(names) == 4:
            self.aim(names[2], ankle)
        rest_direction = self.landmarks[foot][1] - foot_base
        self.aim(foot, self.pose[foot].head + Quaternion(X, swing * .43) @ rest_direction)

    def folded_wings(self, t, intensity):
        for sign in (-1, 1):
            names = self.limbs["wings", sign]
            stride = math.sin(TAU * t + (math.pi if sign < 0 else 0)) * intensity
            directions = [
                (sign*.45, .50+stride*.26, -.48),
                (sign*.14, .64+stride*.34, -.36+max(0,stride)*.45),
                (sign*.39, -.90, .63),
                (sign*.20, -1, .14),
            ]
            for name, direction in zip(names, directions):
                self.aim(name, self.pose[name].head + Vector(direction))

    def frame(self, species, spec, clip, t):
        for bone in self.pose:
            bone.rotation_mode = "QUATERNION"
            bone.rotation_quaternion = Quaternion()
            bone.location.zero()
        if clip == "Death":
            self.collapse(spec, t)
            return
        if clip == "Sleep":
            if "lie" in spec:
                self.lie_down(spec, math.sin(t * TAU))
            else:
                self.collapse(spec, 1, 0, math.sin(t * TAU))
            return
        if clip == "Topple":
            down = smooth(0, .36, t) * (1 - smooth(.66, 1, t))
            lying = smooth(.3, .4, t) * (1 - smooth(.6, .7, t))
            self.collapse(spec, down, math.sin(t * TAU * 5) * 13 * lying)
            return
        running = clip == "Run"
        feeding = clip == "Feed"
        drinking = clip == "Drink"
        courting = clip == "Court"
        crouched = clip in ("Crouch", "Sneak", "Feed", "Drink")
        moving = clip in ("Walk", "Run", "Sneak")
        winged = spec["gait"] == "winged"
        attacking = clip == "Attack"
        airborne = clip in ("Fly", "Dive")
        phase = t * TAU
        hit = math.sin(math.pi * smooth(.13, .73, t)) if attacking or clip == "Dive" else 0
        guard = math.sin(math.pi * smooth(0, .32, t)) if attacking else 0
        _, _, walk_stride, run_stride, walk_lift, run_lift = spec["gait_values"]
        stride, lift = (run_stride, run_lift) if running else (walk_stride, walk_lift)
        if crouched:
            stride, lift = stride * .62, lift * .6
        bob = (.010 if running else .0035) * (1 - math.cos(phase * 2)) if moving else math.sin(phase)*.0026
        drop, pitch = spec["crouch"] if crouched else (0, 0)
        self.shift("Hips", height=bob - drop)
        self.rotate("Hips", x=pitch + (math.sin(phase * 2 + .9) * 1.6 if running else 0))
        self.rotate("Chest", x=math.sin(phase)* (3.4 if moving else 1.3),
                    y=math.sin(phase)*(4 if moving else 1), z=math.sin(phase)* (2.5 if moving else .8))
        for index, name in enumerate(self.tail):
            swing = math.sin(phase - index*.56)
            self.rotate(name, x=swing*(2.6 if moving else 1.5), z=swing*(8 if running else 4.2 if moving else 2.3))
        nod = math.sin(phase * 2 + .5) * (4.2 if running else 1.6 if moving else 0)
        reach = 7 if running else 11 if crouched else 0
        necks = self.spine[2:-1]
        for index, name in enumerate(necks):
            self.rotate(name, x=math.sin(phase - .54*index) * (2.5 if moving else 1.5) + (nod - reach) / len(necks),
                        z=math.sin(phase + .4) * (3.2 if running else 1.2 if moving else 0) / len(necks))
        self.rotate("Head", x=math.sin(phase - .54*len(necks)) * (2.5 if moving else 1.5) - nod * .8 + reach * .75,
                    z=-math.sin(phase + .4) * (2.4 if running else .8 if moving else 0))
        if "arms" in spec:
            for sign in (-1, 1):
                arms = self.limbs["arms", sign]
                self.rotate(arms[0], x=math.sin(phase + sign*1.3)*(11 if running else 5))
                self.rotate(arms[1], y=sign*(9 if running else 3))
        breath = .5 - .5 * math.cos(phase * (2 if running else 1))
        jaw = spec["shut"] - breath * (spec["shut"] * .5 if running else .5)

        if attacking or clip == "Dive":
            if species == "Argentinosaurus":
                self.rotate("Hips", z=12*guard - 16*hit)
                self.rotate("Chest", z=-14*hit, x=-6*hit)
                for index, name in enumerate(self.tail):
                    wave = math.sin(math.pi * smooth(.16 + index*.035, .70 + index*.065, t))
                    self.rotate(name, x=-12*wave, z=17*guard-36*wave)
            elif species == "Triceratops":
                self.shift("Hips", forward=.042*hit, height=-.030*guard)
                self.rotate("Chest", x=-12*hit)
                self.rotate(self.spine[2], x=-20*hit + 5*guard)
                self.rotate("Head", x=15*hit)
            elif species == "Tyrannosaurus":
                self.shift("Hips", forward=.055*hit, height=-.028*guard)
                self.rotate("Chest", x=-9*hit)
                self.rotate(self.spine[2], x=14*guard - 10*hit)
                self.rotate("Head", x=8*guard + 6*hit)
            elif species == "Velociraptor":
                leap = math.sin(math.pi * smooth(.10, .83, t))
                self.shift("Hips", forward=.052*hit, height=.092*leap-.030*guard)
                self.rotate("Chest", x=-18*hit)
                self.rotate("Head", x=18*hit)
                for sign in (-1, 1):
                    a, b = self.limbs["arms", sign]
                    self.rotate(a, x=62*hit-24*guard, y=-sign*23*hit)
                    self.rotate(b, x=-28*hit)
            else:
                self.rotate("Chest", x=-17*hit)
                self.rotate(self.spine[2], x=-27*hit + 8*guard)
                self.rotate("Head", x=21*hit)
        if attacking or clip == "Dive":
            gape = smooth(.02, .28, t) * (1 - smooth(.41, .50, t))
            jaw = spec["shut"] - (spec["shut"] + spec["bite"]) * gape
        if drinking:
            lap = .5 + .5 * math.sin(phase * 4)
            sip = spec.get("sip", 34)
            self.rotate("Hips", x=pitch - 5)
            self.rotate("Chest", x=-sip * .3)
            for name in necks:
                self.rotate(name, x=-sip * .75 / len(necks))
            self.rotate("Head", x=sip * .1 + lap * 3)
            jaw = spec["shut"] - (spec["shut"] + 7) * lap
        if courting:
            sway = math.sin(phase * 2)
            self.rotate("Hips", z=sway * 5)
            self.rotate("Chest", x=6 + sway * 4, z=-sway * 4)
            for name in necks:
                self.rotate(name, x=(16 + math.sin(phase * 3) * 12) / len(necks))
            self.rotate("Head", x=-8 + math.sin(phase * 3 + 1) * 8, z=sway * 8)
            for index, name in enumerate(self.tail):
                self.rotate(name, x=4, z=math.sin(phase * 3 - index * .5) * 11)
            jaw = spec["shut"] - (spec["shut"] + 8) * (.5 + .5 * math.sin(phase * 3))
        if feeding:
            tug = math.sin(phase * 2)
            self.rotate("Hips", x=pitch - 7)
            self.rotate("Chest", x=-13 + tug * 2, z=math.sin(phase) * 2)
            for name in necks:
                self.rotate(name, x=(-30 + tug * 6) / len(necks))
            self.rotate("Head", x=-8 + tug * 8, z=math.sin(phase) * 6)
            jaw = spec["shut"] - (spec["shut"] + 15) * (.5 + .5 * math.sin(phase * 2 + 1.2))
        self.rotate("Jaw", x=jaw)
        if airborne:
            self.shift("Hips", height=.017*math.cos(phase))
            for sign in (-1, 1):
                names = self.limbs["wings", sign]
                flap = math.sin(phase) * (33 if clip == "Fly" else 13)
                self.rotate(names[0], y=-sign*(flap+4), z=sign*(2 if clip == "Fly" else 10))
                self.rotate(names[1], y=-sign*(math.sin(phase-.56)*14-8), z=sign*10)
                self.rotate(names[2], y=-sign*math.sin(phase-.87)*14)
                self.rotate(names[3], y=-sign*math.sin(phase-1.18)*9)
            return

        bpy.context.view_layer.update()
        if winged:
            self.folded_wings(t, 1 if running else .63 if moving else 0)
        for (leg, sign), names in self.limbs.items():
            if leg not in ("hind", "fore"):
                continue
            offset = 0 if sign > 0 else .5
            if leg == "fore":
                offset += .25 if not running or species == "Argentinosaurus" else .42
            cycle = (t + offset) % 1
            support = .61 if not running or species == "Argentinosaurus" else .46
            if not moving:
                fore, up, bend = 0, 0, 0
            elif cycle < support:
                fore, up, bend = stride * (1 - 2*cycle/support), 0, 0
            else:
                swing = (cycle-support) / (1-support)
                fore = stride * (2*smooth(0,1,swing)-1)
                bend = math.sin(math.pi*swing)
                up = lift*bend
            if attacking and species == "Velociraptor":
                up, fore, bend = .102*hit, (-.045 if sign>0 else .045)*hit, -hit
            self.place_leg(names, fore, up, bend)

    def lie_down(self, spec, breath):
        """Rest on the belly with the legs folded forward and the head lowered."""
        drop, fore, hind, nod = spec["lie"]
        self.shift("Hips", height=-drop + breath * .002)
        self.rotate("Chest", x=breath * 1.2)
        necks = self.spine[2:-1]
        for name in necks:
            self.rotate(name, x=(-nod + breath) / len(necks), z=7 / len(necks))
        self.rotate("Head", x=nod * .25, z=5)
        self.rotate("Jaw", x=spec["shut"])
        for index, name in enumerate(self.tail):
            self.rotate(name, x=-6 if index == 0 else 0, z=9)
        bpy.context.view_layer.update()
        if ("wings", 1) in self.limbs:
            self.folded_wings(0, 0)
        for (leg, sign), names in self.limbs.items():
            if leg in ("hind", "fore"):
                self.place_leg(names, fore if leg == "fore" else hind, 0, 0)

    def collapse(self, spec, t, kick=0, breath=None):
        """The legs give way, the body rolls onto its right side and the neck, tail and legs settle on the ground."""
        buckle = smooth(0, .34, t)
        fall = smooth(.14, .60, t) ** 1.5
        settle = smooth(.50, 1, t)
        rebound = math.sin(math.pi * smooth(.60, .88, t))
        asleep = breath is not None
        swell = breath or 0
        hip = spec["spine"][0][1]
        flank = spec["hind"][0][0] + .05
        self.shift("Hips", height=-(hip - flank) * (.38 * buckle * (1 - fall) + fall) + rebound * .008 + swell * .0025)
        self.rotate("Hips", y=81 * fall - 5 * rebound, x=-4 * buckle * (1 - fall))
        self.rotate("Chest", z=-4 * settle, x=swell * 1.6 - (7 if asleep else 0))
        neck_droop, tail_droop = spec["droop"]
        rear = math.sin(math.pi * smooth(0, .5, t))
        chain = self.spine[2:]
        for name in chain:
            self.rotate(name, x=(9 * rear - (34 if asleep else 0) + swell) / len(chain),
                        z=-(neck_droop * settle + 5 * rebound) / len(chain))
        for name in self.tail:
            self.rotate(name, z=(tail_droop * settle + 6 * rebound) / len(self.tail),
                        x=-(5 * buckle + (38 if asleep else 0)) / len(self.tail))
        self.rotate("Jaw", x=spec["shut"] if asleep else
                    spec["shut"] - (spec["shut"] + 14) * smooth(.04, .3, t) * (1 - .45 * settle))
        for (part, sign), names in self.limbs.items():
            relax = fall * (1 - .3 * settle)
            if part in ("hind", "fore"):
                forward = 1 if part == "hind" else -1
                curl = 26 * buckle - 8 * settle
                upper = -34 * settle if sign < 0 else 10 * fall
                self.rotate(names[0], x=forward * curl + kick * sign, y=upper)
                self.rotate(names[1], x=-forward * (40 * buckle - 12 * settle))
                for name in names[2:]:
                    self.rotate(name, x=forward * 14 * relax)
            elif part == "arms":
                self.rotate(names[0], x=18 * relax, y=-sign * 14 * relax)
                self.rotate(names[1], x=-20 * relax)
            else:
                fold = smooth(0, .7, t)
                self.rotate(names[0], y=sign * 46 * fold, z=-sign * 18 * fold)
                self.rotate(names[1], y=sign * 30 * fold, z=sign * 34 * fold)
                self.rotate(names[2], z=-sign * 70 * fold)
                self.rotate(names[3], z=-sign * 30 * fold)

    def bake(self, species, spec, clip, duration):
        action = bpy.data.actions.new(clip)
        action.use_fake_user = True
        self.rig.animation_data.action = action
        count = round(duration * FPS)
        for frame in range(count + 1):
            self.frame(species, spec, clip, frame / count)
            for bone in self.pose:
                bone.keyframe_insert("rotation_quaternion", frame=frame + 1, group=bone.name)
                if bone.name == "Hips":
                    bone.keyframe_insert("location", frame=frame + 1, group=bone.name)
        return action


def make_creature(name, spec, source, destination):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source))
    bpy.context.scene.render.fps = FPS
    bpy.context.scene.render.fps_base = 1
    mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
    mesh.name = name
    mesh.data.materials.clear()
    mesh.data.transform(Matrix.Rotation(math.pi, 4, "Z") @ mesh.matrix_world)
    mesh.matrix_world = Matrix.Identity(4)
    positions = [vertex.co for vertex in mesh.data.vertices]
    centered = [(min(p[i] for p in positions) + max(p[i] for p in positions))/2 for i in (0,1)]
    floor = min(p.z for p in positions)
    mesh.data.transform(Matrix.Translation(Vector((-centered[0], -centered[1]-spec["pivot"], -floor))))
    bpy.context.view_layer.objects.active = mesh
    decimate = mesh.modifiers.new("Game mesh", "DECIMATE")
    decimate.ratio = .30 if name == "Quetzalcoatlus" else .27
    decimate.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    rig, landmarks, limbs, spine, tail = create_skeleton(name, spec, mesh)
    bind_mesh(mesh, spec, landmarks, limbs, spine, tail)
    animator = Animator(rig, landmarks, limbs, spine, tail)
    rig.animation_data_create()
    walk, run = spec["gait_values"][:2]
    clips = [("Idle", 1.9), ("Walk", walk), ("Run", run), ("Attack", spec["attack"]),
             ("Crouch", 2.3), ("Sneak", walk * 1.3), ("Death", spec["death"]),
             ("Topple", spec["death"] * 2.1), ("Feed", 2.2), ("Drink", 2.4), ("Court", 2.0), ("Sleep", 4.2)]
    if spec["gait"] == "winged":
        clips.extend([("Fly", 1.02), ("Dive", .86)])
    for clip, duration in clips:
        animator.bake(name, spec, clip, duration)
    rig.animation_data.action = None
    for bone in animator.pose:
        bone.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(destination), use_selection=True, object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        add_leaf_bones=False, bake_anim=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=.06,
    )
    print(f"{name}: {len(mesh.data.polygons)} tris, {len(landmarks)} bones, {[clip for clip, _ in clips]}", flush=True)


if __name__ == "__main__":
    project = Path(__file__).resolve().parent.parent
    output = project / "Assets/JurassicPix/Models/Creatures"
    for animal, definition in SPECIES.items():
        make_creature(animal, definition, project / (definition["source"] + ".fbx"), output / (animal + ".fbx"))
