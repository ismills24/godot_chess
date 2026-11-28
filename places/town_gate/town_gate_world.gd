extends "res://places/common/place_world.gd"
## The town gate on a bright summer day: a crenellated gatehouse of red sandstone over the
## street, a wide arch for carts and a narrow one for people on foot, three leaded windows
## above, banners on the roof. A brick house on the left, a pale blue shop on the right. The
## gate guard has set up a little table in front of the gate and waits there over a board.
## Space: the street runs toward -Z; the gate's face is at z = GATE_FRONT, x across it.

const FLAG := preload("res://places/town_gate/flag.gdshader")
const PAVING := preload("res://places/town_gate/paving.gdshader")
const TABLE_WOOD := preload("res://places/town_gate/table_wood.gdshader")
const ROUND_TABLE := preload("res://inn/props/round_table.glb")
const STOOL := preload("res://inn/props/stool.glb")
const BARREL := preload("res://inn/props/barrel.glb")
const CRATE := preload("res://inn/props/crate.glb")
const BUCKET := preload("res://inn/props/bucket.glb")

const GATE_FRONT := -14.0
const GATE_BACK := -19.0
const GATE_LEFT := -6.2
const GATE_RIGHT := 6.4
const GATE_TOP := 8.0
## The arches: centre x, half width, height their curve springs from.
const CART_ARCH := Vector3(1.4, 1.75, 3.1)
const FOOT_ARCH := Vector3(-3.9, 0.95, 2.2)
const COURSE := 0.12
const TABLE_AT := Vector3(4.6, 0, -10.6)
const TABLE_HEIGHT := 0.74

var _sandstone := _ashlar({"stone_color": Color("9a4c36"), "stone_color_b": Color("7a3826"),
	"mortar_color": Color("6a4a3e"), "block_size": Vector2(0.62, 0.3), "streaks": 0.5})
var _sandstone_dressed := _ashlar({"stone_color": Color("a85a40"), "stone_color_b": Color("8a4430"),
	"mortar_color": Color("6a4a3e"), "block_size": Vector2(0.9, 0.3), "streaks": 0.3})
var _setts := _paving({"stone_color": Color("5e5953"), "stone_color_b": Color("45403c"),
	"joint_color": Color("2e2a27"), "unit_size": Vector2(0.2, 0.14), "joint": 0.012, "irregular": 1.0,
	"dome": 1.0, "roughness_value": 0.8})
var _pavers := _paving({"stone_color": Color("957666"), "stone_color_b": Color("62564f"),
	"joint_color": Color("4a3e38"), "unit_size": Vector2(0.21, 0.105), "joint": 0.005, "dome": 0.2,
	"tone_spread": 1.4})
var _kerb := _ashlar({"stone_color": Color("8a8580"), "stone_color_b": Color("77726c"),
	"mortar_color": Color("4a4644"), "block_size": Vector2(0.9, 0.3), "streaks": 0.0})
var _plaster_blue := _plaster(Color("9cb8c2"))
var _plaster_cream := _plaster(Color("cdbf9e"))
var _plaster_ochre := _plaster(Color("bc8c50"))
var _roof_tile := _terrain_material({"color_a": Color("5a3a30"), "color_b": Color("3e2620"),
	"scale": 2.0, "strata": 1.0, "speckle": 0.4})
var _slate := _terrain_material({"color_a": Color("3e4248"), "color_b": Color("2c3036"),
	"scale": 2.0, "strata": 1.0, "speckle": 0.4})
var _white_wood := _paint(Color("e8e4da"), 0.6)
var _dark_wood := _paint(Color("3a2a1e"), 0.8)
var _black := _paint(Color("1a1816"), 0.7)
var _shadow := _paint(Color("0c0a09"), 1.0)
var _lead := _leaded_glass()
var _iron := _paint(Color("2a2826"), 0.5, 0.6)


