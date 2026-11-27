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
## The lights a shadow can come from, shared by every piece and refreshed now and then
## (lights come and go: the board light is added when play starts).
static var _lights: Array[Light3D] = []
static var _lights_checked := -1.0
const SHADOW := preload("res://assets/pieces/wood_hd/contact_shadow.gdshader")
## Longest shadow, in the piece's own units (squares are 1.45), so a light near the board's
## level doesn't throw one across the whole board.
const SHADOW_REACH := 3.5
## How dark a shadow from a light that lights the piece on its own gets.
const SHADOW_DARKNESS := 0.7

var _shadow: MeshInstance3D
var _foot := 0.5
var _height := 1.5


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
	_foot = (load(MESHES + base_type + "_base.res") as Mesh).get_aabb().size.x * 0.5
	_height = seat_height + (load(MESHES + head_type + "_head.res") as Mesh).get_aabb().end.y
	_make_shadow()


## The piece's shadow on whatever it stands on (contact_shadow.gdshader), cast away from the
## lights that reach it; _process aims it every frame.
func _make_shadow() -> void:
	var half := _foot + SHADOW_REACH + 0.5
	var quad := PlaneMesh.new()
	quad.size = Vector2(half, half) * 2.0
	_shadow = MeshInstance3D.new()
	_shadow.name = "Shadow"
	_shadow.mesh = quad
	_shadow.position.y = 0.01
	_shadow.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := ShaderMaterial.new()
	material.shader = SHADOW
	material.set_shader_parameter("foot", _foot)
	_shadow.material_override = material
	_shadow.layers = render_layers
	add_child(_shadow)
	_aim_shadow()


func _process(_delta: float) -> void:
	_aim_shadow()


## Points the shadow away from the two lights that reach the piece most, each as dark as its
## share of the light there and as long as the light is low.
func _aim_shadow() -> void:
	if not is_inside_tree():
		return
	_refresh_lights(self)
	var casts: Array = []
	var total := 0.0
	var foot_world := global_position
	for light in _lights:
		if not is_instance_valid(light) or not light.is_visible_in_tree() or light.light_energy <= 0.0:
			continue
		var weight := 0.0
		var toward := Vector3.ZERO  # From the foot toward the light, in the piece's space.
		if light is DirectionalLight3D:
			weight = light.light_energy
			toward = (global_transform.basis.inverse() * light.global_basis.z).normalized()
		else:
			var offset := light.global_position - foot_world
			var distance := maxf(offset.length(), 0.02)
			var reach: float = light.omni_range if light is OmniLight3D else light.spot_range
			if distance >= reach:
				continue
			if light is SpotLight3D:
				var cone := (-light.global_basis.z).angle_to(-offset)
				if cone > deg_to_rad(light.spot_angle):
					continue
			var falloff: float = light.omni_attenuation if light is OmniLight3D else light.spot_attenuation
			weight = light.light_energy * pow(maxf(1.0 - pow(distance / reach, 4.0), 0.0), 2.0) 				* pow(distance, -falloff)
			toward = to_local(light.global_position)
		if weight <= 0.0:
			continue
		total += weight
		casts.append([weight, toward, not light is DirectionalLight3D])
	casts.sort_custom(func(a: Array, b: Array) -> bool: return a[0] > b[0])
	var material := _shadow.material_override as ShaderMaterial
	for i in 2:
		var value := Vector4.ZERO
		if i < casts.size():
			var toward: Vector3 = casts[i][1]
			var across := Vector2(toward.x, toward.z)
			var length := 0.0
			if across.length() > 0.0001:
				if casts[i][2]:
					# The top of the piece projected from the light onto the ground.
					length = _height * across.length() / maxf(toward.y - _height, 0.0001) 						if toward.y > _height else SHADOW_REACH
				else:
					length = _height * across.length() / maxf(toward.y, 0.0001)
				var dir := -across.normalized()
				value = Vector4(dir.x, dir.y, minf(length, SHADOW_REACH), SHADOW_DARKNESS * casts[i][0] / total)
		material.set_shader_parameter("cast_a" if i == 0 else "cast_b", value)


static func _refresh_lights(from: Node) -> void:
	var now := Time.get_ticks_msec() / 1000.0
	if now - _lights_checked < 0.5 and _lights_checked >= 0.0:
		return
	_lights_checked = now
	_lights.clear()
	var world := (from as Node3D).get_world_3d()
	for found in from.get_tree().root.find_children("*", "Light3D", true, false):
		var light := found as Light3D
		if light.get_world_3d() == world:
			_lights.append(light)


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
