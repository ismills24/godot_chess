extends "res://places/common/place_world.gd"
## An old sewer outfall in a wooded park, high summer: a sunken cobbled lane runs down between
## mossy retaining walls to a stone portal set into the hillside, its round-arched doorway
## barred by an iron lattice gate. The banks either side are smothered in nettles and ivy,
## tall trees close overhead and the sun comes through the leaves in patches. The rat-catcher
## sits on a crate before the gate with his cage of rats, a lantern hung up by the door and a
## board on another crate, and he's glad of the company.
## Space: you walk toward -Z, downhill, to the portal's face at z = PORTAL.

const CRATE := preload("res://inn/props/crate.glb")
const BUCKET := preload("res://inn/props/bucket.glb")
const BARREL := preload("res://inn/props/barrel.glb")
const PLANK := preload("res://places/sewer/weathered_plank.gdshader")

const PORTAL := -13.2
const LANE_HALF := 1.45
const WALLS_FROM := -7.6
const TABLE_AT := Vector2(0.25, -10.95)

var _stone := _ashlar({"stone_color": Color("8a8676"), "stone_color_b": Color("6e6c60"),
	"mortar_color": Color("4a4a40"), "moss_color": Color("4a5a24"), "moss": 0.55, "damp_line": -0.2,
	"block_size": Vector2(0.7, 0.36), "streaks": 0.7})
var _dressed := _ashlar({"stone_color": Color("9a9686"), "stone_color_b": Color("807e70"),
	"mortar_color": Color("55554a"), "moss_color": Color("566a2a"), "moss": 0.4,
	"block_size": Vector2(1.0, 0.5), "streaks": 0.6})
var _cobbles := _ashlar({"stone_color": Color("7a7468"), "stone_color_b": Color("5a564c"),
	"mortar_color": Color("3a382e"), "moss_color": Color("4a5a24"), "moss": 0.35,
	"block_size": Vector2(0.22, 0.17), "joint": 0.025, "streaks": 0.0})
var _brick := _ashlar({"stone_color": Color("8a4a34"), "stone_color_b": Color("6a3626"),
	"mortar_color": Color("6a6458"), "moss_color": Color("4a5a24"), "moss": 0.45,
	"block_size": Vector2(0.28, 0.1), "joint": 0.012, "streaks": 0.3})
var _bank := _terrain_material({"color_a": Color("3a4a1e"), "color_b": Color("2a3616"), "top_color": Color("4a6a20"),
	"top_amount": 0.4, "scale": 0.5, "speckle": 0.5})
var _leaves := _foliage(Color("1a3010"), Color("6aa030"), 0.03, {"leaf_size": 0.08})
var _nettles := _foliage(Color("203a12"), Color("5a9a2a"), 0.04, {"leaf_size": 0.06})
var _canopy := _foliage(Color("18300e"), Color("70a838"), 0.06, {"leaf_size": 0.14})
var _bark := _paint(Color("4a3e32"), 0.95)
var _iron := _paint(Color("1e1c1a"), 0.55, 0.6)
var _rust := _paint(Color("5a3420"), 0.8, 0.3)
var _dark := _paint(Color("060706"), 1.0)
var _midges: MultiMesh
var _lantern_light: OmniLight3D


func build() -> void:
	opponent_name = "The rat-catcher"
	eye_height = 1.64
	board_light_color = Color("ffd8a0")
	board_light_energy = 0.6
	walk_path = [Vector2(-0.1, 5.2), Vector2(0.15, 0.5), Vector2(0.4, -5.0), Vector2(TABLE_AT.x, -10.32)]
	first_look = Vector3(0.0, ground_at(0, PORTAL) + 1.4, PORTAL)
	seat_eye = Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, -10.32) + 1.0, -10.32)
	var sun := _sun(58.0, -20.0, Color("fff2d0"), 2.2)
	sun.shadow_blur = 1.2
	_build_lane()
	_build_walls()
	_build_portal()
	_build_banks()
	_build_trees()
	_build_brick_wall()
	_build_camp()
	_build_midges()
	_flush_boxes()
	_animate()


