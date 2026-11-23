extends "res://common/scene_kit.gd"
## The alley uses one batch per structural material and small transparent meshes for atmosphere.

const COBBLES := preload("res://menu/props/cobbles.jpg")
const COBBLE_NORMAL := preload("res://menu/props/cobbles_normal.jpg")
const BARREL := preload("res://menu/props/barrel.glb")
const HANDSAW := preload("res://menu/props/handsaw.glb")
const FACTORY := preload("res://menu/props/factory_bay.glb")
const FIRE_ESCAPE := preload("res://menu/props/fire_escape.glb")
const STOVE := preload("res://menu/props/barrel_stove.glb")
const BASKET := preload("res://menu/props/wicker_basket.glb")
const SPADE := preload("res://menu/props/rusted_spade.glb")
const BOARDS := preload("res://menu/props/salvaged_boards.glb")
const STUDY_PAWN := preload("res://menu/props/chess_pawn.glb")
const CLOTH := preload("res://menu/cloth.gdshader")
const FIRE := preload("res://menu/fire.gdshader")
const RAIN := preload("res://menu/rain.gdshader")
const SKYLINE := preload("res://menu/skyline.gdshader")
const OIL_GLASS := preload("res://menu/oil_glass.gdshader")
const LANTERN_GLASS := preload("res://menu/lantern_glass.gdshader")
const MIST := preload("res://menu/mist.gdshader")
const WET_COBBLES := preload("res://menu/wet_cobbles.gdshader")
const WOOD := preload("res://menu/sign_wood.gdshader")
const BRAZIER_AT := Vector3(2.40, 0, -2.60)
const CHESS_TABLE_AT := Vector3(1.98, 0.73, 2.42)

var _ropes: Array[Node3D] = []
var _flame_light: OmniLight3D
var _hanging_lantern: Node3D
var _lantern_light: OmniLight3D
var _rain_material: ShaderMaterial
var _paving: ShaderMaterial
var _sparks: MultiMesh

var _stone := _paint(Color("414444"), 0.91)
var _timber := _paint(Color("483125"), 0.81)
var _iron := _paint(Color("322d28"), 0.62, 0.53)
var _recess := _paint(Color("151a1b"), 1.0)
var _glass := _glowing(Color("d0681c"), 1.0)


func build(backlight_at: Vector3) -> void:
	_build_ground(backlight_at)
	_build_facades()
	_build_hanging_lantern(Vector3(-3.326, 3.89, 0.28))
	_build_skyline()
	_build_laundry()
	_build_chess_table()
	_build_street_props()
	_place_prop(BARREL, Vector3(-2.66, 0, -2.95), 0.82, 0.45)
	_place_prop(HANDSAW, CHESS_TABLE_AT + Vector3(0.88, 0.04, 0.19), 0.12, -0.58)
	_place_prop(BASKET, Vector3(-1.92, 0.01, -1.16), 0.39, 0.36)
	_place_prop(SPADE, Vector3(3.01, 0.02, -0.79), 1.21, -0.77)
	_place_prop(BOARDS, Vector3(3.13, 0.09, 0.69), 1.67, 0.16)
	_build_fire(BRAZIER_AT)
	_build_mist()
	_build_rain()
	_build_depth_cues()
	_flush_boxes()
	_animate()


func _animate() -> void:
	super()
	for i in _ropes.size():
		_ropes[i].rotation.x = 0.13 * sin(_elapsed * 0.75 + i * 0.82)
	_hanging_lantern.rotation.x = 0.065 * sin(_elapsed * 1.39 + 0.74)
	_hanging_lantern.rotation.z = 0.104 * sin(_elapsed * 1.83) + 0.023 * sin(_elapsed * 2.41)
	_lantern_light.light_energy = 2.9 + 0.16 * sin(_elapsed * 6.1) + 0.06 * sin(_elapsed * 14.2)
	_rain_material.set_shader_parameter("lantern_at", _lantern_light.global_position)
	_paving.set_shader_parameter("lantern_at", _lantern_light.global_position)
	_paving.set_shader_parameter("lantern_strength", _lantern_light.light_energy)
	_flame_light.light_energy = 5.6 + 0.85 * sin(_elapsed * 8.4) + 0.35 * sin(_elapsed * 17.1)
	for i in _sparks.instance_count:
		var life := fposmod(_elapsed * (0.43 + 0.023 * i) + 0.371 * i, 1.0)
		var drift := Vector3(sin(i * 4.0 + life * 3.2) * (0.19 + life * 0.43),
			life * (1.18 + 0.056 * i), cos(i * 3.8 + life * 3.1) * 0.23)
		var radius := (0.014 + 0.004 * (i % 3)) * (1.0 - life)
		_sparks.set_instance_transform(i, Transform3D(Basis.from_scale(Vector3.ONE * radius), drift))


