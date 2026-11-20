extends Node3D
## Reusable presentation asset. No rules-engine dependency or board ownership.
const Shapes = preload("res://assets/pieces/wood/wood_shapes.gd")
const Splats = preload("res://assets/pieces/wood/wood_splats.gd")
const Parts = preload("res://experiments/pawn/workshop_parts.gd")
@export_enum("pawn", "rook", "knight", "bishop", "queen", "king") var piece_kind := "pawn"
@export var dark_wood := false
## Board mode uses depth-tested Gaussian cutouts; close-ups retain soft blending.
@export var depth_tested_splats := false
var head: Node3D
var modules: Array[MeshInstance3D] = []
var seat_height := 0.0
static var sleeve_mesh: Mesh
static var dowel_mesh: Mesh
static var dowel_material: Material
static var sleeve_materials: Dictionary = {}

func _ready() -> void:
    assert(Shapes.KINDS.has(piece_kind))
    seat_height = Shapes.SEATS[piece_kind]
    for module in ["base", "body"]:
        var surface := Splats.new()
        surface.name = module.capitalize()
        add_child(surface)
        surface.configure(piece_kind, module, dark_wood, depth_tested_splats)
        modules.append(surface)
    head = Node3D.new()
    head.name = "Head"
    head.position.y = seat_height
    add_child(head)
    var top := Splats.new()
    top.name = "Carving"
    head.add_child(top)
    top.configure(piece_kind, "head", dark_wood, depth_tested_splats)
    modules.append(top)
    if sleeve_mesh == null:
        var temporary := Node3D.new()
        Parts.socket(temporary, Parts.material(Color.WHITE))
        sleeve_mesh = temporary.get_child(0).mesh
        temporary.free()
    if not sleeve_materials.has(dark_wood):
        var material := ShaderMaterial.new()
        material.shader = preload("res://assets/pieces/wood/wood_solid.gdshader")
        material.set_shader_parameter("dark_wood", dark_wood)
        sleeve_materials[dark_wood] = material
    Parts.mesh_node(head, sleeve_mesh, Vector3.ZERO, sleeve_materials[dark_wood]).name = "SmoothSocket"
    var pin := Node3D.new()
    pin.name = "ThreadedDowel"
    add_child(pin)
    if dowel_mesh == null:
        var temporary := Node3D.new()
        Parts.wooden_dowel(temporary, 0, 0.30, 0.10)
        dowel_mesh = temporary.get_child(0).mesh
        dowel_material = temporary.get_child(0).material_override
        temporary.free()
    Parts.mesh_node(pin, dowel_mesh, Vector3(0, seat_height + 0.15, 0), dowel_material)
    if depth_tested_splats:
        set_process(false)

func set_head_lift(distance: float) -> void:
    head.position.y = seat_height + distance
    head.rotation.y = distance / 0.10 * TAU if distance <= 0.30 else 3.0 * TAU

func _process(_delta: float) -> void:
    var camera := get_viewport().get_camera_3d()
    if camera != null:
        for surface in modules:
            surface.sort_for(camera)