func _animate() -> void:
	super()
	if _lantern_light:
		_lantern_light.light_energy = 0.7 + 0.06 * sin(_elapsed * 9.0) + 0.04 * sin(_elapsed * 21.0)
	if _midges:
		for i in _midges.instance_count:
			var swarm := Vector3(0.3 if i < 20 else -0.8, ground_at(0, -9.0) + 1.6, -9.0 if i < 20 else -5.0)
			var at := swarm + Vector3(sin(_elapsed * (1.3 + i * 0.11) + i) * 0.35, sin(_elapsed * (1.7 + i * 0.07) + i * 2.0) * 0.25,
				cos(_elapsed * (1.1 + i * 0.13) + i * 0.5) * 0.35)
			_midges.set_instance_transform(i, Transform3D(Basis.from_scale(Vector3.ONE * 0.012), at))


func ground_at(_x: float, z: float) -> float:
	return -0.045 * maxf(0.0, 6.0 - z) if z < 6.0 else 0.0


## How high the retaining walls stand over the lane at `z`: rising toward the portal.
func _wall_height(z: float) -> float:
	return lerpf(0.95, 1.45, smoothstep(WALLS_FROM, PORTAL, z))


func _build_lane() -> void:
	# The lane in strips so it follows the slope down.
	var z := 9.0
	while z > PORTAL - 0.2:
		var y := ground_at(0, z - 0.5)
		_box(_cobbles, Vector3(0, y - 0.06, z - 0.5), Vector3(LANE_HALF * 2.0 + 0.4, 0.12, 1.02), Vector3(-0.045, 0, 0))
		z -= 1.0
	# A shallow gutter down the middle, wet.
	var wet := _paint(Color("2a2c22"), 0.15)
	z = WALLS_FROM
	while z > PORTAL:
		var y := ground_at(0, z - 0.5)
		_box(wet, Vector3(0.05, y + 0.002, z - 0.5), Vector3(0.18, 0.01, 1.0), Vector3(-0.045, 0, 0))
		z -= 1.0


func _build_walls() -> void:
	for side: int in [-1, 1]:
		var x := side * (LANE_HALF + 0.22)
		var z := WALLS_FROM
		while z > PORTAL:
			var foot := ground_at(0, z - 0.25)
			var height := _wall_height(z - 0.25)
			_box(_stone, Vector3(x, foot + height * 0.5 - 0.1, z - 0.25), Vector3(0.44, height + 0.2, 0.52))
			_box(_dressed, Vector3(x, foot + height + 0.06, z - 0.25), Vector3(0.56, 0.12, 0.52))
			z -= 0.5
		# The square pier that ends each wall, with its thick cap.
		var foot := ground_at(0, WALLS_FROM)
		_box(_dressed, Vector3(x, foot + 0.55, WALLS_FROM + 0.1), Vector3(0.7, 1.3, 0.7))
		_box(_dressed, Vector3(x, foot + 1.26, WALLS_FROM + 0.1), Vector3(0.82, 0.16, 0.82))
		_box(_dressed, Vector3(x, foot + 1.38, WALLS_FROM + 0.1), Vector3(0.66, 0.1, 0.66))