func _build_ground(backlight_at: Vector3) -> void:
	_paving = _animated_material(WET_COBBLES)
	_paving.set_shader_parameter("cobbles", COBBLES)
	_paving.set_shader_parameter("relief", COBBLE_NORMAL)
	_paving.set_shader_parameter("fire_at", BRAZIER_AT + Vector3(0, 1.20, 0))
	_paving.set_shader_parameter("backlight_at", backlight_at)
	var street := PlaneMesh.new()
	street.size = Vector2(8.6, 55.0)
	_mesh(self, street, _paving, Vector3(0, 0, -17))
	for side: int in [-1, 1]:
		for i in 19:
			_box(_stone, Vector3(side * 3.02, 0.07, 6.2 - 2.58 * i), Vector3(0.31, 0.16, 2.53))
		for i in 10:
			var rubble_size := 0.10 + 0.10 * fposmod(i * 0.63, 1.0)
			_box(_stone, Vector3(side * (2.90 - 0.22 * sin(i * 5.2)), 0.08, 1.1 - i * 2.13),
				Vector3(rubble_size, 0.08, rubble_size * 1.6), Vector3(0.21, i * 0.92, 0.15))


func _build_facades() -> void:
	# Both modules retain their Blender assembly coordinates and relative mesh offsets.
	var factory := FACTORY.instantiate() as Node3D
	factory.position.x = 0.91
	add_child(factory)
	var oil_glass := ShaderMaterial.new()
	oil_glass.shader = OIL_GLASS
	var factory_brick := _masonry(Color("c1a594"), 0.0, true)
	factory_brick.set_shader_parameter("tile_scale", Vector2.ONE)
	for facade: MeshInstance3D in factory.find_children("*", "MeshInstance3D", true, false):
		for index in facade.mesh.get_surface_count():
			var material := facade.mesh.surface_get_material(index)
			if not material:
				continue
			var material_name := material.resource_name.to_lower()
			if "oil lit" in material_name:
				facade.set_surface_override_material(index, oil_glass)
			elif "factory_facade_brick_worldspace" in material_name:
				facade.set_surface_override_material(index, factory_brick)
	_omni(Vector3(2.81, 3.45, 1.24), Color("ff9b50"), 0.86, 3.0)
	var escape := FIRE_ESCAPE.instantiate() as Node3D
	escape.position = Vector3(-1.11, 0.07, -3.16)
	add_child(escape)
	_omni(Vector3(-1.23, 4.72, -2.74), Color("6b9ca6"), 0.78, 4.05)
	var brick := _masonry(Color("d0b0a0"), 0.0, true)
	var older_brick := _masonry(Color("b19f91"), 2.0, false)
	var sidewall := _paint(Color("263337"), 0.95)
	var slate := _paint(Color("2e3d41"), 0.92)
	for side: int in [-1, 1]:
		for bay in 7:
			if side == 1 and bay < 3:
				continue
			var z := 4.35 - bay * 6.55
			var wall := side * (3.33 + 0.13 * (bay % 3))
			var height := 7.55 + 1.12 * ((2 * bay + side + 4) % 3)
			var facade := QuadMesh.new()
			facade.size = Vector2(6.62, height)
			var surface := older_brick if bay > 0 and (bay + side + 3) % 3 == 1 else brick
			var front := _mesh(self, facade, surface, Vector3(wall, height * 0.5, z))
			front.rotation.y = -side * PI * 0.5
			_box(sidewall, Vector3(wall + side * 1.35, height * 0.5, z), Vector3(2.65, height - 0.08, 6.55))
			_box(slate, Vector3(wall + side * 1.08, height + 0.40, z), Vector3(2.59, 0.16, 6.76),
				Vector3(0, 0, side * 0.29))
			_box(_stone, Vector3(wall, height - 0.14, z), Vector3(0.34, 0.29, 6.65))
			if bay < 3:
				_box(sidewall, Vector3(wall + side * 0.52, height + 0.63, z - 1.82), Vector3(0.65, 1.46, 0.66))
				for pot in 2:
					_box(slate, Vector3(wall + side * (0.35 + 0.32 * pot), height + 1.45, z - 1.82),
						Vector3(0.21, 0.34, 0.23))
			for floor in 3:
				var y := 1.75 + 2.22 * floor
				if floor == 0 or (bay + floor + side) % 3 == 0:
					_box(_stone, Vector3(wall, y - 1.05, z), Vector3(0.18, 0.115, 6.61))
				for pane in 2:
					var lit := (bay * 3 + floor * 2 + pane + side + 3) % 5 < 2
					var window_z := z - 1.52 + pane * 3.08 + 0.18 * sin(bay * 3.12 + floor)
					if floor == 0 and pane == 1 and bay % 3 == 2:
						_door(side, Vector3(wall, 1.14, window_z))
					else:
						_window(side, Vector3(wall, y, window_z), lit)
			if bay < 4:
				_box(_iron, Vector3(wall - side * 0.12, height * 0.47, z - 2.93), Vector3(0.10, height * 0.91, 0.12))
				for ring in 3:
					_box(_stone, Vector3(wall - side * 0.13, 0.38 + ring * 2.33, z - 2.93), Vector3(0.17, 0.10, 0.17))
	_box(sidewall, Vector3(0, 3.0, -44.0), Vector3(9.6, 6.0, 0.75))
	for i in 5:
		var height := 7.6 + 1.99 * sin(i * 2.3) + i * 0.56
		var at := Vector3(-4.4 + i * 2.16, 0, -46.7 - 0.9 * (i % 2))
		_box(sidewall, at + Vector3(0, height * 0.5, 0), Vector3(2.47, height, 2.8))
		_box(slate, at + Vector3(0, height, 0), Vector3(2.58, 0.21, 2.89))
		_box(sidewall, at + Vector3(-0.35 + 0.15 * i, height + 0.76, 0.3),
			Vector3(0.64, 1.55 + 0.2 * (i % 3), 0.65))


