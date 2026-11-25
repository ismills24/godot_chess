extends "res://places/common/place_world.gd"
## A beach under limestone cliffs in the late afternoon: pale sand running down to a turquoise
## sea, rollers breaking white along the shore, the cliffs on the left green along their tops
## with palms, and on the headland ahead an old ruined temple looking out to sea. Dark rocks lie
## about the sand. An old sailor sits on a barrel by an upturned boat with a board on a crate,
## his pipe going, waiting for someone to come along the beach.
## Space: you walk toward -Z; the sea is out to +X, the cliffs to -X. The shore follows
## shore_x(z), the same line the sea shader uses.

const SEA := preload("res://places/common/sea.gdshader")
const BARREL := preload("res://inn/props/barrel.glb")
const CRATE := preload("res://inn/props/crate.glb")
const BUCKET := preload("res://inn/props/bucket.glb")

const WATER := -0.3
const TABLE_AT := Vector2(0.6, -9.0)

var _sand := _terrain_material({"color_a": Color("e0c89c"), "color_b": Color("d0b484"), "low_color": Color("a08a68"),
	"low_height": -0.24, "scale": 0.35, "speckle": 0.35, "ripples": 1.0})
var _cliff := _terrain_material({"color_a": Color("c8b48e"), "color_b": Color("9a8a70"), "top_color": Color("4a6a2a"),
	"top_amount": 0.42, "scale": 0.22, "strata": 1.0, "speckle": 0.4, "cracks": 0.6})
var _boulder := _terrain_material({"color_a": Color("4a4238"), "color_b": Color("2e2a24"), "top_color": Color("5a6a3a"),
	"top_amount": 0.08, "scale": 0.8, "speckle": 0.5, "cracks": 0.8, "low_color": Color("1e2a24"), "low_height": -0.05})
var _temple_stone := _ashlar({"stone_color": Color("b8a888"), "stone_color_b": Color("9a8c70"),
	"mortar_color": Color("7a6e5a"), "block_size": Vector2(0.7, 0.4), "moss": 0.25, "moss_color": Color("5a6a3a"),
	"streaks": 0.6})
var _bark := _paint(Color("6a5a48"), 0.95)
var _drift := _paint(Color("9a8e7e"), 0.95)
var _hull := _paint(Color("3a5a6a"), 0.85)
var _hull_wood := _paint(Color("5a4434"), 0.9)
var _rope := _paint(Color("a89060"), 0.95)
var _gulls: Array[Node3D] = []


func build() -> void:
	opponent_name = "The old sailor"
	eye_height = 1.64
	board_light_color = Color("ffe8c0")
	board_light_energy = 0.3
	walk_path = [Vector2(-0.2, 6.5), Vector2(0.3, 2.0), Vector2(0.9, -3.5), Vector2(0.7, -7.2), Vector2(TABLE_AT.x, -8.4)]
	first_look = Vector3(-1.5, 6.5, -34.0)
	seat_eye = Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, -8.38) + 1.08, -8.38)
	var sun := _sun(34.0, 62.0, Color("ffe4b8"), 1.9)
	sun.directional_shadow_max_distance = 60.0
	_build_sand()
	_build_sea()
	_build_cliffs()
	_build_temple()
	_build_rocks()
	_build_palms()
	_build_camp()
	_build_gulls()
	_flush_boxes()
	_animate()


func _animate() -> void:
	super()
	for k in _gulls.size():
		var gull := _gulls[k]
		var angle := _elapsed * (0.18 + 0.05 * k) + k * 2.1
		var centre := Vector3(6.0 + k * 5.0, 9.0 + k * 1.5, -18.0 - k * 6.0)
		gull.position = centre + Vector3(cos(angle) * (7.0 + k), sin(_elapsed * 0.7 + k) * 0.6, sin(angle) * (5.0 + k))
		gull.rotation.y = -angle
		var flap := sin(_elapsed * (5.0 + k)) * 0.5 if fposmod(_elapsed + k * 1.7, 5.0) < 2.0 else 0.08
		gull.get_child(0).rotation.z = flap
		gull.get_child(1).rotation.z = -flap


static func shore_x(z: float) -> float:
	return 3.0 + 0.8 * sin(z * 0.25) + 0.4 * sin(z * 0.61 + 1.3)


## Where the cliff foot is along the beach: it swings out toward the sea far ahead, into the
## headland.
static func cliff_x(z: float) -> float:
	var out := maxf(0.0, -22.0 - z)
	return -7.5 + 0.6 * sin(z * 0.3) + out * 0.42


func ground_at(x: float, z: float) -> float:
	var d := x - shore_x(z)
	var y := WATER - 0.06 * d
	if d < -5.0:
		y += 0.03 * pow(-d - 5.0, 1.6)
	return y + 0.06 * sin(x * 0.9 + z * 0.4) * sin(z * 0.7)


