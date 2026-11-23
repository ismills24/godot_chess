extends Control
## Standalone art workbench. Uses the menu's pixel finish; owns no shop/game state.
const ShopContainer := preload("res://assets/shop/shop_container.gd")
const Containers := preload("res://assets/shop/shop_containers.gd")
const FINISH := preload("res://menu/pixel_finish.gdshader")
const LABELS := ["01  Compass / wine crate", "02  Pawn / cardboard", "03  Knight / wine crate", "04  Bishop / wine crate", "05  Key / cardboard"]
const PRICES := [18, 4, 12, 9, 7]
const DEFAULTS := [Vector3(2.1, 1.0, 1.25), Vector3(1.55, 0.9, 1.1), Vector3(1.15, 1.55, 0.95), Vector3(1.0, 1.35, 0.9), Vector3(1.25, 0.75, 0.95)]
const KINDS := [0, 1, 0, 0, 1]

var containers: Array[Node3D] = []
var _items: Array[Node3D] = []
var _item_bounds: Array[AABB] = []
var _tags: Array[Node3D] = []
var _sizes: Array[Vector3] = []
var _sliders: Array[HSlider] = []
var _values: Array[Label] = []
var _stage: SubViewport
var _world: Node3D
var _stack: Node3D
var _camera: Camera3D
var _display: ColorRect
var _sidebar: PanelContainer
var _floor_shelf: MeshInstance3D
var _middle_shelf: MeshInstance3D
var _status: Label
var _seed_label: Label
var _selected := 2
var _seed := 17
var _animate := false
var _clock := 0.0
var _tick := 0.0
var _yaw := 0.16
var _pitch := 0.20
var _zoom := 1.0
var _target := Vector3.ZERO
var _camera_distance := 7.0
var _base_size := Vector3.ONE
var _syncing := false

func _ready() -> void:
	Engine.max_fps = 60
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	for size: Vector3 in DEFAULTS:
		_sizes.append(size)
	_build_world()
	_build_controls()
	_populate()
	_sync_controls()
	resized.connect(_resize)
	_resize()
	if "--capture-shop" in OS.get_cmdline_user_args():
		_capture.call_deferred()
	if "--verify-shop" in OS.get_cmdline_user_args():
		_verify_preview.call_deferred()