func _build_skyline() -> void:
	var nearer := ShaderMaterial.new()
	nearer.shader = SKYLINE
	nearer.set_shader_parameter("haze_tint", Color("142b34"))
	var farther := ShaderMaterial.new()
	farther.shader = SKYLINE
	farther.set_shader_parameter("haze_tint", Color("264653"))
	var roofs := [
		Vector3(-12.2, 21.0, -48.0), Vector3(-8.35, 17.3, -52.0),
		Vector3(-4.8, 23.9, -58.0), Vector3(3.45, 20.6, -55.0),
		Vector3(8.3, 22.4, -49.0), Vector3(12.1, 24.7, -54.0),
	]
	for i in roofs.size():
		var rooftop: Vector3 = roofs[i]
		var material := nearer if i in [0, 1, 4] else farther
		var span := 3.24 + 0.29 * (i % 3)
		var body := rooftop.y - 1.25
		_box(material, Vector3(rooftop.x, body * 0.5, rooftop.z), Vector3(span, body, 3.52))
		var roof := PrismMesh.new()
		roof.size = Vector3(span + 0.39, 2.05, 3.78)
		_mesh(self, roof, material, rooftop - Vector3(0, 0.25, 0)).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		for pair: float in [-0.63, 0.62]:
			var chimney := rooftop + Vector3(pair, 1.04, 0.48)
			_box(material, chimney, Vector3(0.47, 1.92, 0.68))
			_box(material, chimney + Vector3(0, 0.86, 0), Vector3(0.61, 0.21, 0.81))

	# The narrow central spire is set behind the laundry, beyond the lane's end wall.
	var spire_at := Vector3(-0.66, 3.55, -55.4)
	_box(farther, spire_at + Vector3(0, 5.45, 0), Vector3(2.68, 10.9, 2.94))
	_box(farther, spire_at + Vector3(0, 11.52, 0), Vector3(1.86, 1.28, 2.0))
	_box(farther, spire_at + Vector3(0, 12.73, 0), Vector3(1.05, 1.45, 1.18))
	_cylinder(self, spire_at + Vector3(0, 14.39, 0), 0.75, 0.055, 2.17, farther)
	_box(farther, spire_at + Vector3(0, 15.91, 0), Vector3(0.13, 0.96, 0.14))


