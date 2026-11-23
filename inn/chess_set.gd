extends "res://common/scene_kit.gd"
## A small wooden chess set built from primitives: a framed board and turned pieces.
## Local space: origin under the board's centre, light (white) pieces' home rank toward +Z.
## `build(placement)` takes the piece-placement field of a FEN, so a set can show the start
## or any game in progress; `captured` lists taken pieces (FEN letters) to stand beside it.
## A set can also be played on: `show_pieces` moves each piece to where the game has it,
## `mark` lights the chosen piece and where it can go, and `square_at` reads a square off a
## point on the board.

const SQUARE := 0.055
const START := "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"
const KINDS := "pnbrqk"  # pawn 0, knight 1, bishop 2, rook 3, queen 4, king 5
## The board's playing surface, above the set's origin.
const SURFACE := 0.0245
const MOVE_SECONDS := 0.32

static var _squares: StandardMaterial3D
static var _frame: StandardMaterial3D
static var _light: StandardMaterial3D
static var _dark: StandardMaterial3D
static var _chosen: StandardMaterial3D
static var _reach: StandardMaterial3D
static var _threat: StandardMaterial3D
static var _last: StandardMaterial3D
static var _check: StandardMaterial3D

## The pieces in play, by the game's piece id, each a node standing on its square.
var _live := {}
## Taken pieces lined up off the board, per colour (0 light, 1 dark).
var _taken := [0, 0]
var _marks: Node3D


func build(placement := START, captured := "") -> void:
	_make_materials()
	_box_mesh(self, _frame, Vector3(0, 0.012, 0), Vector3(SQUARE * 8 + 0.05, 0.024, SQUARE * 8 + 0.05))
	var surface := PlaneMesh.new()
	surface.size = Vector2(SQUARE * 8, SQUARE * 8)
	_mesh(self, surface, _squares, Vector3(0, SURFACE, 0))
	_marks = Node3D.new()
	add_child(_marks)
	var ranks := placement.split("/")
	for row in ranks.size():
		var rank := 7 - row
		var file := 0
		for symbol in ranks[row]:
			if symbol.is_valid_int():
				file += symbol.to_int()
				continue
			_piece(self, square_center(file, rank), symbol)
			file += 1
	for symbol in captured:
		_piece(self, _taken_spot(symbol), symbol)


## The centre of the square at `file`, `rank` (rank 0 the light side's home rank), on the surface.
static func square_center(file: int, rank: int) -> Vector3:
	return Vector3((file - 3.5) * SQUARE, SURFACE, (3.5 - rank) * SQUARE)


## The square (file, rank) under `local` (a point in the set's space), or (-1, -1) off the board.
static func square_at(local: Vector3) -> Vector2i:
	var file := floori(local.x / SQUARE + 4.0)
	var rank := floori(4.0 - local.z / SQUARE)
	if file < 0 or file > 7 or rank < 0 or rank > 7:
		return Vector2i(-1, -1)
	return Vector2i(file, rank)


## Puts the set's pieces where the game has them. `pieces` holds {id, square, symbol} with
## squares numbered rank * 8 + file. The first call replaces the pieces built from the
## placement; after that, pieces that moved slide (knights hop) to their new squares, taken
## pieces are set aside off the board, and a pawn that promoted changes shape.
func show_pieces(pieces: Array, animate := true) -> void:
	if _live.is_empty():
		for child in get_children():
			if child is Node3D and child.has_meta("symbol"):
				child.queue_free()
	var seen := {}
	for entry: Dictionary in pieces:
		var id: int = entry["id"]
		var symbol: String = entry["symbol"]
		var square: int = entry["square"]
		var to := square_center(square % 8, square / 8)
		seen[id] = true
		var holder: Node3D = _live.get(id)
		if holder and holder.get_meta("symbol") != symbol:
			# Promoted: the old shape goes, the new one stands where it ends up.
			var from := holder.position
			holder.queue_free()
			holder = _piece(self, from, symbol)
			_live[id] = holder
		if not holder:
			_live[id] = _piece(self, to, symbol)
			continue
		if holder.position.distance_to(to) > 0.001:
			_slide(holder, to, symbol.to_lower() == "n", animate)
	for id: int in _live.keys():
		if not seen.has(id):
			var holder: Node3D = _live[id]
			_live.erase(id)
			_slide(holder, _taken_spot(holder.get_meta("symbol")), true, animate)


## Lights the board: `chosen` the picked-up piece's square, `reach` where it can go (those
## in `threat` are captures), `last` the two squares of the last move, `check` a king in
## check. Squares are rank * 8 + file; -1 for none.
func mark(chosen: int, reach: Array, threat: Array, last: Array, check := -1) -> void:
	for child in _marks.get_children():
		child.queue_free()
	for square: int in last:
		_square_glow(square, _last, 0.94, 0.0008)
	if check >= 0:
		_square_glow(check, _check, 0.94, 0.0012)
	if chosen >= 0:
		_square_glow(chosen, _chosen, 0.94, 0.0016)
	for square: int in reach:
		if square in threat:
			# A capture: a ring round the edge of the square, under the piece.
			for side in 4:
				var along := Vector3(SQUARE * 0.84, 0.002, SQUARE * 0.12)
				var offset := Vector3(0, 0, SQUARE * 0.36)
				if side >= 2:
					along = Vector3(along.z, along.y, along.x)
					offset = Vector3(offset.z, 0, offset.x)
				if side % 2 == 1:
					offset = -offset
				_box_mesh(_marks, _threat, _square_point(square) + offset + Vector3(0, 0.001, 0), along)
		else:
			var dot := CylinderMesh.new()
			dot.top_radius = SQUARE * 0.17
			dot.bottom_radius = SQUARE * 0.17
			dot.height = 0.003
			dot.radial_segments = 8
			dot.rings = 1
			_mesh(_marks, dot, _reach, _square_point(square) + Vector3(0, 0.0015, 0))