func build() -> void:
	opponent_name = "The gate guard"
	eye_height = 1.64
	board_light_color = Color("fff0d8")
	board_light_energy = 0.35
	walk_path = [Vector2(2.2, 9.5), Vector2(2.5, 5.0), Vector2(3.4, -2.5), Vector2(4.4, -7.6), Vector2(4.6, -9.85)]
	first_look = Vector3(0.6, 6.6, GATE_FRONT)
	seat_eye = Vector3(TABLE_AT.x, 1.2, -9.92)
	_sun(52.0, 28.0, Color("fff2dc"), 1.6)
	_build_street()
	_build_gatehouse()
	_build_brick_house()
	_build_shop()
	_build_beyond()
	_build_table()
	_flush_boxes()
	_animate()


func _build_street() -> void:
	# The road through the cart arch, kerbs, and pavements either side of it.
	var road_left := CART_ARCH.x - CART_ARCH.y - 0.1
	var road_right := CART_ARCH.x + CART_ARCH.y + 0.1
	_box(_setts, Vector3((road_left + road_right) * 0.5, -0.06, -14.0), Vector3(road_right - road_left, 0.12, 60.0))
	for edge: float in [road_left, road_right]:
		_box(_kerb, Vector3(edge, 0.0, -11.0), Vector3(0.16, 0.14, 54.0))
	_box(_pavers, Vector3((GATE_LEFT - 1.0 + road_left) * 0.5, 0.0, -14.0), Vector3(road_left - GATE_LEFT + 1.0, 0.12, 60.0))
	_box(_pavers, Vector3((road_right + 8.0) * 0.5, 0.0, -14.0), Vector3(8.0 - road_right, 0.12, 60.0))
	# A gutter grate and a manhole in the road.
	_box(_iron, Vector3(road_right - 0.35, 0.002, -4.0), Vector3(0.3, 0.01, 0.5))
	var cover := CylinderMesh.new()
	cover.top_radius = 0.32
	cover.bottom_radius = 0.32
	cover.height = 0.02
	cover.radial_segments = 12
	_mesh(self, cover, _iron, Vector3(CART_ARCH.x - 0.4, 0.0, 2.0))


