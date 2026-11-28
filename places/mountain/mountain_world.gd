extends "res://places/common/place_world.gd"
## A mountain top in early winter sun: a great bare slab of weathered grey sandstone, pitted
## with pools of ice, breaking off in a jutting ledge over a drop of hundreds of metres. Below,
## brown wooded valleys and pale fields fold away into ridge after ridge of blue hills under a
## wide streaky sky. Pitch pines and scrub hold on along the right. A hermit sits cross-legged
## near the edge with a board on a flat stone, the whole view behind him.
## Space: you walk toward -Z; the drop is ahead (-Z) and to the left (-X).

const VALLEY_FLOOR := -190.0
const TABLE_AT := Vector2(0.4, -7.9)

const SANDSTONE := preload("res://places/mountain/sandstone.gdshader")
const ICE := preload("res://places/mountain/ice.gdshader")
const CLOTH := preload("res://places/mountain/cloth.gdshader")
const WOOD := preload("res://places/mountain/wood.gdshader")
## Frozen pools in the slab's hollows: (x, z, radius).
const POOLS: Array[Vector3] = [Vector3(-1.2, -2.0, 0.9), Vector3(1.6, -5.2, 0.6), Vector3(-2.8, -6.5, 0.7),
	Vector3(2.8, 1.2, 0.5), Vector3(-0.4, 2.6, 0.8), Vector3(-1.8, -9.0, 0.5), Vector3(-0.42, -7.95, 0.24), Vector3(1.04, -7.84, 0.17)]

var _sandstone := _stone_material({"color_a": Color("6e6c66"), "color_b": Color("545450"), "lichen_color": Color("7a786e"),
	"crust_color": Color("8a8a82"), "lichen": 0.25, "scale": 0.45, "strata": 0.5, "cracks": 0.5, "pits": 0.55,
	"low_color": Color("5a5c58"), "low_height": -1.4})
var _lichen := _stone_material({"color_a": Color("8a8a7c"), "color_b": Color("6a6c62"), "lichen_color": Color("7a8a5a"),
	"crust_color": Color("9c9e92"), "lichen": 0.35, "scale": 0.6, "strata": 0.3, "cracks": 0.4, "pits": 0.3})
var _valley := _terrain_material({"color_a": Color("5a4430"), "color_b": Color("3a3424"), "top_color": Color("a08860"),
	"top_amount": 0.18, "scale": 0.012, "speckle": 0.2})
var _relief := FastNoiseLite.new()
var _rim_noise := FastNoiseLite.new()
var _ice := _stone_material({}, ICE)
var _bark := _paint(Color("3a2e26"), 0.95)
var _needles := _foliage(Color("1a2a14"), Color("4a6a2a"), 0.035, {"leaf_size": 0.07})
var _scrub := _foliage(Color("2a2418"), Color("6a5434"), 0.02, {"leaf_size": 0.06})
var _evergreen := _foliage(Color("1a2a18"), Color("3e5a2a"), 0.015)
var _hawks: Array[Node3D] = []


func build() -> void:
	opponent_name = "The hermit"
	eye_height = 1.64
	board_light_color = Color("fff4e0")
	board_light_energy = 0.25
	walk_path = [Vector2(2.6, 4.2), Vector2(2.0, 0.5), Vector2(1.1, -3.6), Vector2(TABLE_AT.x, -7.32)]
	first_look = Vector3(-260.0, -60.0, -480.0)
	var player_at := Vector2(TABLE_AT.x, -7.32)
	seat_eye = Vector3(player_at.x, ground_at(player_at.x, player_at.y) + 0.96, player_at.y)
	var sun := _sun(30.0, 100.0, Color("fff0dc"), 2.0)
	sun.directional_shadow_max_distance = 30.0
	sun.directional_shadow_split_1 = 0.06
	sun.directional_shadow_split_2 = 0.2
	sun.directional_shadow_split_3 = 0.5
	sun.shadow_blur = 1.2
	sun.shadow_bias = 0.06
	sun.shadow_normal_bias = 2.0
	_relief.seed = 5
	_relief.frequency = 1.6
	_relief.fractal_octaves = 3
	_rim_noise.seed = 9
	_rim_noise.frequency = 7.0
	_rim_noise.fractal_octaves = 3
	_build_slab()
	_build_ledge()
	_build_pools()
	_build_vista()
	_build_trees()
	_build_spot()
	_build_hawks()
	_flush_boxes()
	_animate()