func _door(side: int, at: Vector3) -> void:
	_box(_recess, at, Vector3(0.14, 2.04, 1.17))
	for i in 5:
		_box(_timber, at + Vector3(-side * 0.09, 0, (i - 2) * 0.204), Vector3(0.09, 1.91, 0.19))
	for edge: int in [-1, 1]:
		_box(_stone, at + Vector3(-side * 0.12, 0, edge * 0.60), Vector3(0.24, 2.13, 0.14))
	_box(_stone, at + Vector3(-side * 0.13, 1.03, 0), Vector3(0.26, 0.21, 1.40))


func _window(side: int, at: Vector3, lit: bool) -> void:
	_box(_recess, at - Vector3(side * 0.033, 0, 0), Vector3(0.12, 1.43, 1.04))
	_box(_glass if lit else _recess, at - Vector3(side * 0.11, 0, 0), Vector3(0.05, 1.26, 0.84))
	for end: int in [-1, 1]:
		_box(_timber, at + Vector3(-side * 0.17, 0, end * 0.45), Vector3(0.09, 1.39, 0.12))
	_box(_timber, at - Vector3(side * 0.17, 0, 0), Vector3(0.09, 1.26, 0.055))
	_box(_timber, at - Vector3(side * 0.17, 0.1, 0), Vector3(0.10, 0.075, 0.92))
	_box(_stone, at + Vector3(-side * 0.14, -0.76, 0), Vector3(0.41, 0.17, 1.21))
	_box(_stone, at + Vector3(-side * 0.14, 0.77, 0), Vector3(0.33, 0.14, 1.14))


func _build_hanging_lantern(wall: Vector3) -> void:
	_box(_iron, wall + Vector3(0.037, -0.18, 0), Vector3(0.086, 0.57, 0.21))
	_box(_iron, wall + Vector3(0.45, 0.013, 0), Vector3(0.94, 0.072, 0.086))
	_box(_iron, wall + Vector3(0.38, -0.21, 0), Vector3(0.80, 0.048, 0.055), Vector3(0, 0, 0.51))
	_box(_iron, wall + Vector3(0.887, -0.042, 0), Vector3(0.062, 0.124, 0.078))

	_hanging_lantern = Node3D.new()
	_hanging_lantern.name = "LanternSwing"
	_hanging_lantern.position = wall + Vector3(0.887, -0.095, 0)
	add_child(_hanging_lantern)

	var link := TorusMesh.new()
	link.inner_radius = 0.020
	link.outer_radius = 0.035
	link.rings = 12
	link.ring_segments = 6
	for i in 6:
		var chain_link := _mesh(_hanging_lantern, link, _iron, Vector3(0, -0.028 - i * 0.054, 0))
		chain_link.rotation_degrees = Vector3(90, i * 90.0, 0)
		chain_link.scale.z = 1.20

	var glass := _animated_material(LANTERN_GLASS)
	_lantern_section(-0.569, 0.342, 0.118, 0.185, glass)
	_lantern_section(-0.377, 0.077, 0.221, 0.076, _iron)
	_lantern_section(-0.417, 0.031, 0.216, 0.216, _iron)
	_lantern_section(-0.749, 0.062, 0.092, 0.133, _iron)
	_lantern_section(-0.805, 0.060, 0.024, 0.075, _iron)
	var upright := BoxMesh.new()
	upright.size = Vector3(0.018, 0.354, 0.018)
	for i in 6:
		var angle := i * TAU / 6.0
		var lower := Vector3(cos(angle), 0, sin(angle)) * 0.118 + Vector3(0, -0.740, 0)
		var upper := Vector3(cos(angle), 0, sin(angle)) * 0.185 + Vector3(0, -0.398, 0)
		var bar := _mesh(_hanging_lantern, upright, _iron, (lower + upper) * 0.5)
		bar.basis = Basis(Quaternion(Vector3.UP, (upper - lower).normalized()))

	var haze := ShaderMaterial.new()
	haze.shader = MIST
	haze.set_shader_parameter("halo", true)
	haze.set_shader_parameter("opacity", 0.22)
	haze.set_shader_parameter("mist_color", Color("d48d45"))
	var halo := QuadMesh.new()
	halo.size = Vector2(1.6, 1.6)
	_mesh(_hanging_lantern, halo, haze, Vector3(0.09, -0.56, 0.18))
	for part: MeshInstance3D in _hanging_lantern.find_children("*", "MeshInstance3D", false, false):
		part.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

	_lantern_light = OmniLight3D.new()
	_lantern_light.name = "LanternLight"
	_lantern_light.position = Vector3(0.012, -0.57, 0)
	_lantern_light.light_color = Color("ffad62")
	_lantern_light.omni_range = 5.95
	_hanging_lantern.add_child(_lantern_light)


