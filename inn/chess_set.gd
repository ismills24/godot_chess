extends "res://common/scene_kit.gd"
## A small wooden chess set: a framed board built from primitives and the wood-set pieces.
## Local space: origin under the board's centre, light (white) pieces' home rank toward +Z.
## The board can be any size from 2x2 to 10x10 (`files` x `ranks`, 8x8 by default). It always
## fits the footprint of an 8x8 board, so a smaller board has bigger squares and pieces, and
## the scenes it sits in never need to make room for it.
## `build(placement)` takes the piece-placement field of a FEN, so a set can show the start
## or any game in progress; `captured` lists taken pieces (FEN letters) to stand beside it.
## `resize` rebuilds the board empty at another size, for a game that isn't 8x8.
## A set can also be played on: `show_pieces` moves each piece to where the game has it,
## `mark` lights the chosen piece and where it can go, `show_tiles` marks special squares,
## and `square_index_at` reads a square off a point on the board.
## Beer: `spill_beer` throws beer onto squares and leaves a puddle on each, `dry_beer` dries them
## off again (common/beer_spill.gd). Slippery squares passed to `show_tiles` are shown this way.

## The width of a square on an 8x8 board; the longer side of any board spans 8 of these.
const SQUARE := 0.055
const FOOTPRINT := SQUARE * 8
const MAX_SIDE := 10
const START := "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR"
const KINDS := "pnbrqk"  # pawn 0, knight 1, bishop 2, rook 3, queen 4, king 5
## The board's playing surface, above the set's origin.
const SURFACE := 0.0245
const MOVE_SECONDS := 0.32
const WOOD_PIECE := preload("res://assets/pieces/wood_hd/wood_hd_piece.gd")
const WOOD_KINDS := ["pawn", "knight", "bishop", "rook", "queen", "king"]
## The wood pieces are modelled for 1.45-unit squares.
const WOOD_SQUARE := 1.45
## Roughly how tall each kind stands, in squares (pawn, knight, bishop, rook, queen, king);
## ability tags float just above.
const PIECE_HEIGHTS := [0.8, 1.0, 1.05, 0.85, 1.2, 1.35]
const BEER_SPILL := preload("res://common/beer_spill.gd")
## The tile shown as a beer puddle instead of a tint and a name.
const BEER_TILE := "slippery"

## The pieces' finish (wood_hd_piece.gd's `style`: wood, grain, plastic, cel, marble, metal,
## porcelain). Set before building.
static var piece_style := "wood"
## The board's finish, one of assets/boards/styles (the same names as the pieces' styles);
## empty matches piece_style. Set before building.
static var board_style := ""

## Board materials by style and size: [surface, frame].
static var _boards := {}
static var _chosen: StandardMaterial3D
static var _reach: StandardMaterial3D
static var _threat: StandardMaterial3D
static var _last: StandardMaterial3D
static var _check: StandardMaterial3D

## The board's size in squares, and the width of one square.
var files := 8
var ranks := 8
var square := SQUARE
## The pieces in play, by the game's piece id, each a node standing on its square.
var _live := {}
## Taken pieces lined up off the board, per colour (0 light, 1 dark).
var _taken := [0, 0]
var _marks: Node3D
## Markers on special squares (walls, fire...), rebuilt by show_tiles.
var _tiles: Node3D
## Beer puddles on the board (common/beer_spill.gd), made by build.
var _beer: Node3D
## Whether show_tiles has run since the board was built: the first call puts any Slippery
## squares' puddles straight down, later ones spill the new ones in.
var _tiles_shown := false
## Visual layers for the pieces, board and marks (see set_play_layers).
var _piece_layers := 1
## The board's own meshes: the playing surface and the frame.
var _board_meshes: Array[MeshInstance3D] = []


## Sets the board's size before build (or use resize after it). Clamped to 2..10 a side.
func configure(new_files: int, new_ranks: int) -> void:
	files = clampi(new_files, 2, MAX_SIDE)
	ranks = clampi(new_ranks, 2, MAX_SIDE)
	square = FOOTPRINT / maxi(files, ranks)


## Rebuilds the board at `new_files` x `new_ranks` with no pieces on it; follow with
## show_pieces. Keeps the set's visual layers.
func resize(new_files: int, new_ranks: int) -> void:
	for child in get_children():
		if child is Node3D and child.has_meta("symbol"):
			remove_child(child)
			child.queue_free()
	for mesh in _board_meshes:
		mesh.queue_free()
	_board_meshes.clear()
	for node in [_marks, _tiles, _beer]:
		if node:
			node.queue_free()
	_marks = null
	_tiles = null
	_beer = null
	_tiles_shown = false
	_live.clear()
	_taken = [0, 0]
	configure(new_files, new_ranks)
	build("")


