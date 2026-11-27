extends SceneTree
## Close-up of the high-resolution set next to the splat set it was baked from: the top row
## is wood_hd, the bottom row the original wood set, maple and walnut alternating.
## Run (not headless): Godot --path . --script res://assets/pieces/wood_hd/preview_wood_hd.gd -- <out.png>

const HdPiece = preload("res://assets/pieces/wood_hd/wood_hd_piece.gd")
const KINDS = ["pawn", "knight", "bishop", "rook", "queen", "king"]


func _init() -> void:
	call_deferred("_capture")


func _capture() -> void:
	root.size = Vector2i(1600, 900)
	var stage := Node3D.new()
	root.add_child(stage)
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color("2a2430")
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color("8a8090")
	environment.environment.ambient_light_energy = 0.6
	stage.add_child(environment)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40, -35, 0)
	sun.light_color = Color("ffd9a8")
	sun.shadow_enabled = true
	stage.add_child(sun)
	for row in 2:
		for i in KINDS.size():
			var piece: Node3D
			if row == 0:
				piece = HdPiece.new()
			else:
				piece = load("res://assets/pieces/wood/LightPawn.tscn").instantiate()
				piece.set("depth_tested_splats", true)
			piece.set("piece_kind", KINDS[i])
			piece.set("dark_wood", i % 2 == 1)
			piece.position = Vector3((i - 2.5) * 1.35, 0, -row * 1.8)
			piece.rotation.y = deg_to_rad(90.0 if KINDS[i] == "knight" else 0.0) - 0.5
			stage.add_child(piece)
	var camera := Camera3D.new()
	stage.add_child(camera)
	camera.position = Vector3(0, 3.2, 5.6)
	camera.look_at(Vector3(0, 0.7, -0.9))
	camera.fov = 52
	camera.current = true
	for frame in 12:
		await process_frame
	await RenderingServer.frame_post_draw
	var out := "user://wood_hd_preview.png"
	var args := OS.get_cmdline_user_args()
	if not args.is_empty():
		out = args[0]
	root.get_texture().get_image().save_png(out)
	quit()
