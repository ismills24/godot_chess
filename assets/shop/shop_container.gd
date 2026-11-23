@tool
extends Node3D
## Bottom-centred container, opening towards +Z. Dimensions exclude projecting flaps.
## Geometry is rebuilt at unit scale; UVs stay in local metres, so grain never stretches.

enum Kind { WINE_CRATE, CARDBOARD }
const MIN_SIZE := Vector3(0.55, 0.45, 0.45)
const MAX_SIZE := Vector3(3.2, 2.6, 2.0)
const WOOD := preload("res://assets/shop/textures/wine_crate_wood.png")
const CARD := preload("res://assets/shop/textures/worn_cardboard.png")
const BOARD_THICKNESS := 0.065
const PAPER_THICKNESS := 0.018

@export var kind: Kind = Kind.WINE_CRATE:
	set(value):
		kind = value
		_schedule_build()
@export var dimensions := Vector3(1.8, 1.3, 1.0):
	set(value):
		dimensions = value.clamp(MIN_SIZE, MAX_SIZE) if value.is_finite() else Vector3(1.8, 1.3, 1.0)
		_schedule_build()
@export var wear_seed := 17:
	set(value):
		wear_seed = value
		_schedule_build()
@export_range(0.0, 1.0) var wear := 0.6:
	set(value):
		wear = clampf(value, 0.0, 1.0)
		_schedule_build()

var item_anchor: Marker3D
var tag_anchor: Marker3D
var triangle_count := 0
var _pending := false
var _body: MeshInstance3D
var _hardware: MeshInstance3D
var _surface: SurfaceTool
var _metal: SurfaceTool
var _rng := RandomNumberGenerator.new()
var _face_tint := Color.WHITE
var _uv_offset := Vector2.ZERO
static var _wood_material: StandardMaterial3D
static var _card_material: StandardMaterial3D
static var _iron_material: StandardMaterial3D

func _ready() -> void:
	rebuild()

## Applies all parameters and builds immediately, including before entering a scene.
func configure(new_kind: Kind, size: Vector3, seed_value: int, wear_amount := 0.6) -> void:
	kind = new_kind
	dimensions = size
	wear_seed = seed_value
	wear = wear_amount
	rebuild()

## Keeps the node, anchors and their children; only the geometry and anchor positions change.
func resize(size: Vector3) -> void:
	dimensions = size
	rebuild()

## Changes deterministic wear without changing dimensions or attached merchandise.
func set_wear(seed_value: int, wear_amount := 0.6) -> void:
	wear_seed = seed_value
	wear = wear_amount
	rebuild()

func usable_size() -> Vector3:
	var thickness := BOARD_THICKNESS if kind == Kind.WINE_CRATE else PAPER_THICKNESS
	return dimensions - Vector3.ONE * thickness * 2.0

func _schedule_build() -> void:
	if not is_inside_tree() or _pending:
		return
	_pending = true
	_flush_build.call_deferred()

func _flush_build() -> void:
	if _pending:
		rebuild()

func rebuild() -> void:
	_pending = false
	_ensure_nodes()
	_rng.seed = wear_seed
	triangle_count = 0
	_surface = SurfaceTool.new()
	_surface.begin(Mesh.PRIMITIVE_TRIANGLES)
	_metal = SurfaceTool.new()
	_metal.begin(Mesh.PRIMITIVE_TRIANGLES)
	if kind == Kind.WINE_CRATE:
		_build_crate()
	else:
		_build_cardboard()
	_body.mesh = _surface.commit()
	_body.material_override = _wood_material if kind == Kind.WINE_CRATE else _card_material
	_hardware.visible = kind == Kind.WINE_CRATE
	_hardware.mesh = _metal.commit() if kind == Kind.WINE_CRATE else null
	_hardware.material_override = _iron_material
	var thickness := BOARD_THICKNESS if kind == Kind.WINE_CRATE else PAPER_THICKNESS
	item_anchor.position = Vector3(0, thickness + 0.015, 0)
	tag_anchor.position = Vector3(0, 0.08, dimensions.z * 0.5 + 0.04)