func _lantern_section(y: float, height: float, foot: float, top: float, material: Material) -> void:
	var section := CylinderMesh.new()
	section.height = height
	section.bottom_radius = foot
	section.top_radius = top
	section.radial_segments = 6
	section.rings = 0
	_mesh(_hanging_lantern, section, material, Vector3(0, y, 0))


func _build_laundry() -> void:
	var colors := [Color("8d724f"), Color("704c38"), Color("405453"), Color("a19272")]
	var rope := CylinderMesh.new()
	rope.top_radius = 0.018
	rope.bottom_radius = 0.018
	rope.radial_segments = 6
	rope.rings = 0
	rope.height = 1.0
	rope.material = _timber
	for i in 3:
		var line := Node3D.new()
		line.name = "LaundryLine%d" % i
		line.position = Vector3(0, 4.54 + i * 0.46, -5.75 - 6.95 * i)
		add_child(line)
		_ropes.append(line)
		var rope_batch := MultiMesh.new()
		rope_batch.transform_format = MultiMesh.TRANSFORM_3D
		rope_batch.mesh = rope
		rope_batch.instance_count = 12
		for segment in 12:
			var from := _rope_point(segment / 12.0)
			var to := _rope_point((segment + 1) / 12.0)
			var offset := to - from
			var direction := Basis(Quaternion(Vector3.UP, offset.normalized()))
			var scale_along_rope := Basis.from_scale(Vector3(1, offset.length() + 0.03, 1))
			rope_batch.set_instance_transform(segment, Transform3D(direction * scale_along_rope, (from + to) * 0.5))
		var rope_instance := MultiMeshInstance3D.new()
		rope_instance.multimesh = rope_batch
		line.add_child(rope_instance)
		for cloth in 4:
			var point := _rope_point(0.17 + cloth * 0.198)
			var fabric := _animated_material(CLOTH)
			fabric.set_shader_parameter("cloth_color", colors[(cloth + i) % colors.size()])
			var panel := PlaneMesh.new()
			var height := 0.72 + 0.18 * ((cloth + i) % 3)
			panel.size = Vector2(0.66 + 0.14 * (cloth % 2), height)
			panel.orientation = PlaneMesh.FACE_Z
			panel.subdivide_width = 9
			panel.subdivide_depth = 7
			var hanging := _mesh(line, panel, fabric, point - Vector3(0, height * 0.46, 0))
			hanging.rotation.z = 0.062 * sin(i * 1.3 + cloth * 2.6)
	var weathered_canvas := _animated_material(CLOTH)
	weathered_canvas.set_shader_parameter("cloth_color", Color("796d58"))
	var tarp := PlaneMesh.new()
	tarp.size = Vector2(0.74, 2.34)
	tarp.orientation = PlaneMesh.FACE_Z
	tarp.subdivide_width = 9
	tarp.subdivide_depth = 7
	var hung_tarp := _mesh(self, tarp, weathered_canvas, Vector3(3.06, 4.13, -1.33))
	hung_tarp.rotation_degrees = Vector3(0, -92.0, -3.5)
	_box(_timber, Vector3(3.26, 5.30, -1.33), Vector3(0.48, 0.09, 0.87))


