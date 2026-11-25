extends Node3D
## A sheltered chalk landing: amber gaslight, teal night fill, brick at the far edge.
## The board owns the continuous floor. No clutter intrudes into the playing area.
const SQUARE_SIZE = 1.45
var _layout: Node3D

func _ready() -> void:
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("101e23")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.36, 0.55, 0.64)
	environment.ambient_light_energy = 0.38
	environment.tonemap_mode = Environment.TONE_MAPPER_AGX
	environment.tonemap_exposure = 1.15
	environment.tonemap_white = 6.0
	environment.adjustment_enabled = true
	environment.adjustment_contrast = 1.04
	environment.adjustment_saturation = 1.08
	var world := WorldEnvironment.new()
	world.environment = environment
	add_child(world)
	var night := DirectionalLight3D.new()
	night.name = "CoolNightFill"
	night.rotation_degrees = Vector3(-48, 145, 0)
	night.light_color = Color("85b4c3")
	night.light_energy = 0.65
	add_child(night)
	fit(8, 8)

func fit(width: int, height: int) -> void:
	if _layout != null:
		remove_child(_layout)
		_layout.queue_free()
	_layout = Node3D.new()
	_layout.name = "Layout"
	add_child(_layout)
	var half := Vector2(width, height) * SQUARE_SIZE * 0.5
	var back := -half.y - 2.8
	var stone := ShaderMaterial.new()
	stone.shader = preload("res://stages/street/street_stone.gdshader")
	stone.set_shader_parameter("tint", Color("343e40"))
	stone.set_shader_parameter("board_half", half + Vector2(0.5, 0.5))
	# Masonry clears the board sightline in its shader when orbiting behind it.
	_box("StoneFooting", stone, Vector3(0, 0.17, back), Vector3(half.x * 2 + 10, 0.34, 0.75))
	var brick := ShaderMaterial.new()
	brick.shader = preload("res://stages/street/street_brick.gdshader")
	brick.set_shader_parameter("masonry", preload("res://menu/props/brick.jpg"))
	brick.set_shader_parameter("relief", preload("res://menu/props/brick_normal.jpg"))
	brick.set_shader_parameter("rough_map", preload("res://menu/props/brick_rough.jpg"))
	brick.set_shader_parameter("color_tint", Color("ad8c79"))
	brick.set_shader_parameter("tile_scale", Vector2(4, 2))
	brick.set_shader_parameter("board_half", half + Vector2(0.5, 0.5))
	_box("BrickRecess", brick, Vector3(0, 2.5, back - 0.25), Vector3(half.x * 2 + 10, 5.0, 0.7))
	_box("StoneCoping", stone, Vector3(0, 5.05, back - 0.25), Vector3(half.x * 2 + 10.2, 0.13, 0.85))
	var iron := _paint(Color("252d2e"), 0.82)
	var lamp_at := Vector3(-half.x - 1.4, 3.5, back + 0.3)
	_box("LampPost", iron, lamp_at + Vector3(0, -1.8, 0), Vector3(0.12, 3.6, 0.12))
	_box("LanternBase", iron, lamp_at, Vector3(0.65, 0.12, 0.65))
	_box("LanternCap", iron, lamp_at + Vector3(0, 0.9, 0), Vector3(0.76, 0.16, 0.76))
	var glow := _paint(Color("ffc98b"), 0.7)
	glow.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	glow.emission_enabled = true
	glow.emission = Color("ffb76c")
	glow.emission_energy_multiplier = 1.8
	_box("LanternGlass", glow, lamp_at + Vector3(0, 0.45, 0), Vector3(0.48, 0.78, 0.48))
	for x in [-1, 1]:
		for z in [-1, 1]:
			_box("LanternFrame", iron, lamp_at + Vector3(x * 0.27, 0.45, z * 0.27), Vector3(0.055, 0.85, 0.055))
	var lantern := OmniLight3D.new()
	lantern.name = "LanternPool"
	lantern.position = lamp_at + Vector3(0, 0.45, 0.35)
	lantern.light_color = Color("ffbc7b")
	lantern.light_energy = 1.7
	lantern.omni_range = 25.0
	lantern.omni_attenuation = 0.65
	lantern.shadow_enabled = true
	_layout.add_child(lantern)
	# Broad warm bounce keeps carved silhouettes readable across the entire board.
	var key := DirectionalLight3D.new()
	key.name = "WarmBounce"
	key.rotation_degrees = Vector3(-58, -35, 0)
	key.light_color = Color("ffce99")
	key.light_energy = 0.24
	key.shadow_enabled = true
	_layout.add_child(key)

func _box(label: String, material: Material, at: Vector3, size: Vector3) -> void:
	var mesh := BoxMesh.new()
	mesh.size = size
	var node := MeshInstance3D.new()
	node.name = label
	node.mesh = mesh
	node.material_override = material
	node.position = at
	_layout.add_child(node)

static func _paint(color: Color, roughness: float) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = roughness
	return material