## The gatehouse, laid up in courses so the arches step round in stone.
func _build_gatehouse() -> void:
	var depth := GATE_FRONT - GATE_BACK
	var mid_z := (GATE_FRONT + GATE_BACK) * 0.5
	var rows := int(ceil(5.2 / COURSE))
	for row in rows:
		var y := row * COURSE + COURSE * 0.5
		var gaps: Array[Vector2] = []
		for arch: Vector3 in [FOOT_ARCH, CART_ARCH]:
			var half := _arch_half_width(arch, y)
			if half > 0.0:
				gaps.append(Vector2(arch.x - half, arch.x + half))
		var from := GATE_LEFT
		for gap in gaps:
			if gap.x > from:
				_box(_sandstone, Vector3((from + gap.x) * 0.5, y, mid_z), Vector3(gap.x - from, COURSE, depth))
			from = gap.y
		_box(_sandstone, Vector3((from + GATE_RIGHT) * 0.5, y, mid_z), Vector3(GATE_RIGHT - from, COURSE, depth))
	var solid_from := rows * COURSE
	_box(_sandstone, Vector3((GATE_LEFT + GATE_RIGHT) * 0.5, (solid_from + GATE_TOP) * 0.5, mid_z),
		Vector3(GATE_RIGHT - GATE_LEFT, GATE_TOP - solid_from, depth))
	# Dressed voussoirs round each arch face, a hood over the cart arch.
	for arch: Vector3 in [FOOT_ARCH, CART_ARCH]:
		var stones := 13
		for k in stones:
			var angle := PI * (k + 0.5) / stones
			var radius := arch.y + 0.16
			var at := Vector3(arch.x - cos(angle) * radius, arch.z + sin(angle) * radius, GATE_FRONT + 0.04)
			_box(_sandstone_dressed, at, Vector3(0.3, 0.24, 0.1), Vector3(0, 0, angle - PI * 0.5))
	# Shadow deep inside the passages, light at the far end.
	for arch: Vector3 in [FOOT_ARCH, CART_ARCH]:
		_box(_shadow, Vector3(arch.x, 0.02, mid_z), Vector3(arch.y * 2.0, 0.02, depth - 0.6))
	# Stepped buttresses between and beside the arches, with sloping weatherings.
	for x: float in [-2.45, 3.75, 5.75]:
		_buttress(x)
	# A string course over the arches, a parapet and its merlons on top.
	_box(_sandstone_dressed, Vector3(0.1, 5.05, GATE_FRONT + 0.08), Vector3(GATE_RIGHT - GATE_LEFT + 0.1, 0.18, 0.2))
	_box(_sandstone_dressed, Vector3(0.1, GATE_TOP - 0.15, GATE_FRONT + 0.1), Vector3(GATE_RIGHT - GATE_LEFT + 0.2, 0.22, 0.24))
	var merlon_x := GATE_LEFT + 0.25
	while merlon_x < GATE_RIGHT - 0.2:
		_box(_sandstone, Vector3(merlon_x, GATE_TOP + 0.35, GATE_FRONT + 0.25), Vector3(0.5, 0.7, 0.5))
		_box(_sandstone_dressed, Vector3(merlon_x, GATE_TOP + 0.74, GATE_FRONT + 0.25), Vector3(0.56, 0.08, 0.56))
		merlon_x += 0.95
	# The parapet walk behind, and the merlons down the sides.
	for z_step in 5:
		_box(_sandstone, Vector3(GATE_LEFT + 0.25, GATE_TOP + 0.35, GATE_FRONT - 0.7 - z_step * 0.95), Vector3(0.5, 0.7, 0.5))
	# Three tall leaded windows.
	for x: float in [-3.7, -0.4, 2.9]:
		_window(Vector3(x, 5.5, GATE_FRONT))
	# A carved panel over the cart arch.
	_box(_sandstone_dressed, Vector3(CART_ARCH.x + 0.6, 5.42, GATE_FRONT + 0.06), Vector3(1.2, 0.5, 0.06))
	# Flagpoles on the roof, flying the town's banners.
	for spot: Vector3 in [Vector3(-1.6, GATE_TOP, mid_z), Vector3(2.4, GATE_TOP, mid_z - 0.6)]:
		_box(_white_wood, spot + Vector3(0, 2.4, 0), Vector3(0.07, 4.8, 0.07))
		_box(_white_wood, spot + Vector3(0, 4.85, 0), Vector3(0.12, 0.1, 0.12))
		var flag := QuadMesh.new()
		flag.size = Vector2(1.6, 0.8)
		flag.center_offset = Vector3(0.8, 0, 0)
		var material := _animated_material(FLAG)
		var node := _mesh(self, flag, material, spot + Vector3(0.04, 4.35, 0))
		node.rotation.y = 0.5
	# A sign beside the passage and the guard's notice board.
	_box(_white_wood, Vector3(CART_ARCH.x, 3.9, GATE_FRONT + 0.12), Vector3(0.5, 0.42, 0.04))
	_box(_paint(Color("b0201a"), 0.6), Vector3(CART_ARCH.x, 3.9, GATE_FRONT + 0.145), Vector3(0.36, 0.3, 0.01))
	_box(_dark_wood, Vector3(5.2, 1.5, GATE_FRONT + 0.06), Vector3(0.7, 0.9, 0.06))
	for note in 4:
		_box(_paint(Color("e0d6bc"), 0.9), Vector3(4.98 + (note % 2) * 0.3, 1.32 + (note / 2) * 0.38, GATE_FRONT + 0.095),
			Vector3(0.2, 0.26, 0.01), Vector3(0, 0, (note - 1.5) * 0.06))


## Half the width of `arch` (x, half width, spring height) at height `y`, or 0 above it.
func _arch_half_width(arch: Vector3, y: float) -> float:
	if y < arch.z:
		return arch.y
	var rise := y - arch.z
	if rise >= arch.y:
		return 0.0
	return sqrt(arch.y * arch.y - rise * rise)