func _rope_point(t: float) -> Vector3:
	return Vector3(lerpf(-3.57, 3.57, t), -sin(PI * t) * 0.51 + 0.31 * t, 0)


func _build_street_props() -> void:
	var old_oak := ShaderMaterial.new()
	old_oak.shader = WOOD
	old_oak.set_shader_parameter("wood_color", Color("685144"))
	for at: Vector3 in [Vector3(-2.54, 0.32, -1.92), Vector3(-2.76, 0.25, -4.20)]:
		_box(_recess, at, Vector3(0.67, 0.51, 0.68))
		for slat in 4:
			var y := at.y - 0.21 + slat * 0.14
			_box(old_oak, Vector3(at.x, y, at.z + 0.35), Vector3(0.73, 0.128, 0.049))
			_box(_timber, Vector3(at.x + 0.35, y, at.z), Vector3(0.050, 0.128, 0.73))
			_box(_timber, at + Vector3(-0.27 + 0.18 * slat, 0.28, 0), Vector3(0.164, 0.058, 0.74))
		for brace: int in [-1, 1]:
			_box(_timber, at + Vector3(brace * 0.27, 0, 0.39), Vector3(0.068, 0.57, 0.062))
		_box(_timber, at + Vector3(0.39, 0, 0), Vector3(0.066, 0.62, 0.065), Vector3(0.74, 0, 0))


func _build_chess_table() -> void:
	var at := CHESS_TABLE_AT
	for plank in 6:
		_box(_timber, at + Vector3(0, -0.03, (plank - 2.5) * 0.276), Vector3(1.91, 0.13, 0.268))
	for x: int in [-1, 1]:
		for z: int in [-1, 1]:
			_box(_timber, at + Vector3(x * 0.78, -0.37, z * 0.63), Vector3(0.125, 0.70, 0.13))
	_box(_timber, at + Vector3(0, -0.42, 0), Vector3(1.68, 0.12, 0.13))
	_box(_iron, at + Vector3(0, 0.051, 0), Vector3(1.47, 0.05, 1.47))
	var maple := _paint(Color("a0815e"), 0.61)
	var walnut := _paint(Color("554139"), 0.60)
	for file in 8:
		for rank in 8:
			_box(maple if (file + rank) % 2 == 0 else walnut,
				at + Vector3((file - 3.5) * 0.174, 0.079, (rank - 3.5) * 0.174), Vector3(0.172, 0.016, 0.172))
	var chessmen := [Vector4i(1, 6, 0, 1), Vector4i(3, 6, 1, 1), Vector4i(4, 4, 2, 1),
		Vector4i(5, 5, 0, 1), Vector4i(3, 4, 0, 0), Vector4i(2, 2, 0, 0),
		Vector4i(4, 1, 2, 0), Vector4i(6, 2, 1, 0)]
	for piece: Vector4i in chessmen:
		var square := at + Vector3((piece.x - 3.5) * 0.174, 0.087, (piece.y - 3.5) * 0.174)
		if piece.z == 0 and piece.w == 1:
			_place_prop(STUDY_PAWN, square, 0.20, piece.x * 0.6)
		else:
			_chessman(square, maple if piece.w == 1 else walnut, piece.z)
	_omni(at + Vector3(-0.61, 1.15, 0.27), Color("ffb066"), 0.45, 2.6)


func _chessman(at: Vector3, material: Material, kind: int) -> void:
	var height: float = [0.112, 0.155, 0.197][kind]
	_cylinder(self, at + Vector3(0, 0.017, 0), 0.062, 0.048, 0.033, material)
	_cylinder(self, at + Vector3(0, height * 0.5 + 0.022, 0), 0.046, 0.023, height, material)
	var crown := at + Vector3(0, height + 0.034, 0)
	if kind == 0:
		var head := SphereMesh.new()
		head.radius = 0.032
		head.height = 0.062
		head.radial_segments = 8
		head.rings = 4
		_mesh(self, head, material, crown + Vector3(0, 0.018, 0))
		return
	_cylinder(self, crown, 0.037, 0.049, 0.043, material)
	if kind == 2:
		_box(material, crown + Vector3(0, 0.047, 0), Vector3(0.018, 0.071, 0.02))
		_box(material, crown + Vector3(0, 0.053, 0), Vector3(0.066, 0.016, 0.02))
	else:
		for x: int in [-1, 1]:
			for z: int in [-1, 1]:
				_box(material, crown + Vector3(x * 0.027, 0.025, z * 0.027), Vector3(0.024, 0.025, 0.024))