func _build_sand() -> void:
	_heightfield(Vector2(-12, -60), Vector2(26, 80), 0.5, ground_at, _sand)


func _build_sea() -> void:
	var water := _animated_material(SEA)
	var plane := PlaneMesh.new()
	plane.size = Vector2(500, 600)
	plane.subdivide_width = 80
	plane.subdivide_depth = 80
	var sea := _mesh(self, plane, water, Vector3(248, WATER, -200))
	sea.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _cliff_height(x: float, z: float) -> float:
	var foot := cliff_x(z)
	var rise := smoothstep(foot + 1.2, foot - 1.6, x)
	var top := 7.0 + 1.5 * sin(z * 0.17) + 0.8 * sin(z * 0.53 + x * 0.3)
	var rough := 0.5 * sin(x * 1.7 + z * 0.9) + 0.35 * sin(z * 2.3 - x * 1.1)
	var y := lerpf(-0.5, top + rough * 0.4, rise) + rough * rise * (1.0 - rise) * 2.5
	# The headland drops down to the sea in a steep bluff at its tip.
	var tip := smoothstep(-44.0, -52.0, z)
	return lerpf(y, -2.0, tip)


func _build_cliffs() -> void:
	_heightfield(Vector2(-40, -60), Vector2(62, 80), 0.6, _cliff_height, _cliff, true)
	# Greenery along the cliff tops, scrub clinging to the face.
	var leaves := _foliage(Color("1e3a14"), Color("5a8a2a"), 0.03)
	var clumps: Array = []
	var rng := RandomNumberGenerator.new()
	rng.seed = 5
	for k in 260:
		var z := rng.randf_range(-50.0, 12.0)
		var x := cliff_x(z) - rng.randf_range(0.8, 14.0)
		var y := _cliff_height(x, z)
		if y < 4.0:
			continue
		var size := rng.randf_range(0.5, 1.4)
		clumps.append(_blob_at(Vector3(x, y + size * 0.3, z), Vector3(size * 1.3, size * 0.8, size * 1.2), rng.randf() * TAU))
	for k in 40:
		var z := rng.randf_range(-40.0, 10.0)
		var x := cliff_x(z) - rng.randf_range(-0.6, 1.0)
		var y := _cliff_height(x, z)
		if y < 0.6:
			continue
		clumps.append(_blob_at(Vector3(x, y, z), Vector3.ONE * rng.randf_range(0.25, 0.5), rng.randf() * TAU))
	_scatter(_lump_mesh(11, 0.45, false), leaves, clumps)


## The ruined temple on the headland: a squat square shrine with its doorway and a broken
## wall beside it, looking out to sea.
func _build_temple() -> void:
	var at := Vector3(-3.0, 0, -38.0)
	at.y = _cliff_height(at.x, at.z) - 0.3
	_box(_temple_stone, at + Vector3(0, 1.6, 0), Vector3(4.6, 3.2, 3.6))
	_box(_temple_stone, at + Vector3(0, 3.35, 0), Vector3(5.0, 0.3, 4.0))
	_box(_temple_stone, at + Vector3(0, 3.9, 0), Vector3(3.6, 0.8, 2.8))
	_box(_paint(Color("1a1610"), 1.0), at + Vector3(0, 1.0, 1.81), Vector3(1.0, 1.7, 0.04))
	for side: int in [-1, 1]:
		_box(_temple_stone, at + Vector3(side * 0.75, 1.0, 1.9), Vector3(0.35, 2.0, 0.3))
	_box(_temple_stone, at + Vector3(0, 2.15, 1.9), Vector3(1.9, 0.3, 0.35))
	# A broken wall and fallen blocks.
	_box(_temple_stone, at + Vector3(-4.0, 0.9, 0.5), Vector3(3.0, 1.8, 0.6))
	_box(_temple_stone, at + Vector3(-5.2, 1.6, 0.5), Vector3(0.8, 3.2, 0.7))
	_box(_temple_stone, at + Vector3(-2.4, 0.3, 2.6), Vector3(0.8, 0.5, 0.6), Vector3(0.2, 0.6, 0.1))
	_box(_temple_stone, at + Vector3(2.8, 0.25, 2.0), Vector3(0.7, 0.4, 0.5), Vector3(0, 1.2, 0.2))
	# A thatched lookout hut on stilts down the cliff path, nearer the beach.
	var hut := Vector3(-6.4, 0, -16.0)
	hut.y = _cliff_height(hut.x, hut.z)
	var thatch := _paint(Color("8a7044"), 1.0)
	for corner: Vector2 in [Vector2(-0.7, -0.7), Vector2(0.7, -0.7), Vector2(-0.7, 0.7), Vector2(0.7, 0.7)]:
		_box(_bark, hut + Vector3(corner.x, 1.0, corner.y), Vector3(0.1, 2.0 + 2.0, 0.1))
	_box(_hull_wood, hut + Vector3(0, 1.1, 0), Vector3(1.6, 0.08, 1.6))
	_box(thatch, hut + Vector3(0, 2.4, 0), Vector3(2.2, 0.5, 2.2), Vector3(0, PI * 0.25, 0))
	_box(thatch, hut + Vector3(0, 2.75, 0), Vector3(1.2, 0.4, 1.2), Vector3(0, PI * 0.25, 0))