## The portal: a stone face set into the hill with a round-arched doorway and an iron gate.
func _build_portal() -> void:
	var foot := ground_at(0, PORTAL)
	var width := 4.6
	var height := 3.2
	var arch_half := 0.62
	var spring := 1.55
	var rows := int(ceil((spring + arch_half) / 0.1))
	for row in rows:
		var y := row * 0.1 + 0.05
		var half := arch_half
		if y > spring:
			var rise := y - spring
			half = sqrt(maxf(arch_half * arch_half - rise * rise, 0.0))
		for side: int in [-1, 1]:
			var from := half
			_box(_stone, Vector3(side * (from + width * 0.5) * 0.5, foot + y, PORTAL - 0.4), Vector3(width * 0.5 - from, 0.1, 0.8))
	var solid := rows * 0.1
	_box(_stone, Vector3(0, foot + (solid + height) * 0.5, PORTAL - 0.4), Vector3(width, height - solid, 0.8))
	# A heavy lintel course and a coping on top, stepping back into the bank.
	_box(_dressed, Vector3(0, foot + height + 0.12, PORTAL - 0.35), Vector3(width + 0.3, 0.24, 0.95))
	_box(_dressed, Vector3(0, foot + height + 0.34, PORTAL - 0.6), Vector3(width + 0.2, 0.2, 0.9))
	# Voussoirs round the arch.
	for k in 11:
		var angle := PI * (k + 0.5) / 11.0
		var at := Vector3(-cos(angle) * (arch_half + 0.15), foot + spring + sin(angle) * (arch_half + 0.15), PORTAL + 0.02)
		_box(_dressed, at, Vector3(0.22, 0.2, 0.06), Vector3(0, 0, angle - PI * 0.5))
	# Pilasters either side of the doorway.
	for side: int in [-1, 1]:
		_box(_dressed, Vector3(side * 1.25, foot + 1.3, PORTAL + 0.05), Vector3(0.36, 2.6, 0.14))
	# Darkness behind the gate.
	_box(_dark, Vector3(0, foot + 1.0, PORTAL - 1.0), Vector3(arch_half * 2.0, 2.4, 0.4))
	# The lattice gate: diagonal iron bars in a square frame under the arch.
	var gate := Node3D.new()
	gate.position = Vector3(0, foot, PORTAL - 0.12)
	add_child(gate)
	var bar_h := spring + arch_half - 0.05
	for side: int in [-1, 1]:
		_box_mesh(gate, _iron, Vector3(side * (arch_half - 0.03), bar_h * 0.5, 0), Vector3(0.05, bar_h, 0.05))
	for y: float in [0.05, spring, bar_h * 0.5]:
		_box_mesh(gate, _iron, Vector3(0, y, 0), Vector3(arch_half * 2.0, 0.04, 0.04))
	for k in range(-6, 7):
		for slope: int in [-1, 1]:
			var bar := _box_mesh(gate, _iron if k % 3 else _rust, Vector3(k * 0.16, bar_h * 0.5, 0.01 * slope), Vector3(0.018, bar_h * 1.5, 0.018))
			bar.rotation.z = slope * PI * 0.25
	# Clip the bars that stick out past the frame by hiding them behind the portal face; the
	# stone either side of the opening covers the overhang.
	# Water stains under the threshold and a dribble across the lane.
	_box(_paint(Color("1e2418"), 0.3), Vector3(0, foot + 0.005, PORTAL + 0.3), Vector3(1.4, 0.01, 0.6))


## Height of the banks over the lane: smothered slopes up from the wall tops to the woods.
func _bank_height(x: float, z: float) -> float:
	var ax := absf(x)
	var lane := ground_at(0, z)
	var wall := _wall_height(z) if z < WALLS_FROM else 0.0
	var y := lane - 0.3
	if ax > LANE_HALF:
		var out := ax - LANE_HALF
		var front_rise := smoothstep(WALLS_FROM + 3.0, WALLS_FROM - 1.0, z) * wall
		y = lane + front_rise + out * 0.55 + 0.2 * sin(x * 1.3 + z * 0.7)
		if z > WALLS_FROM:
			# Beyond the walls the banks come down to the lane edge.
			y = lane + minf(out, 6.0) * lerpf(0.55, 0.25, smoothstep(WALLS_FROM, 6.0, z)) + 0.15 * sin(x + z)
	if z < PORTAL - 0.6:
		y = maxf(y, ground_at(0, PORTAL) + 3.6 + (PORTAL - 0.6 - z) * 0.35)
	return y


