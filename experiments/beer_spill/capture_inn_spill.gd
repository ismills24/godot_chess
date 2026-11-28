extends SceneTree
## End-to-end check of the bartender's beer spill: seats you at the inn board (run with
## `--inn-autoplay` so an AI plays your side), waits for the RandomTiles beer spill to fire during
## play, and saves play-view stills: <prefix>_pour.png as the beer slops out of his slammed-down
## mug [pour seconds after it lands, default 0.22] and <prefix>_settled.png once the puddles settle.
## Not headless:
## Godot --path . --resolution 1600x900 --script res://experiments/beer_spill/capture_inn_spill.gd -- <out_prefix> [pour seconds] --inn-autoplay


func _init() -> void:
	call_deferred("_go")


func _go() -> void:
	var args := OS.get_cmdline_user_args()
	var pour := 0.22
	if args.size() > 1 and args[1].is_valid_float():
		pour = args[1].to_float()
	root.size = Vector2i(1600, 900)
	var scene: Node = load("res://inn/Inn.tscn").instantiate()
	scene.set("skip_intro", true)
	root.add_child(scene)
	var waited := 0.0
	while waited < 120.0 and (scene.get("_pending_beer") as Array).is_empty():
		await create_timer(0.1).timeout
		waited += 0.1
	if (scene.get("_pending_beer") as Array).is_empty():
		push_error("no beer spilled within 120 s")
		quit(1)
		return
	print("beer due on ", scene.get("_pending_beer"), " after ", waited, " s; he drinks")
	await scene.get("_bartender").mug_down
	await create_timer(pour).timeout
	await _save(args[0] + "_pour.png")
	await create_timer(3.0).timeout
	print("puddles on ", scene.get("_board").beer_squares())
	await _save(args[0] + "_settled.png")
	quit()


func _save(path: String) -> void:
	await RenderingServer.frame_post_draw
	root.get_texture().get_image().save_png(path)
