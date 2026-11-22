extends Node3D
## A wrought-iron bracket juts from the wall; the title board and option plaques hang
## from it on short chains, square to the lane so they face the camera.
## Local space: origin at the wall mount, +X along the arm, +Z out of the sign face.

const WOOD := preload("res://menu/sign_wood.gdshader")
const ARM_LENGTH := 2.30
const TITLE_SIZE := Vector2(1.62, 0.92)
const TITLE_DROP := 0.24
const HANG_X := 1.46
const PLAQUE_SIZE := Vector2(1.62, 0.34)
const PLAQUE_FRONT := 0.05
const PLAQUE_GAP := 0.13
const LINK_PITCH := 0.034
const MOTION_STEP := 1.0 / 30.0

var _plaques: Array[Node3D] = []
var _swings: Array[Node3D] = []
var _captions: Array[MeshInstance3D] = []
var _readouts: Array[MeshInstance3D] = []
var _trims: Array[StandardMaterial3D] = []
var _title_swing: Node3D
var _title_font := SystemFont.new()
var _body_font := SystemFont.new()
var _glyph_font := SystemFont.new()
var _iron := _metal(Color("3a332c"), 0.55)
var _brass := _metal(Color("b89869"), 0.47)
var _ink := _metal(Color("f0d0a3"), 0.22)
var _selected_ink := _metal(Color("fff0cb"), 0.18)
var _dull_trim := Color("8c6946")
var _selected_trim := Color("e5b573")
var _link := TorusMesh.new()
var _elapsed := 0.0
var _update := 0.0


func build() -> void:
	_title_font.font_names = PackedStringArray(["Georgia", "Liberation Serif", "Noto Serif", "DejaVu Serif"])
	_title_font.font_weight = 700
	_body_font.font_names = PackedStringArray(["Segoe UI", "Liberation Sans", "Noto Sans", "DejaVu Sans"])
	_body_font.font_weight = 620
	_glyph_font.font_names = PackedStringArray(["Segoe UI Symbol", "DejaVu Sans", "Noto Sans Symbols 2"])
	_link.inner_radius = 0.011
	_link.outer_radius = 0.021
	_link.rings = 8
	_link.ring_segments = 5
	_build_bracket()
	_build_title()
	for i in 3:
		_build_plaque(i)
	_sway()


func set_reduced_motion(enabled: bool) -> void:
	set_process(not enabled)
	if enabled:
		_elapsed = 0.0
		_sway()


func _process(delta: float) -> void:
	_elapsed += delta
	_update += delta
	if _update >= MOTION_STEP:
		_update = fmod(_update, MOTION_STEP)
		_sway()


func _sway() -> void:
	# Small enough that the plaques stay easy to click; picking reads live transforms.
	_title_swing.rotation.x = 0.030 * sin(_elapsed * 0.83) + 0.008 * sin(_elapsed * 2.1)
	_title_swing.rotation.z = 0.010 * sin(_elapsed * 1.17 + 0.6)
	for i in _swings.size():
		_swings[i].rotation.x = 0.024 * sin(_elapsed * 1.05 + 1.3 * (i + 1))
		_swings[i].rotation.z = 0.009 * sin(_elapsed * 1.41 + 0.9 * i)


func set_entries(labels: PackedStringArray, readouts: PackedStringArray) -> void:
	for i in _plaques.size():
		_swings[i].visible = i < labels.size()
		if not _swings[i].visible:
			continue
		var readout := readouts[i] if i < readouts.size() else ""
		(_readouts[i].mesh as TextMesh).text = readout
		_readouts[i].visible = not readout.is_empty()
		var caption := _captions[i]
		var label := caption.mesh as TextMesh
		label.text = labels[i]
		caption.position.x = -0.13 if _readouts[i].visible else 0.0
		label.pixel_size = 0.00142 if _readouts[i].visible else 0.00216


func show_focus(active: int) -> void:
	for i in _plaques.size():
		var selected := i == active
		_trims[i].albedo_color = _selected_trim if selected else _dull_trim
		_trims[i].emission_energy_multiplier = 1.4 if selected else 0.0
		_captions[i].material_override = _selected_ink if selected else _ink


