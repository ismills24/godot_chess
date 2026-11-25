extends "res://places/common/place_world.gd"
## A mountain top in early winter sun: a great bare slab of weathered grey sandstone, pitted
## with pools of ice, breaking off in a jutting ledge over a drop of hundreds of metres. Below,
## brown wooded valleys and pale fields fold away into ridge after ridge of blue hills under a
## wide streaky sky. Pitch pines and scrub hold on along the right. A hermit sits cross-legged
## near the edge with a board on a flat stone, the whole view behind him.
## Space: you walk toward -Z; the drop is ahead (-Z) and to the left (-X).

const VALLEY_FLOOR := -190.0
const TABLE_AT := Vector2(0.4, -7.9)

var _sandstone := _terrain_material({"color_a": Color("6e6c66"), "color_b": Color("545450"), "top_color": Color("7a786e"),
	"top_amount": 0.15, "scale": 0.45, "strata": 0.5, "speckle": 0.45, "cracks": 0.5,
	"low_color": Color("5a5c58"), "low_height": -1.4})
var _lichen := _terrain_material({"color_a": Color("8a8a7c"), "color_b": Color("6a6c62"), "top_color": Color("7a8a5a"),
	"top_amount": 0.25, "scale": 0.6, "speckle": 0.5, "cracks": 0.4})
var _valley := _terrain_material({"color_a": Color("5a4430"), "color_b": Color("3a3424"), "top_color": Color("a08860"),
	"top_amount": 0.18, "scale": 0.012, "speckle": 0.2})
var _ice := _paint(Color("a8b4bc"), 0.08, 0.1)
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
	sun.directional_shadow_max_distance = 50.0
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
	if e < 0.0:
		# Over the edge: a rounded lip, then sheer cliff.
		y -= 1.2 * smoothstep(0.0, 0.8, -e) + 80.0 * smoothstep(0.6, 3.0, -e)
	return y


func _build_slab() -> void:
	_heightfield(Vector2(-12, -18), Vector2(30, 28), 0.25, _slab_height, _sandstone, true)


## The jutting ledge: an overhanging lip of rock, its underside cut back into the cliff.
func _build_ledge() -> void:
	var lip := Vector3(-3.2, -0.55, -12.2)
	_rock(lip, Vector3(2.6, 0.7, 1.8), 31, _lichen, Vector3(0.05, 0.4, 0.04))
	_rock(lip + Vector3(-1.2, -1.6, 0.6), Vector3(1.8, 1.8, 1.6), 32, _lichen, Vector3(0, 1.1, 0))
	_rock(lip + Vector3(0.5, -4.5, 1.6), Vector3(2.4, 3.2, 2.2), 33, _lichen)
	# Boulders and broken slabs about the top.
	var spots: Array[Vector4] = [Vector4(5.5, -3.0, 0.8, 41), Vector4(7.0, 1.5, 1.1, 42), Vector4(-4.0, 2.5, 0.6, 43),
		Vector4(3.8, -8.5, 0.7, 44), Vector4(-2.5, -4.0, 0.35, 45)]
	for spot in spots:
		_rock(Vector3(spot.x, ground_at(spot.x, spot.y) + spot.z * 0.2, spot.y), Vector3(spot.z * 1.5, spot.z * 0.7, spot.z * 1.3),
			int(spot.w), _lichen, Vector3(0, spot.w, 0))


## Shallow hollows in the slab holding frozen rainwater.
func _build_pools() -> void:
	var pools: Array[Vector3] = [Vector3(-1.2, -2.0, 0.9), Vector3(1.6, -5.2, 0.6), Vector3(-2.8, -6.5, 0.7),
		Vector3(2.8, 1.2, 0.5), Vector3(-0.4, 2.6, 0.8), Vector3(-1.8, -9.0, 0.5)]
	for pool in pools:
		var y := ground_at(pool.x, pool.y) + 0.015
		var sheet := CylinderMesh.new()
		sheet.top_radius = pool.z
		sheet.bottom_radius = pool.z
		sheet.height = 0.02
		sheet.radial_segments = 9
		var node := _mesh(self, sheet, _ice, Vector3(pool.x, y, pool.y))
		node.scale = Vector3(1.4, 1.0, 0.8)
		node.rotation.y = pool.x
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


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
	_scatter(_lump_mesh(17, 0.5, false, 8, 5), _needles, clumps)
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
	_scatter(_lump_mesh(19, 0.5, false), _evergreen, scrub)
	_scatter(_lump_mesh(23, 0.7, true, 7, 4), _scrub, brush)


## The hermit's spot near the edge: a flat stone for the board, a staff, a bowl and a bundle.
func _build_spot() -> void:
	var stone_at := Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, TABLE_AT.y), TABLE_AT.y)
	_box(_lichen, stone_at + Vector3(0, 0.08, 0), Vector3(0.62, 0.2, 0.6), Vector3(0, 0.1, 0))
	_place_board(stone_at + Vector3(0, 0.18, 0), 0.0)
	var seat := Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, TABLE_AT.y - 0.66), TABLE_AT.y - 0.66)
	_seat_opponent({
		"seat": "ground", "height": 0.96, "girth": 0.88,
		"skin": Color("a87a58"), "coat": Color("6a5a44"), "legs": Color("5a4a38"), "robe": true,
		"hat": "none", "hair": Color("e0dcd4"), "bald": true,
		"beard": "long", "beard_color": Color("ece8e0"),
		"extras": ["beads"], "phase": 3.3,
	}, seat, 0.0)
	var staff := _paint(Color("5a4028"), 0.9)
	_box(staff, seat + Vector3(-0.55, 0.85, 0.1), Vector3(0.04, 1.8, 0.04), Vector3(0.1, 0, 0.35))
	_cyl(_paint(Color("6a4a30"), 0.7), seat + Vector3(0.55, 0.04, 0.35), 0.08, 0.08)
	_scatter(_lump_mesh(27, 0.3, true), _paint(Color("5a4a34"), 1.0), [_blob_at(seat + Vector3(-0.6, 0.12, -0.3), Vector3(0.3, 0.2, 0.25), 0.4)])
	# A cushion of folded blanket for you, across the board.
	_box(_paint(Color("7a3a2a"), 1.0), Vector3(TABLE_AT.x, ground_at(TABLE_AT.x, -7.3) + 0.04, -7.3), Vector3(0.5, 0.07, 0.4))


func _build_hawks() -> void:
	var feathers := _paint(Color("2a2420"), 0.9)
	for k in 2:
		var hawk := Node3D.new()
		add_child(hawk)
		_box_mesh(hawk, feathers, Vector3.ZERO, Vector3(1.6, 0.05, 0.3))
		_box_mesh(hawk, feathers, Vector3(0, 0, 0.1), Vector3(0.14, 0.1, 0.6))
		_hawks.append(hawk)