func build(placement := START, captured := "") -> void:
	_make_materials()
	var style := board_style if board_style != "" else piece_style
	var board: Array = _board_materials(style, files, ranks, square)
	var field := Vector2(square * files, square * ranks)
	_board_meshes.append(_box_mesh(self, board[1], Vector3(0, 0.012, 0),
		Vector3(field.x + 0.05, 0.024, field.y + 0.05)))
	var surface := PlaneMesh.new()
	surface.size = field
	_board_meshes.append(_mesh(self, surface, board[0], Vector3(0, SURFACE, 0)))
	for mesh in _board_meshes:
		mesh.layers = _piece_layers
	_marks = Node3D.new()
	add_child(_marks)
	_tiles = Node3D.new()
	add_child(_tiles)
	_beer = BEER_SPILL.new()
	_beer.name = "Beer"
	_beer.square_size = square
	# Under the pieces' contact shadows (about 0.0005 up) and the move marks.
	_beer.lift = 0.0003
	_beer.layers = _piece_layers
	add_child(_beer)
	var rows := placement.split("/") if placement != "" else PackedStringArray()
	for row in rows.size():
		var rank := ranks - 1 - row
		var file := 0
		for symbol in rows[row]:
			if symbol.is_valid_int():
				file += symbol.to_int()
				continue
			_piece(self, square_center(file, rank), symbol)
			file += 1
	for symbol in captured:
		_piece(self, _taken_spot(symbol), symbol)


## The centre of the square at `file`, `rank` (rank 0 the light side's home rank), on the surface.
func square_center(file: int, rank: int) -> Vector3:
	return Vector3((file - (files - 1) * 0.5) * square, SURFACE, ((ranks - 1) * 0.5 - rank) * square)


## The centre of square `index` (rank * files + file, as the game numbers them), on the surface.
func square_point(index: int) -> Vector3:
	return square_center(index % files, index / files)


## The square (file, rank) under `local` (a point in the set's space), or (-1, -1) off the board.
func square_at(local: Vector3) -> Vector2i:
	var file := floori(local.x / square + files * 0.5)
	var rank := floori(ranks * 0.5 - local.z / square)
	if file < 0 or file >= files or rank < 0 or rank >= ranks:
		return Vector2i(-1, -1)
	return Vector2i(file, rank)


## The game's index (rank * files + file) of the square under `local`, or -1 off the board.
func square_index_at(local: Vector3) -> int:
	var at := square_at(local)
	return -1 if at.x < 0 else at.y * files + at.x


## Puts the set's pieces where the game has them. `pieces` holds {id, square, symbol} with
## squares numbered rank * files + file, and optionally `badges`: [{code, color}], the
## piece's abilities, shown as little tags beside it. The first call replaces the pieces
## built from the placement; after that, pieces that moved slide (knights hop) to their new
## squares, taken pieces are set aside off the board, and a pawn that promoted changes shape.
func show_pieces(pieces: Array, animate := true) -> void:
	if _live.is_empty():
		for child in get_children():
			if child is Node3D and child.has_meta("symbol"):
				remove_child(child)
				child.queue_free()
	var seen := {}
	for entry: Dictionary in pieces:
		var id: int = entry["id"]
		var symbol: String = entry["symbol"]
		var to := square_point(entry["square"])
		seen[id] = true
		var holder: Node3D = _live.get(id)
		if holder and holder.get_meta("symbol") != symbol:
			# Promoted: the old shape goes, the new one stands where it ends up.
			var from := holder.position
			holder.queue_free()
			holder = _piece(self, from, symbol)
			_live[id] = holder
		if not holder:
			holder = _piece(self, to, symbol)
			_live[id] = holder
		_set_badges(holder, entry.get("badges", []))
		if holder.position.distance_to(to) > 0.001:
			_slide(holder, to, symbol.to_lower() == "n", animate)
	for id: int in _live.keys():
		if not seen.has(id):
			var holder: Node3D = _live[id]
			_live.erase(id)
			_set_badges(holder, [])
			_slide(holder, _taken_spot(holder.get_meta("symbol")), true, animate)


