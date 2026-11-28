extends SceneTree
## Checks the Slippery hook: show_tiles spills puddles on Slippery squares, dries them when the
## squares stop being slippery, and leaves hand-spilled puddles alone. Prints PASS or FAIL.
## Run: Godot --path . --headless --script res://experiments/beer_spill/check_beer_sync.gd


func _init() -> void:
	call_deferred("_go")


func _go() -> void:
	var board: Node3D = load("res://inn/chess_set.gd").new()
	root.add_child(board)
	board.configure(6, 6)
	board.build("")
	var ok := true
	board.show_tiles([{"square": 3, "id": "wall", "name": "Wall"}])
	ok = _expect(board, [], "a wall is no puddle") and ok
	board.spill_beer([30], false)
	board.show_tiles([_slippery(7), _slippery(8)])
	ok = _expect(board, [7, 8, 30], "slippery squares spill, hand spills stay") and ok
	board.show_tiles([_slippery(8), _slippery(20)])
	await create_timer(1.5).timeout
	ok = _expect(board, [8, 20, 30], "square 7 dried, 20 spilled") and ok
	board.show_tiles([])
	board.dry_beer([30])
	await create_timer(1.5).timeout
	ok = _expect(board, [], "all dry") and ok
	print("PASS" if ok else "FAIL")
	quit(0 if ok else 1)


func _slippery(square: int) -> Dictionary:
	return {"square": square, "id": "slippery", "name": "Slippery"}


func _expect(board: Node3D, squares: Array, what: String) -> bool:
	var got: Array = board.beer_squares()
	got.sort()
	var good := got == squares
	print(("ok   " if good else "FAIL ") + what + ": " + str(got))
	return good