func _animate() -> void:
	super()
	for k in _hawks.size():
		var angle := _elapsed * (0.12 + 0.03 * k) + k * 3.0
		var hawk := _hawks[k]
		hawk.position = Vector3(-40.0 - k * 25.0, 12.0 + k * 6.0, -70.0 - k * 30.0) + Vector3(cos(angle), 0, sin(angle)) * (18.0 + k * 6.0)
		hawk.rotation = Vector3(0, -angle, 0.25)


## Signed distance to the edge of the top (negative past it, over the drop).
func _edge(x: float, z: float) -> float:
	var left := x + 6.0 + 1.5 * sin(z * 0.23) + 0.6 * sin(z * 0.9)
	var ahead := z + 10.6 + 0.8 * sin(x * 0.4) + 3.2 * exp(-pow((x + 2.0) / 1.9, 2.0))
	return minf(left, ahead)


func _rock_height(x: float, z: float) -> float:
	# Weathered rolls and hollows on the slab, rising to the right toward the trees.
	var y := 0.18 * sin(x * 0.9 + z * 0.4) * sin(z * 0.7 - x * 0.2) + 0.1 * sin(x * 2.3 + 1.0) * sin(z * 1.9)
	y += maxf(0.0, x - 3.5) * 0.22 + maxf(0.0, z - 2.0) * 0.05
	return y


func ground_at(x: float, z: float) -> float:
	return _rock_height(x, z)


func _slab_height(x: float, z: float) -> float:
	var e := _edge(x, z)
	var y := _rock_height(x, z)
	# Small weathered knobs and runnels, a few centimetres, that the walk ignores.
	y += 0.03 * _relief.get_noise_2d(x, z)
	y = _pool_hollow(x, z, y)
	if e < 0.0:
		# Over the edge: a rounded lip, then sheer cliff.
		y -= 1.2 * smoothstep(0.0, 0.8, -e) + 80.0 * smoothstep(0.6, 3.0, -e)
	return y


## Where pool `k`'s ice lies: level, a little below the rock at its middle.
func _pool_level(k: int) -> float:
	return _rock_height(POOLS[k].x, POOLS[k].y) - 0.06


## The hollows the pools sit in: the rock dished down to a flat floor under the ice, rising to
## a lobed, ragged rim.
func _pool_hollow(x: float, z: float, y: float) -> float:
	for k in POOLS.size():
		var pool := POOLS[k]
		var d := Vector2(x - pool.x, z - pool.y) / Vector2(pool.z * 1.4, pool.z * 0.8)
		if d.length() > 1.8:
			continue
		var ragged := 1.0 + 0.08 * _rim_noise.get_noise_2d(x, z)
		y = minf(y, _pool_level(k) - 0.01 + 0.16 * smoothstep(0.92, 1.45, d.length() * ragged))
	return y

func _build_slab() -> void:
	_heightfield(Vector2(-12, -18), Vector2(30, 28), 0.08, _slab_height, _sandstone)


func _stone_material(params: Dictionary, shader: Shader = SANDSTONE) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = shader
	for key: String in params:
		material.set_shader_parameter(key, params[key])
	return material


## A smooth weathered boulder (the old faceted lumps read as low-poly at full resolution).
func _boulder(at: Vector3, size: Vector3, seed: int, material: Material, angles := Vector3.ZERO) -> MeshInstance3D:
	var node := _mesh(self, _lump_mesh(seed, 0.35, false, 48, 28), material, at)
	node.rotation = angles
	node.scale = size
	return node


