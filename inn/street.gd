extends "res://common/scene_kit.gd"
## The street outside the inn, in the menu alley's look: wet cobbles under rain and teal mist,
## sooty brick, warm lamplight. The inn's front: a brick ground storey with leaded windows lit
## from inside and the open door, a jettied timber-and-plaster storey above, and THE HANGING
## PAWN board hung over the door between a pair of lanterns, one of them gone out.
## Taller neighbours crowd it on both sides.
## World space, shared with the inn: the front wall's outer face is FACE_Z, the street runs
## along x in front of it (+z).

const COBBLES := preload("res://menu/props/cobbles.jpg")
const COBBLE_NORMAL := preload("res://menu/props/cobbles_normal.jpg")
const STREET_COBBLES := preload("res://inn/street_cobbles.gdshader")
const STREET_RAIN := preload("res://inn/street_rain.gdshader")
const MIST := preload("res://menu/mist.gdshader")

## The inn's frontage: left and right ends, the outer face, the door (centre x, width, height).
const INN_LEFT := -5.1
const INN_RIGHT := 3.3
const FACE_Z := 1.45
const WALL_BACK := 1.1
const DOOR_X := 0.6
const DOOR_WIDTH := 1.1
const DOOR_HEIGHT := 2.2
const GROUND_STOREY := 3.3
const JETTY := 0.32
const UPPER_TOP := 6.7
## Brick tiles per metre, matching the menu alley's facades.
const BRICK_DENSITY := Vector2(0.267, 0.28)
## The pair of door lanterns hang symmetrically either side of the sign; this is the lit
## (left) one, the right one mirrors it across the door.
const LANTERN_AT := Vector3(DOOR_X - 1.22, 2.55, FACE_Z + 0.42)

var _stone := _paint(Color("414444"), 0.91)
var _timber := _paint(Color("2e2119"), 0.85)
var _iron := _paint(Color("2b2725"), 0.6, 0.5)
var _recess := _paint(Color("151a1b"), 1.0)
var _plaster := _paint(Color("5a544a"), 0.95)
var _slate := _paint(Color("2e3d41"), 0.92)
var _sidewall := _paint(Color("263337"), 0.95)
## The menu alley's window glass, lit from inside.
var _glass := _glowing(Color("d0681c"), 1.0)
var _paving: ShaderMaterial
var _rain: ShaderMaterial
var _lamp: OmniLight3D
var _board: Node3D


func build() -> void:
	_build_ground()
	_build_inn_front()
	_build_sign()
	_build_neighbours()
	_build_rain()
	_build_mist()
	_build_crates(Vector3(INN_RIGHT - 0.8, 0.05, FACE_Z + 0.4))
	_flush_boxes()
	_animate()


func _animate() -> void:
	super()
	_lamp.light_energy = 2.3 + 0.15 * sin(_elapsed * 6.1) + 0.06 * sin(_elapsed * 14.2)
	_paving.set_shader_parameter("lantern_strength", _lamp.light_energy)
	_board.rotation.x = 0.035 * sin(_elapsed * 1.2)


func _build_ground() -> void:
	_paving = _animated_material(STREET_COBBLES)
	_paving.set_shader_parameter("cobbles", COBBLES)
	_paving.set_shader_parameter("relief", COBBLE_NORMAL)
	_paving.set_shader_parameter("fire_at", LANTERN_AT)
	_paving.set_shader_parameter("backlight_at", Vector3(-9.0, 3.4, FACE_Z + 0.6))
	_paving.set_shader_parameter("lantern_at", LANTERN_AT)
	var street := PlaneMesh.new()
	street.size = Vector2(34.0, 12.0)
	_mesh(self, street, _paving, Vector3(0, 0, FACE_Z + 6.0))
	# A worn kerb and stone step at the inn's door; rubble along the walls.
	_box(_stone, Vector3(DOOR_X, 0.05, FACE_Z + 0.25), Vector3(DOOR_WIDTH + 0.5, 0.1, 0.5))
	for i in 12:
		var size := 0.1 + 0.1 * fposmod(i * 0.63, 1.0)
		_box(_stone, Vector3(-12.0 + i * 2.1 + 0.4 * sin(i * 5.2), 0.05, FACE_Z + 0.15 + 0.1 * fposmod(i * 0.37, 1.0)),
			Vector3(size, 0.07, size * 1.5), Vector3(0.2, i * 0.92, 0.15))


