extends SceneTree
## A still of a scene's play view only: starts seated at the board (skip_intro), waits for
## the sharp pieces to fade in, and saves the window. No intro, no movie writer.
## Run (not headless): Godot --path . --resolution 1600x900 --script res://experiments/board/capture_play.gd -- <scene.tscn> <out.png>


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
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(args[1])
	quit()
