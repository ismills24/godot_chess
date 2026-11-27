extends SceneTree
## Stills of a scene's play view with one strong lamp moved round the board, to check the
## pieces' shadows follow the light. Saves <out_prefix>_<n>.png per angle.
## Run (not headless): Godot --path . --resolution 1600x900 --script res://experiments/board/capture_light_angles.gd -- <scene.tscn> <out_prefix> [style]

## Lamp spots round the board's centre, as seen from the light side: [name, azimuth degrees
## (0 right, 90 far), height, distance] in metres.
const SPOTS := [
	["left", 180.0, 0.35, 0.45],
	["right", 0.0, 0.35, 0.45],
	["far", 90.0, 0.35, 0.45],
	["near", 270.0, 0.35, 0.45],
	["overhead", 0.0, 0.45, 0.02],
	["low_left", 200.0, 0.15, 0.5],
]


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
	var board: Node3D = scene.get("_board")
	var lamp := OmniLight3D.new()
	lamp.light_color = Color("ffe2b8")
	lamp.light_energy = 3.0
	lamp.omni_range = 2.0
	lamp.omni_attenuation = 1.0
	lamp.layers = 1 | (1 << 10)
	board.add_child(lamp)
	# A small glowing bulb where the lamp hangs, so each still shows where the light is.
	var bulb := MeshInstance3D.new()
	var ball := SphereMesh.new()
	ball.radius = 0.012
	ball.height = 0.024
	bulb.mesh = ball
	var glow := StandardMaterial3D.new()
	glow.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	glow.albedo_color = Color("fff4d8")
	bulb.material_override = glow
	bulb.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	bulb.layers = 1 << 10
	lamp.add_child(bulb)
	for i in SPOTS.size():
		var spot: Array = SPOTS[i]
		var angle := deg_to_rad(spot[1])
		# In the board's space: +X to the light side's right, +Z toward the light side.
		lamp.position = Vector3(cos(angle) * spot[3], spot[2], -sin(angle) * spot[3])
		# The pieces look for new lights every half second.
		await create_timer(0.6).timeout
		await RenderingServer.frame_post_draw
		root.get_texture().get_image().save_png("%s_%d_%s.png" % [args[1], i, spot[0]])
	quit()