func _build_rocks() -> void:
	var spots: Array[Vector4] = [Vector4(-2.8, -6.0, 1.6, 1), Vector4(-3.6, -3.2, 1.0, 2), Vector4(-1.4, -14.0, 1.2, 3),
		Vector4(2.4, -19.0, 0.9, 4), Vector4(3.8, -12.5, 0.5, 5), Vector4(-4.2, 2.0, 1.3, 6), Vector4(1.8, -26.0, 1.1, 7),
		Vector4(4.4, -30.0, 0.8, 8), Vector4(-0.5, -21.0, 0.6, 9), Vector4(4.0, 1.0, 0.4, 10)]
	for spot in spots:
		var y := ground_at(spot.x, spot.y)
		_rock(Vector3(spot.x, y + spot.z * 0.25, spot.y), Vector3(spot.z * 1.6, spot.z * 0.9, spot.z * 1.2), int(spot.w),
			_boulder, Vector3(0, spot.w * 1.3, 0.1))
	# Pebbles and shells scattered along the tide line.
	var shell := _paint(Color("e8dcc8"), 0.6)
	var rng := RandomNumberGenerator.new()
	rng.seed = 3
	for k in 60:
		var z := rng.randf_range(-30.0, 8.0)
		var x := shore_x(z) - rng.randf_range(0.5, 3.5)
		_box(shell if k % 3 == 0 else _boulder, Vector3(x, ground_at(x, z) + 0.01, z), Vector3(0.06, 0.03, 0.05),
			Vector3(0, rng.randf() * TAU, 0))
	# Seaweed in dark strands.
	var weed := _paint(Color("2a3018"), 0.9)
	for k in 30:
		var z := rng.randf_range(-24.0, 8.0)
		var x := shore_x(z) - rng.randf_range(1.0, 2.5)
		_box(weed, Vector3(x, ground_at(x, z) + 0.005, z), Vector3(0.5, 0.01, 0.06), Vector3(0, rng.randf() * TAU, 0))


func _build_palms() -> void:
	var fronds := _foliage(Color("2a4a18"), Color("6a9a30"), 0.05, {"tips": 0.7, "tip_color": Color("a89040")})
	var spots: Array[Vector3] = [Vector3(-8.5, 0, 4.0), Vector3(-9.5, 0, -2.0), Vector3(-8.2, 0, -9.0),
		Vector3(-10.5, 0, -13.0), Vector3(-6.5, 0, -20.0), Vector3(-5.0, 0, -27.0), Vector3(-1.5, 0, -33.0),
		Vector3(-6.0, 0, -36.0), Vector3(-11.0, 0, 8.0)]
	for k in spots.size():
		var at := spots[k]
		at.y = _cliff_height(at.x, at.z) - 0.2
		_palm(at, 4.0 + (k % 3) * 1.2, Vector3(1.0 + 0.4 * (k % 2), 0, 0.3 * ((k % 3) - 1)), k, _bark, fronds)