func _ensure_nodes() -> void:
	if _wood_material == null:
		_wood_material = _material(WOOD)
		_card_material = _material(CARD)
		_iron_material = _material(null)
		_iron_material.albedo_color = Color("403c36")
	if is_instance_valid(_body):
		return
	_body = MeshInstance3D.new()
	_body.name = "ContainerMesh"
	add_child(_body)
	_hardware = MeshInstance3D.new()
	_hardware.name = "Nails"
	add_child(_hardware)
	item_anchor = Marker3D.new()
	item_anchor.name = "ItemAnchor"
	add_child(item_anchor)
	tag_anchor = Marker3D.new()
	tag_anchor.name = "PriceTagAnchor"
	add_child(tag_anchor)

static func _material(texture: Texture2D) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_texture = texture
	material.vertex_color_use_as_albedo = true
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	material.roughness = 1.0
	return material

func _new_board() -> void:
	var tone := _rng.randf_range(0.73, 1.03)
	_face_tint = Color(tone, tone * _rng.randf_range(0.94, 1.0), tone * 0.91)
	_uv_offset = Vector2(_rng.randf(), _rng.randf())

func _build_crate() -> void:
	var w := dimensions.x
	var h := dimensions.y
	var d := dimensions.z
	var t := BOARD_THICKNESS
	# Horizontal grain follows the long edge of each individual board.
	var rows := maxi(2, ceili(h / 0.25))
	var pitch := h / rows
	for row in rows:
		var y := (row + 0.5) * pitch
		_new_board()
		_cuboid(Vector3(0, y, -d * 0.5 + t * 0.5), Vector3(w, pitch - 0.018, t))
		for side in [-1.0, 1.0]:
			_new_board()
			var length := d - t
			var broken: bool = row == rows - 2 and side > 0 and _rng.randf() < wear
			if broken:
				length *= 0.80
			_cuboid(Vector3(side * (w - t) * 0.5, y, -(d - t - length) * 0.5),
				Vector3(length, pitch - 0.018, t), Basis(Vector3.UP, PI * 0.5))
	var depth_rows := maxi(2, ceili(d / 0.25))
	for row in depth_rows:
		for y in [t * 0.5, h - t * 0.5]:
			_new_board()
			_cuboid(Vector3(0, y, -d * 0.5 + (row + 0.5) * d / depth_rows),
				Vector3(w, t, d / depth_rows - 0.014))
	# Uprights, narrow front lip and iron nail heads. No stretched whole-crate mesh.
	for side in [-1.0, 1.0]:
		for z in [-d * 0.5 + t, d * 0.5 - t * 0.5]:
			_new_board()
			_cuboid(Vector3(side * (w * 0.5 - 0.085), h * 0.5, z),
				Vector3(h, 0.12, t), Basis(Vector3.BACK, PI * 0.5))
		for y in [0.13, h - 0.13]:
			_cuboid(Vector3(side * (w * 0.5 - 0.085), y, d * 0.5 + 0.007),
				Vector3(0.038, 0.038, 0.022), Basis.IDENTITY, true)
	_new_board()
	_cuboid(Vector3(0, 0.075, d * 0.5 - t * 0.5), Vector3(w, 0.15, t))