func _build_banks() -> void:
	_heightfield(Vector2(-16, -34), Vector2(32, 46), 0.5, _bank_height, _bank)
	# Nettles, ivy and ferns heaped thick over the banks and spilling over the wall tops.
	var growth: Array = []
	var rng := RandomNumberGenerator.new()
	rng.seed = 9
	for k in 700:
		var x := rng.randf_range(-14.0, 14.0)
		var z := rng.randf_range(-30.0, 10.0)
		if absf(x) < LANE_HALF + 0.3:
			continue
		var size := rng.randf_range(0.35, 0.9)
		growth.append(_blob_at(Vector3(x, _bank_height(x, z) + size * 0.25, z), Vector3(size * 1.3, size * 0.75, size * 1.3), rng.randf() * TAU))
	# Over the portal's coping and hanging down its face.
	for k in 40:
		var x := rng.randf_range(-2.6, 2.6)
		var size := rng.randf_range(0.3, 0.6)
		growth.append(_blob_at(Vector3(x, ground_at(0, PORTAL) + 3.6 + rng.randf_range(0.0, 0.4), PORTAL - rng.randf_range(0.3, 1.4)),
			Vector3(size * 1.2, size * 0.8, size), rng.randf() * TAU))
	_scatter(_lump_mesh(13, 0.5, false), _nettles, growth)
	# Ivy along the wall faces.
	var ivy: Array = []
	for side: int in [-1, 1]:
		for k in 26:
			var z := rng.randf_range(PORTAL + 0.3, WALLS_FROM)
			var y := ground_at(0, z) + rng.randf_range(0.5, _wall_height(z) + 0.1)
			var size := rng.randf_range(0.15, 0.35)
			ivy.append(_blob_at(Vector3(side * (LANE_HALF - 0.02), y, z), Vector3(size * 0.4, size, size * 1.4), 0.0))
	# Ferns along the foot of the walls, on the lane edge.
	for k in 30:
		var side := -1 if k % 2 == 0 else 1
		var z := rng.randf_range(PORTAL + 0.5, 4.0)
		var size := rng.randf_range(0.15, 0.3)
		ivy.append(_blob_at(Vector3(side * (LANE_HALF - 0.1), ground_at(0, z) + 0.05, z), Vector3(size * 1.4, size * 0.6, size * 1.4), rng.randf()))
	_scatter(_lump_mesh(15, 0.6, false, 8, 5), _leaves, ivy)


## Tall trees on the banks, their crowns closing over the lane.
func _build_trees() -> void:
	var crowns: Array = []
	var rng := RandomNumberGenerator.new()
	rng.seed = 4
	var spots: Array[Vector2] = [Vector2(-4.5, -4.0), Vector2(5.0, -9.0), Vector2(-6.0, -14.0), Vector2(3.5, -17.0),
		Vector2(-3.5, 3.5), Vector2(6.5, 1.0), Vector2(-8.0, -8.0), Vector2(8.5, -14.0), Vector2(0.5, -20.0)]
	for spot in spots:
		var foot := Vector3(spot.x, _bank_height(spot.x, spot.y) - 0.2, spot.y)
		var height := rng.randf_range(8.0, 11.0)
		var lean := Vector3(-signf(spot.x) * rng.randf_range(0.6, 1.6), height, rng.randf_range(-0.5, 0.5))
		var trunk := CylinderMesh.new()
		trunk.top_radius = 0.12
		trunk.bottom_radius = 0.3
		trunk.height = lean.length()
		trunk.radial_segments = 7
		var node := _mesh(self, trunk, _bark, foot + lean * 0.5)
		node.basis = Basis(Quaternion(Vector3.UP, lean.normalized()))
		var top := foot + lean
		for k in 7:
			var offset := Vector3(rng.randf_range(-2.8, 2.8), rng.randf_range(-1.6, 1.2), rng.randf_range(-2.8, 2.8))
			var size := rng.randf_range(1.6, 2.8)
			crowns.append(_blob_at(top + offset, Vector3(size * 1.2, size * 0.7, size * 1.2), rng.randf() * TAU))
	_scatter(_lump_mesh(29, 0.55, false), _canopy, crowns)


