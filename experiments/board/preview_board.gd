extends SceneTree
## Stills of the shared chess set (inn/chess_set.gd) with a game in progress, at full
## resolution under a warm table light: from White's chair, and low across the board.
## Run (not headless): Godot --path . --script res://experiments/board/preview_board.gd -- <out_prefix>

const ChessSet = preload("res://inn/chess_set.gd")
## A middlegame with every piece type on the board, and a few taken.
const PLACEMENT := "r1bq1rk1/pp2bppp/2n1pn2/3p4/2PP4/2N1PN2/PP1B1PPP/R2QKB1R"
const CAPTURED := "Pp"
const SHOTS := {
	"seat": [Vector3(0, 0.46, 0.44), Vector3(0, 0, -0.01), 36.0],
	"low": [Vector3(-0.30, 0.14, 0.32), Vector3(0.0, 0.015, -0.02), 40.0],
}


func _init() -> void:
	call_deferred("_capture")


func _capture() -> void:
	root.size = Vector2i(1600, 900)
	var stage := Node3D.new()
	root.add_child(stage)
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color("1c1418")
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color("6a5a66")
	environment.environment.ambient_light_energy = 0.5
	environment.environment.tonemap_mode = Environment.TONE_MAPPER_AGX
	stage.add_child(environment)
	var table := MeshInstance3D.new()
	var top := PlaneMesh.new()
	top.size = Vector2(3, 3)
	table.mesh = top
	var wood := StandardMaterial3D.new()
	wood.albedo_color = Color("5a2e1c")
	wood.roughness = 0.7
	table.material_override = wood
	stage.add_child(table)
	var lamp := OmniLight3D.new()
	lamp.position = Vector3(-0.4, 0.5, 0.25)
	lamp.light_color = Color("ffc890")
	lamp.light_energy = 1.6
	lamp.omni_range = 2.0
	lamp.shadow_enabled = true
	stage.add_child(lamp)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-60, 150, 0)
	fill.light_color = Color("a8b8ff")
	fill.light_energy = 0.25
	stage.add_child(fill)
	var board := ChessSet.new()
	stage.add_child(board)
	board.build(PLACEMENT, CAPTURED)
	var camera := Camera3D.new()
	camera.near = 0.01
	stage.add_child(camera)
	camera.current = true
	var args := OS.get_cmdline_user_args()
	var prefix: String = args[0] if not args.is_empty() else "user://board"
	for shot: String in SHOTS:
		var setup: Array = SHOTS[shot]
		camera.position = setup[0]
		camera.look_at(setup[1])
		camera.fov = setup[2]
		for frame in 8:
			await process_frame
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png(prefix + "_" + shot + ".png")
	quit()