func _build_world() -> void:
	_stage = SubViewport.new()
	_stage.own_world_3d = true
	_stage.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	_stage.size = Vector2i(704, 500)
	add_child(_stage)
	_world = Node3D.new()
	_stage.add_child(_world)
	var environment := Environment.new()
	environment.background_mode = Environment.BG_COLOR
	environment.background_color = Color("15272c")
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.36, 0.55, 0.64)
	environment.ambient_light_energy = 0.52
	environment.tonemap_mode = Environment.TONE_MAPPER_AGX
	environment.tonemap_exposure = 1.15
	environment.adjustment_enabled = true
	environment.adjustment_contrast = 1.04
	environment.adjustment_saturation = 1.10
	var env := WorldEnvironment.new()
	env.environment = environment
	_world.add_child(env)
	_camera = Camera3D.new()
	_camera.fov = 42
	_camera.current = true
	_world.add_child(_camera)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-35, -35, 0)
	fill.light_color = Color("92afb7")
	fill.light_energy = 0.65
	_world.add_child(fill)
	var light := OmniLight3D.new()
	light.position = Vector3(-2.8, 4.5, 3.8)
	light.light_color = Color("ffcc8c")
	light.light_energy = 3.1
	light.omni_range = 12
	light.shadow_enabled = true
	_world.add_child(light)
	var bounce := OmniLight3D.new()
	bounce.position = Vector3(3, 2.8, 2.6)
	bounce.light_color = Color("9cb7ba")
	bounce.light_energy = 1.1
	bounce.omni_range = 9
	_world.add_child(bounce)
	var floor_material := _textured("res://menu/props/cobbles.jpg", Vector3(8, 8, 1))
	var floor_mesh := PlaneMesh.new()
	floor_mesh.size = Vector2(22, 18)
	_mesh(_world, floor_mesh, floor_material, Vector3(0, -0.045, -3))
	var brick := _textured("res://menu/props/brick.jpg", Vector3(2.2, 2.2, 1))
	brick.albedo_color = Color("85817a")
	_block(_world, Vector3(4.8, 2.6, -1.8), Vector3(1.5, 5.2, 3.0), brick)
	_block(_world, Vector3(-5.3, 2.6, -1.8), Vector3(1.5, 5.2, 3), brick)
	# A broad segmented brick arch frames the shop without obscuring its silhouette.
	for i in 15:
		var angle := PI * float(i + 0.5) / 15.0
		var segment := _block(_world, Vector3(cos(angle) * 4.6 - 0.25, 2.0 + sin(angle) * 2.5, -1.8), Vector3(1.04, 0.60, 3.0), brick)
		segment.rotation.z = angle + PI * 0.5
	var distant := _plain(Color("25383d"))
	for i in 9:
		var height := 2.0 + float((i * 7) % 5) * 0.44
		_block(_world, Vector3((i - 4) * 1.55, height * 0.5, -9), Vector3(1.4, height, 1.5), distant)
		_block(_world, Vector3((i - 4) * 1.55 + 0.3, height + 0.3, -9), Vector3(0.23, 0.65, 0.3), distant)
	var wood := _textured("res://assets/shop/textures/wine_crate_wood.png", Vector3.ONE)
	wood.albedo_color = Color("82766a")
	_floor_shelf = _block(_world, Vector3.ZERO, Vector3.ONE, wood)
	_middle_shelf = _block(_world, Vector3.ZERO, Vector3.ONE, wood)
	_stack = Node3D.new()
	_world.add_child(_stack)
	# Bridge seepage: a small number of low-poly drips and dark puddle patches.
	var water := StandardMaterial3D.new()
	water.albedo_color = Color("6e8b90")
	water.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	for x in [-4.3, -3.6, 3.8, 4.6]:
		for j in 4:
			_block(_world, Vector3(x, 0.45 + j * 0.9, -0.8), Vector3(0.014, 0.09 + j * 0.018, 0.014), water)
		var puddle := CylinderMesh.new()
		puddle.top_radius = 0.34
		puddle.bottom_radius = 0.34
		puddle.height = 0.008
		puddle.radial_segments = 10
		var puddle_node := _mesh(_world, puddle, _plain(Color("1f3338")), Vector3(x, -0.035, -0.8))
		puddle_node.scale.z = 1.5
	_display = ColorRect.new()
	var finish := ShaderMaterial.new()
	finish.shader = FINISH
	finish.set_shader_parameter("stage_texture", _stage.get_texture())
	finish.set_shader_parameter("vignette", 0.25)
	_display.material = finish
	_display.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_display)

func _populate() -> void:
	for i in 5:
		var box := Containers.create_wine_crate(_sizes[i], _seed + i * 31) if KINDS[i] == ShopContainer.Kind.WINE_CRATE else Containers.create_cardboard_box(_sizes[i], _seed + i * 31)
		_stack.add_child(box)
		containers.append(box)
		var item: Node3D
		if i in [1, 2, 3]:
			var piece: String = ["", "LightPawn", "LightKnight", "DarkBishop"][i]
			item = load("res://assets/pieces/wood/" + piece + ".tscn").instantiate()
			item.set("depth_tested_splats", true)
		else:
			item = _artifact(i == 0)
		box.item_anchor.add_child(item)
		_items.append(item)
		_item_bounds.append(_bounds(item))
		var tag := _price_tag(PRICES[i])
		box.tag_anchor.add_child(tag)
		_tags.append(tag)
	_layout()

