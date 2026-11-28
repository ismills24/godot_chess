extends "res://common/scene_kit.gd"
## The base of each place's world: what the director (place.gd) reads to walk you in and play,
## and helpers for building outdoor places from code (terrain, rocks, foliage, trees, sun).
## A place sets, in build(): walk_path, first_look, seat_eye, board and opponent.

const ChessSet := preload("res://inn/chess_set.gd")
const Opponent := preload("res://places/common/opponent.gd")
const TERRAIN := preload("res://places/common/terrain.gdshader")
const FOLIAGE := preload("res://places/common/foliage.gdshader")
const ASHLAR := preload("res://places/common/ashlar.gdshader")

## The walk in, on the ground (x, z); its heights come from ground_at().
var walk_path: Array[Vector2] = []
## What you're looking at before you set off.
var first_look := Vector3.ZERO
## Your eyes once you've sat down across the board.
var seat_eye := Vector3.ZERO
var eye_height := 1.64
var board: Node3D
var opponent: Node3D
## What the status line calls them ("The alchemist").
var opponent_name := "Your opponent"
var board_light_color := Color("ffc890")
var board_light_energy := 0.9
var _moving: Array[Node] = []
var _cylinders: Dictionary = {}


## Height of the walkable ground under (x, z).
func ground_at(_x: float, _z: float) -> float:
	return 0.0


func set_reduced_motion(enabled: bool) -> void:
	super(enabled)
	for node in _moving:
		node.set_reduced_motion(enabled)


## Sets the chess set down on a surface at `at` (the surface's height), its light side (the
## side you play from) facing `yaw`.
func _place_board(at: Vector3, yaw: float) -> void:
	var chess := ChessSet.new()
	add_child(chess)
	chess.build()
	chess.position = at
	chess.rotation.y = yaw
	board = chess


## Seats the opponent at `at` (the ground under their seat), facing `yaw`.
func _seat_opponent(spec: Dictionary, at: Vector3, yaw: float) -> void:
	var person := Opponent.new()
	add_child(person)
	person.position = at
	person.rotation.y = yaw
	person.build(spec)
	opponent = person
	_moving.append(person)


## A batched upright cylinder (bottles, jars, posts), drawn with the boxes by _flush_boxes().
## (Branches go in the same batches: _rod() adds a cylinder lying between two points.)
func _cyl(material: Material, at: Vector3, radius: float, height: float) -> void:
	if not _cylinders.has(material):
		_cylinders[material] = []
	_cylinders[material].append(Transform3D(Basis.from_scale(Vector3(radius, height, radius)), at))


## A batched cylinder from `from` to `to`, `radius` thick (branches, rails), flushed with _cyl()'s.
func _rod(material: Material, from: Vector3, to: Vector3, radius: float) -> void:
	if not _cylinders.has(material):
		_cylinders[material] = []
	var y := (to - from).normalized()
	var x := Vector3.RIGHT if absf(y.x) < 0.9 else Vector3.BACK
	var z := x.cross(y).normalized()
	x = y.cross(z).normalized()
	_cylinders[material].append(Transform3D(Basis(x, y, z) * Basis.from_scale(Vector3(radius, from.distance_to(to), radius)),
		(from + to) * 0.5))


func _flush_boxes() -> void:
	super()
	var unit := CylinderMesh.new()
	unit.top_radius = 1.0
	unit.bottom_radius = 1.0
	unit.height = 1.0
	unit.radial_segments = 32
	unit.rings = 1
	for material: Material in _cylinders:
		_scatter(unit, material, _cylinders[material])
	_cylinders.clear()


func _terrain_material(params: Dictionary) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = TERRAIN
	for key: String in params:
		material.set_shader_parameter(key, params[key])
	return material


func _ashlar(params: Dictionary) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = ASHLAR
	for key: String in params:
		material.set_shader_parameter(key, params[key])
	return material