## The inn's own front.
func _build_inn_front() -> void:
	# Ground storey: sooty brick around the door, in panels so the bricks stay to scale.
	var door_left := DOOR_X - DOOR_WIDTH * 0.5
	var door_right := DOOR_X + DOOR_WIDTH * 0.5
	_brick_panel(Rect2(INN_LEFT, 0, door_left - INN_LEFT, GROUND_STOREY), 0.0)
	_brick_panel(Rect2(door_right, 0, INN_RIGHT - door_right, GROUND_STOREY), 0.7)
	_brick_panel(Rect2(door_left, DOOR_HEIGHT, DOOR_WIDTH, GROUND_STOREY - DOOR_HEIGHT), 1.3)
	# The wall's thickness behind the brick skin, and its reveals at the door.
	var depth := FACE_Z - WALL_BACK
	_box(_sidewall, Vector3((INN_LEFT + door_left) * 0.5, GROUND_STOREY * 0.5, WALL_BACK + depth * 0.5 - 0.01),
		Vector3(door_left - INN_LEFT, GROUND_STOREY, depth - 0.02))
	_box(_sidewall, Vector3((door_right + INN_RIGHT) * 0.5, GROUND_STOREY * 0.5, WALL_BACK + depth * 0.5 - 0.01),
		Vector3(INN_RIGHT - door_right, GROUND_STOREY, depth - 0.02))
	_box(_sidewall, Vector3(DOOR_X, (DOOR_HEIGHT + GROUND_STOREY) * 0.5, WALL_BACK + depth * 0.5 - 0.01),
		Vector3(DOOR_WIDTH, GROUND_STOREY - DOOR_HEIGHT, depth - 0.02))
	# The doorway: stone jambs, a heavy timber lintel, the threshold worn down the middle.
	for side: int in [-1, 1]:
		_box(_stone, Vector3(DOOR_X + side * (DOOR_WIDTH * 0.5 + 0.08), DOOR_HEIGHT * 0.5, FACE_Z + 0.02),
			Vector3(0.18, DOOR_HEIGHT + 0.05, depth + 0.06))
	_box(_timber, Vector3(DOOR_X, DOOR_HEIGHT + 0.1, FACE_Z - depth * 0.5 + 0.03), Vector3(DOOR_WIDTH + 0.5, 0.22, depth + 0.06))
	_box(_stone, Vector3(DOOR_X, 0.015, (WALL_BACK + FACE_Z) * 0.5), Vector3(DOOR_WIDTH, 0.03, depth))
	# Leaded windows lit from the bar room, two left of the door and one right.
	for x: float in [-1.35, -3.55]:
		_window(Vector3(x, 1.5, FACE_Z), true)
	_window(Vector3(2.4, 1.5, FACE_Z), true)
	# The jetty: a heavy beam on carved brackets, the upper storey overhanging the street.
	var front := FACE_Z + JETTY
	_box(_timber, Vector3((INN_LEFT + INN_RIGHT) * 0.5, GROUND_STOREY + 0.1, FACE_Z + JETTY * 0.5),
		Vector3(INN_RIGHT - INN_LEFT + 0.2, 0.22, JETTY + 0.12))
	for x: float in [INN_LEFT + 0.25, -2.5, DOOR_X - 1.1, DOOR_X + 1.1, INN_RIGHT - 0.25]:
		_box(_timber, Vector3(x, GROUND_STOREY - 0.22, FACE_Z + 0.14), Vector3(0.14, 0.34, 0.28), Vector3(-0.6, 0, 0))
	# Upper storey: plaster between dark studs, braces and rails; three windows, one lit.
	var upper := UPPER_TOP - GROUND_STOREY - 0.2
	var upper_mid := GROUND_STOREY + 0.2 + upper * 0.5
	_box(_plaster, Vector3((INN_LEFT + INN_RIGHT) * 0.5, upper_mid, front - 0.06), Vector3(INN_RIGHT - INN_LEFT, upper, 0.1))
	var studs := 12
	for i in studs + 1:
		var x := INN_LEFT + (INN_RIGHT - INN_LEFT) * i / float(studs)
		_box(_timber, Vector3(x, upper_mid, front), Vector3(0.13, upper, 0.06))
	for rail: float in [GROUND_STOREY + 0.28, GROUND_STOREY + 1.25, UPPER_TOP - 0.12]:
		_box(_timber, Vector3((INN_LEFT + INN_RIGHT) * 0.5, rail, front + 0.01), Vector3(INN_RIGHT - INN_LEFT + 0.1, 0.13, 0.07))
	for i in studs:
		if i % 3 == 1:
			continue
		var x := INN_LEFT + (INN_RIGHT - INN_LEFT) * (i + 0.5) / float(studs)
		var lean := 0.62 if i % 2 == 0 else -0.62
		_box(_timber, Vector3(x, GROUND_STOREY + 0.76, front + 0.005), Vector3(0.09, 1.1, 0.05), Vector3(0, 0, lean))
	for window: Array in [[-3.3, true], [DOOR_X, false], [2.35, false]]:
		_window(Vector3(window[0], GROUND_STOREY + 2.2, front), window[1])
	# Roof: slate eaves sloping back, a chimney stack with pots.
	_box(_slate, Vector3((INN_LEFT + INN_RIGHT) * 0.5, UPPER_TOP + 0.5, front - 0.6),
		Vector3(INN_RIGHT - INN_LEFT + 0.4, 0.14, 2.3), Vector3(-0.55, 0, 0))
	_box(_sidewall, Vector3(-3.8, UPPER_TOP + 1.3, front - 1.5), Vector3(0.7, 1.8, 0.6))
	for pot in 2:
		_box(_slate, Vector3(-3.95 + pot * 0.3, UPPER_TOP + 2.3, front - 1.5), Vector3(0.2, 0.3, 0.22))
	# A pair of lanterns on iron brackets either side of the door: the left one burning,
	# lighting the step and the sign; the right one long gone out.
	_door_lantern(LANTERN_AT, true)
	_door_lantern(Vector3(2.0 * DOOR_X - LANTERN_AT.x, LANTERN_AT.y, LANTERN_AT.z), false)
	_lamp = _omni(LANTERN_AT + Vector3(0, -0.1, 0.12), Color("ffad62"), 2.3, 6.0)