## The sailor's spot: a crate for a table, a barrel for him, a driftwood log for you, his boat
## pulled up and turned over, a rod stuck in the sand, a net, a bucket.
func _build_camp() -> void:
	var foot := Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, TABLE_AT.y), TABLE_AT.y)
	_place_prop(CRATE, foot - Vector3(0, 0.05, 0), 0.62, 0.15)
	_place_board(foot + Vector3(0, 0.57, 0), 0.0)
	var barrel_at := Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, TABLE_AT.y - 0.68), TABLE_AT.y - 0.68)
	_place_prop(BARREL, barrel_at - Vector3(0, 0.04, 0), 0.62, 0.0)
	_seat_opponent({
		"seat": "chair", "seat_height": 0.58, "height": 1.02, "girth": 1.08,
		"skin": Color("a8704c"), "coat": Color("2a4060"), "sleeves": Color("2a4060"), "legs": Color("3a3a48"),
		"hat": "straw", "hat_color": Color("c8a868"), "hair": Color("c8c0b0"),
		"beard": "full", "beard_color": Color("d8d0c4"),
		"extras": ["stripes", "pipe", "earring", "scarf"], "stripe_color": Color("d8d8d0"), "scarf_color": Color("a02a20"),
		"phase": 2.4,
	}, barrel_at, 0.0)
	# Your seat: a bleached driftwood log lying across.
	var log_at := Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, -8.38) + 0.16, -8.38)
	var trunk := CylinderMesh.new()
	trunk.top_radius = 0.17
	trunk.bottom_radius = 0.2
	trunk.height = 2.2
	trunk.radial_segments = 8
	_mesh(self, trunk, _drift, log_at).rotation = Vector3(0, 0.15, PI * 0.5)
	_box(_drift, log_at + Vector3(-1.0, 0.18, 0.1), Vector3(0.08, 0.5, 0.08), Vector3(0.3, 0, 0.6))
	# The upturned rowing boat.
	var boat := Node3D.new()
	boat.position = Vector3(-1.6, ground_at(-1.6, -9.8) + 0.05, -9.8)
	boat.rotation = Vector3(0, 0.5, 0.05)
	add_child(boat)
	for side: int in [-1, 1]:
		var plank := _box_mesh(boat, _hull, Vector3(side * 0.5, 0.25, 0), Vector3(0.06, 0.5, 3.4))
		plank.rotation.z = side * -0.5
	_box_mesh(boat, _hull, Vector3(0, 0.46, 0), Vector3(0.6, 0.06, 3.4))
	_box_mesh(boat, _hull_wood, Vector3(0, 0.5, 0), Vector3(0.1, 0.04, 3.5))
	for end: int in [-1, 1]:
		_box_mesh(boat, _hull, Vector3(0, 0.25, end * 1.72), Vector3(0.9, 0.45, 0.06))
	_box(_hull_wood, boat.position + Vector3(0.9, 0.04, 0.4), Vector3(0.12, 0.05, 2.0), Vector3(0, 0.3, 0))
	# His rod stuck in the sand, its line down into the sea.
	var rod_at := Vector3(2.0, ground_at(2.0, -9.6), -9.6)
	_box(_bark, rod_at + Vector3(0.35, 1.0, 0), Vector3(0.03, 2.4, 0.03), Vector3(0, 0, -0.5))
	_box(_rope, rod_at + Vector3(1.6, 1.0, 0), Vector3(1.6, 0.006, 0.006), Vector3(0, 0, -0.95))
	# A heap of net with floats, a bucket and a lantern.
	_scatter(_lump_mesh(21, 0.5, true), _paint(Color("5a5040"), 1.0),
		[_blob_at(Vector3(-0.5, ground_at(-0.5, -10.4) + 0.08, -10.4), Vector3(0.6, 0.2, 0.5), 0.3)])
	var float_paint := _paint(Color("c84a20"), 0.6)
	for k in 4:
		_box(float_paint, Vector3(-0.7 + k * 0.15, ground_at(-0.6, -10.3) + 0.22, -10.3 + (k % 2) * 0.2), Vector3.ONE * 0.08)
	_place_prop(BUCKET, Vector3(1.4, ground_at(1.4, -9.2), -9.2), 0.3, 0.0)
	var lantern := _glowing(Color("ffcc70"), 1.5)
	_box(_paint(Color("2a2420"), 0.6, 0.4), Vector3(1.2, foot.y + 0.62, TABLE_AT.y - 0.2), Vector3(0.12, 0.02, 0.12))
	_box(lantern, Vector3(1.2, foot.y + 0.72, TABLE_AT.y - 0.2), Vector3(0.09, 0.16, 0.09))
	# Footprints in the sand leading up from the water to his seat.
	var print_mark := _paint(Color("b89c70"), 1.0)
	for k in 14:
		var z := -9.0 + k * 0.45
		var x := 1.4 + 0.4 * sin(k * 0.6) + (0.12 if k % 2 == 0 else -0.12)
		_box(print_mark, Vector3(x, ground_at(x, z) + 0.004, z), Vector3(0.1, 0.006, 0.22), Vector3(0, 0.3, 0))


func _build_gulls() -> void:
	var white := _paint(Color("f0f0ec"), 0.8)
	var grey := _paint(Color("8a9098"), 0.8)
	for k in 3:
		var gull := Node3D.new()
		add_child(gull)
		for side: int in [-1, 1]:
			var wing := _pivot(gull, Vector3.ZERO)
			_box_mesh(wing, grey, Vector3(side * 0.35, 0, 0), Vector3(0.7, 0.02, 0.16))
		_box_mesh(gull, white, Vector3(0, 0, 0), Vector3(0.1, 0.1, 0.4))
		_gulls.append(gull)