## The jutting ledge: an overhanging lip of rock, its underside cut back into the cliff.
func _build_ledge() -> void:
	var lip := Vector3(-3.2, -0.55, -12.2)
	_boulder(lip, Vector3(2.6, 0.7, 1.8), 31, _lichen, Vector3(0.05, 0.4, 0.04))
	_boulder(lip + Vector3(-1.2, -1.6, 0.6), Vector3(1.8, 1.8, 1.6), 32, _lichen, Vector3(0, 1.1, 0))
	_boulder(lip + Vector3(0.5, -4.5, 1.6), Vector3(2.4, 3.2, 2.2), 33, _lichen)
	# Boulders and broken slabs about the top.
	var spots: Array[Vector4] = [Vector4(5.5, -3.0, 0.8, 41), Vector4(7.0, 1.5, 1.1, 42), Vector4(-4.0, 2.5, 0.6, 43),
		Vector4(3.8, -8.5, 0.7, 44), Vector4(-2.5, -4.0, 0.35, 45)]
	for spot in spots:
		_boulder(Vector3(spot.x, ground_at(spot.x, spot.y) + spot.z * 0.2, spot.y), Vector3(spot.z * 1.5, spot.z * 0.7, spot.z * 1.3),
			int(spot.w), _lichen, Vector3(0, spot.w, 0))


## Shallow hollows in the slab holding frozen rainwater.
func _build_pools() -> void:
	var sheet := _ice_sheet_mesh()
	for pool in POOLS:
		# The ice lies level just above its hollow's flat floor; the ice shader gives it a ragged
		# outline, inside the rock's rim (the slab's grid is too coarse to cut one cleanly).
		var y := _pool_level(POOLS.find(pool)) - 0.006
		var node := _mesh(self, sheet, _ice, Vector3(pool.x, y, pool.y))
		node.scale = Vector3(pool.z * 1.4, 1.0, pool.z * 0.8)
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## A flat disc of unit radius, finely divided so the hollow's rim cuts it cleanly.
static func _ice_sheet_mesh() -> ArrayMesh:
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var around := 64
	var rings := 6
	for r in rings:
		for k in around:
			var corners: Array[Vector3] = []
			for c: Vector2i in [Vector2i(0, 0), Vector2i(0, 1), Vector2i(1, 0), Vector2i(1, 0), Vector2i(0, 1), Vector2i(1, 1)]:
				var radius := float(r + c.x) / rings
				var angle := TAU * (k + c.y) / around
				corners.append(Vector3(cos(angle) * radius, 0, sin(angle) * radius))
			for v in corners:
				tool.set_normal(Vector3.UP)
				tool.add_vertex(v)
	return tool.commit()


## Far below: valleys of bare brown woods and pale fields, then ridge after ridge of hills
## going blue into the distance.
func _valley_height(x: float, z: float) -> float:
	var y := VALLEY_FLOOR + 35.0 * sin(x * 0.006 + 1.0) * sin(z * 0.005) + 18.0 * sin(x * 0.017 + z * 0.011)
	y += 60.0 * smoothstep(-250.0, -600.0, z) * (0.5 + 0.5 * sin(x * 0.004))
	# Steep wooded slopes falling from the mountain's foot.
	var near := maxf(0.0, 1.0 - Vector2(x, z + 20.0).length() / 220.0)
	return y + near * near * 160.0


func _build_vista() -> void:
	_heightfield(Vector2(-900, -900), Vector2(1100, 1000), 22.0, _valley_height, _valley)
	# Fields: pale patches on the valley floor.
	var field := _paint(Color("b0986a"), 1.0)
	var rng := RandomNumberGenerator.new()
	rng.seed = 12
	for k in 26:
		var x := rng.randf_range(-500.0, 80.0)
		var z := rng.randf_range(-500.0, -150.0)
		_box(field, Vector3(x, _valley_height(x, z) + 1.5, z), Vector3(rng.randf_range(20, 60), 1.0, rng.randf_range(15, 45)),
			Vector3(0, rng.randf() * TAU, 0))
	# Ridges into the distance, each paler and bluer.
	_ridge(1100.0, -2.4, 1.2, -120.0, 140.0, 3, _flat(Color("566070")), 0.5)
	_ridge(1700.0, -2.4, 1.3, -80.0, 170.0, 4, _flat(Color("7a8aa0")), 0.6)
	_ridge(2400.0, -2.6, 1.4, -40.0, 160.0, 5, _flat(Color("9aaac0")), 0.55)


