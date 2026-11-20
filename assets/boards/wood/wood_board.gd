extends Node3D
## Ground plane is Y=0. Files run +X; ranks run -Z from White's side.
const SQUARE_SIZE = 1.45
const HALF_FIELD = SQUARE_SIZE * 4.0
const FRAME_WIDTH = 0.42

static func square_center(file: int, rank: int) -> Vector3:
    return Vector3((file - 3.5) * SQUARE_SIZE, 0, (3.5 - rank) * SQUARE_SIZE)

static func is_light_square(file: int, rank: int) -> bool:
    return (file + rank) % 2 == 1

func _ready() -> void:
    var squares := Node3D.new()
    squares.name = "Squares"
    add_child(squares)
    var tile := BoxMesh.new()
    tile.size = Vector3(SQUARE_SIZE, 0.09, SQUARE_SIZE)
    var light := _material(Color("c9a979"))
    var dark := _material(Color("795337"))
    for rank in 8:
        for file in 8:
            var square := MeshInstance3D.new()
            square.name = String.chr(97 + file) + str(rank + 1)
            square.mesh = tile
            square.material_override = light if is_light_square(file, rank) else dark
            square.position = square_center(file, rank) + Vector3(0, -0.045, 0)
            squares.add_child(square)
    var frame := Node3D.new()
    frame.name = "Frame"
    add_child(frame)
    var walnut := _material(Color("634129"))
    var edge := _material(Color("493020"))
    var span := HALF_FIELD * 2.0
    _box(frame, "Foundation", Vector3(0, -0.22, 0), Vector3(span + FRAME_WIDTH * 2, 0.32, span + FRAME_WIDTH * 2), edge)
    for side in [-1.0, 1.0]:
        _box(frame, "EndRail", Vector3(0, -0.045, side * (HALF_FIELD + FRAME_WIDTH * 0.5)),
            Vector3(span + FRAME_WIDTH * 2, 0.11, FRAME_WIDTH), walnut)
        _box(frame, "SideRail", Vector3(side * (HALF_FIELD + FRAME_WIDTH * 0.5), -0.045, 0),
            Vector3(FRAME_WIDTH, 0.11, span), walnut)
    # A fine inset line separates the field from the plain walnut frame.
    var inlay := _material(Color("a68655"))
    for side in [-1.0, 1.0]:
        _box(frame, "EndInlay", Vector3(0, 0.012, side * (HALF_FIELD + 0.045)), Vector3(span + 0.12, 0.006, 0.018), inlay)
        _box(frame, "SideInlay", Vector3(side * (HALF_FIELD + 0.045), 0.012, 0), Vector3(0.018, 0.006, span + 0.12), inlay)

func _material(color: Color) -> ShaderMaterial:
    var result := ShaderMaterial.new()
    result.shader = preload("res://assets/boards/wood/board_wood.gdshader")
    result.set_shader_parameter("wood_tint", color)
    return result

func _box(parent: Node3D, node_name: String, at: Vector3, dimensions: Vector3, mat: Material) -> void:
    var node := MeshInstance3D.new()
    node.name = node_name
    var mesh := BoxMesh.new()
    mesh.size = dimensions
    node.mesh = mesh
    node.material_override = mat
    node.position = at
    parent.add_child(node)