func _foliage(dark: Color, light: Color, sway := 0.04, params := {}) -> ShaderMaterial:
	var material := _animated_material(FOLIAGE)
	material.set_shader_parameter("dark_color", dark)
	material.set_shader_parameter("light_color", light)
	material.set_shader_parameter("sway", sway)
	for key: String in params:
		material.set_shader_parameter(key, params[key])
	return material


## A grid of ground from `corner` (x, z) across `size`, `step` apart, its height at each point
## given by `height` (x, z) -> y. Sampled at twice that density (up to a cap on the grid's size)
## so slopes and silhouettes stay smooth at full resolution.
func _heightfield(corner: Vector2, size: Vector2, step: float, height: Callable, material: Material,
		faceted := false) -> MeshInstance3D:
	var fine := step * 0.5
	var limit := 40000.0 if faceted else 90000.0
	var cells := (size.x / fine) * (size.y / fine)
	if cells > limit:
		fine *= sqrt(cells / limit)
	fine = minf(fine, step)
	var columns := int(ceil(size.x / fine))
	var rows := int(ceil(size.y / fine))
	var heights := PackedFloat32Array()
	heights.resize((columns + 1) * (rows + 1))
	for j in rows + 1:
		for i in columns + 1:
			heights[j * (columns + 1) + i] = height.call(corner.x + size.x * i / columns, corner.y + size.y * j / rows)
	var at := func(i: int, j: int) -> Vector3:
		return Vector3(corner.x + size.x * i / columns, heights[j * (columns + 1) + i], corner.y + size.y * j / rows)
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	if faceted:
		tool.set_smooth_group(-1)
		for j in rows:
			for i in columns:
				var a: Vector3 = at.call(i, j)
				var b: Vector3 = at.call(i + 1, j)
				var c: Vector3 = at.call(i, j + 1)
				var d: Vector3 = at.call(i + 1, j + 1)
				for v: Vector3 in [a, b, c, c, b, d]:
					tool.set_uv(Vector2(v.x, v.z))
					tool.add_vertex(v)
		tool.generate_normals()
	else:
		# Normals straight from the height grid (central differences), smooth across every cell.
		for j in rows + 1:
			for i in columns + 1:
				var v: Vector3 = at.call(i, j)
				var left: Vector3 = at.call(maxi(i - 1, 0), j)
				var right: Vector3 = at.call(mini(i + 1, columns), j)
				var back: Vector3 = at.call(i, maxi(j - 1, 0))
				var front: Vector3 = at.call(i, mini(j + 1, rows))
				tool.set_normal((front - back).cross(right - left).normalized())
				tool.set_uv(Vector2(v.x, v.z))
				tool.add_vertex(v)
		for j in rows:
			for i in columns:
				var k := j * (columns + 1) + i
				for index: int in [k, k + 1, k + columns + 1, k + columns + 1, k + 1, k + columns + 2]:
					tool.add_index(index)
	var mesh := tool.commit()
	var node := _mesh(self, mesh, material, Vector3.ZERO)
	return node


## A lumpy faceted blob of unit radius: rocks, bushes, cloud-like clumps. Built at about twice
## the asked-for segments and rings, with finer noise on top of the big lumps, so rocks show
## chipped planes and bushes a broken outline at full resolution.
static func _lump_mesh(seed: int, roughness: float, faceted := true, segments := 10, rings := 6) -> ArrayMesh:
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	sphere.radial_segments = clampi(segments * 2, 12, 40)
	sphere.rings = clampi(rings * 2, 8, 24)
	var arrays := sphere.get_mesh_arrays()
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	var noise := FastNoiseLite.new()
	noise.seed = seed
	noise.frequency = 0.9
	noise.fractal_octaves = 3
	var detail := FastNoiseLite.new()
	detail.seed = seed + 101
	detail.frequency = 3.2
	detail.fractal_octaves = 3
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_smooth_group(-1 if faceted else 0)
	for index in indices:
		var v := vertices[index]
		var push := 1.0 + roughness * (noise.get_noise_3dv(v * 1.7) + 0.28 * detail.get_noise_3dv(v * 1.7))
		tool.set_uv(Vector2(v.x, v.z))
		tool.add_vertex(v * push)
	tool.generate_normals()
	return tool.commit()