func _build_cardboard() -> void:
	var w := dimensions.x * 0.5
	var h := dimensions.y
	var d := dimensions.z * 0.5
	# A box on its side, open towards the camera. Large planar dents and flaps.
	var dent := wear * minf(0.09, h * 0.1)
	var top_left := Vector3(-w + dent, h - _rng.randf() * dent, d)
	var top_right := Vector3(w - dent * 0.4, h - _rng.randf() * dent, d)
	var bl := Vector3(-w, 0, d)
	var br := Vector3(w, 0, d)
	var back_bl := Vector3(-w, 0, -d)
	var back_br := Vector3(w, 0, -d)
	var back_tl := Vector3(-w, h, -d)
	var back_tr := Vector3(w, h, -d)
	_new_board()
	_panel([back_bl, back_br, br, bl])
	_panel([back_bl, back_tl, back_tr, back_br])
	_panel([bl, top_left, back_tl, back_bl])
	_panel([back_br, back_tr, top_right, br])
	_panel([top_left, top_right, back_tr, back_tl])
	var flap := minf(h * 0.33, 0.4)
	var droop := _rng.randf_range(0.06, 0.20) * wear
	# One simple notch per free edge keeps damage legible at low resolution.
	_flap(top_left, top_right, Vector3(0, -droop, flap), true)
	_flap(br, bl, Vector3(0, -flap * 0.55, flap * 0.7), true)
	_flap(bl, top_left, Vector3(-flap * 0.65, -0.05, flap * 0.55), false)
	_flap(top_right, br, Vector3(flap * 0.55, 0.02, flap * 0.6), false)

func _flap(a: Vector3, b: Vector3, extension: Vector3, torn: bool) -> void:
	var middle := a.lerp(b, 0.58)
	var edge_middle := middle + extension * (0.74 if torn else 0.98)
	_panel([a, middle, edge_middle, a + extension])
	_panel([middle, b, b + extension * 0.94, edge_middle])

func _panel(points: Array[Vector3]) -> void:
	var normal := (points[1] - points[0]).cross(points[2] - points[0]).normalized()
	var back: Array[Vector3] = []
	for point in points:
		back.append(point - normal * PAPER_THICKNESS)
	_quad(points, _surface)
	_quad([back[3], back[2], back[1], back[0]], _surface, 0.82)
	for i in 4:
		var next := (i + 1) % 4
		_quad([points[i], back[i], back[next], points[next]], _surface, 0.64)

func _cuboid(at: Vector3, size: Vector3, basis := Basis.IDENTITY, iron := false) -> void:
	var a := size * -0.5
	var b := size * 0.5
	var faces := [
		[Vector3(a.x,a.y,b.z), Vector3(b.x,a.y,b.z), Vector3(b.x,b.y,b.z), Vector3(a.x,b.y,b.z)],
		[Vector3(b.x,a.y,a.z), Vector3(a.x,a.y,a.z), Vector3(a.x,b.y,a.z), Vector3(b.x,b.y,a.z)],
		[Vector3(b.x,a.y,b.z), Vector3(b.x,a.y,a.z), Vector3(b.x,b.y,a.z), Vector3(b.x,b.y,b.z)],
		[Vector3(a.x,a.y,a.z), Vector3(a.x,a.y,b.z), Vector3(a.x,b.y,b.z), Vector3(a.x,b.y,a.z)],
		[Vector3(a.x,b.y,b.z), Vector3(b.x,b.y,b.z), Vector3(b.x,b.y,a.z), Vector3(a.x,b.y,a.z)],
		[Vector3(a.x,a.y,a.z), Vector3(b.x,a.y,a.z), Vector3(b.x,a.y,b.z), Vector3(a.x,a.y,b.z)] ]
	for face in faces:
		var points: Array[Vector3] = []
		for v: Vector3 in face:
			points.append(at + basis * v)
		_quad(points, _metal if iron else _surface)

func _quad(points: Array[Vector3], target: SurfaceTool, shade := 1.0) -> void:
	var normal := (points[1] - points[0]).cross(points[2] - points[0]).normalized()
	var span := Vector2(points[0].distance_to(points[1]), points[1].distance_to(points[2]))
	var uv := [Vector2.ZERO, Vector2(span.x,0), span, Vector2(0,span.y)]
	for index in [0, 2, 1, 0, 3, 2]:
		target.set_normal(normal)
		target.set_color(Color(_face_tint.r * shade, _face_tint.g * shade, _face_tint.b * shade))
		target.set_uv(uv[index] * 0.85 + _uv_offset)
		target.add_vertex(points[index])
	triangle_count += 2