func _build_trees() -> void:
	var clumps: Array = []
	var spots: Array[Vector3] = [Vector3(6.0, 0, -1.0), Vector3(8.5, 0, -4.5), Vector3(7.2, 0, 3.5),
		Vector3(10.5, 0, 0.5), Vector3(4.8, 0, -6.8), Vector3(12.0, 0, -6.0), Vector3(9.5, 0, 6.5), Vector3(5.6, 0, 7.0)]
	for k in spots.size():
		var at := spots[k]
		at.y = ground_at(at.x, at.z) - 0.1
		_pine(at, 4.0 + (k % 3) * 1.4, k + 7, _bark, _needles, clumps)
	_scatter(_lump_mesh(17, 0.5, false, 36, 20), _needles, clumps)
	# Low evergreen scrub and dry winter brush along the right and the edges.
	var scrub: Array = []
	var brush: Array = []
	var rng := RandomNumberGenerator.new()
	rng.seed = 8
	for k in 70:
		var x := rng.randf_range(3.0, 14.0)
		var z := rng.randf_range(-10.0, 9.0)
		var size := rng.randf_range(0.35, 0.9)
		scrub.append(_blob_at(Vector3(x, ground_at(x, z) + size * 0.25, z), Vector3(size * 1.4, size * 0.7, size * 1.2), rng.randf() * TAU))
	for k in 45:
		var x := rng.randf_range(-6.0, 6.0)
		var z := rng.randf_range(-10.0, 8.0)
		if _edge(x, z) < 1.0 or Vector2(x - TABLE_AT.x, z - TABLE_AT.y).length() < 2.0 or absf(x - 1.5) < 1.3:
			continue
		var size := rng.randf_range(0.2, 0.45)
		brush.append(_blob_at(Vector3(x, ground_at(x, z) + size * 0.3, z), Vector3(size, size * 0.9, size), rng.randf() * TAU))
	_scatter(_lump_mesh(19, 0.5, false, 32, 18), _evergreen, scrub)
	_scatter(_lump_mesh(23, 0.7, false, 24, 14), _scrub, brush)


## The hermit's spot near the edge: a flat stone for the board, a staff, a bowl and a bundle.
func _build_spot() -> void:
	var stone_at := Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, TABLE_AT.y), TABLE_AT.y)
	# A flat-topped stone, its edges worn round, sunk a little into the slab.
	var stone := _mesh(self, _pillow_mesh(61, 0.14, 0.08, 96, 48), _lichen, stone_at + Vector3(0, 0.06, 0))
	stone.scale = Vector3(0.33, 0.12, 0.32)
	stone.rotation.y = 0.1
	_place_board(stone_at + Vector3(0, 0.18, 0), 0.0)
	var seat := Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, TABLE_AT.y - 0.66), TABLE_AT.y - 0.66)
	_seat_opponent({
		"seat": "ground", "height": 0.96, "girth": 0.88,
		"skin": Color("a87a58"), "coat": Color("6a5a44"), "legs": Color("5a4a38"), "robe": true,
		"hat": "none", "hair": Color("e0dcd4"), "bald": true,
		"beard": "long", "beard_color": Color("ece8e0"),
		"extras": ["beads"], "phase": 3.3,
	}, seat, 0.0)
	# His staff, a knotty pole leaning by him.
	var pole := CylinderMesh.new()
	pole.top_radius = 0.018
	pole.bottom_radius = 0.024
	pole.height = 1.8
	pole.radial_segments = 16
	pole.rings = 4
	var staff := _mesh(self, pole, _paint(Color("5a4028"), 0.85), seat + Vector3(-0.55, 0.85, 0.1))
	staff.rotation = Vector3(0.1, 0, 0.35)
	# A turned wooden bowl.
	var bowl := _mesh(self, _bowl_mesh(), _stone_material({"light_color": Color("7a5636"), "dark_color": Color("4e3420")}, WOOD), seat + Vector3(0.55, ground_at(seat.x + 0.55, seat.z + 0.35) - seat.y, 0.35))
	bowl.rotation.y = 0.7
	# His bundle, wrapped in wool.
	var bundle := _mesh(self, _lump_mesh(27, 0.3, false, 40, 24), _cloth(Color("5a4a34"), Color("43372a")), seat + Vector3(-0.6, 0.12, -0.3))
	bundle.scale = Vector3(0.3, 0.2, 0.25)
	bundle.rotation.y = 0.4
	# A cushion of folded blanket for you, across the board.
	var cushion := _mesh(self, _pillow_mesh(5, 0.35, 0.02, 48, 24), _cloth(Color("7a3a2a"), Color("4e241a")),
		Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, -7.3) + 0.035, -7.3))
	cushion.scale = Vector3(0.25, 0.04, 0.2)