## A square iron lantern hung from a wall bracket; `at` is the glass's centre.
func _door_lantern(at: Vector3, lit: bool) -> void:
	_box(_iron, at + Vector3(0, 0.36, -0.22), Vector3(0.05, 0.05, 0.48))
	_box(_iron, at + Vector3(0, 0.25, -0.4), Vector3(0.12, 0.3, 0.04))
	_box(_iron, at + Vector3(0, 0.22, 0), Vector3(0.18, 0.04, 0.18))
	# Gone out: the same amber glass, dark, just catching the street.
	_box(_glowing(Color("ffb35c"), 3.4) if lit else _glowing(Color("6a5a44"), 0.55), at, Vector3(0.13, 0.22, 0.13))
	for corner: Vector2 in [Vector2(-1, -1), Vector2(-1, 1), Vector2(1, -1), Vector2(1, 1)]:
		_box(_iron, at + Vector3(corner.x * 0.07, 0, corner.y * 0.07), Vector3(0.02, 0.24, 0.02))
	_box(_iron, at + Vector3(0, -0.13, 0), Vector3(0.16, 0.03, 0.16))
	if lit:
		_halo(at, 1.8, 0.3, Color("d48d45"))


## A brick skin on the inn's face covering `area` (x, y, width, height) at FACE_Z.
func _brick_panel(area: Rect2, age: float) -> void:
	var brick := _masonry(Color("c6a898"), age, true)
	brick.set_shader_parameter("tile_scale", area.size * BRICK_DENSITY)
	var quad := QuadMesh.new()
	quad.size = area.size
	_mesh(self, quad, brick, Vector3(area.position.x + area.size.x * 0.5, area.position.y + area.size.y * 0.5, FACE_Z))


## A window as in the menu alley, turned to face the street: a dark recess, one tall pane
## (orange glass when `lit`), a timber mullion and transom, side frames, a stone sill and
## lintel. `at` is its centre on the wall face.
func _window(at: Vector3, lit: bool) -> void:
	_box(_recess, at + Vector3(0, 0, 0.033), Vector3(1.04, 1.43, 0.12))
	_box(_glass if lit else _recess, at + Vector3(0, 0, 0.11), Vector3(0.84, 1.26, 0.05))
	for end: int in [-1, 1]:
		_box(_timber, at + Vector3(end * 0.45, 0, 0.17), Vector3(0.12, 1.39, 0.09))
	_box(_timber, at + Vector3(0, 0, 0.17), Vector3(0.055, 1.26, 0.09))
	_box(_timber, at + Vector3(0, 0.1, 0.17), Vector3(0.92, 0.075, 0.10))
	_box(_stone, at + Vector3(0, -0.76, 0.14), Vector3(1.21, 0.17, 0.41))
	_box(_stone, at + Vector3(0, 0.77, 0.14), Vector3(1.14, 0.14, 0.33))