func _buttress(x: float) -> void:
	_box(_sandstone, Vector3(x, 1.8, GATE_FRONT + 0.4), Vector3(0.75, 3.6, 0.8))
	_box(_sandstone_dressed, Vector3(x, 3.75, GATE_FRONT + 0.3), Vector3(0.8, 0.22, 0.72), Vector3(0.55, 0, 0))
	_box(_sandstone, Vector3(x, 4.3, GATE_FRONT + 0.15), Vector3(0.6, 1.2, 0.3))
	_box(_sandstone_dressed, Vector3(x, 4.85, GATE_FRONT + 0.15), Vector3(0.66, 0.16, 0.42), Vector3(0.6, 0, 0))


func _window(at: Vector3) -> void:
	var width := 1.15
	var height := 1.65
	_box(_shadow, at + Vector3(0, height * 0.5, 0.0), Vector3(width, height, 0.1))
	var glass := QuadMesh.new()
	glass.size = Vector2(width - 0.1, height - 0.1)
	_mesh(self, glass, _lead, at + Vector3(0, height * 0.5, 0.06))
	# Stone surround, a mullion and a transom.
	for side: int in [-1, 1]:
		_box(_sandstone_dressed, at + Vector3(side * (width * 0.5 + 0.06), height * 0.5, 0.06), Vector3(0.14, height + 0.12, 0.16))
	_box(_sandstone_dressed, at + Vector3(0, -0.06, 0.1), Vector3(width + 0.4, 0.12, 0.24))
	_box(_sandstone_dressed, at + Vector3(0, height + 0.06, 0.06), Vector3(width + 0.26, 0.14, 0.16))
	_box(_sandstone_dressed, at + Vector3(0, height * 0.5, 0.07), Vector3(0.08, height, 0.1))
	_box(_sandstone_dressed, at + Vector3(0, height * 0.66, 0.07), Vector3(width, 0.07, 0.1))
	# A hood mould dripping down either side.
	_box(_sandstone_dressed, at + Vector3(0, height + 0.2, 0.12), Vector3(width + 0.5, 0.1, 0.16))
	for side: int in [-1, 1]:
		_box(_sandstone_dressed, at + Vector3(side * (width * 0.5 + 0.2), height + 0.05, 0.12), Vector3(0.1, 0.3, 0.16))


func _paving(params: Dictionary) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = PAVING
	for key: String in params:
		material.set_shader_parameter(key, params[key])
	return material


## Every mesh under `prop` drawn with `material` instead of its own.
static func _reclad(prop: Node3D, material: Material) -> void:
	for mesh: MeshInstance3D in prop.find_children("*", "MeshInstance3D", true, false):
		mesh.material_override = material


func _plaster(color: Color) -> ShaderMaterial:
	return _terrain_material({"color_a": color, "color_b": color.darkened(0.12), "scale": 1.4,
		"speckle": 0.35, "top_color": color.darkened(0.25), "top_amount": 0.0, "low_color": color.darkened(0.3),
		"low_height": 0.25, "roughness_value": 0.95})


func _brick(color: Color, color_b: Color) -> ShaderMaterial:
	return _ashlar({"stone_color": color, "stone_color_b": color_b, "mortar_color": Color("8a8078"),
		"block_size": Vector2(0.3, 0.1), "joint": 0.012, "streaks": 0.25})


func _leaded_glass() -> ShaderMaterial:
	var shader := Shader.new()
	shader.code = """
shader_type spatial;
void fragment() {
	vec2 d = vec2(UV.x * 7.0 + UV.y * 9.0, UV.x * 7.0 - UV.y * 9.0);
	vec2 f = abs(fract(d) - 0.5);
	float lead = step(0.42, max(f.x, f.y));
	vec3 glass = mix(vec3(0.16, 0.2, 0.24), vec3(0.42, 0.5, 0.56), smoothstep(0.2, 1.0, UV.y + 0.2 * sin(UV.x * 5.0)));
	ALBEDO = mix(glass, vec3(0.06, 0.06, 0.07), lead);
	ROUGHNESS = mix(0.12, 0.7, lead);
	METALLIC = 0.2;
	SPECULAR = 0.8;
}
"""
	var material := ShaderMaterial.new()
	material.shader = shader
	return material