func _rock(at: Vector3, size: Vector3, seed: int, material: Material, angles := Vector3.ZERO) -> MeshInstance3D:
	var node := _mesh(self, _lump_mesh(seed, 0.35, true, 14, 9), material, at)
	node.rotation = angles
	node.scale = size
	return node


## Many copies of `mesh` in one draw: `transforms` places each.
func _scatter(mesh: Mesh, material: Material, transforms: Array, shadows := true) -> MultiMeshInstance3D:
	var batch := MultiMesh.new()
	batch.transform_format = MultiMesh.TRANSFORM_3D
	batch.mesh = mesh
	batch.instance_count = transforms.size()
	for index in transforms.size():
		batch.set_instance_transform(index, transforms[index])
	var node := MultiMeshInstance3D.new()
	node.multimesh = batch
	node.material_override = material
	if not shadows:
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(node)
	return node


static func _blob_at(at: Vector3, size: Vector3, yaw: float) -> Transform3D:
	return Transform3D(Basis(Vector3.UP, yaw).scaled(size), at)


func _sun(pitch_degrees: float, yaw_degrees: float, color: Color, energy: float) -> DirectionalLight3D:
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-pitch_degrees, yaw_degrees, 0)
	sun.light_color = color
	sun.light_energy = energy
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 40.0
	# Tuned for full resolution and 8k shadow maps: tight bias so contact shadows reach the
	# ground, a soft penumbra, and blended cascades so no seam steps across the scene.
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_blend_splits = true
	sun.shadow_bias = 0.03
	sun.shadow_normal_bias = 1.0
	sun.shadow_blur = 1.4
	add_child(sun)
	return sun


## A pine: a leaning trunk with clumps of needles on its upper branches.
func _pine(at: Vector3, height: float, seed: int, bark: Material, needles: Material, clumps: Array) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	var lean := Vector3(rng.randf_range(-0.12, 0.12), 1.0, rng.randf_range(-0.12, 0.12)).normalized()
	var trunk := CylinderMesh.new()
	trunk.top_radius = 0.04 * height / 4.0
	trunk.bottom_radius = 0.11 * height / 4.0
	trunk.height = height
	trunk.radial_segments = 20
	trunk.rings = 6
	var stem := _mesh(self, trunk, bark, at + lean * height * 0.5)
	stem.basis = Basis(Quaternion(Vector3.UP, lean))
	var count := 5 + rng.randi_range(0, 3)
	for k in count:
		var up := rng.randf_range(0.4, 1.0)
		var reach := (1.1 - up) * height * 0.32 + 0.2
		var angle := rng.randf() * TAU
		var centre := at + lean * height * up + Vector3(cos(angle) * reach, rng.randf_range(-0.1, 0.25), sin(angle) * reach)
		var branch_from := at + lean * height * (up - 0.05)
		var elbow := branch_from.lerp(centre, 0.55) + Vector3(0, 0.06, 0)
		_rod(bark, branch_from, elbow, 0.03 * height / 4.0 + 0.008)
		_rod(bark, elbow, centre, 0.02 * height / 4.0 + 0.006)
		var size := (0.45 + 0.35 * (1.0 - up)) * height / 4.0
		clumps.append(_blob_at(centre, Vector3(size * 1.3, size * 0.75, size * 1.2), rng.randf() * TAU))
	clumps.append(_blob_at(at + lean * height * 1.02, Vector3.ONE * 0.45 * height / 4.0, 0.0))