## A low old brick wall along the right at the top of the lane.
func _build_brick_wall() -> void:
	var z := 6.0
	while z > -2.0:
		var y := ground_at(0, z)
		_box(_brick, Vector3(LANE_HALF + 0.6, y + 0.35, z - 0.25), Vector3(0.32, 0.9, 0.52))
		_box(_brick, Vector3(LANE_HALF + 0.6, y + 0.84, z - 0.25), Vector3(0.38, 0.08, 0.52))
		z -= 0.5


## The rat-catcher's spot before the gate.
func _build_camp() -> void:
	var foot := ground_at(0, TABLE_AT.y)
	_hd_crate(Vector3(TABLE_AT.x, foot - 0.02, TABLE_AT.y), 0.56, 0.1, 5)
	_place_board(Vector3(TABLE_AT.x, foot + 0.54, TABLE_AT.y), 0.0)
	var seat := Vector3(TABLE_AT.x, ground_at(0, TABLE_AT.y - 0.66), TABLE_AT.y - 0.66)
	_hd_crate(seat - Vector3(0, 0.02, 0), 0.46, 0.5, 4)
	_seat_opponent({
		"seat": "chair", "seat_height": 0.46, "height": 0.96, "girth": 0.95,
		"skin": Color("b88a68"), "coat": Color("4a3a28"), "sleeves": Color("4a3a28"), "legs": Color("3a3224"),
		"hat": "cap", "hat_color": Color("3a3428"), "hair": Color("5a3a22"),
		"beard": "stubble", "beard_color": Color("5a3a22"),
		"extras": ["scarf", "eyepatch", "belt_pouches"], "scarf_color": Color("6a5a2a"), "phase": 4.1,
	}, seat, 0.0)
	# Your seat: an upturned bucket.
	_place_prop(BUCKET, Vector3(TABLE_AT.x, ground_at(0, -10.3), -10.3), 0.42, 0.0)
	# The rat cage beside him with its prisoners.
	var cage_at := Vector3(TABLE_AT.x + 0.85, ground_at(0, TABLE_AT.y - 0.5), TABLE_AT.y - 0.5)
	_box(_dark.duplicate(), cage_at + Vector3(0, 0.01, 0), Vector3(0.6, 0.02, 0.4))
	for x in 7:
		_box(_iron, cage_at + Vector3(-0.3 + x * 0.1, 0.2, 0.2), Vector3(0.012, 0.4, 0.012))
		_box(_iron, cage_at + Vector3(-0.3 + x * 0.1, 0.2, -0.2), Vector3(0.012, 0.4, 0.012))
	for z in 5:
		_box(_iron, cage_at + Vector3(0.3, 0.2, -0.2 + z * 0.1), Vector3(0.012, 0.4, 0.012))
		_box(_iron, cage_at + Vector3(-0.3, 0.2, -0.2 + z * 0.1), Vector3(0.012, 0.4, 0.012))
	_box(_iron, cage_at + Vector3(0, 0.4, 0), Vector3(0.62, 0.02, 0.42))
	var fur := _paint(Color("5a5048"), 0.95)
	for k in 3:
		var rat := cage_at + Vector3(-0.15 + k * 0.14, 0.05, -0.05 + (k % 2) * 0.1)
		_box(fur, rat, Vector3(0.06, 0.06, 0.13), Vector3(0, k * 0.8, 0))
		_box(_paint(Color("b08070"), 0.8), rat + Vector3(0, 0.0, -0.1), Vector3(0.012, 0.012, 0.12), Vector3(0, k * 0.8, 0))
	# His long-handled noose pole against the wall, a sack, and a lantern on a hook.
	_box(_bark, Vector3(-LANE_HALF + 0.1, ground_at(0, -11.4) + 1.0, -11.4), Vector3(0.04, 2.0, 0.04), Vector3(0, 0, 0.12))
	_scatter(_lump_mesh(37, 0.35, true), _paint(Color("8a7a5a"), 1.0),
		[_blob_at(Vector3(-0.8, ground_at(0, -12.0) + 0.22, -12.0), Vector3(0.3, 0.32, 0.25), 0.0)])
	var lamp_at := Vector3(-0.95, ground_at(0, PORTAL) + 2.1, PORTAL + 0.18)
	_box(_iron, lamp_at + Vector3(0, 0.2, -0.08), Vector3(0.02, 0.02, 0.2))
	_box(_iron, lamp_at + Vector3(0, 0.12, 0), Vector3(0.14, 0.03, 0.14))
	_box(_glowing(Color("ffb850"), 3.0), lamp_at, Vector3(0.1, 0.16, 0.1))
	_box(_iron, lamp_at + Vector3(0, -0.1, 0), Vector3(0.14, 0.03, 0.14))
	_lantern_light = _omni(lamp_at + Vector3(0, 0, 0.2), Color("ffb060"), 0.7, 3.5)
	_place_prop(BARREL, Vector3(1.05, ground_at(0, -12.5), -12.5), 0.7, 0.3)