## The tall brick house on the left, running up the street to the gate.
func _build_brick_house() -> void:
	var brick := _brick(Color("8a3e2c"), Color("6e2e22"))
	var face := GATE_LEFT - 0.1
	_box(brick, Vector3(face - 3.0, 4.0, -2.0), Vector3(6.0, 8.0, 24.0))
	_box(_slate, Vector3(face - 1.6, 8.45, -2.0), Vector3(3.6, 0.15, 24.4), Vector3(0, 0, -0.5))
	_box(_slate, Vector3(face - 4.4, 8.45, -2.0), Vector3(3.6, 0.15, 24.4), Vector3(0, 0, 0.5))
	_box(_paint(Color("c8bca8"), 0.9), Vector3(face + 0.06, 3.2, -2.0), Vector3(0.12, 0.16, 24.0))
	for z: float in [6.0, 2.5, -1.0, -4.5, -8.0, -11.5]:
		for y: float in [1.4, 4.6]:
			_box(_shadow, Vector3(face + 0.01, y + 0.6, z), Vector3(0.04, 1.3, 0.8))
			_box(_white_wood, Vector3(face + 0.05, y + 0.6, z), Vector3(0.04, 1.3, 0.06))
			_box(_white_wood, Vector3(face + 0.05, y + 0.6, z), Vector3(0.04, 0.06, 0.8))
			_box(_paint(Color("d8ccb4"), 0.9), Vector3(face + 0.08, y - 0.1, z), Vector3(0.12, 0.1, 1.0))
	# A shop sign and a doorway.
	_box(_black, Vector3(face + 0.06, 3.0, 0.5), Vector3(0.06, 0.6, 1.6))
	_box(_paint(Color("1e3a2a"), 0.6), Vector3(face + 0.02, 1.1, 0.5), Vector3(0.06, 2.2, 1.1))
	# A slate chalkboard out on the pavement.
	_box(_black, Vector3(-3.2, 0.5, -12.4), Vector3(0.6, 0.9, 0.06), Vector3(0.15, 0.3, 0))
	_box(_paint(Color("d0c8b0"), 0.9), Vector3(-3.2, 0.62, -12.36), Vector3(0.4, 0.04, 0.01), Vector3(0.15, 0.3, 0))


