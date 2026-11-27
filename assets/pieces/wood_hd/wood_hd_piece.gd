extends Node3D
## A high-resolution maple or walnut piece: the wood set's shapes and finish
## (assets/pieces/wood) as smooth baked meshes instead of splats. Same local space:
## origin at the centre of the foot, +Y up, the knight facing +X. Set the exports before
## adding the node to the tree. Rebake the meshes with bake_wood_hd.gd.

const KINDS = ["pawn", "rook", "knight", "bishop", "queen", "king"]
const SEATS = {"pawn": 0.91, "rook": 1.02, "knight": 0.48, "bishop": 1.16, "queen": 1.34, "king": 1.48}
const FINISHES = {"maple": 0, "walnut": 1, "gilt": 2}
const MESHES := "res://assets/pieces/wood_hd/meshes/"
const STYLES := "res://assets/pieces/wood_hd/styles/"

@export_enum("pawn", "rook", "knight", "bishop", "queen", "king") var piece_kind := "pawn"
@export var dark_wood := false
## "auto" follows dark_wood (walnut or maple); "gilt" is gold leaf over the same carving.
@export_enum("auto", "maple", "walnut", "gilt") var finish := "auto"
## Empty overrides use piece_kind.
@export var base_kind := ""
@export var body_kind := ""
@export var head_kind := ""
## The finish: "wood" (the wood set's painted maple/walnut) or one of styles/<name>.gdshader:
## "grain" (boxwood/rosewood), "plastic", "cel" (outlined), "marble", "metal" (brass/pewter),
## "porcelain" (white/cobalt).
@export_enum("wood", "grain", "plastic", "cel", "marble", "metal", "porcelain") var style := "wood"
## Visual layers for the piece's meshes; set_render_layers() changes them later.
@export_flags_3d_render var render_layers := 1

var head: Node3D
var seat_height := 0.0

static var _materials := {}


func finish_index() -> int:
	if FINISHES.has(finish):
		return FINISHES[finish]
	return 1 if dark_wood else 0


func _ready() -> void:
	var body_type: String = body_kind if body_kind in KINDS else piece_kind
	var head_type: String = head_kind if head_kind in KINDS else piece_kind
	var base_type: String = base_kind if base_kind in KINDS else piece_kind
	seat_height = SEATS[body_type]
	_module(self, base_type + "_base", true)
	_module(self, body_type + "_body", false)
	head = Node3D.new()
	head.name = "Head"
	head.position.y = seat_height
	add_child(head)
	_module(head, head_type + "_head", false)


## Lifts the head off its seat (unscrewing for the first 0.30 units), like the wood set.
func set_head_lift(distance: float) -> void:
	head.position.y = seat_height + distance
	head.rotation.y = distance / 0.10 * TAU if distance <= 0.30 else 3.0 * TAU


func set_render_layers(layers: int) -> void:
	render_layers = layers
	for node in find_children("*", "MeshInstance3D", true, false):
		(node as MeshInstance3D).layers = layers


func _module(parent: Node3D, mesh_name: String, felt: bool) -> void:
	var node := MeshInstance3D.new()
	node.name = mesh_name.get_slice("_", 1).capitalize()
	node.mesh = load(MESHES + mesh_name + ".res")
	node.material_override = _material(finish_index(), felt, style)
	node.layers = render_layers
	parent.add_child(node)


static func _material(finish_id: int, felt: bool, look: String) -> ShaderMaterial:
	var key := "%d/%s/%s" % [finish_id, felt, look]
	if not _materials.has(key):
		var material := ShaderMaterial.new()
		if look == "wood":
			material.shader = preload("res://assets/pieces/wood_hd/wood_hd.gdshader")
		else:
			material.shader = load(STYLES + look + ".gdshader")
		if look == "cel":
			var outline := ShaderMaterial.new()
			outline.shader = preload("res://assets/pieces/wood_hd/styles/outline.gdshader")
			material.next_pass = outline
		material.set_shader_parameter("finish", finish_id)
		material.set_shader_parameter("felt_pad", felt)
		_materials[key] = material
	return _materials[key]