func _layout() -> void:
	var lower_width := _sizes[0].x + _sizes[1].x + 0.13
	var upper_width := _sizes[2].x + _sizes[3].x + _sizes[4].x + 0.16
	var width := maxf(lower_width, upper_width) + 0.18
	var base_y := 0.24
	var upper_y := base_y + maxf(_sizes[0].y, _sizes[1].y) + 0.10
	_floor_shelf.position = Vector3(0, base_y * 0.5, 0)
	_floor_shelf.mesh.size = Vector3(width + 0.16, base_y, 1.55)
	_middle_shelf.position = Vector3(0, upper_y - 0.045, 0)
	_middle_shelf.mesh.size = Vector3(width, 0.09, 1.4)
	containers[0].position = Vector3(-lower_width * 0.5 + _sizes[0].x * 0.5, base_y, 0.03)
	containers[1].position = Vector3(lower_width * 0.5 - _sizes[1].x * 0.5, base_y, 0.03)
	var cursor := -upper_width * 0.5
	for i in range(2, 5):
		containers[i].position = Vector3(cursor + _sizes[i].x * 0.5, upper_y, -0.08)
		cursor += _sizes[i].x + 0.08
	for i in 5:
		containers[i].rotation_degrees.y = [-2.0, 2.0, -3.0, 1.5, 3.0][i]
		# Tags hang in front of the shared support and projecting cardboard flaps.
		var tag_front := maxf(0.87, _sizes[i].z * 0.5 + 0.27)
		_tags[i].position.z = tag_front - containers[i].position.z - containers[i].tag_anchor.position.z
		_fit_item(i)
	var height := upper_y + maxf(_sizes[2].y, maxf(_sizes[3].y, _sizes[4].y))
	_target = Vector3(0, height * 0.48, 0)
	var aspect := float(_stage.size.x) / maxf(_stage.size.y, 1)
	var needed := maxf(height * 0.65 + 0.3, (width * 0.60 + 0.3) / aspect)
	_camera_distance = needed / tan(deg_to_rad(_camera.fov * 0.5)) + 1.6
	_update_camera()
	if is_instance_valid(_status):
		_status.text = "%d triangles\n128 × 128 textures · nearest filtering" % _total_triangles()

func _fit_item(index: int) -> void:
	var room: Vector3 = containers[index].usable_size()
	var bounds := _item_bounds[index]
	var scale_factor := minf(room.x * 0.7 / maxf(bounds.size.x, 0.01), room.y * 0.78 / maxf(bounds.size.y, 0.01))
	scale_factor = minf(scale_factor, room.z * 0.66 / maxf(bounds.size.z, 0.01))
	_items[index].scale = Vector3.ONE * scale_factor
	_items[index].position = Vector3(-bounds.get_center().x, -bounds.position.y, -bounds.get_center().z) * scale_factor

func _build_controls() -> void:
	_sidebar = PanelContainer.new()
	var panel := StyleBoxFlat.new()
	panel.bg_color = Color("172025")
	panel.border_color = Color("56605b")
	panel.border_width_left = 1
	panel.content_margin_left = 24
	panel.content_margin_right = 24
	panel.content_margin_top = 28
	panel.content_margin_bottom = 24
	_sidebar.add_theme_stylebox_override("panel", panel)
	add_child(_sidebar)
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 12)
	_sidebar.add_child(column)
	_label(column, "VICTORIAN SLUM / 01", 13, Color("b6a582"))
	_label(column, "Crates & cardboard", 24, Color("f0dfbd"))
	_label(column, "Resize a container.\nThe stack adapts around it.", 15, Color("a3b0b0"))
	column.add_child(HSeparator.new())
	_label(column, "CONTAINER", 12, Color("b6a582"))
	var select := OptionButton.new()
	for caption in LABELS:
		select.add_item(caption)
	select.selected = _selected
	select.item_selected.connect(func(index: int):
		_selected = index
		_animate = false
		get_node("%Animate").set_pressed_no_signal(false)
		_sync_controls())
	column.add_child(select)
	for axis in 3:
		var row := HBoxContainer.new()
		column.add_child(row)
		var name_label := _label(row, ["Width", "Height", "Depth"][axis], 15, Color("d2d9d4"))
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		_values.append(_label(row, "", 15, Color("ddbd83")))
		var slider := HSlider.new()
		slider.min_value = ShopContainer.MIN_SIZE[axis]
		slider.max_value = ShopContainer.MAX_SIZE[axis]
		slider.step = 0.01
		slider.custom_minimum_size.y = 24
		slider.value_changed.connect(_dimension_changed.bind(axis))
		column.add_child(slider)
		_sliders.append(slider)
	var animate := CheckButton.new()
	animate.name = "Animate"
	animate.text = "Animate dimensions"
	column.add_child(animate)
	animate.owner = self
	animate.unique_name_in_owner = true
	animate.toggled.connect(func(enabled: bool):
		_animate = enabled
		_base_size = _sizes[_selected]
		_clock = 0.0)
	column.add_child(HSeparator.new())
	_seed_label = _label(column, "", 14, Color("a3b0b0"))
	var reroll := Button.new()
	reroll.text = "New wear seed"
	reroll.pressed.connect(_reroll)
	column.add_child(reroll)
	var reset := Button.new()
	reset.text = "Reset dimensions & camera"
	reset.pressed.connect(_reset)
	column.add_child(reset)
	var spacer := Control.new()
	spacer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	column.add_child(spacer)
	_status = _label(column, "", 13, Color("a3b0b0"))
	_label(column, "Right-drag to orbit · Wheel to zoom\nR: new wear · Space: animate", 12, Color("7f9394"))
	var title := Label.new()
	title.text = "UNDER THE VIADUCT"
	title.position = Vector2(30, 24)
	title.add_theme_font_size_override("font_size", 19)
	title.add_theme_color_override("font_color", Color("ddd2b8"))
	title.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(title)