## THE HANGING PAWN: a board hung on chains from the jetty beam over the door.
func _build_sign() -> void:
	const SIZE := Vector2(1.9, 0.66)
	## Hung a little crooked: one chain longer than the other drops the right end.
	const TILT := -0.1
	var hang := Vector3(DOOR_X, GROUND_STOREY - 0.02, FACE_Z + 0.3)
	_board = _pivot(self, hang)
	var centre := Vector3(0, -0.32 - SIZE.y * 0.5, 0)
	var face := _pivot(_board, centre)
	face.rotation.z = TILT
	var chain := _paint(Color("1e1b19"), 0.6, 0.6)
	for side: int in [-1, 1]:
		var x := side * (SIZE.x * 0.5 - 0.12)
		# Where this chain meets the board's top edge once it's tilted.
		var hook := centre + Vector3(x, SIZE.y * 0.5 + 0.02, 0).rotated(Vector3.BACK, TILT)
		_box_mesh(_board, chain, Vector3(hook.x, hook.y * 0.5, 0), Vector3(0.025, -hook.y, 0.025))
		_box_mesh(_board, _iron, Vector3(hook.x, 0.0, 0), Vector3(0.08, 0.05, 0.12))
	var board := _paint(Color("24170e"), 0.85)
	for plank in 3:
		_box_mesh(face, board, Vector3(0, (plank - 1) * SIZE.y / 3.0, 0.004 * (plank % 2)),
			Vector3(SIZE.x - 0.02 * (plank % 2), SIZE.y / 3.0 - 0.008, 0.05))
	for edge: int in [-1, 1]:
		_box_mesh(face, _timber, Vector3(0, edge * (SIZE.y * 0.5 + 0.02), 0.005), Vector3(SIZE.x + 0.08, 0.045, 0.07))
		_box_mesh(face, _timber, Vector3(edge * (SIZE.x * 0.5 + 0.02), 0, 0.005), Vector3(0.045, SIZE.y + 0.08, 0.07))
		_box_mesh(face, _iron, Vector3(edge * (SIZE.x * 0.5 - 0.12), 0, 0.03), Vector3(0.05, SIZE.y + 0.09, 0.012))
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Georgia", "Liberation Serif", "Noto Serif", "DejaVu Serif"])
	font.font_weight = 900
	var gilt := _glowing(Color("d8a458"), 0.45)
	_sign_text(face, font, gilt, "THE HANGING", 0.0017, Vector3(0, 0.15, 0.035))
	# PAWN and a gilt pawn beside it, the pair centred on the board.
	const PAWN_GAP := 0.07
	const CELL := 0.023
	var word := font.get_string_size("PAWN", HORIZONTAL_ALIGNMENT_LEFT, -1, 96).x * 0.003
	var pawn_width := CELL * 6.0
	var shift := (PAWN_GAP + pawn_width) * 0.5
	_sign_text(face, font, gilt, "PAWN", 0.003, Vector3(-shift, -0.1, 0.035))
	_sign_pawn(face, gilt, Vector3(word * 0.5 - shift + PAWN_GAP + pawn_width * 0.5, -0.1, 0.035), CELL)


func _sign_text(parent: Node3D, font: Font, material: Material, text: String, pixel: float, at: Vector3) -> void:
	var letters := TextMesh.new()
	letters.text = text
	letters.font = font
	letters.font_size = 96
	letters.pixel_size = pixel
	letters.depth = 0.01
	_mesh(parent, letters, material, at)


## A blocky pawn in relief, `cell` metres per block, centred on `at`.
func _sign_pawn(parent: Node3D, material: Material, at: Vector3, cell: float) -> void:
	const ART := [
		"..##..",
		".####.",
		".####.",
		"..##..",
		".####.",
		"..##..",
		"..##..",
		".####.",
		"######",
		"######",
	]
	for row in ART.size():
		for column in 6:
			if ART[row][column] == "#":
				var spot := Vector3((column - 2.5) * cell, (ART.size() * 0.5 - row - 0.5) * cell, 0)
				_box_mesh(parent, material, at + spot, Vector3(cell, cell, 0.012))