## The pale blue shop on the right with its tiled roof and dormer.
func _build_shop() -> void:
	var face := 7.0
	_box(_plaster_blue, Vector3(face + 3.0, 2.8, -6.0), Vector3(6.0, 5.6, 11.0))
	_box(_roof_tile, Vector3(face + 1.5, 6.4, -6.0), Vector3(3.8, 0.16, 11.4), Vector3(0, 0, 0.62))
	_box(_roof_tile, Vector3(face + 4.5, 6.4, -6.0), Vector3(3.8, 0.16, 11.4), Vector3(0, 0, -0.62))
	_box(_plaster_blue, Vector3(face + 3.0, 6.6, -6.0), Vector3(2.0, 1.6, 11.0))
	# A dormer.
	_box(_plaster_cream, Vector3(face + 0.9, 6.5, -6.0), Vector3(1.0, 1.0, 1.2))
	_box(_roof_tile, Vector3(face + 0.9, 7.15, -6.0), Vector3(1.4, 0.1, 1.5), Vector3(0, 0, 0.3))
	_box(_white_wood, Vector3(face + 0.39, 6.45, -6.0), Vector3(0.04, 0.7, 0.8))
	_box(_shadow, Vector3(face + 0.38, 6.45, -6.0), Vector3(0.04, 0.6, 0.7))
	# Shop front: a black fascia, a big window and a door.
	_box(_black, Vector3(face - 0.08, 2.75, -6.0), Vector3(0.16, 0.45, 7.0))
	_box(_paint(Color("e8c060"), 0.4, 0.4), Vector3(face - 0.17, 2.75, -6.0), Vector3(0.01, 0.12, 3.0))
	for z: float in [-8.0, -4.0]:
		_box(_white_wood, Vector3(face - 0.05, 1.35, z), Vector3(0.12, 2.3, 2.6))
		_box(_paint(Color("2a3438"), 0.15, 0.2), Vector3(face - 0.12, 1.45, z), Vector3(0.02, 1.9, 2.3))
		_box(_white_wood, Vector3(face - 0.13, 1.45, z), Vector3(0.03, 1.9, 0.05))
	_box(_paint(Color("2a4058"), 0.6), Vector3(face - 0.05, 1.1, -6.0), Vector3(0.1, 2.2, 1.0))
	# Upstairs windows.
	for z: float in [-9.0, -6.0, -3.0]:
		_box(_white_wood, Vector3(face - 0.03, 4.2, z), Vector3(0.08, 1.1, 0.9))
		_box(_shadow, Vector3(face - 0.08, 4.2, z), Vector3(0.02, 0.95, 0.75))
		_box(_white_wood, Vector3(face - 0.1, 4.2, z), Vector3(0.02, 0.95, 0.04))
		_box(_white_wood, Vector3(face - 0.1, 4.2, z), Vector3(0.02, 0.04, 0.75))
	# A wooden picnic bench, a barrel planter with flowers and an A-board.
	_box(_dark_wood, Vector3(face - 1.1, 0.72, -2.0), Vector3(0.8, 0.05, 1.6))
	for side: int in [-1, 1]:
		_box(_dark_wood, Vector3(face - 1.1 + side * 0.6, 0.42, -2.0), Vector3(0.28, 0.04, 1.6))
		_box(_dark_wood, Vector3(face - 1.1, 0.36, -2.0 + side * 0.65), Vector3(1.4, 0.7, 0.06), Vector3(0, 0, 0))
	_place_prop(BARREL, Vector3(face - 0.5, 0.06, -10.4), 0.8, 0.4)
	var flowers := _foliage(Color("2a4a1a"), Color("5a8a2a"), 0.02)
	var bloom := _paint(Color("d84a6a"), 0.8)
	_scatter(_lump_mesh(3, 0.4, false), flowers, [_blob_at(Vector3(face - 0.5, 0.98, -10.4), Vector3(0.38, 0.25, 0.38), 0.0)])
	for k in 7:
		var angle := k * 0.9
		_box(bloom, Vector3(face - 0.5 + cos(angle) * 0.22, 1.12 + 0.04 * (k % 2), -10.4 + sin(angle) * 0.22), Vector3.ONE * 0.06)
	_box(_white_wood, Vector3(face - 0.7, 0.45, 0.6), Vector3(0.05, 0.9, 0.5), Vector3(0, 0, 0.18))
	_box(_white_wood, Vector3(face - 0.5, 0.45, 0.6), Vector3(0.05, 0.9, 0.5), Vector3(0, 0, -0.18))
	_box(_black, Vector3(face - 0.72, 0.5, 0.6), Vector3(0.01, 0.5, 0.36), Vector3(0, 0, 0.18))