## Marks the special squares: `tiles` holds {square, name, color} and optionally `id` (the
## tile's id). Each gets a tint over the square with its name written on it, except Slippery
## squares (id "slippery"), which get a beer puddle instead: squares that turned slippery since
## the last call get beer spilled on them, and puddles whose square isn't slippery any more dry off.
func show_tiles(tiles: Array) -> void:
	if not _tiles:
		return
	for child in _tiles.get_children():
		child.queue_free()
	var beer := {}
	for entry: Dictionary in tiles:
		if entry.get("id", "") == BEER_TILE:
			beer[int(entry["square"])] = square_point(entry["square"])
			continue
		var at := square_point(entry["square"])
		var color: Color = entry.get("color", Color.ORCHID)
		var tint := StandardMaterial3D.new()
		tint.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		tint.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		tint.albedo_color = Color(color, 0.5)
		var plane := PlaneMesh.new()
		plane.size = Vector2(square * 0.94, square * 0.94)
		var mesh := _mesh(_tiles, plane, tint, at + Vector3(0, 0.0006, 0))
		mesh.layers = _piece_layers
		var label := _tag(String(entry.get("name", "")), color.lightened(0.5), square * 0.2)
		label.billboard = BaseMaterial3D.BILLBOARD_DISABLED
		label.rotation.x = -PI * 0.5
		label.position = at + Vector3(0, 0.002, square * 0.3)
		_tiles.add_child(label)
	_beer.sync(beer, _tiles_shown)
	_tiles_shown = true


## Throws beer onto `squares` (game indices), a beat apart, leaving a puddle on each until
## dry_beer. The beer comes from the far (dark) side, the bartender's. With `animate` false the
## puddles are just there.
## With `source` (a world-space point, e.g. the lip of the bartender's mug) the beer arcs out of it.
func spill_beer(squares: Array, animate := true, source: Variant = null) -> void:
	var points := {}
	for index: int in squares:
		points[index] = square_point(index)
	_beer.spill_all(points, animate, _beer.to_local(source) if source is Vector3 else null)


## Dries the beer off `squares` (game indices); a short fade unless `animate` is false.
func dry_beer(squares: Array, animate := true) -> void:
	for index: int in squares:
		_beer.dry(index, animate)


## The squares with a beer puddle on them.
func beer_squares() -> Array:
	return _beer.puddle_keys() if _beer else []


## The beer node itself (common/beer_spill.gd), for its `settled` and `dried` signals.
func beer() -> Node3D:
	return _beer


## Lights the board: `chosen` the picked-up piece's square, `reach` where it can go (those
## in `threat` are captures), `last` the two squares of the last move, `check` a king in
## check. Squares are rank * files + file; -1 for none.
func mark(chosen: int, reach: Array, threat: Array, last: Array, check := -1) -> void:
	for child in _marks.get_children():
		child.queue_free()
	for index: int in last:
		_square_glow(index, _last, 0.94, 0.0008)
	if check >= 0:
		_square_glow(check, _check, 0.94, 0.0012)
	if chosen >= 0:
		_square_glow(chosen, _chosen, 0.94, 0.0016)
	for index: int in reach:
		if index in threat:
			# A capture: a ring round the edge of the square, under the piece.
			for side in 4:
				var along := Vector3(square * 0.84, 0.002, square * 0.12)
				var offset := Vector3(0, 0, square * 0.36)
				if side >= 2:
					along = Vector3(along.z, along.y, along.x)
					offset = Vector3(offset.z, 0, offset.x)
				if side % 2 == 1:
					offset = -offset
				_box_mesh(_marks, _threat, square_point(index) + offset + Vector3(0, 0.001, 0), along)
		else:
			var dot := CylinderMesh.new()
			dot.top_radius = square * 0.17
			dot.bottom_radius = square * 0.17
			dot.height = 0.003
			dot.radial_segments = 8
			dot.rings = 1
			_mesh(_marks, dot, _reach, square_point(index) + Vector3(0, 0.0015, 0))
	_layer_marks()


## Puts the board, every piece and any made later (promotions), and the marks on visual
## `layers`; the scenes use this to draw the board in play sharper than the room
## (common/sharp_layer.gd).
func set_play_layers(layers: int) -> void:
	_piece_layers = layers
	for child in get_children():
		if child is Node3D and child.has_meta("symbol"):
			for model in child.get_children():
				if model is Label3D:
					model.layers = layers
				else:
					model.set_render_layers(layers)
	for mesh in _board_meshes:
		mesh.layers = layers
	if _tiles:
		for tile in _tiles.get_children():
			(tile as VisualInstance3D).layers = layers
	if _beer:
		_beer.layers = layers
	_layer_marks()


func _layer_marks() -> void:
	if _marks:
		for mark_mesh in _marks.get_children():
			(mark_mesh as VisualInstance3D).layers = _piece_layers


func _square_glow(index: int, material: Material, fill: float, lift: float) -> void:
	var glow := PlaneMesh.new()
	glow.size = Vector2(square * fill, square * fill)
	_mesh(_marks, glow, material, square_point(index) + Vector3(0, lift, 0))


