extends "res://places/common/place_world.gd"
## The apothecary's shop at night: a low timbered room crammed floor to beams with shelves of
## bottles and jars, many of them faintly aglow, pink and violet and green. Flasks hang from the
## beams. A big leaded window at the back lets in cold blue moonlight between pink curtains,
## over the counter with its alembic. In the middle of the shop, on a worn pink rug, the
## alchemist sits at a little round table with a board set out and a candle burning.
## Room space: x LEFT..RIGHT, z FRONT (the door) .. BACK (the window wall), y up.

const OLD_WOOD := preload("res://inn/old_wood.gdshader")
const OLD_PLASTER := preload("res://inn/old_plaster.gdshader")
const HALO := preload("res://inn/halo.gdshader")
const MIST := preload("res://menu/mist.gdshader")
const RUG := preload("res://places/apothecary/rug.gdshader")
const TABLE_TOP := preload("res://places/apothecary/table_top.gdshader")
const WAX := preload("res://places/apothecary/wax.gdshader")
const ROUND_TABLE := preload("res://inn/props/round_table.glb")
const STOOL := preload("res://inn/props/stool.glb")
const BARREL := preload("res://inn/props/barrel.glb")
const CRATE := preload("res://inn/props/crate.glb")
const CANDLESTICK := preload("res://inn/props/candlestick.glb")
const BOWL := preload("res://inn/props/bowl.glb")
const JUG := preload("res://inn/props/jug.glb")

const LEFT := -3.3
const RIGHT := 3.3
const FRONT := 1.2
const BACK := -7.6
const BEAMS := 2.9
const TABLE_AT := Vector3(0.3, 0, -4.4)
const TABLE_HEIGHT := 0.74
const WINDOW := Vector3(-0.1, 1.05, BACK)
const WINDOW_SIZE := Vector2(1.5, 1.75)

var _plaster := _plaster_material(Color("2e2638"))
var _timber := _wood(Color("2a1a14"))
var _shelf_wood := _wood(Color("4a2e1e"))
var _floor_wood := _wood(Color("3a2418"))
var _iron := _paint(Color("2a2626"), 0.5, 0.6)
var _brass := _paint(Color("9a7430"), 0.35, 0.8)
var _paper := _paint(Color("c8b898"), 0.9)
var _cork := _paint(Color("6a4a2a"), 0.9)
var _dark_glass: Array[Material] = [_paint(Color("1e2a3a"), 0.1, 0.3), _paint(Color("3a1e30"), 0.1, 0.3),
	_paint(Color("2a3a22"), 0.1, 0.3), _paint(Color("4a3018"), 0.1, 0.3)]
## Potions that glow: pink, magenta, violet, teal, amber, green.
var _potions: Array[Material] = [_glowing(Color("ff5aa8"), 1.6), _glowing(Color("d040e0"), 1.5),
	_glowing(Color("8a5aff"), 1.4), _glowing(Color("40e0d0"), 1.4), _glowing(Color("ffb040"), 1.5),
	_glowing(Color("80ff60"), 1.2)]
var _halo: ShaderMaterial
var _motes: MultiMesh
var _hanging: Array[Node3D] = []
var _candle: OmniLight3D
var _flame := _glowing(Color("ffd27a"), 7.0)
## Upright cylinders batched by material, drawn round (32 sides) by _flush_boxes().
var _rounds: Dictionary = {}


func build() -> void:
	opponent_name = "The alchemist"
	eye_height = 1.62
	board_light_color = Color("ffb0e0")
	board_light_energy = 0.7
	walk_path = [Vector2(0.7, 0.7), Vector2(0.9, -1.0), Vector2(0.6, -2.5), Vector2(0.3, -3.72)]
	first_look = WINDOW + Vector3(0, 0.9, 0)
	seat_eye = Vector3(TABLE_AT.x, 1.22, -3.75)
	_halo = _animated_material(HALO)
	_halo.set_shader_parameter("glow_color", Color(1.0, 0.7, 0.4))
	_build_shell()
	_build_window()
	_build_shelves()
	_build_counter()
	_build_hanging()
	_build_table()
	_build_clutter()
	_build_lights()
	_build_motes()
	_flush_boxes()
	_animate()