## Empties stacked against the wall by the door: slatted crates, one knocked askew.
func _build_crates(at: Vector3) -> void:
	var slats := _paint(Color("4a3626"), 0.9)
	for crate: Array in [[Vector3(0, 0, 0), 0.1], [Vector3(-0.62, 0, 0.05), -0.15], [Vector3(-0.3, 0.55, 0.02), 0.35]]:
		var base: Vector3 = at + crate[0]
		var yaw: float = crate[1]
		var turn := Basis(Vector3.UP, yaw)
		for slat in 4:
			for face: int in [-1, 1]:
				_box(slats, base + turn * Vector3(0, 0.08 + slat * 0.13, face * 0.26), Vector3(0.55, 0.1, 0.03), Vector3(0, yaw, 0))
				_box(slats, base + turn * Vector3(face * 0.26, 0.08 + slat * 0.13, 0), Vector3(0.03, 0.1, 0.5), Vector3(0, yaw, 0))
		for corner: Vector2 in [Vector2(-1, -1), Vector2(-1, 1), Vector2(1, -1), Vector2(1, 1)]:
			_box(_timber, base + turn * Vector3(corner.x * 0.26, 0.26, corner.y * 0.26), Vector3(0.05, 0.52, 0.05), Vector3(0, yaw, 0))
	# A couple of empties dumped in the top one.
	for bottle in 2:
		_cylinder(self, at + Vector3(-0.35 + bottle * 0.12, 0.85, 0.0), 0.035, 0.03, 0.22, _glowing(Color("1e3a2a"), 0.2))


## Taller brick houses either side, set a little in and out of the inn's line.
func _build_neighbours() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 911
	for house: Array in [[-16.0, INN_LEFT, FACE_Z + 0.18, 9.4, 0.0], [INN_RIGHT, 16.0, FACE_Z - 0.22, 8.6, 2.0]]:
		var left: float = house[0]
		var right: float = house[1]
		var face: float = house[2]
		var height: float = house[3]
		var brick := _masonry(Color("d0b0a0") if house[4] == 0.0 else Color("b19f91"), house[4], true)
		brick.set_shader_parameter("tile_scale", Vector2(right - left, height) * BRICK_DENSITY)
		var quad := QuadMesh.new()
		quad.size = Vector2(right - left, height)
		_mesh(self, quad, brick, Vector3((left + right) * 0.5, height * 0.5, face))
		_box(_sidewall, Vector3((left + right) * 0.5, height * 0.5, face - 1.4), Vector3(right - left, height, 2.78))
		# The party wall where the house meets the inn's lower front.
		var edge := right if left < 0 else left
		_box(_sidewall, Vector3(edge, height * 0.5, (face + WALL_BACK) * 0.5), Vector3(0.12, height, absf(face - WALL_BACK) + 0.4))
		_box(_stone, Vector3((left + right) * 0.5, height - 0.14, face + 0.05), Vector3(right - left, 0.28, 0.2))
		_box(_slate, Vector3((left + right) * 0.5, height + 0.4, face - 1.0), Vector3(right - left + 0.2, 0.16, 2.6),
			Vector3(-0.3, 0, 0))
		for floor in 3:
			var y := 1.55 + 2.4 * floor
			_box(_stone, Vector3((left + right) * 0.5, y - 0.95, face + 0.04), Vector3(right - left, 0.11, 0.1))
			var x := (right - 1.3) if left < 0 else (left + 1.3)
			while x > left + 0.8 and x < right - 0.8:
				var lit := rng.randf() < 0.3
				if floor == 0 and absf(x - (INN_LEFT - 3.0 if left < 0 else INN_RIGHT + 3.2)) < 0.9:
					_house_door(Vector3(x, 1.1, face))
				else:
					_window(Vector3(x, y, face), lit)
				x += -2.4 if left < 0 else 2.4
		# A drainpipe down the corner by the inn.
		var pipe_x := right - 0.25 if left < 0 else left + 0.25
		_box(_iron, Vector3(pipe_x, height * 0.48, face + 0.08), Vector3(0.09, height * 0.95, 0.09))
	# Wall lamps further along, glowing into the mist (no light of their own).
	for lamp: Vector3 in [Vector3(-9.5, 3.3, FACE_Z + 0.5), Vector3(8.8, 3.1, FACE_Z + 0.1)]:
		_box(_iron, lamp + Vector3(0, 0.2, -0.25), Vector3(0.05, 0.05, 0.5))
		_box(_iron, lamp + Vector3(0, 0.08, 0), Vector3(0.18, 0.06, 0.18))
		_box(_glowing(Color("ffb35c"), 3.0), lamp + Vector3(0, -0.1, 0), Vector3(0.14, 0.22, 0.14))
		_halo(lamp + Vector3(0, -0.1, 0.15), 3.2, 0.5, Color("e0954c"))


