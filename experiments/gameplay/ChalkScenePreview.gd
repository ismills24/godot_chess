extends Node3D
## Art review scene using the same street stage and wood assets as gameplay.
func _ready() -> void:
	var display := get_parent().get_parent().get_node("Display") as ColorRect
	display.material.set_shader_parameter("stage_texture", get_viewport().get_texture())
	add_child(preload("res://stages/street/StreetSetting.tscn").instantiate())
	var board := preload("res://stages/street/StreetBoard.tscn").instantiate()
	add_child(board)
	var position_data := [
		["LightKing", 6, 0], ["LightRook", 0, 0], ["LightKnight", 2, 2],
		["LightBishop", 4, 3], ["LightPawn", 1, 3], ["LightPawn", 5, 1],
		["LightPawn", 6, 2], ["DarkKing", 6, 7], ["DarkRook", 0, 7],
		["DarkKnight", 5, 4], ["DarkBishop", 2, 5], ["DarkPawn", 1, 6],
		["DarkPawn", 5, 6], ["DarkPawn", 7, 5]]
	for entry in position_data:
		var piece := load("res://assets/pieces/wood/" + entry[0] + ".tscn").instantiate() as Node3D
		piece.set("depth_tested_splats", true)
		piece.position = board.cell_center(entry[1], entry[2])
		add_child(piece)
	var camera := Camera3D.new()
	add_child(camera)
	camera.position = Vector3(0.4, 14.5, 11.5)
	camera.look_at(Vector3(0, 0, -0.5))
	camera.fov = 44
	camera.current = true
	if "--capture-chalk" in OS.get_cmdline_user_args():
		for frame in 10:
			await get_tree().process_frame
		await RenderingServer.frame_post_draw
		var error := get_tree().root.get_texture().get_image().save_png("res://artifacts/gameplay_concepts/chalk-implemented.png")
		if error != OK:
			push_error("Could not save chalk preview: " + error_string(error))
		get_tree().quit(error)