func _animate() -> void:
	super()
	if _candle:
		_candle.light_energy = 0.9 + 0.08 * sin(_elapsed * 11.0) + 0.05 * sin(_elapsed * 23.0)
	for k in _hanging.size():
		var flask := _hanging[k]
		flask.rotation.z = 0.05 * sin(_elapsed * 0.7 + k * 1.3)
		flask.rotation.x = 0.04 * sin(_elapsed * 0.53 + k * 2.1)
	if _motes:
		for i in _motes.instance_count:
			var life := fposmod(_elapsed * (0.05 + 0.003 * (i % 7)) + i * 0.137, 1.0)
			var base := Vector3(fposmod(i * 2.17, RIGHT - LEFT) + LEFT, 0.3, fposmod(i * 3.71, FRONT - BACK) + BACK)
			var at := base + Vector3(0.3 * sin(_elapsed * 0.4 + i), life * 2.4, 0.3 * cos(_elapsed * 0.33 + i * 1.7))
			var size := 0.012 * sin(life * PI)
			_motes.set_instance_transform(i, Transform3D(Basis.from_scale(Vector3.ONE * size), at))


## Batched upright cylinder, as place_world's, but kept here so it can be drawn smooth.
func _cyl(material: Material, at: Vector3, radius: float, height: float) -> void:
	if not _rounds.has(material):
		_rounds[material] = []
	_rounds[material].append(Transform3D(Basis.from_scale(Vector3(radius, height, radius)), at))


func _flush_boxes() -> void:
	super()
	var unit := CylinderMesh.new()
	unit.top_radius = 1.0
	unit.bottom_radius = 1.0
	unit.height = 1.0
	unit.radial_segments = 32
	unit.rings = 0
	for material: Material in _rounds:
		_scatter(unit, material, _rounds[material])
	_rounds.clear()


## A smooth turned shape: `profile` is (radius, height) pairs from the foot up, spun round y.
static func _lathe(profile: PackedVector2Array, segments := 40) -> ArrayMesh:
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var count := profile.size()
	# Smooth normals from the profile's slope at each point.
	var normals: Array[Vector2] = []
	for i in count:
		var before := profile[maxi(i - 1, 0)]
		var after := profile[mini(i + 1, count - 1)]
		var along := (after - before).normalized()
		normals.append(Vector2(along.y, -along.x))
	for i in count - 1:
		for k in segments:
			var corners := [Vector2i(k, i), Vector2i(k + 1, i), Vector2i(k, i + 1),
				Vector2i(k, i + 1), Vector2i(k + 1, i), Vector2i(k + 1, i + 1)]
			for c: Vector2i in corners:
				var angle := TAU * c.x / segments
				var point := profile[c.y]
				var n := normals[c.y]
				tool.set_normal(Vector3(cos(angle) * n.x, n.y, sin(angle) * n.x))
				tool.set_uv(Vector2(float(c.x) / segments, point.y))
				tool.add_vertex(Vector3(cos(angle) * point.x, point.y, sin(angle) * point.x))
	return tool.commit()


func _wood(color: Color) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = OLD_WOOD
	material.set_shader_parameter("wood_color", color)
	return material


func _plaster_material(color: Color) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = OLD_PLASTER
	material.set_shader_parameter("plaster_color", color)
	material.set_shader_parameter("top", BEAMS)
	return material


