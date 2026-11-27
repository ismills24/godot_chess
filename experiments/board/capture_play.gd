extends SceneTree
## A still of a scene's play view only: starts seated at the board (skip_intro), waits for
## the sharp pieces to fade in, and saves the window. No intro, no movie writer.
## Run (not headless): Godot --path . --resolution 1600x900 --script res://experiments/board/capture_play.gd -- <scene.tscn> <out.png> [style] [reach]
## "reach" catches the opponent mid-reach over the board, to check their arm draws over it.


func _init() -> void:
	call_deferred("_go")


func _go() -> void:
	var args := OS.get_cmdline_user_args()
	root.size = Vector2i(1600, 900)
	if args.size() > 2 and args[2] != "":
		load("res://inn/chess_set.gd").piece_style = args[2]
	var scene: Node = load(args[0]).instantiate()
	scene.set("skip_intro", true)
	root.add_child(scene)
	for frame in 75:
		await process_frame
	if args.size() > 3 and args[3] == "reach":
		var bartender: Node = scene.get("_bartender")
		if bartender:
			bartender.reach()
		else:
			var board: Node3D = scene.get("_board")
			scene.get("_world").opponent.reach(board.to_global(board.square_center(4, 6)))
		await create_timer(0.4).timeout
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(args[1])
	quit()