func _build_midges() -> void:
	var dot := BoxMesh.new()
	dot.size = Vector3.ONE
	_midges = MultiMesh.new()
	_midges.transform_format = MultiMesh.TRANSFORM_3D
	_midges.mesh = dot
	_midges.instance_count = 36
	var node := MultiMeshInstance3D.new()
	node.multimesh = _midges
	node.material_override = _glowing(Color("fff0c0"), 1.5)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(node)


## A crate whose lid is rebuilt as real bevelled planks in a procedural wood: the prop's 1k
## texture smears across the screen in the play view, where the table crate fills the frame.
func _hd_crate(at: Vector3, height: float, yaw: float, planks: int) -> void:
	var crate := _place_prop(CRATE, at, height, yaw)
	var lid: MeshInstance3D
	for mesh: MeshInstance3D in crate.find_children("*", "MeshInstance3D", true, false):
		if "lid" in mesh.name.to_lower():
			lid = mesh
	if not lid:
		return
	# The lid's box in the crate's own (unscaled, unrotated) frame.
	var to_crate := crate.global_transform.affine_inverse() * lid.global_transform
	var box := to_crate * lid.get_aabb()
	lid.visible = false
	var factor := crate.basis.get_scale().x
	var size := box.size * factor
	var top := crate.global_transform * Vector3(box.get_center().x, box.end.y, box.get_center().z)
	var frame := Node3D.new()
	frame.position = top
	frame.rotation.y = yaw
	add_child(frame)
	# Planks run along the crate's longer side.
	var along := size.x >= size.z
	var length := size.x if along else size.z
	var span := size.z if along else size.x
	var thick := clampf(size.y, 0.018, 0.03)
	var gap := 0.008
	var width := (span - gap * (planks - 1)) / planks
	var rng := RandomNumberGenerator.new()
	rng.seed = planks * 31 + int(at.z * 100.0)
	var wood := ShaderMaterial.new()
	wood.shader = PLANK
	var nail := _paint(Color("2a2420"), 0.5, 0.7)
	var nail_mesh := CylinderMesh.new()
	nail_mesh.top_radius = 0.0055
	nail_mesh.bottom_radius = 0.0065
	nail_mesh.height = 0.004
	nail_mesh.radial_segments = 16
	nail_mesh.rings = 0
	var nails: Array = []
	for k in planks:
		var offset := -span * 0.5 + width * (k + 0.5) + gap * k
		var plank_len := length - rng.randf_range(0.0, 0.01)
		var lift := rng.randf_range(-0.002, 0.002)
		var mesh := _bevel_box(Vector3(plank_len, thick, width - rng.randf_range(0.0, 0.004)), 0.004)
		var node := _mesh(frame, mesh, wood, Vector3(0, -thick * 0.5 + lift, offset))
		node.rotation = Vector3(rng.randf_range(-0.004, 0.004), rng.randf_range(-0.006, 0.006), 0)
		if not along:
			node.position = Vector3(offset, -thick * 0.5 + lift, 0)
			node.rotation.y += PI * 0.5
		# Two nails into the battens at each end.
		for end: float in [-1.0, 1.0]:
			for side: float in [-0.25, 0.25]:
				var p := Vector3(end * (plank_len * 0.5 - 0.035), lift + 0.0015, side * width)
				p = node.basis * p + node.position
				nails.append(Transform3D(Basis(), p))
	var nail_batch := MultiMesh.new()
	nail_batch.transform_format = MultiMesh.TRANSFORM_3D
	nail_batch.mesh = nail_mesh
	nail_batch.instance_count = nails.size()
	for index in nails.size():
		nail_batch.set_instance_transform(index, nails[index])
	var nail_node := MultiMeshInstance3D.new()
	nail_node.multimesh = nail_batch
	nail_node.material_override = nail
	frame.add_child(nail_node)