func _sync_controls() -> void:
	_syncing = true
	for axis in 3:
		_sliders[axis].value = _sizes[_selected][axis]
		_values[axis].text = "%.2f" % _sizes[_selected][axis]
	_seed_label.text = "WEAR SEED   %d" % _seed
	_syncing = false

func _dimension_changed(value: float, axis: int) -> void:
	if _syncing:
		return
	_sizes[_selected][axis] = value
	containers[_selected].resize(_sizes[_selected])
	_values[axis].text = "%.2f" % value
	_layout()

func _reroll() -> void:
	_seed += 1
	for i in 5:
		containers[i].set_wear(_seed + i * 31)
	_sync_controls()
	_layout()

func _reset() -> void:
	_animate = false
	get_node("%Animate").set_pressed_no_signal(false)
	_yaw = 0.16
	_pitch = 0.20
	_zoom = 1.0
	for i in 5:
		_sizes[i] = DEFAULTS[i]
		containers[i].resize(_sizes[i])
	_sync_controls()
	_layout()

func _resize() -> void:
	var panel_width := clampf(size.x * 0.23, 290, 340)
	var scene_size := Vector2(maxf(size.x - panel_width, 160), size.y)
	_sidebar.position = Vector2(scene_size.x, 0)
	_sidebar.size = Vector2(panel_width, size.y)
	_display.size = scene_size
	_stage.size = Vector2i((scene_size * (704.0 / maxf(scene_size.x, scene_size.y))).round()).max(Vector2i(2, 2))
	if not containers.is_empty():
		_layout()

func _update_camera() -> void:
	_camera.position = _target + Vector3(sin(_yaw) * cos(_pitch), sin(_pitch), cos(_yaw) * cos(_pitch)) * _camera_distance * _zoom
	_camera.look_at(_target)

func _process(delta: float) -> void:
	if not _animate:
		return
	_clock += delta
	_tick += delta
	if _tick < 0.10:
		return
	_tick = 0.0
	var size_now := _base_size + Vector3(sin(_clock) * 0.45, sin(_clock * 0.73) * 0.3, sin(_clock * 0.87) * 0.2)
	_sizes[_selected] = size_now.clamp(ShopContainer.MIN_SIZE, ShopContainer.MAX_SIZE)
	containers[_selected].resize(_sizes[_selected])
	_sync_controls()
	_layout()

func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and Input.is_mouse_button_pressed(MOUSE_BUTTON_RIGHT):
		_yaw = clampf(_yaw - event.relative.x * 0.004, -0.65, 0.65)
		_pitch = clampf(_pitch + event.relative.y * 0.004, 0.05, 0.65)
		_update_camera()
	elif event is InputEventMouseButton and event.pressed:
		if event.position.x > _display.size.x:
			return
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			_zoom = maxf(0.7, _zoom - 0.06)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			_zoom = minf(1.6, _zoom + 0.06)
		_update_camera()
	elif event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_R:
			_reroll()
		elif event.keycode == KEY_SPACE:
			get_node("%Animate").button_pressed = not _animate

func _price_tag(price: int) -> Node3D:
	var root := Node3D.new()
	var paper := _plain(Color("d6bd8c"))
	_block(root, Vector3(0, -0.17, 0.035), Vector3(0.46, 0.34, 0.018), paper)
	var string_material := _plain(Color("8b7855"))
	for x in [-0.16, 0.16]:
		_block(root, Vector3(x, 0.025, 0.042), Vector3(0.012, 0.12, 0.012), string_material)
	var text := Label3D.new()
	text.text = str(price)
	text.position = Vector3(0, -0.17, 0.049)
	text.font_size = 64
	text.pixel_size = 0.0041
	text.outline_size = 0
	text.modulate = Color("2d241b")
	text.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	root.add_child(text)
	return root