func _build_shell() -> void:
	# Floorboards running back from the door.
	var board_width := 0.24
	var x := LEFT
	while x < RIGHT:
		_box(_floor_wood, Vector3(x + board_width * 0.5, -0.03, (FRONT + BACK) * 0.5), Vector3(board_width - 0.006, 0.06, FRONT - BACK))
		x += board_width
	# Plaster walls with timber posts and rails.
	for wall: float in [LEFT, RIGHT]:
		var out := signf(wall)
		_box(_plaster, Vector3(wall + out * 0.05, 1.8, (FRONT + BACK) * 0.5), Vector3(0.1, 3.6, FRONT - BACK))
		for post in 5:
			_box(_timber, Vector3(wall - out * 0.04, BEAMS * 0.5, FRONT - 0.2 - post * 2.15), Vector3(0.14, BEAMS, 0.18))
	_box(_plaster, Vector3(0, 1.8, BACK - 0.05), Vector3(RIGHT - LEFT, 3.6, 0.1))
	_box(_plaster, Vector3(0, 1.8, FRONT + 0.05), Vector3(RIGHT - LEFT, 3.6, 0.1))
	# The door you came in by, shut behind you.
	_box(_timber, Vector3(0.7, 1.05, FRONT), Vector3(1.0, 2.1, 0.08))
	# Beams across, boards over them, and the roof sloping up beyond.
	for k in 6:
		var z := FRONT - 0.5 - k * 1.65
		_box(_timber, Vector3(0, BEAMS + 0.1, z), Vector3(RIGHT - LEFT, 0.2, 0.22))
	_box(_timber, Vector3(0, BEAMS + 0.1, (FRONT + BACK) * 0.5), Vector3(0.22, 0.2, FRONT - BACK))
	_box(_shelf_wood, Vector3(0, BEAMS + 0.25, (FRONT + BACK) * 0.5), Vector3(RIGHT - LEFT, 0.06, FRONT - BACK))
	# A pink rug across the middle of the floor.
	var rug := ShaderMaterial.new()
	rug.shader = RUG
	var rug_mesh := PlaneMesh.new()
	rug_mesh.size = Vector2(2.6, 3.8 + 0.14)
	_mesh(self, rug_mesh, rug, Vector3(0.3, 0.004, -3.6)).rotation.y = 0.04



## The moonlit window at the back, leaded panes and pink curtains drawn aside.
func _build_window() -> void:
	var glass := ShaderMaterial.new()
	var shader := Shader.new()
	shader.code = """
shader_type spatial;
render_mode unshaded;
void fragment() {
	vec2 p = UV * vec2(4.0, 6.0);
	vec2 f = abs(fract(p) - 0.5);
	float lead = step(0.44, max(f.x, f.y));
	vec3 night = mix(vec3(0.1, 0.5, 0.8), vec3(0.45, 0.85, 1.0), smoothstep(0.0, 1.0, UV.y * 0.7 + 0.2 * sin(UV.x * 3.0)));
	ALBEDO = mix(night * 1.4, vec3(0.03, 0.03, 0.05), lead);
}
"""
	glass.shader = shader
	var pane := QuadMesh.new()
	pane.size = WINDOW_SIZE
	_mesh(self, pane, glass, WINDOW + Vector3(0, WINDOW_SIZE.y * 0.5, 0.06))
	for side: int in [-1, 1]:
		_box(_timber, WINDOW + Vector3(side * (WINDOW_SIZE.x * 0.5 + 0.06), WINDOW_SIZE.y * 0.5, 0.08), Vector3(0.12, WINDOW_SIZE.y + 0.2, 0.12))
	_box(_timber, WINDOW + Vector3(0, -0.06, 0.12), Vector3(WINDOW_SIZE.x + 0.3, 0.1, 0.24))
	_box(_timber, WINDOW + Vector3(0, WINDOW_SIZE.y + 0.06, 0.08), Vector3(WINDOW_SIZE.x + 0.3, 0.12, 0.12))
	_box(_timber, WINDOW + Vector3(0, WINDOW_SIZE.y * 0.5, 0.08), Vector3(0.06, WINDOW_SIZE.y, 0.06))
	var curtain := _paint(Color("b03a78"), 0.95)
	var curtain_dark := _paint(Color("7a2050"), 0.95)
	for side: int in [-1, 1]:
		for fold in 4:
			var x := side * (WINDOW_SIZE.x * 0.5 + 0.12 + fold * 0.1)
			_box(curtain if fold % 2 == 0 else curtain_dark, WINDOW + Vector3(x, 0.85, 0.22),
				Vector3(0.11, 2.2, 0.06 + 0.04 * (fold % 2)), Vector3(0, 0, side * 0.03 * fold))
	_box(_brass, WINDOW + Vector3(0, WINDOW_SIZE.y + 0.24, 0.22), Vector3(WINDOW_SIZE.x + 1.2, 0.03, 0.03))