func _build_fire(at: Vector3) -> void:
	_place_prop(STOVE, at, 0.92, 0.65)
	var ember := _glowing(Color("d9531e"), 2.6)
	var coal_black := _paint(Color("2b211e"), 0.94)
	for i in 9:
		var angle := i * 2.40
		var radius := 0.04 + 0.18 * fposmod(i * 0.73, 1.0)
		var chunk := at + Vector3(cos(angle) * radius, 0.914 + (i % 3) * 0.008, sin(angle) * radius)
		_box(coal_black if i % 3 == 1 else ember, chunk, Vector3(0.083, 0.027, 0.064),
			Vector3(0.12, angle, 0.15))
	var fire := MultiMesh.new()
	fire.transform_format = MultiMesh.TRANSFORM_3D
	fire.use_custom_data = true
	fire.mesh = _flame_mesh()
	fire.instance_count = 8
	fire.custom_aabb = AABB(Vector3(-0.58, -0.12, -0.58), Vector3(1.16, 1.7, 1.16))
	for i in fire.instance_count:
		var angle := i * 2.39996
		var radius := 0.04 + 0.17 * fposmod(i * 0.47, 1.0)
		var growth := 0.62 + 0.68 * fposmod(i * 0.61, 1.0)
		var width := 0.20 + 0.14 * fposmod(i * 0.39, 1.0)
		var scale := Vector3(width, growth, width)
		var offset := Vector3(cos(angle) * radius, -0.035 * (i % 3), sin(angle) * radius)
		var direction := Basis(Vector3.UP, angle) * Basis.from_scale(scale)
		fire.set_instance_transform(i, Transform3D(direction, offset))
		fire.set_instance_custom_data(i, Color(fposmod(i * 0.382, 1.0), fposmod(i * 0.17, 1.0), 1.0 - i / 8.0))
	var flames := MultiMeshInstance3D.new()
	flames.name = "FlameTongues"
	flames.multimesh = fire
	flames.material_override = _animated_material(FIRE)
	flames.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	flames.position = at + Vector3(0, 0.93, 0)
	flames.scale = Vector3(1.25, 1.45, 1.25)
	add_child(flames)
	_flame_light = _omni(at + Vector3(0.09, 1.16, 0.14), Color("ff8a45"), 5.6, 7.0)
	_flame_light.shadow_enabled = true
	_flame_light.shadow_bias = 0.06

	var plume := _animated_material(MIST)
	plume.set_shader_parameter("smoke", true)
	plume.set_shader_parameter("opacity", 0.62)
	plume.set_shader_parameter("mist_color", Color("5c5951"))
	var smoke := _mist_sheet(plume, at + Vector3(0.20, 2.75, -0.13), Vector2(2.4, 4.0))
	smoke.rotation.y = -0.2

	_sparks = MultiMesh.new()
	_sparks.transform_format = MultiMesh.TRANSFORM_3D
	var coal := BoxMesh.new()
	coal.material = _glowing(Color("ffae4c"), 4.0)
	_sparks.mesh = coal
	_sparks.instance_count = 12
	var sparks := MultiMeshInstance3D.new()
	sparks.multimesh = _sparks
	sparks.position = at + Vector3(0, 0.98, 0)
	sparks.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(sparks)