## A box of `size` with its edges chamfered by `bevel`, flat-shaded so the chamfers catch light.
static func _bevel_box(size: Vector3, bevel: float) -> ArrayMesh:
	var h := size * 0.5
	var b := minf(bevel, minf(h.x, minf(h.y, h.z)) * 0.9)
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_smooth_group(-1)
	# corner(s, a): the corner with signs s, on the face whose normal is axis a.
	var corner := func(s: Vector3, a: int) -> Vector3:
		var v := Vector3(s.x * (h.x - b), s.y * (h.y - b), s.z * (h.z - b))
		v[a] = s[a] * h[a]
		return v
	var add := func(points: Array) -> void:
		var centre := Vector3.ZERO
		for p: Vector3 in points:
			centre += p
		centre /= points.size()
		for k in range(1, points.size() - 1):
			var a: Vector3 = points[0]
			var c1: Vector3 = points[k]
			var c2: Vector3 = points[k + 1]
			# Godot's front faces wind clockwise: the cross product points inward.
			if (c1 - a).cross(c2 - a).dot(centre) > 0.0:
				var t := c1
				c1 = c2
				c2 = t
			for v: Vector3 in [a, c1, c2]:
				tool.set_uv(Vector2(v.x, v.z))
				tool.add_vertex(v)
	var signs := [-1.0, 1.0]
	# The six faces.
	for a in 3:
		for sa: float in signs:
			var quad: Array = []
			for pair: Vector2 in [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, 1)]:
				var s := Vector3.ZERO
				s[a] = sa
				s[(a + 1) % 3] = pair.x
				s[(a + 2) % 3] = pair.y
				quad.append(corner.call(s, a))
			add.call(quad)
	# The twelve chamfers, along axis k between faces a and c.
	for k in 3:
		var a := (k + 1) % 3
		var c := (k + 2) % 3
		for sa: float in signs:
			for sc: float in signs:
				var s0 := Vector3.ZERO
				s0[a] = sa
				s0[c] = sc
				s0[k] = -1.0
				var s1 := s0
				s1[k] = 1.0
				add.call([corner.call(s0, a), corner.call(s1, a), corner.call(s1, c), corner.call(s0, c)])
	# The eight corner facets.
	for sx: float in signs:
		for sy: float in signs:
			for sz: float in signs:
				var s := Vector3(sx, sy, sz)
				add.call([corner.call(s, 0), corner.call(s, 1), corner.call(s, 2)])
	tool.generate_normals()
	return tool.commit()