## Shelving units along both side walls, crammed with bottles and jars.
func _build_shelves() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 41
	for wall: float in [LEFT, RIGHT]:
		var inward := -signf(wall)
		for unit in 3:
			var z_from := -0.4 - unit * 2.3
			var z_to := z_from - 2.0
			if wall == LEFT and unit == 0:
				continue
			var x := wall + inward * 0.22
			for side: float in [z_from, z_to]:
				_box(_shelf_wood, Vector3(x, 1.35, side), Vector3(0.4, 2.7, 0.06))
			for shelf in 5:
				var y := 0.5 + shelf * 0.5
				_box(_shelf_wood, Vector3(x, y, (z_from + z_to) * 0.5), Vector3(0.4, 0.04, z_from - z_to))
				var z := z_from - 0.1
				while z > z_to + 0.08:
					var room := 0.44
					z -= _shelf_item(Vector3(x + rng.randf_range(-0.06, 0.06), y + 0.02, z), rng, room)
			# A small label board on each unit.
			_box(_paper, Vector3(x + inward * 0.21, 2.82, (z_from + z_to) * 0.5), Vector3(0.01, 0.1, 0.5))


## One thing on a shelf at `at` (its foot): a bottle, a jar or a little row of vials. Returns
## how much shelf it took up.
func _shelf_item(at: Vector3, rng: RandomNumberGenerator, headroom: float) -> float:
	var roll := rng.randf()
	var glowing := rng.randf() < 0.55
	var glass: Material = _potions[rng.randi() % _potions.size()] if glowing else _dark_glass[rng.randi() % _dark_glass.size()]
	if roll < 0.5:
		var radius := rng.randf_range(0.035, 0.06)
		var height := rng.randf_range(0.14, minf(0.3, headroom - 0.1))
		_cyl(glass, at + Vector3(0, height * 0.5, 0), radius, height)
		_cyl(glass, at + Vector3(0, height + 0.03, 0), radius * 0.4, 0.06)
		_cyl(_cork, at + Vector3(0, height + 0.07, 0), radius * 0.45, 0.025)
		if rng.randf() < 0.5:
			_box(_paper, at + Vector3(0, height * 0.45, 0), Vector3(radius * 2.05, height * 0.3, radius * 1.4))
		return radius * 2.0 + rng.randf_range(0.01, 0.05)
	elif roll < 0.8:
		# A squat jar with a wide lid.
		var radius := rng.randf_range(0.06, 0.09)
		var height := rng.randf_range(0.12, 0.2)
		_cyl(glass, at + Vector3(0, height * 0.5, 0), radius, height)
		_cyl(_dark_glass[0] if glowing else _cork, at + Vector3(0, height + 0.015, 0), radius * 1.05, 0.03)
		return radius * 2.0 + 0.03
	else:
		# Vials in a rack.
		for k in 4:
			_cyl(_potions[(k + rng.randi()) % _potions.size()], at + Vector3(0, 0.05, -k * 0.035), 0.012, 0.1)
		_box(_shelf_wood, at + Vector3(0, 0.03, -0.05), Vector3(0.05, 0.02, 0.16))
		return 0.18