func pick(camera: Camera3D, screen_at: Vector2) -> int:
	var eye := camera.project_ray_origin(screen_at)
	var aim := camera.project_ray_normal(screen_at)
	for i in _plaques.size():
		var plaque := _plaques[i]
		if not plaque.is_visible_in_tree():
			continue
		var origin := plaque.to_local(eye)
		var direction := plaque.to_local(eye + aim) - origin
		if direction.z >= -0.0001:
			continue
		var distance := (PLAQUE_FRONT - origin.z) / direction.z
		var contact := origin + direction * distance
		if distance >= 0.0 and Rect2(-PLAQUE_SIZE * 0.5, PLAQUE_SIZE).has_point(Vector2(contact.x, contact.y)):
			return i
	return -1


func _build_bracket() -> void:
	_block(self, Vector3(0.0, -0.16, 0), Vector3(0.05, 0.74, 0.22), _iron)
	for bolt: float in [0.13, -0.45]:
		_block(self, Vector3(0.03, bolt, 0), Vector3(0.03, 0.05, 0.05), _brass)
	_block(self, Vector3(ARM_LENGTH * 0.5, 0, 0), Vector3(ARM_LENGTH, 0.055, 0.05), _iron)
	_block(self, Vector3(ARM_LENGTH + 0.02, 0, 0), Vector3(0.07, 0.10, 0.07), _iron)
	var brace_from := Vector2(0.03, -0.50)
	var brace_to := Vector2(1.02, -0.02)
	var brace := brace_to - brace_from
	var strut := BoxMesh.new()
	strut.size = Vector3(brace.length(), 0.04, 0.04)
	var node := _mount(self, strut, _iron, Vector3((brace_from.x + brace_to.x) * 0.5, (brace_from.y + brace_to.y) * 0.5, 0))
	node.rotation.z = brace.angle()
	# Scrollwork curls fill the bracket triangle.
	for curl: Vector3 in [Vector3(0.27, -0.21, 0.13), Vector3(0.56, -0.12, 0.085), Vector3(0.78, -0.07, 0.05)]:
		var ring := TorusMesh.new()
		ring.outer_radius = curl.z
		ring.inner_radius = curl.z - 0.022
		ring.rings = 12
		ring.ring_segments = 4
		var scroll := _mount(self, ring, _iron, Vector3(curl.x, curl.y, 0))
		scroll.rotation.x = PI * 0.5


func _build_title() -> void:
	_title_swing = Node3D.new()
	_title_swing.name = "TitleSwing"
	_title_swing.position.x = HANG_X
	add_child(_title_swing)
	var top := -TITLE_DROP
	var center := top - TITLE_SIZE.y * 0.5
	for side: int in [-1, 1]:
		var x := side * TITLE_SIZE.x * 0.31
		_block(_title_swing, Vector3(x, -0.025, 0), Vector3(0.05, 0.03, 0.05), _iron)
		_chain(_title_swing, Vector3(x, -0.04, 0), top + 0.02)
		_block(_title_swing, Vector3(x, top + 0.01, 0), Vector3(0.07, 0.04, 0.08), _iron)

	var walnut := _wood(Color("503023"))
	var brass_rim := _metal(Color("8c6b46"), 0.48)
	_block(_title_swing, Vector3(0, center, 0), Vector3(TITLE_SIZE.x, TITLE_SIZE.y, 0.07), walnut)
	_block(_title_swing, Vector3(0, center, 0.040), Vector3(TITLE_SIZE.x - 0.08, TITLE_SIZE.y - 0.08, 0.016), brass_rim)
	_block(_title_swing, Vector3(0, center, 0.050), Vector3(TITLE_SIZE.x - 0.15, TITLE_SIZE.y - 0.15, 0.016), walnut)
	_lettering(_title_swing, "GODOT", _title_font, 0.00290, Vector3(0, center + 0.205, 0.062), _ink)
	_lettering(_title_swing, "CHESS", _title_font, 0.00290, Vector3(0, center - 0.020, 0.062), _ink)
	_lettering(_title_swing, "♞", _glyph_font, 0.00260, Vector3(0, center - 0.245, 0.062), _brass)
	for side: int in [-1, 1]:
		_block(_title_swing, Vector3(side * 0.30, center - 0.245, 0.060), Vector3(0.36, 0.014, 0.01), _brass)
		for end: int in [-1, 1]:
			_rivet(_title_swing, Vector3(side * (TITLE_SIZE.x * 0.5 - 0.075), center + end * (TITLE_SIZE.y * 0.5 - 0.075), 0.060))