func _artifact(compass: bool) -> Node3D:
	var root := Node3D.new()
	var brass := _plain(Color("a78a48"))
	if compass:
		var disc := CylinderMesh.new()
		disc.top_radius = 0.45
		disc.bottom_radius = 0.45
		disc.height = 0.14
		disc.radial_segments = 10
		_mesh(root, disc, brass, Vector3(0, 0.07, 0))
		var face := CylinderMesh.new()
		face.top_radius = 0.36
		face.bottom_radius = 0.36
		face.height = 0.01
		face.radial_segments = 10
		_mesh(root, face, _plain(Color("c6c5a9")), Vector3(0, 0.15, 0))
		var needle := _block(root, Vector3(0, 0.17, 0), Vector3(0.055, 0.025, 0.57), _plain(Color("6a3028")))
		needle.rotation.y = 0.48
		# A raised lid makes the small artifact read from the shop camera.
		var lid := _mesh(root, disc, brass, Vector3(0, 0.50, -0.40))
		lid.rotation.x = PI * 0.41
	else:
		var ring := TorusMesh.new()
		ring.inner_radius = 0.16
		ring.outer_radius = 0.24
		ring.rings = 10
		ring.ring_segments = 4
		var bow := _mesh(root, ring, brass, Vector3(-0.38, 0.28, 0))
		bow.rotation.x = PI * 0.5
		_block(root, Vector3(0.16, 0.28, 0), Vector3(0.75, 0.075, 0.075), brass)
		for x in [0.31, 0.48]:
			_block(root, Vector3(x, 0.19, 0), Vector3(0.075, 0.20, 0.075), brass)
	return root

static func _bounds(root: Node3D) -> AABB:
	var bounds := AABB()
	var found := false
	for node in root.find_children("*", "MeshInstance3D", true, false):
		if node.mesh == null:
			continue
		var local: AABB = (root.global_transform.affine_inverse() * node.global_transform) * node.get_aabb()
		bounds = bounds.merge(local) if found else local
		found = true
	return bounds

func _total_triangles() -> int:
	var count := 0
	for box in containers:
		count += box.triangle_count
	return count

static func _label(parent: Node, value: String, font_size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = value
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_color", color)
	parent.add_child(label)
	return label

static func _plain(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.94
	return material

static func _textured(path: String, uv_scale: Vector3) -> StandardMaterial3D:
	var material := _plain(Color.WHITE)
	material.albedo_texture = load(path)
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	material.uv1_scale = uv_scale
	return material

static func _block(parent: Node3D, at: Vector3, size_value: Vector3, material: Material) -> MeshInstance3D:
	var box := BoxMesh.new()
	box.size = size_value
	return _mesh(parent, box, material, at)

static func _mesh(parent: Node3D, mesh_value: Mesh, material: Material, at: Vector3) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.mesh = mesh_value
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node

func _capture() -> void:
	for frame in 12:
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	DirAccess.make_dir_recursive_absolute("res://artifacts/shop")
	var error := get_viewport().get_texture().get_image().save_png("res://artifacts/shop/shop-default.png")
	if error != OK:
		get_tree().quit(error)
		return
	# Exercise the same signals the interactive controls use, then capture the result.
	_sliders[0].value = 2.5
	_sliders[1].value = 1.05
	_sliders[2].value = 1.5
	for frame in 6:
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	error = get_viewport().get_texture().get_image().save_png("res://artifacts/shop/shop-resized.png")
	print("SHOP_CAPTURE: default and resized, dimensions=", containers[_selected].dimensions)
	get_tree().quit(error)

func _verify_preview() -> void:
	await get_tree().process_frame
	_sliders[0].value = 2.5
	assert(is_equal_approx(containers[_selected].dimensions.x, 2.5))
	assert(containers[_selected].scale.is_equal_approx(Vector3.ONE))
	var before: PackedVector3Array = containers[_selected].get_node("ContainerMesh").mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
	_reroll()
	assert(containers[_selected].dimensions.is_equal_approx(_sizes[_selected]))
	var after: PackedVector3Array = containers[_selected].get_node("ContainerMesh").mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
	assert(before != after)
	_reset()
	assert(containers[2].dimensions == DEFAULTS[2])
	get_node("%Animate").button_pressed = true
	_process(0.5)
	assert(containers[2].dimensions != DEFAULTS[2])
	print("PASS: preview slider signals, rebuilt geometry, wear seed, reset, animation")
	get_tree().quit()