## The counter under the window, with an alembic bubbling and a great glass carboy.
func _build_counter() -> void:
	var counter_z := BACK + 0.65
	_box(_shelf_wood, Vector3(-0.1, 0.48, counter_z), Vector3(3.4, 0.96, 0.6))
	_box(_timber, Vector3(-0.1, 0.98, counter_z), Vector3(3.5, 0.05, 0.7))
	var top := 1.005
	# The carboy: a big round flask of pink potion.
	var flask := SphereMesh.new()
	flask.radius = 0.22
	flask.height = 0.4
	flask.radial_segments = 48
	flask.rings = 24
	_mesh(self, flask, _potions[0], Vector3(0.9, top + 0.2, counter_z))
	_cyl(_dark_glass[1], Vector3(0.9, top + 0.44, counter_z), 0.06, 0.12)
	# The alembic: a still on a brass burner, its long arm reaching to a receiving flask.
	_cyl(_brass, Vector3(-0.9, top + 0.06, counter_z), 0.1, 0.12)
	var still := SphereMesh.new()
	still.radius = 0.14
	still.height = 0.26
	still.radial_segments = 48
	still.rings = 24
	_mesh(self, still, _potions[3], Vector3(-0.9, top + 0.25, counter_z))
	_cyl(_brass, Vector3(-0.9, top + 0.44, counter_z), 0.03, 0.14)
	_box(_brass, Vector3(-0.62, top + 0.42, counter_z), Vector3(0.6, 0.02, 0.02), Vector3(0, 0, -0.3))
	_cyl(_potions[5], Vector3(-0.32, top + 0.1, counter_z), 0.06, 0.2)
	_box(_flame, Vector3(-0.9, top + 0.13, counter_z), Vector3(0.04, 0.03, 0.04))
	# Scales, a mortar and a stack of books.
	_cyl(_paint(Color("5a5048"), 0.8), Vector3(0.2, top + 0.04, counter_z + 0.05), 0.08, 0.08)
	for k in 4:
		var colors := [Color("5a1a2a"), Color("2a3a5a"), Color("4a3a1a"), Color("2a4a2a")]
		_box(_paint(colors[k], 0.8), Vector3(0.45, top + 0.03 + k * 0.05, counter_z - 0.05), Vector3(0.28, 0.05, 0.2), Vector3(0, k * 0.2, 0))
	_box(_brass, Vector3(-0.3, top + 0.2, counter_z + 0.15), Vector3(0.02, 0.4, 0.02))
	_box(_brass, Vector3(-0.3, top + 0.38, counter_z + 0.15), Vector3(0.3, 0.015, 0.015))


## Flasks hung from the beams on cords, glowing, and two lanterns.
func _build_hanging() -> void:
	var spots: Array[Vector3] = [Vector3(-2.2, 0, -0.8), Vector3(-1.6, 0, -2.6), Vector3(1.9, 0, -1.4),
		Vector3(2.4, 0, -3.3), Vector3(-2.3, 0, -5.4), Vector3(1.6, 0, -5.9), Vector3(-0.9, 0, -6.4),
		Vector3(1.1, 0, -0.3)]
	for k in spots.size():
		var drop := 0.45 + 0.35 * fposmod(k * 0.61, 1.0)
		var hook := Node3D.new()
		hook.position = Vector3(spots[k].x, BEAMS, spots[k].z)
		add_child(hook)
		_box_mesh(hook, _iron, Vector3(0, -drop * 0.5, 0), Vector3(0.01, drop, 0.01))
		var bulb := SphereMesh.new()
		bulb.radius = 0.09 + 0.03 * (k % 2)
		bulb.height = bulb.radius * 2.0
		bulb.radial_segments = 48
		bulb.rings = 24
		_mesh(hook, bulb, _potions[k % _potions.size()], Vector3(0, -drop - bulb.radius, 0))
		_box_mesh(hook, _cork, Vector3(0, -drop + 0.02, 0), Vector3(0.04, 0.05, 0.04))
		_hanging.append(hook)
	var lantern_glass := _glowing(Color("ffc060"), 3.0)
	for spot: Vector3 in [Vector3(1.1, 0, -2.3), Vector3(-0.7, 0, -4.9)]:
		var hook := Node3D.new()
		hook.position = Vector3(spot.x, BEAMS, spot.z)
		add_child(hook)
		_box_mesh(hook, _iron, Vector3(0, -0.2, 0), Vector3(0.012, 0.4, 0.012))
		_box_mesh(hook, _iron, Vector3(0, -0.42, 0), Vector3(0.16, 0.03, 0.16))
		_box_mesh(hook, lantern_glass, Vector3(0, -0.53, 0), Vector3(0.12, 0.2, 0.12))
		_box_mesh(hook, _iron, Vector3(0, -0.65, 0), Vector3(0.16, 0.03, 0.16))
		var lamp := OmniLight3D.new()
		lamp.position = Vector3(0, -0.53, 0)
		lamp.light_color = Color("ffb060")
		lamp.light_energy = 0.8
		lamp.omni_range = 3.2
		hook.add_child(lamp)
		_hanging.append(hook)


