extends SceneTree
## Play-view stills of the beer spill on a scene's board (like experiments/board/capture_play.gd):
## seated at the board, spills beer on `squares`, and saves <prefix>_splash.png mid-splash,
## <prefix>_settled.png once the puddles have settled and <prefix>_drying.png partway through
## drying the first square off.
## Run (not headless): Godot --path . --resolution 1600x900 --script res://experiments/beer_spill/capture_beer.gd -- <scene.tscn> <out_prefix> [squares, e.g. 7,15,20,28] [splash seconds]


func _init() -> void:
	call_deferred("_go")


func _go() -> void:
	var args := OS.get_cmdline_user_args()
	root.size = Vector2i(1600, 900)
	var scene: Node = load(args[0]).instantiate()
	scene.set("skip_intro", true)
	root.add_child(scene)
	for frame in 75:
		await process_frame
	var board: Node3D = scene.get("_board")
	var squares: Array = []
	for part in (args[2] if args.size() > 2 and args[2] != "" else "7,15,20,28").split(","):
		squares.append(part.to_int())
	board.spill_beer(squares)
	await create_timer(float(args[3]) if args.size() > 3 else 0.42).timeout
	await _save(args[1] + "_splash.png")
	await create_timer(2.8).timeout
	await _save(args[1] + "_settled.png")
	board.dry_beer([squares[0]])
	await create_timer(0.45).timeout
	await _save(args[1] + "_drying.png")
	quit()


func _save(path: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(path)
