import bpy
import math
import os
from mathutils import Vector, Quaternion

OUTPUT = r'C:\Users\Berkant\Desktop\Berkant\unity\dropy\Art\LowPolyFlower'
os.makedirs(OUTPUT, exist_ok=True)

# A separate scene preserves any work in the scene that was already open.
scene = bpy.data.scenes.new('Low Poly Flower')
bpy.context.window.scene = scene
flower = bpy.data.collections.new('FLOWER - mesh asset')
studio = bpy.data.collections.new('STUDIO - presentation')
scene.collection.children.link(flower)
scene.collection.children.link(studio)

def material(name, hex_color, roughness=0.72):
    rgb = tuple(int(hex_color[i:i+2], 16) / 255 for i in (0, 2, 4))
    linear = tuple(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in rgb)
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*linear, 1)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value = (*linear, 1)
    bsdf.inputs['Roughness'].default_value = roughness
    return mat

pink = [material('Petal - rose', 'F2779A'),
        material('Petal - light', 'FFABBE'),
        material('Petal - blush', 'F993AF'),
        material('Petal - underside', 'C94E7B')]
green = [material('Leaf - forest', '28754F'),
         material('Leaf - fresh', '57A968'),
         material('Leaf - lime facet', '83BE78')]
gold = [material('Pollen - amber', 'E9A52A'),
        material('Pollen - golden', 'FFD362'),
        material('Pollen - pale', 'FFE39A')]
floor_mat = material('Studio - warm ivory', 'EEE9DF')

def move_to(obj, collection):
    for existing in list(obj.users_collection):
        existing.objects.unlink(obj)
    collection.objects.link(obj)
    return obj

def mesh_object(name, verts, faces, mats, indices=None, collection=flower):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for mat in mats:
        mesh.materials.append(mat)
    if indices:
        for poly, idx in zip(mesh.polygons, indices):
            poly.material_index = idx
    return obj

root = bpy.data.objects.new('LowPoly_Flower', None)
flower.objects.link(root)
root.empty_display_type = 'PLAIN_AXES'
root.empty_display_size = 0.2
root['description'] = 'Faceted pink flower; all geometry uses solid materials, no textures.'

def asset(obj):
    obj.parent = root
    return obj

# A seven-sided, gently curved tapered stem.
stem_points = [Vector(p) for p in [(0, 0, 0.02), (-.08, .015, .65),
                                  (-.13, .04, 1.25), (.035, .02, 2.05),
                                  (.10, .015, 2.65), (0, 0, 3.18)]]
verts, faces, colors = [], [], []
sides = 7
for i, p in enumerate(stem_points):
    tangent = (stem_points[min(i+1, len(stem_points)-1)] - stem_points[max(i-1, 0)]).normalized()
    u = tangent.cross(Vector((0, 1, 0))).normalized()
    v = tangent.cross(u).normalized()
    radius = .064 - .026 * i / (len(stem_points)-1)
    for j in range(sides):
        a = 2 * math.pi * j / sides
        verts.append(p + radius * (math.cos(a)*u + math.sin(a)*v))
for i in range(len(stem_points)-1):
    for j in range(sides):
        faces.append((i*sides+j, i*sides+(j+1)%sides,
                      (i+1)*sides+(j+1)%sides, (i+1)*sides+j))
        colors.append(1 if j in (1, 2, 3) else 0)
faces.extend([tuple(reversed(range(sides))), tuple((len(stem_points)-1)*sides+j for j in range(sides))])
colors.extend([0, 1])
asset(mesh_object('Stem - curved, 7 sides', verts, faces, green, colors))

def leaf(name, start, delta, width):
    start, delta = Vector(start), Vector(delta)
    direction = delta.normalized()
    across = direction.cross(Vector((0, 0, 1))).normalized()
    ridge = across.cross(direction).normalized()
    if ridge.z < 0:
        ridge = -ridge
    verts = [start]
    for t, w in [(.28, width*.83), (.62, width), (.84, width*.51)]:
        center = start + delta*t + Vector((0, 0, math.sin(t*math.pi)*.07))
        verts.extend([center-across*w, center+ridge*.075, center+across*w])
    verts.append(start+delta)
    faces = [(0, 2, 1), (0, 3, 2)]
    indices = [0, 1]
    for a, b in [(1, 4), (4, 7)]:
        faces.extend([(a, b+1, b), (a, a+1, b+1),
                      (a+1, a+2, b+2), (a+1, b+2, b+1)])
        indices.extend([0, 1, 2, 1])
    faces.extend([(7, 8, 10), (8, 9, 10), (0, 1, 4, 7, 10, 9, 6, 3)])
    indices.extend([0, 2, 0])
    return asset(mesh_object(name, verts, faces, green, indices))

