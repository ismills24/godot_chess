extends Node3D
## Shared builder helpers for code-built 3D scenes (the alley menu, the inn).
## Boxes queued with _box() are drawn as one MultiMesh per material by _flush_boxes().
## Materials made with _animated_material() get "elapsed" stepped at MOTION_STEP by _animate().

const BRICK := preload("res://menu/props/brick.jpg")
const BRICK_NORMAL := preload("res://menu/props/brick_normal.jpg")
const BRICK_ROUGH := preload("res://menu/props/brick_rough.jpg")
const EXPOSED_BRICK := preload("res://menu/props/exposed_brick.jpg")
const EXPOSED_BRICK_NORMAL := preload("res://menu/props/exposed_brick_normal.jpg")
const EXPOSED_BRICK_ROUGH := preload("res://menu/props/exposed_brick_rough.jpg")
const MASONRY := preload("res://menu/masonry.gdshader")
const MOTION_STEP := 1.0 / 30.0

var _batches: Dictionary = {}
var _moving_materials: Array[ShaderMaterial] = []
var _elapsed := 0.0
var _update := 0.0


func set_reduced_motion(enabled: bool) -> void:
	set_process(not enabled)


func _process(delta: float) -> void:
	_elapsed += delta
	_update += delta
	if _update >= MOTION_STEP:
		_update = fmod(_update, MOTION_STEP)
		_animate()


func _animate() -> void:
	for material in _moving_materials:
		material.set_shader_parameter("elapsed", _elapsed)


func _masonry(tint: Color, age: float, exposed: bool) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = MASONRY
	material.set_shader_parameter("masonry", EXPOSED_BRICK if exposed else BRICK)
	material.set_shader_parameter("relief", EXPOSED_BRICK_NORMAL if exposed else BRICK_NORMAL)
	material.set_shader_parameter("rough_map", EXPOSED_BRICK_ROUGH if exposed else BRICK_ROUGH)
	material.set_shader_parameter("color_tint", tint)
	material.set_shader_parameter("age", age)
	return material


func _mist_sheet(mist: ShaderMaterial, at: Vector3, dimensions: Vector2) -> MeshInstance3D:
	var quad := QuadMesh.new()
	quad.size = dimensions
	var sheet := _mesh(self, quad, mist, at)
	sheet.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return sheet


func _flame_mesh() -> ArrayMesh:
	var mesh := SurfaceTool.new()
	mesh.begin(Mesh.PRIMITIVE_TRIANGLES)
	var heights := [0.0, 0.26, 0.59, 0.83, 1.0]
	var widths := [0.40, 0.44, 0.24, 0.09, 0.0]
	for band in range(heights.size() - 1):
		for side in 5:
			var shade := 0.27 + 0.67 * fposmod(side * 0.379 + band * 0.29, 1.0)
			for corner: Vector2i in [Vector2i(0, 0), Vector2i(1, 0), Vector2i(0, 1),
					Vector2i(0, 1), Vector2i(1, 0), Vector2i(1, 1)]:
				var ring := band + corner.y
				var angle := (side + corner.x) * TAU / 5.0 + ring * 0.28
				var height: float = heights[ring]
				var width: float = widths[ring]
				mesh.set_color(Color(shade, shade, shade))
				mesh.set_uv(Vector2(0, height))
				mesh.add_vertex(Vector3(cos(angle) * width + height * height * 0.25, height, sin(angle) * width))
	return mesh.commit()


func _place_prop(scene: PackedScene, at: Vector3, height: float, yaw: float) -> Node3D:
	var prop := scene.instantiate() as Node3D
	add_child(prop)
	var bounds := AABB()
	var found := false
	for mesh: MeshInstance3D in prop.find_children("*", "MeshInstance3D", true, false):
		var local := prop.global_transform.affine_inverse() * mesh.global_transform * mesh.get_aabb()
		bounds = bounds.merge(local) if found else local
		found = true
	if found and bounds.size.y > 0.001:
		var factor := height / bounds.size.y
		var base := bounds.get_center() - Vector3(0, bounds.size.y * 0.5, 0)
		prop.basis = Basis(Vector3.UP, yaw).scaled(Vector3.ONE * factor)
		prop.position = at - prop.basis * base
	return prop


func _box(mat: Material, at: Vector3, size: Vector3, angles := Vector3.ZERO) -> void:
	if not _batches.has(mat):
		_batches[mat] = []
	_batches[mat].append(Transform3D(Basis.from_euler(angles) * Basis.from_scale(size), at))


func _flush_boxes() -> void:
	for material: Material in _batches:
		var box := BoxMesh.new()
		box.size = Vector3.ONE
		box.material = material
		var batch := MultiMesh.new()
		batch.transform_format = MultiMesh.TRANSFORM_3D
		batch.mesh = box
		var transforms: Array = _batches[material]
		batch.instance_count = transforms.size()
		for index in transforms.size():
			batch.set_instance_transform(index, transforms[index])
		var instance := MultiMeshInstance3D.new()
		instance.multimesh = batch
		add_child(instance)
	_batches.clear()


## An empty node to hang limbs from, so poses are plain rotations.
func _pivot(parent: Node3D, at: Vector3) -> Node3D:
	var node := Node3D.new()
	node.position = at
	parent.add_child(node)
	return node


## An unbatched box under `parent`, for parts that move with it.
func _box_mesh(parent: Node3D, material: Material, at: Vector3, size: Vector3) -> MeshInstance3D:
	var box := BoxMesh.new()
	box.size = size
	return _mesh(parent, box, material, at)

func _cylinder(parent: Node3D, at: Vector3, foot: float, top: float, height: float, material: Material) -> void:
	var cylinder := CylinderMesh.new()
	cylinder.bottom_radius = foot
	cylinder.top_radius = top
	cylinder.height = height
	cylinder.radial_segments = 10
	cylinder.rings = 0
	_mesh(parent, cylinder, material, at)


func _animated_material(shader: Shader) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = shader
	_moving_materials.append(material)
	return material


func _omni(at: Vector3, color: Color, energy: float, reach: float) -> OmniLight3D:
	var lamp := OmniLight3D.new()
	lamp.position = at
	lamp.light_color = color
	lamp.light_energy = energy
	lamp.omni_range = reach
	add_child(lamp)
	return lamp


func _mesh(parent: Node3D, mesh: Mesh, material: Material, at: Vector3) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node


static func _paint(color: Color, roughness: float, metallic := 0.0) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = roughness
	material.metallic = metallic
	return material


static func _glowing(color: Color, energy: float) -> StandardMaterial3D:
	var material := _paint(color, 0.6)
	material.emission_enabled = true
	material.emission = color
	material.emission_energy_multiplier = energy
	return material