func _house_door(at: Vector3) -> void:
	_box(_recess, at, Vector3(1.17, 2.04, 0.1))
	for i in 5:
		_box(_timber, at + Vector3((i - 2) * 0.204, 0, 0.06), Vector3(0.19, 1.91, 0.06))
	for edge: int in [-1, 1]:
		_box(_stone, at + Vector3(edge * 0.6, 0, 0.08), Vector3(0.14, 2.13, 0.2))
	_box(_stone, at + Vector3(0, 1.03, 0.1), Vector3(1.4, 0.21, 0.24))


## A soft glow sheet facing the street.
func _halo(at: Vector3, size: float, opacity: float, color: Color) -> void:
	var haze := ShaderMaterial.new()
	haze.shader = MIST
	haze.set_shader_parameter("halo", true)
	haze.set_shader_parameter("opacity", opacity)
	haze.set_shader_parameter("mist_color", color)
	var glow := QuadMesh.new()
	glow.size = Vector2(size, size)
	_mesh(self, glow, haze, at).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _build_rain() -> void:
	var streak := QuadMesh.new()
	streak.size = Vector2(0.039, 0.37)
	var rain := MultiMesh.new()
	rain.transform_format = MultiMesh.TRANSFORM_3D
	rain.use_custom_data = true
	rain.mesh = streak
	rain.instance_count = 300
	var from := Vector2(-9.0, FACE_Z + JETTY + 0.1)
	var span := Vector2(19.0, 11.0)
	rain.custom_aabb = AABB(Vector3(from.x - 0.5, -0.3, from.y), Vector3(span.x + 1.0, 8.6, span.y))
	var rng := RandomNumberGenerator.new()
	rng.seed = 1827
	for i in rain.instance_count:
		rain.set_instance_transform(i, Transform3D.IDENTITY)
		rain.set_instance_custom_data(i, Color(rng.randf(), rng.randf(), rng.randf(), rng.randf()))
	var streaks := MultiMeshInstance3D.new()
	streaks.multimesh = rain
	streaks.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_rain = _animated_material(STREET_RAIN)
	_rain.set_shader_parameter("rain_from", from)
	_rain.set_shader_parameter("rain_span", span)
	_rain.set_shader_parameter("fire_at", LANTERN_AT)
	_rain.set_shader_parameter("lantern_at", LANTERN_AT)
	streaks.material_override = _rain
	add_child(streaks)


func _build_mist() -> void:
	# Teal banks drifting down the street either side, kept off the walk to the door.
	var banks := [
		[Vector3(-9.5, 1.6, FACE_Z + 1.6), Vector2(10.0, 3.2), 0.0],
		[Vector3(9.0, 1.4, FACE_Z + 1.2), Vector2(10.0, 2.8), 1.7],
		[Vector3(-4.0, 4.6, FACE_Z + 2.4), Vector2(9.0, 3.0), 3.1],
		[Vector3(5.0, 5.0, FACE_Z + 2.0), Vector2(9.0, 3.4), 4.4],
		[Vector3(0.0, 0.45, FACE_Z + 5.5), Vector2(16.0, 0.9), 5.8],
	]
	for i in banks.size():
		var bank: Array = banks[i]
		var mist := _animated_material(MIST)
		mist.set_shader_parameter("phase", bank[2])
		mist.set_shader_parameter("opacity", 0.22 + 0.05 * i)
		mist.set_shader_parameter("mist_color", Color("355d63").lerp(Color("507c83"), i / 4.0))
		_mist_sheet(mist, bank[0], bank[1])