leaf('Leaf - left', (-.12, .035, 1.12), (-1.05, -.14, .58), .27)
leaf('Leaf - right', (.025, .02, 1.92), (.99, -.12, .40), .25)
leaf('Leaf - small back', (.08, .02, 2.42), (-.38, .46, .35), .15)

# Bloom tilts toward the viewing side, with visibly raised petal ridges.
center = Vector((0, 0, 3.22))
normal = Vector((0, -.30, .954)).normalized()
bloom_rotation = Vector((0, 0, 1)).rotation_difference(normal)

def bloom_point(p):
    return center + bloom_rotation @ Vector(p)

profile = [(.16, .075, .005), (.44, .23, -.035),
           (.79, .31, -.005), (1.04, .24, .075), (1.18, .075, .15)]
for petal_index in range(9):
    angle = 2*math.pi*petal_index/9 + .08
    rot = Quaternion((0, 0, 1), angle)
    length_scale = 1 + .035*math.sin(petal_index*2.7)
    pv = []
    for x, width, z in profile:
        for y, lift in [(-width, 0), (0, .065), (width, 0)]:
            pv.append(bloom_point(rot @ Vector((x*length_scale, y, z+lift))))
    for x, width, z in profile:
        pv.append(bloom_point(rot @ Vector((x*length_scale, 0, z-.05))))
    pf, pm = [], []
    for ring in range(4):
        a, b = ring*3, (ring+1)*3
        pf.extend([(a, b, b+1), (a, b+1, a+1),
                   (a+1, b+1, b+2), (a+1, b+2, a+2),
                   (a, 15+ring, 16+ring, b),
                   (a+2, b+2, 16+ring, 15+ring)])
        pm.extend([0, 2, 1 if (petal_index+ring)%3 else 2, 2, 3, 3])
    pf.extend([(0, 1, 2, 15), (12, 19, 14, 13)])
    pm.extend([3, 1])
    asset(mesh_object('Petal %02d' % (petal_index+1), pv, pf, pink, pm))

def ico(name, pos, scale, mats, subdivisions=1):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=pos)
    obj = move_to(bpy.context.object, flower)
    obj.name = name
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = bloom_rotation
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for mat in mats:
        obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = False
        poly.material_index = poly.index % len(mats)
    return asset(obj)

ico('Calyx - faceted green base', bloom_point((0, 0, -.075)), (.23, .23, .17), green, 1)
ico('Flower center - golden dome', bloom_point((0, 0, .075)), (.325, .325, .15), gold[:2], 2)
for i in range(13):
    theta = i*2.39996323
    radius = .24 * math.sqrt((i+.5)/13)
    height = .17 + .035*(1-radius/.24)
    ico('Pollen %02d' % (i+1), bloom_point((math.cos(theta)*radius, math.sin(theta)*radius, height)),
        (.045, .045, .035), gold[1:], 1)

# Presentation objects remain separate from the exportable flower collection.
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.015))
floor = move_to(bpy.context.object, studio)
floor.name = 'Studio floor'
floor.data.materials.append(floor_mat)

def aim(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat('-Z', 'Y').to_euler()

camera_data = bpy.data.cameras.new('Flower portrait camera')
camera = bpy.data.objects.new('Flower portrait camera', camera_data)
studio.objects.link(camera)
camera.location = (5.5, -9, 7.6)
aim(camera, (0, 0, 1.85))
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 4.95
camera_data.lens = 50
scene.camera = camera

for name, location, energy, size, color in [
    ('Key - large softbox', (-3.5, -4.5, 7), 650, 4, (1, .89, .80)),
    ('Fill - cool softbox', (4, -1, 5), 400, 4, (.83, .91, 1)),
    ('Rim - upper back', (1, 4, 6), 750, 3, (1, .94, .84))]:
    data = bpy.data.lights.new(name, 'AREA')
    data.energy, data.shape, data.size, data.color = energy, 'DISK', size, color
    obj = bpy.data.objects.new(name, data)
    studio.objects.link(obj)
    obj.location = location
    aim(obj, (0, 0, 2))

scene.world = bpy.data.worlds.new('Flower studio world')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.65, .72, .8, 1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .35
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.cycles.use_denoising = True
scene.render.resolution_x = 960
scene.render.resolution_y = 1080
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.filepath = os.path.join(OUTPUT, 'LowPolyFlower.png')
scene.view_settings.view_transform = 'AgX'

bpy.ops.object.select_all(action='DESELECT')
for obj in flower.objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.shading.type = 'MATERIAL'

triangles = sum(len(p.vertices)-2 for obj in flower.objects if obj.type == 'MESH' for p in obj.data.polygons)
scene['flower_triangles'] = triangles
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUTPUT, 'LowPolyFlower.blend'))
result = {'scene': scene.name, 'objects': len(flower.objects), 'triangles': triangles,
          'blend': bpy.data.filepath, 'preview': scene.render.filepath}
