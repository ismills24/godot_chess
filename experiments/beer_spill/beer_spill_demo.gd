extends Node
## Beer spill demo: the inn's play view (no walk-in) with keys to try the spill on the real board.
##   B  spill beer on 3 random squares (at least one with a piece on it)
##   D  dry the oldest puddle
##   C  dry every puddle
##   S  act like the Slippery board effect: show_tiles with a fresh random set of Slippery
##      squares, so new ones spill in and the old ones dry off (the hook the engine effect uses)
## Run: Godot --path . res://experiments/beer_spill/BeerSpillDemo.tscn

const INN := preload("res://inn/Inn.tscn")

var _inn: Node
## Puddles spilled with B, oldest first.
var _spilled: Array = []


func _ready() -> void:
	_inn = INN.instantiate()
	_inn.set("skip_intro", true)
	add_child(_inn)
	var hud := CanvasLayer.new()
	hud.layer = 5
	add_child(hud)
	var hint := Label.new()
	hint.text = "B spill  ·  D dry oldest  ·  C dry all  ·  S random Slippery tiles (show_tiles)"
	hint.position = Vector2(20, 16)
	hint.add_theme_font_size_override("font_size", 18)
	hint.add_theme_color_override("font_outline_color", Color.BLACK)
	hint.add_theme_constant_override("outline_size", 6)
	hud.add_child(hint)


func _board() -> Node3D:
	return _inn.get("_board")


func _unhandled_key_input(event: InputEvent) -> void:
	if not (event is InputEventKey and event.pressed and not event.echo):
		return
	var board := _board()
	if board == null:
		return
	match (event as InputEventKey).keycode:
		KEY_B:
			var squares := _random_squares(3)
			board.spill_beer(squares)
			_spilled.append_array(squares)
		KEY_D:
			if not _spilled.is_empty():
				board.dry_beer([_spilled.pop_front()])
		KEY_C:
			board.dry_beer(board.beer_squares())
			_spilled.clear()
		KEY_S:
			# What the inn passes to show_tiles once the engine puts Slippery tiles down.
			var tiles := []
			for index: int in _random_squares(randi_range(2, 4)):
				tiles.append({"square": index, "id": "slippery", "name": "Slippery"})
			board.show_tiles(tiles)


## `count` distinct squares, at least one with a piece standing on it.
func _random_squares(count: int) -> Array:
	var board := _board()
	var occupied: Array = []
	var match_node: Node = _inn.get("_match")
	if match_node:
		for piece: Dictionary in match_node.Pieces():
			occupied.append(int(piece["square"]))
	var pool := range(board.files * board.ranks)
	pool.shuffle()
	var picked: Array = []
	if not occupied.is_empty():
		picked.append(occupied.pick_random())
	for index: int in pool:
		if picked.size() >= count:
			break
		if index not in picked:
			picked.append(index)
	return picked