## A palm: a curving ringed trunk and a crown of drooping fronds.
func _palm(at: Vector3, height: float, bend: Vector3, seed: int, bark: Material, frond: Material) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed
	var segments := 14
	var previous := at
	var top := at
	for k in segments:
		var t := float(k + 1) / segments
		var point := at + Vector3(0, height * t, 0) + bend * t * t
		var piece := CylinderMesh.new()
		# Each piece flares at its foot, so the trunk reads as ringed.
		piece.top_radius = 0.1 - 0.035 * t
		piece.bottom_radius = 0.118 - 0.035 * t
		piece.height = previous.distance_to(point) + 0.02
		piece.radial_segments = 20
		piece.rings = 2
		var node := _mesh(self, piece, bark, (previous + point) * 0.5)
		node.basis = Basis(Quaternion(Vector3.UP, (point - previous).normalized()))
		previous = point
		top = point
	var fronds := 9
	for k in fronds:
		var angle := TAU * k / fronds + rng.randf_range(-0.2, 0.2)
		_mesh(self, _frond_mesh(1.6 + rng.randf_range(-0.3, 0.4), rng.randf_range(0.5, 1.0)), frond, top).rotation.y = angle
	var nut := _paint(Color("3a2a14"), 0.7)
	for k in 3:
		var angle := k * 2.1
		var coconut := SphereMesh.new()
		coconut.radius = 0.06
		coconut.height = 0.11
		coconut.radial_segments = 16
		coconut.rings = 8
		_mesh(self, coconut, nut, top + Vector3(cos(angle) * 0.1, -0.08, sin(angle) * 0.1))


## One palm frond along +X: a strip arching up then drooping, its leaflets fanned either side.
static func _frond_mesh(length: float, droop: float) -> ArrayMesh:
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var steps := 18
	var spine: Array[Vector3] = []
	for k in steps + 1:
		var t := float(k) / steps
		spine.append(Vector3(length * t, 0.35 * t - droop * t * t * 1.4, 0))
	for k in steps:
		var t := float(k) / steps
		# Alternate steps reach further, so the edge breaks into separate leaflets.
		var width := 0.45 * sin(PI * (t * 0.85 + 0.1)) * (1.0 if k % 2 == 0 else 0.62)
		var a := spine[k]
		var b := spine[k + 1]
		for side: float in [-1.0, 1.0]:
			# Leaflets hang down from the spine on both sides.
			var a_out := a + Vector3(0.08, -0.12, side * width)
			var b_out := b + Vector3(0.08, -0.12, side * width * 0.9)
			for v: Vector3 in [a, b, a_out, a_out, b, b_out]:
				tool.set_uv(Vector2(t, 0.5 + side * 0.5 * (0.0 if v == a or v == b else 1.0)))
				tool.add_vertex(v)
	tool.generate_normals()
	return tool.commit()


## Distant hills: a ring of curtain from `from_angle` to `to_angle` (radians, 0 = -Z, turning
## toward +X) `radius` away, its ragged top `height` above `base`.
func _ridge(radius: float, from_angle: float, to_angle: float, base: float, height: float, seed: int,
		material: Material, roughness := 0.45) -> void:
	var noise := FastNoiseLite.new()
	noise.seed = seed
	noise.frequency = 1.2
	noise.fractal_octaves = 6
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var steps := 600
	var columns: Array[Array] = []
	for k in steps + 1:
		var angle := lerpf(from_angle, to_angle, float(k) / steps)
		var along := Vector3(sin(angle), 0, -cos(angle))
		var top := base + height * (1.0 - roughness + roughness * (0.5 + 0.9 * noise.get_noise_1d(angle * 6.0)))
		columns.append([along * radius + Vector3(0, base - height * 2.0, 0), along * radius + Vector3(0, top, 0)])
	for k in steps:
		var a: Vector3 = columns[k][0]
		var b: Vector3 = columns[k][1]
		var c: Vector3 = columns[k + 1][0]
		var d: Vector3 = columns[k + 1][1]
		for v: Vector3 in [a, b, c, c, b, d]:
			tool.set_normal(-(v * Vector3(1, 0, 1)).normalized())
			tool.set_uv(Vector2(v.x, v.y))
			tool.add_vertex(v)
	var node := _mesh(self, tool.commit(), material, Vector3.ZERO)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


static func _flat(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.cull_mode = BaseMaterial3D.CULL_DISABLED
	return material