func _square_point(square: int) -> Vector3:
	return square_center(square % 8, square / 8)


func _square_glow(square: int, material: Material, fill: float, lift: float) -> void:
	var glow := PlaneMesh.new()
	glow.size = Vector2(SQUARE * fill, SQUARE * fill)
	_mesh(_marks, glow, material, _square_point(square) + Vector3(0, lift, 0))


## Where the next taken piece of `symbol`'s colour stands, off the board's right-hand edge.
func _taken_spot(symbol: String) -> Vector3:
	var light := symbol == symbol.to_upper()
	var side := 0 if light else 1
	var n: int = _taken[side]
	_taken[side] += 1
	var z: float = (SQUARE * 4 - 0.02 - n * 0.035) * (1.0 if light else -1.0)
	return Vector3(SQUARE * 4 + 0.05 + 0.03 * (n % 2), 0.0, z)


func _slide(holder: Node3D, to: Vector3, hop: bool, animate: bool) -> void:
	if not animate:
		holder.position = to
		return
	var from := holder.position
	var lift := 0.05 if hop else 0.012
	var tween := holder.create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_method(func(t: float) -> void:
		holder.position = from.lerp(to, t) + Vector3(0, lift * sin(t * PI), 0),
		0.0, 1.0, MOVE_SECONDS)


func _make_materials() -> void:
	if _squares:
		return
	var checks := Image.create(8, 8, false, Image.FORMAT_RGB8)
	for file in 8:
		for rank in 8:
			checks.set_pixel(file, rank, Color("b48c5e") if (file + rank) % 2 == 0 else Color("644029"))
	_squares = StandardMaterial3D.new()
	_squares.albedo_texture = ImageTexture.create_from_image(checks)
	_squares.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	_squares.roughness = 0.55
	_frame = _paint(Color("2c1c12"), 0.6)
	_light = _paint(Color("c9a77a"), 0.5)
	# Near-black with a sheen, so the dark pieces stand out against the dark squares.
	_dark = _paint(Color("140c08"), 0.28)
	# Candle-gold for your piece and its moves, ember red for captures and check, a dull
	# amber wash for the last move played.
	_chosen = _see_through(_glowing(Color("ffb648"), 0.9), 0.55)
	_reach = _glowing(Color("ffc860"), 1.1)
	_threat = _glowing(Color("ff5a2a"), 1.2)
	_last = _see_through(_glowing(Color("d88a34"), 0.8), 0.6)
	_check = _see_through(_glowing(Color("ff3a1a"), 1.0), 0.55)


static func _see_through(material: StandardMaterial3D, alpha: float) -> StandardMaterial3D:
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.albedo_color.a = alpha
	return material


## One piece standing at `at` (the board surface under its foot), built under its own node
## (returned) so it can be moved.
func _piece(parent: Node3D, at: Vector3, symbol: String) -> Node3D:
	var holder := Node3D.new()
	holder.position = at
	holder.set_meta("symbol", symbol)
	parent.add_child(holder)
	var material := _light if symbol == symbol.to_upper() else _dark
	var kind := KINDS.find(symbol.to_lower())
	var height: float = [0.026, 0.03, 0.04, 0.034, 0.05, 0.055][kind]
	_cylinder(holder, Vector3(0, 0.004, 0), 0.019, 0.017, 0.008, material)
	_cylinder(holder, Vector3(0, 0.008 + height * 0.5, 0), 0.013, 0.008, height, material)
	var crown := Vector3(0, 0.008 + height, 0)
	match kind:
		0, 2:
			var head := SphereMesh.new()
			head.radius = 0.01 if kind == 0 else 0.009
			head.height = 0.02 if kind == 0 else 0.026
			head.radial_segments = 6
			head.rings = 3
			_mesh(holder, head, material, crown + Vector3(0, 0.006, 0))
		1:
			var facing := 0.0 if material == _light else PI
			var head := _box_mesh(holder, material, crown + Vector3(0, 0.008, 0), Vector3(0.012, 0.03, 0.024))
			head.rotation = Vector3(0.35, facing, 0)
		3:
			_cylinder(holder, crown + Vector3(0, 0.006, 0), 0.015, 0.014, 0.012, material)
		4, 5:
			_cylinder(holder, crown + Vector3(0, 0.004, 0), 0.016, 0.01, 0.008, material)
			if kind == 4:
				var orb := SphereMesh.new()
				orb.radius = 0.007
				orb.height = 0.014
				orb.radial_segments = 6
				orb.rings = 3
				_mesh(holder, orb, material, crown + Vector3(0, 0.014, 0))
			else:
				_box_mesh(holder, material, crown + Vector3(0, 0.018, 0), Vector3(0.005, 0.02, 0.005))
				_box_mesh(holder, material, crown + Vector3(0, 0.02, 0), Vector3(0.015, 0.005, 0.005))
	return holder
