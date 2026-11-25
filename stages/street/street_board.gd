extends Node3D
## Chalk on a continuous landing. Logical cells stay regular; the drawn lines wander.
const SQUARE_SIZE = 1.45
const BOARD_SHADER := preload("res://stages/street/street_board.gdshader")
@export var board_width := 8
@export var board_height := 8
@export var wear_seed := 7

func cell_center(file: int, rank: int) -> Vector3:
	return Vector3((file - (board_width - 1) * 0.5) * SQUARE_SIZE, 0, ((board_height - 1) * 0.5 - rank) * SQUARE_SIZE)

func _ready() -> void:
	var squares := Node3D.new()
	squares.name = "Squares"
	add_child(squares)
	for rank in board_height:
		for file in board_width:
			var marker := Marker3D.new()
			marker.name = String.chr(97 + file) + str(rank + 1)
			marker.position = cell_center(file, rank)
			squares.add_child(marker)
	var material := ShaderMaterial.new()
	material.shader = BOARD_SHADER
	material.set_shader_parameter("board_size", Vector2(board_width, board_height))
	material.set_shader_parameter("square_size", SQUARE_SIZE)
	material.set_shader_parameter("drawing_seed", float(wear_seed))
	material.set_shader_parameter("cobbles", preload("res://menu/props/cobbles.jpg"))
	material.set_shader_parameter("relief", preload("res://menu/props/cobbles_normal.jpg"))
	var plane := PlaneMesh.new()
	plane.size = Vector2(maxf(100, board_width * SQUARE_SIZE + 20), maxf(100, board_height * SQUARE_SIZE + 20))
	var ground := MeshInstance3D.new()
	ground.name = "ChalkLanding"
	ground.mesh = plane
	ground.material_override = material
	ground.position.y = -0.015
	add_child(ground)