## A piece's ability tags: one short code per ability, in its colour, floating just over the
## piece's head so it's clear whose they are. Rebuilt only when they change.
func _set_badges(holder: Node3D, badges: Array) -> void:
	var codes: Array[String] = []
	for badge: Dictionary in badges:
		codes.append(String(badge["code"]))
	var key := ",".join(codes)
	if holder.get_meta("badges", "") == key:
		return
	holder.set_meta("badges", key)
	for child in holder.get_children():
		if child is Label3D:
			holder.remove_child(child)
			child.queue_free()
	var kind := maxi(KINDS.find(String(holder.get_meta("symbol")).to_lower()), 0)
	var codes_text := " ".join(codes)
	if codes_text != "":
		var first: Dictionary = badges[0]
		var label := _tag(codes_text, first.get("color", Color.WHITE) if badges.size() == 1 else Color("f4e2b8"),
			square * 0.3)
		label.position = Vector3(0, square * (PIECE_HEIGHTS[kind] + 0.2), 0)
		holder.add_child(label)


## A short label drawn over everything, `height` tall.
func _tag(text: String, color: Color, height: float) -> Label3D:
	var label := Label3D.new()
	label.text = text
	label.font_size = 48
	label.pixel_size = height / 48.0
	label.outline_size = 14
	label.modulate = color
	label.outline_modulate = Color(0.05, 0.03, 0.02)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.render_priority = 2
	label.outline_render_priority = 1
	label.layers = _piece_layers
	return label


## Where the next taken piece of `symbol`'s colour stands, off the board's right-hand edge.
func _taken_spot(symbol: String) -> Vector3:
	var light := symbol == symbol.to_upper()
	var side := 0 if light else 1
	var n: int = _taken[side]
	_taken[side] += 1
	var half := Vector2(square * files, square * ranks) * 0.5
	var z: float = (half.y - 0.02 - n * 0.035) * (1.0 if light else -1.0)
	return Vector3(half.x + 0.05 + 0.03 * (n % 2), 0.0, z)


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
	if _chosen:
		return
	# Candle-gold for your piece and its moves, ember red for captures and check, a dull
	# amber wash for the last move played.
	_chosen = _see_through(_glowing(Color("ffb648"), 0.9), 0.55)
	_reach = _glowing(Color("ffc860"), 1.1)
	_threat = _glowing(Color("ff5a2a"), 1.2)
	_last = _see_through(_glowing(Color("d88a34"), 0.8), 0.6)
	_check = _see_through(_glowing(Color("ff3a1a"), 1.0), 0.55)


## The playing surface's and the frame's materials for board `style`
## (assets/boards/styles/<style>.gdshader) at one board size, made once each.
static func _board_materials(style: String, board_files: int, board_ranks: int, size: float) -> Array:
	var key := "%s/%dx%d" % [style, board_files, board_ranks]
	if not _boards.has(key):
		var shader: Shader = load("res://assets/boards/styles/%s.gdshader" % style)
		var pair := []
		for is_frame in [false, true]:
			var material := ShaderMaterial.new()
			material.shader = shader
			material.set_shader_parameter("frame", is_frame)
			material.set_shader_parameter("square_size", size)
			material.set_shader_parameter("board_size", Vector2(board_files, board_ranks))
			pair.append(material)
		_boards[key] = pair
	return _boards[key]


static func _see_through(material: StandardMaterial3D, alpha: float) -> StandardMaterial3D:
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.albedo_color.a = alpha
	return material


## One piece standing at `at` (the board surface under its foot), built under its own node
## (returned) so it can be moved. The pieces are the high-resolution maple and walnut
## set (assets/pieces/wood_hd), scaled from its 1.45-unit squares to this board's.
func _piece(parent: Node3D, at: Vector3, symbol: String) -> Node3D:
	var holder := Node3D.new()
	holder.position = at
	holder.set_meta("symbol", symbol)
	parent.add_child(holder)
	var light := symbol == symbol.to_upper()
	# Anything that isn't one of the six standard pieces stands as a pawn.
	var kind := maxi(KINDS.find(symbol.to_lower()), 0)
	var model := WOOD_PIECE.new() as Node3D
	model.set("piece_kind", WOOD_KINDS[kind])
	model.set("dark_wood", not light)
	model.set("style", piece_style)
	model.render_layers = _piece_layers
	model.scale = Vector3.ONE * (square / WOOD_SQUARE)
	# The wood knight faces +X; turn it to look across the board at the other side.
	model.rotation.y = deg_to_rad((90.0 if kind == 1 else 0.0) + (0.0 if light else 180.0))
	holder.add_child(model)
	return holder