func _build_plaque(index: int) -> void:
	# Each plaque hangs from the one above, so the stack reads as one chained sign.
	var parent := _title_swing if index == 0 else _plaques[index - 1]
	var hang_from := -TITLE_DROP - TITLE_SIZE.y if index == 0 else -PLAQUE_SIZE.y * 0.5
	var swing := Node3D.new()
	swing.name = "PlaqueSwing%d" % (index + 1)
	swing.position.y = hang_from
	parent.add_child(swing)
	_swings.append(swing)
	for side: int in [-1, 1]:
		_chain(swing, Vector3(side * PLAQUE_SIZE.x * 0.33, -0.012, 0), -PLAQUE_GAP + 0.01)

	var plaque := Node3D.new()
	plaque.name = "Plaque%d" % (index + 1)
	plaque.position.y = -PLAQUE_GAP - PLAQUE_SIZE.y * 0.5
	swing.add_child(plaque)
	_plaques.append(plaque)
	var trim := _metal(_dull_trim, 0.48)
	trim.emission_enabled = true
	trim.emission = _selected_trim
	trim.emission_energy_multiplier = 0.0
	_trims.append(trim)
	_block(plaque, Vector3.ZERO, Vector3(PLAQUE_SIZE.x, PLAQUE_SIZE.y, 0.06), trim)
	_block(plaque, Vector3(0, 0, 0.032), Vector3(PLAQUE_SIZE.x - 0.055, PLAQUE_SIZE.y - 0.055, 0.016), _wood(Color("402c22")))
	for side: int in [-1, 1]:
		_rivet(plaque, Vector3(side * (PLAQUE_SIZE.x * 0.5 - 0.045), 0, 0.042))
	var caption := _lettering(plaque, "", _body_font, 0.00216, Vector3(0, 0.003, PLAQUE_FRONT), _ink)
	caption.name = "Caption"
	_captions.append(caption)
	_readouts.append(_lettering(plaque, "", _body_font, 0.00137, Vector3(0.60, 0.003, PLAQUE_FRONT), _ink))


func _chain(parent: Node3D, from: Vector3, bottom: float) -> void:
	var count := maxi(1, int((from.y - bottom) / LINK_PITCH))
	for i in count:
		var link := _mount(parent, _link, _iron, from - Vector3(0, i * LINK_PITCH, 0))
		link.rotation_degrees = Vector3(90, 90.0 * (i % 2), 0)
		link.scale.z = 1.25
		link.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _rivet(parent: Node3D, at: Vector3) -> void:
	var mesh := CylinderMesh.new()
	mesh.top_radius = 0.022
	mesh.bottom_radius = 0.021
	mesh.height = 0.02
	mesh.radial_segments = 8
	mesh.rings = 0
	var node := _mount(parent, mesh, _brass, at)
	node.rotation.x = PI * 0.5


func _lettering(parent: Node3D, content: String, font: Font, pixel_size: float, at: Vector3, material: Material) -> MeshInstance3D:
	var mesh := TextMesh.new()
	mesh.text = content
	mesh.font = font
	mesh.font_size = 96
	mesh.pixel_size = pixel_size
	mesh.depth = 0.012
	mesh.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	mesh.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	return _mount(parent, mesh, material, at)


func _block(parent: Node3D, at: Vector3, dimensions: Vector3, material: Material) -> void:
	var mesh := BoxMesh.new()
	mesh.size = dimensions
	_mount(parent, mesh, material, at)


func _mount(parent: Node3D, mesh: Mesh, material: Material, at: Vector3) -> MeshInstance3D:
	var instance := MeshInstance3D.new()
	instance.mesh = mesh
	instance.material_override = material
	instance.position = at
	parent.add_child(instance)
	return instance


func _wood(color: Color) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = WOOD
	material.set_shader_parameter("wood_color", color)
	return material


func _metal(color: Color, metallic: float) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.metallic = metallic
	material.roughness = 0.52
	return material