## The little table in the middle of the shop, the board, the candle and the alchemist.
func _build_table() -> void:
	var table := _place_prop(ROUND_TABLE, TABLE_AT, TABLE_HEIGHT, 0.6)
	var surface := TABLE_AT + Vector3(0, TABLE_HEIGHT, 0)
	_place_board(surface, 0.0)
	_place_prop(STOOL, Vector3(TABLE_AT.x, 0, TABLE_AT.z - 0.7), 0.47, 0.3)
	_place_prop(STOOL, Vector3(TABLE_AT.x, 0, TABLE_AT.z + 0.7), 0.47, 1.4)
	_seat_opponent({
		"seat": "chair", "seat_height": 0.47, "height": 0.98, "girth": 0.92,
		"skin": Color("d8b8a0"), "coat": Color("3a2050"), "legs": Color("2a1a30"), "robe": true,
		"hat": "wizard_hood", "hat_color": Color("4a2a64"), "hair": Color("c8c4bc"),
		"beard": "long", "beard_color": Color("d8d4cc"),
		"extras": ["spectacles", "beads"], "phase": 0.6,
	}, Vector3(TABLE_AT.x, 0, TABLE_AT.z - 0.7), 0.0)
	# A candle on the far left of the table, in a brass holder, and a vial of violet potion.
	var wick := surface + Vector3(-0.33, 0.0, -0.24)
	_build_candle(wick)
	_build_vial(surface + Vector3(0.34, 0.0, -0.26))
	_dress_table(table, wick)


## The table's wood, re-dressed so its top stays sharp when it fills the seated view.
func _dress_table(table: Node3D, wick: Vector3) -> void:
	for mesh: MeshInstance3D in table.find_children("*", "MeshInstance3D", true, false):
		for index in mesh.mesh.get_surface_count():
			var original := mesh.get_active_material(index) as StandardMaterial3D
			if original == null or original.albedo_texture == null:
				continue
			var dressed := ShaderMaterial.new()
			dressed.shader = TABLE_TOP
			dressed.set_shader_parameter("albedo_tex", original.albedo_texture)
			dressed.set_shader_parameter("normal_tex", original.normal_texture)
			dressed.set_shader_parameter("orm_tex", original.roughness_texture)
			dressed.set_shader_parameter("top_y", TABLE_AT.y + TABLE_HEIGHT)
			dressed.set_shader_parameter("centre", Vector2(TABLE_AT.x, TABLE_AT.z))
			dressed.set_shader_parameter("wick", Vector2(wick.x, wick.z))
			mesh.set_surface_override_material(index, dressed)