func _build_depth_cues() -> void:
	# Distant wall lamps give the fog bank a warm layer between the lane and the skyline.
	for lamp: Vector4 in [Vector4(-3.18, 3.55, -14.6, 1.0), Vector4(3.12, 3.25, -25.5, 0.62)]:
		var side := signf(lamp.x)
		var at := Vector3(lamp.x, lamp.y, lamp.z)
		_box(_iron, at + Vector3(-side * 0.30, 0.20, 0), Vector3(0.62, 0.05, 0.05))
		_box(_iron, at + Vector3(-side * 0.58, 0.06, 0), Vector3(0.18, 0.06, 0.18))
		_box(_glowing(Color("ffb35c"), 3.2 * lamp.w), at + Vector3(-side * 0.58, -0.13, 0), Vector3(0.15, 0.24, 0.15))
		var haze := ShaderMaterial.new()
		haze.shader = MIST
		haze.set_shader_parameter("halo", true)
		haze.set_shader_parameter("opacity", 0.55 * lamp.w)
		haze.set_shader_parameter("mist_color", Color("e0954c"))
		var glow := QuadMesh.new()
		glow.size = Vector2(3.6, 3.6)
		var halo := _mesh(self, glow, haze, at + Vector3(-side * 0.58, -0.13, 0.2))
		halo.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		_omni(at + Vector3(-side * 0.75, -0.35, 0), Color("ffa04e"), 2.2 * lamp.w, 4.6)

	# Foreground clutter frames the lower edge, as in the concept art.
	var scrap := _paint(Color("6e6650"), 0.9)
	var rng := RandomNumberGenerator.new()
	rng.seed = 4021
	for i in 14:
		var at := Vector3(rng.randf_range(-2.3, 0.6), 0.012, rng.randf_range(0.6, 3.2))
		_box(scrap, at, Vector3(rng.randf_range(0.10, 0.22), 0.008, rng.randf_range(0.08, 0.18)),
			Vector3(rng.randf_range(-0.12, 0.12), rng.randf() * TAU, rng.randf_range(-0.12, 0.12)))
	var plank := ShaderMaterial.new()
	plank.shader = WOOD
	plank.set_shader_parameter("wood_color", Color("5a4436"))
	_box(plank, Vector3(-1.55, 0.06, 2.55), Vector3(0.20, 0.06, 2.1), Vector3(0, 0.42, 0))
	_box(plank, Vector3(-1.20, 0.12, 2.75), Vector3(0.18, 0.05, 1.8), Vector3(0.0, 0.15, 0.06))
	_box(_timber, Vector3(-2.05, 0.30, 1.4), Vector3(0.16, 0.08, 1.5), Vector3(0.32, -0.25, 0.1))
	_box(_timber, Vector3(-2.2, 0.18, 3.1), Vector3(0.42, 0.34, 0.42), Vector3(0, 0.3, 0))


func _build_rain() -> void:
	var streak := QuadMesh.new()
	streak.size = Vector2(0.039, 0.37)
	var rain := MultiMesh.new()
	rain.transform_format = MultiMesh.TRANSFORM_3D
	rain.use_custom_data = true
	rain.mesh = streak
	rain.instance_count = 172
	rain.custom_aabb = AABB(Vector3(-4.3, -0.3, -22.4), Vector3(8.6, 8.6, 27.5))
	var rng := RandomNumberGenerator.new()
	rng.seed = 1827
	for i in rain.instance_count:
		rain.set_instance_transform(i, Transform3D.IDENTITY)
		rain.set_instance_custom_data(i, Color(rng.randf(), rng.randf(), rng.randf(), rng.randf()))
	var streaks := MultiMeshInstance3D.new()
	streaks.name = "RainStreaks"
	streaks.multimesh = rain
	streaks.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_rain_material = _animated_material(RAIN)
	_rain_material.set_shader_parameter("fire_at", BRAZIER_AT + Vector3(0, 1.20, 0))
	streaks.material_override = _rain_material
	add_child(streaks)


func _build_mist() -> void:
	for layer in 5:
		var mist := _animated_material(MIST)
		mist.set_shader_parameter("phase", layer * 2.73)
		mist.set_shader_parameter("opacity", 0.18 + layer * 0.072)
		mist.set_shader_parameter("mist_color", Color("355d63").lerp(Color("507c83"), layer / 4.0))
		var sheet := _mist_sheet(mist, Vector3(0, 1.18 + layer * 0.52, -2.8 - layer * 6.15),
			Vector2(7.5, 2.55 + layer * 1.18))
		sheet.rotation.y = 0.16 * cos(layer * 2.0)