func _cloth(base: Color, stripe: Color) -> ShaderMaterial:
	var material := _stone_material({"base_color": base, "stripe_color": stripe}, CLOTH)
	return material


## A unit rounded box: a sphere pushed out toward a cube (smaller `roundness` = sharper edges),
## its outline wobbled by up to `wobble` so it reads as worn stone or stuffed cloth.
static func _pillow_mesh(seed: int, roundness: float, wobble: float, segments: int, rings: int) -> ArrayMesh:
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	sphere.radial_segments = segments
	sphere.rings = rings
	var arrays := sphere.get_mesh_arrays()
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	var noise := FastNoiseLite.new()
	noise.seed = seed
	noise.frequency = 0.7
	noise.fractal_octaves = 3
	var shape := func(v: float) -> float: return signf(v) * pow(absf(v), roundness)
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_smooth_group(0)
	for index in indices:
		var v := vertices[index]
		var p := Vector3(shape.call(v.x), shape.call(v.y), shape.call(v.z))
		# Wobble the sides more than the top, which stays flat.
		var side := Vector2(p.x, p.z)
		side *= 1.0 + wobble * noise.get_noise_3d(p.x * 2.0, p.y * 0.6, p.z * 2.0)
		var top := p.y + wobble * 0.3 * noise.get_noise_2d(p.x * 3.0 + 9.0, p.z * 3.0) * (1.0 - absf(p.y))
		tool.set_uv(Vector2(p.x, p.z))
		tool.add_vertex(Vector3(side.x, top, side.y))
	tool.generate_normals()
	return tool.commit()


## A shallow turned bowl, about 17 cm across, lathed from its profile.
static func _bowl_mesh() -> ArrayMesh:
	var profile: Array[Vector2] = [Vector2(0.0, 0.0), Vector2(0.04, 0.0), Vector2(0.045, 0.006), Vector2(0.065, 0.02),
		Vector2(0.079, 0.042), Vector2(0.086, 0.062), Vector2(0.087, 0.068), Vector2(0.082, 0.069),
		Vector2(0.079, 0.062), Vector2(0.072, 0.042), Vector2(0.058, 0.024), Vector2(0.03, 0.013), Vector2(0.0, 0.011)]
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_smooth_group(0)
	var around := 48
	for k in around:
		var a0 := TAU * k / around
		var a1 := TAU * (k + 1) / around
		for j in profile.size() - 1:
			var p := profile[j]
			var q := profile[j + 1]
			var v00 := Vector3(cos(a0) * p.x, p.y, sin(a0) * p.x)
			var v01 := Vector3(cos(a1) * p.x, p.y, sin(a1) * p.x)
			var v10 := Vector3(cos(a0) * q.x, q.y, sin(a0) * q.x)
			var v11 := Vector3(cos(a1) * q.x, q.y, sin(a1) * q.x)
			for v: Vector3 in [v00, v10, v01, v01, v10, v11]:
				tool.add_vertex(v)
	tool.generate_normals()
	return tool.commit()


func _build_hawks() -> void:
	var feathers := _paint(Color("2a2420"), 0.9)
	for k in 2:
		var hawk := Node3D.new()
		add_child(hawk)
		_box_mesh(hawk, feathers, Vector3.ZERO, Vector3(1.6, 0.05, 0.3))
		_box_mesh(hawk, feathers, Vector3(0, 0, 0.1), Vector3(0.14, 0.1, 0.6))
		_hawks.append(hawk)