## A beeswax candle burnt half down in a brass chamberstick, wax run down one side.
func _build_candle(wick: Vector3) -> void:
	var first_part := get_child_count()
	var brass := _paint(Color("6a4a20"), 0.35, 0.6)
	var dish := _lathe(PackedVector2Array([Vector2(0.0, 0.0), Vector2(0.062, 0.0), Vector2(0.066, 0.004),
		Vector2(0.066, 0.009), Vector2(0.06, 0.012), Vector2(0.03, 0.013), Vector2(0.022, 0.016),
		Vector2(0.02, 0.028), Vector2(0.025, 0.032), Vector2(0.025, 0.036), Vector2(0.019, 0.037), Vector2(0.0, 0.037)]))
	_mesh(self, dish, brass, wick)
	# A ring handle on one side of the dish.
	var handle := TorusMesh.new()
	handle.inner_radius = 0.012
	handle.outer_radius = 0.018
	handle.rings = 24
	handle.ring_segments = 12
	var ring := _mesh(self, handle, brass, wick + Vector3(0.072, 0.008, 0.0))
	ring.rotation.x = PI * 0.5
	# The candle: slightly uneven, a cupped top, wax runs down one side.
	var height := 0.1
	var wax := ShaderMaterial.new()
	wax.shader = WAX
	wax.set_shader_parameter("top_y", wick.y + 0.034 + height)
	var profile := PackedVector2Array([Vector2(0.0, 0.0), Vector2(0.019, 0.0)])
	for k in 9:
		var t := (k + 1) / 9.0
		profile.append(Vector2(0.019 + 0.0012 * sin(t * 11.0), t * height))
	profile.append_array([Vector2(0.017, height + 0.002), Vector2(0.008, height - 0.004), Vector2(0.0, height - 0.005)])
	var stick := _mesh(self, _lathe(profile), wax, wick + Vector3(0, 0.034, 0))
	stick.rotation.y = 0.6
	# Drips: thin runs down the side with a blob at the bottom of each.
	for k in 3:
		var angle := 0.8 + k * 0.7
		var length := 0.03 + 0.025 * k
		var side := Vector3(cos(angle), 0, sin(angle))
		var run := CapsuleMesh.new()
		run.radius = 0.0035
		run.height = length
		run.radial_segments = 12
		run.rings = 4
		_mesh(self, run, wax, wick + side * 0.0195 + Vector3(0, 0.034 + height - length * 0.5, 0))
		var blob := SphereMesh.new()
		blob.radius = 0.0055
		blob.height = 0.009
		blob.radial_segments = 16
		blob.rings = 8
		_mesh(self, blob, wax, wick + side * 0.02 + Vector3(0, 0.034 + height - length + 0.002, 0))
	# The wick and the flame.
	var top := wick + Vector3(0, 0.034 + height, 0)
	_mesh(self, _lathe(PackedVector2Array([Vector2(0.0012, -0.004), Vector2(0.0012, 0.008), Vector2(0.0, 0.011)]), 8),
		_paint(Color("1a1410"), 0.9), top).rotation.z = 0.15
	var flame := _mesh(self, _flame_shape(), _flame, top + Vector3(0, 0.006, 0))
	flame.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var core := _mesh(self, _flame_shape(), _glowing(Color("ff9a40"), 4.0), top + Vector3(0, 0.004, 0))
	core.scale = Vector3(0.55, 0.45, 0.55)
	core.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var glow := QuadMesh.new()
	glow.size = Vector2(0.22, 0.22)
	_mesh(self, glow, _halo, top + Vector3(0, 0.025, 0))
	# Sat right under its own light, the candle would only throw huge, blotchy shadows of
	# its drips across the table, and speckle itself; it casts none.
	for index in range(first_part, get_child_count()):
		var part := get_child(index) as GeometryInstance3D
		if part:
			part.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_candle = OmniLight3D.new()
	_candle.position = top + Vector3(0, 0.05, 0)
	_candle.light_color = Color("ffb070")
	_candle.omni_range = 2.4
	_candle.shadow_enabled = true
	_candle.shadow_blur = 1.0
	_candle.shadow_bias = 0.04
	_candle.shadow_normal_bias = 1.5
	add_child(_candle)


## A smooth teardrop flame, 3.5 cm tall.
static func _flame_shape() -> ArrayMesh:
	var profile := PackedVector2Array()
	for k in 13:
		var t := k / 12.0
		profile.append(Vector2(0.0065 * sin(PI * pow(t, 0.55)) * (1.0 - t * 0.35), t * 0.035))
	return _lathe(profile, 20)


## A little glass vial of glowing potion, its neck stoppered with cork.
func _build_vial(foot: Vector3) -> void:
	var glass := StandardMaterial3D.new()
	glass.albedo_color = Color(0.75, 0.7, 0.95, 0.28)
	glass.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	glass.roughness = 0.05
	glass.metallic_specular = 0.9
	glass.rim_enabled = true
	glass.rim = 0.8
	glass.rim_tint = 0.3
	var shell := PackedVector2Array([Vector2(0.0, 0.0), Vector2(0.021, 0.0), Vector2(0.024, 0.004),
		Vector2(0.024, 0.085), Vector2(0.02, 0.1), Vector2(0.009, 0.112), Vector2(0.008, 0.13),
		Vector2(0.0105, 0.133), Vector2(0.0105, 0.137), Vector2(0.0, 0.137)])
	var potion := PackedVector2Array([Vector2(0.0, 0.003), Vector2(0.021, 0.003), Vector2(0.022, 0.006),
		Vector2(0.022, 0.07), Vector2(0.0, 0.07)])
	_mesh(self, _lathe(potion), _potions[2], foot)
	var vial := _mesh(self, _lathe(shell), glass, foot)
	vial.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var cork := _lathe(PackedVector2Array([Vector2(0.0, 0.12), Vector2(0.0075, 0.12), Vector2(0.0085, 0.15),
		Vector2(0.0078, 0.153), Vector2(0.0, 0.153)]), 20)
	_mesh(self, cork, _cork, foot)


