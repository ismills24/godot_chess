extends SceneTree
## End-to-end check of the bartender's beer spill: seats you at the inn board (run with `-- <out.png> --inn-autoplay`
## so the AI plays your side), waits for the RandomTiles beer spill to make tiles Slippery during play, lets the
## puddles settle and saves a play-view still. Not headless.
## Godot --path . --resolution 1600x900 --script res://experiments/beer_spill/capture_inn_spill.gd -- <out.png> --inn-autoplay


func _init() -> void:
	call_deferred("_go")


func _go() -> void:
	var args := OS.get_cmdline_user_args()
	root.size = Vector2i(1600, 900)
	var scene: Node = load("res://inn/Inn.tscn").instantiate()
	scene.set("skip_intro", true)
	root.add_child(scene)
	var board: Node3D = null
	var waited := 0.0
	while waited < 120.0:
		await create_timer(0.25).timeout
		waited += 0.25
		board = scene.get("_board")
		if board != null and not board.beer_squares().is_empty():
			break
	if board == null or board.beer_squares().is_empty():
		push_error("no beer spilled within 120 s")
		quit(1)
		return
	print("beer on squares ", board.beer_squares(), " after ", waited, " s")
	await create_timer(3.0).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(args[0])
	quit()