## Through the arches: the street going on beyond the gate, sunlit.
func _build_beyond() -> void:
	_box(_setts, Vector3(CART_ARCH.x, -0.06, -32.0), Vector3(3.8, 0.12, 26.0))
	_box(_pavers, Vector3(-5.0, 0.0, -32.0), Vector3(8.0, 0.12, 26.0))
	_box(_pavers, Vector3(8.0, 0.0, -32.0), Vector3(10.0, 0.12, 26.0))
	var fronts: Array[Material] = [_plaster_cream, _plaster_ochre, _brick(Color("9a5a44"), Color("7a4434")), _plaster_blue]
	var z := -21.0
	var k := 0
	while z > -44.0:
		var width := 4.0 + (k % 3) * 1.1
		for side: int in [-1, 1]:
			var x := CART_ARCH.x + side * 6.5
			var height := 6.0 + ((k + side) % 3) * 1.3
			_box(fronts[(k + side + 4) % 4], Vector3(x + side * 2.0, height * 0.5, z - width * 0.5), Vector3(4.0, height, width - 0.1))
			_box(_roof_tile, Vector3(x + side * 2.0, height + 0.5, z - width * 0.5), Vector3(4.4, 1.0, width), Vector3(0, 0, 0))
			for row in 2:
				_box(_shadow, Vector3(x - side * 0.02, 1.6 + row * 2.4, z - width * 0.5), Vector3(0.04, 1.1, 0.8))
		z -= width
		k += 1
	# A church tower closing the view.
	_box(_paint(Color("c8b898"), 0.9), Vector3(CART_ARCH.x + 1.0, 8.0, -48.0), Vector3(4.0, 16.0, 4.0))
	_box(_slate, Vector3(CART_ARCH.x + 1.0, 17.5, -48.0), Vector3(3.0, 3.0, 3.0), Vector3(0, PI * 0.25, 0))


## The guard's table: a round table set out on the pavement before the gate, two stools, his
## halberd leaning on the wall and a pitcher.
func _build_table() -> void:
	var oak := ShaderMaterial.new()
	oak.shader = TABLE_WOOD
	_reclad(_place_prop(ROUND_TABLE, TABLE_AT + Vector3(0, 0.06, 0), TABLE_HEIGHT, 0.3), oak)
	var surface := TABLE_AT + Vector3(0, 0.06 + TABLE_HEIGHT, 0)
	_place_board(surface, 0.0)
	_place_prop(STOOL, Vector3(TABLE_AT.x, 0.06, TABLE_AT.z - 0.68), 0.46, 0.2)
	_place_prop(STOOL, Vector3(TABLE_AT.x, 0.06, TABLE_AT.z + 0.68), 0.46, 1.1)
	_seat_opponent({
		"seat": "chair", "seat_height": 0.46, "height": 1.06, "girth": 1.12,
		"skin": Color("c89878"), "coat": Color("8a1e1a"), "legs": Color("3a3634"),
		"hat": "helm", "hat_color": Color("6a6c70"), "hair": Color("4a2e1c"),
		"beard": "full", "beard_color": Color("4a2e1c"),
		"extras": ["tabard", "mail", "belt_pouches"], "tabard_color": Color("9a201a"),
		"trim_color": Color("d8b040"), "phase": 1.2,
	}, Vector3(TABLE_AT.x, 0.06, TABLE_AT.z - 0.68), 0.0)
	# His halberd, leaning against the buttress.
	var pole_at := Vector3(5.6, 0.06, GATE_FRONT + 0.85)
	var lean := Vector3(0, 0, 0.12)
	_box(_dark_wood, pole_at + Vector3(0, 1.2, 0), Vector3(0.05, 2.4, 0.05), lean)
	var head := pole_at + Vector3(0, 2.4, 0.29)
	_box(_iron, head + Vector3(0, 0.15, 0), Vector3(0.03, 0.4, 0.04), lean)
	_box(_iron, head + Vector3(0.14, 0.0, 0), Vector3(0.24, 0.22, 0.02), lean)
	_box(_iron, head + Vector3(-0.1, 0.02, 0), Vector3(0.14, 0.05, 0.02), lean)
	# A pitcher and two cups on the table's edge.
	var pewter := _paint(Color("7a7874"), 0.35, 0.7)
	_cylinder(self, surface + Vector3(-0.3, 0.06, -0.3), 0.035, 0.028, 0.12, pewter)
	_cylinder(self, surface + Vector3(0.31, 0.03, -0.3), 0.022, 0.022, 0.06, pewter)
	_place_prop(BUCKET, Vector3(TABLE_AT.x + 0.9, 0.06, TABLE_AT.z - 0.9), 0.32, 0.0)
	_place_prop(CRATE, Vector3(TABLE_AT.x + 1.1, 0.06, TABLE_AT.z + 0.1), 0.45, 0.4)


func ground_at(_x: float, _z: float) -> float:
	return 0.06