func _build_clutter() -> void:
	# A barrel by the door, a big glazed urn on a stand, crates, papers on the floor.
	_place_prop(BARREL, Vector3(-2.5, 0, 0.3), 0.9, 0.3)
	_box(_shelf_wood, Vector3(2.7, 0.35, -0.2), Vector3(0.7, 0.7, 0.7))
	var urn := SphereMesh.new()
	urn.radius = 0.32
	urn.height = 0.7
	urn.radial_segments = 48
	urn.rings = 24
	var glaze := _paint(Color("5a3a78"), 0.3)
	_mesh(self, urn, glaze, Vector3(2.7, 1.05, -0.2))
	_cyl(glaze, Vector3(2.7, 1.45, -0.2), 0.14, 0.12)
	_cyl(_brass, Vector3(2.7, 1.53, -0.2), 0.16, 0.04)
	_place_prop(CRATE, Vector3(-2.6, 0, -6.6), 0.5, 0.2)
	_place_prop(CRATE, Vector3(-2.6, 0.5, -6.6), 0.4, 0.7)
	_place_prop(JUG, Vector3(2.5, 0, -6.4), 0.4, 0.0)
	_place_prop(BOWL, Vector3(-0.2, 1.005, BACK + 0.6), 0.08, 0.0)
	var rng := RandomNumberGenerator.new()
	rng.seed = 7
	for k in 7:
		var at := Vector3(rng.randf_range(-1.8, 2.2), 0.008 + k * 0.001, rng.randf_range(-6.0, -1.0))
		if at.distance_to(TABLE_AT) < 1.0:
			continue
		_box(_paper, at, Vector3(0.21, 0.004, 0.28), Vector3(0, rng.randf() * TAU, 0))
	for k in 3:
		_place_prop(CANDLESTICK, Vector3(-2.9, 1.0 + k * 0.5, -1.6 - k * 1.7), 0.16, 0.0)


func _build_lights() -> void:
	# Cold moonlight through the window.
	var moon := SpotLight3D.new()
	moon.position = WINDOW + Vector3(0.0, 1.6, 0.4)
	moon.rotation_degrees = Vector3(-38, 0, 0)
	moon.light_color = Color("70b8ff")
	moon.light_energy = 5.0
	moon.spot_range = 9.0
	moon.spot_angle = 38.0
	moon.shadow_enabled = true
	add_child(moon)
	_omni(WINDOW + Vector3(0, 1.0, 0.6), Color("60a8ff"), 1.4, 3.5)
	# The shelves' own glow.
	for spot: Vector3 in [Vector3(-2.7, 1.4, -2.8), Vector3(2.7, 1.6, -1.6), Vector3(2.7, 1.2, -4.8), Vector3(-2.7, 1.6, -5.6)]:
		_omni(spot, Color("ff60c0"), 1.0, 3.0)
	_omni(Vector3(0.9, 1.4, BACK + 0.8), Color("ff70b0"), 1.0, 2.2)


## Glittering motes drifting up through the air.
func _build_motes() -> void:
	var mote := BoxMesh.new()
	mote.size = Vector3.ONE
	_motes = MultiMesh.new()
	_motes.transform_format = MultiMesh.TRANSFORM_3D
	_motes.mesh = mote
	_motes.instance_count = 60
	var node := MultiMeshInstance3D.new()
	node.multimesh = _motes
	node.material_override = _glowing(Color("ffa0e8"), 4.0)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(node)
