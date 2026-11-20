extends Control

const Shape = preload("res://experiments/pawn/pawn_shape.gd")
var samples: Array[Dictionary] = []
var viewport: SubViewport
var display: TextureRect
var camera: Camera3D
var splats: MeshInstance3D
var splat_material: ShaderMaterial
var palette_material: ShaderMaterial
var count_label: Label
var pixelated := true
var spinning := false
var yaw := 0.55
var elevation := 0.25
var zoom := 3.2
var stroke_scale := 1.0
var cached_arrays: Array = []
var cached_scale := -1.0

func _ready() -> void:
    RenderingServer.set_default_clear_color(Color("101722"))
    var backdrop := ColorRect.new()
    backdrop.color = Color("101722")
    backdrop.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    add_child(backdrop)
    viewport = SubViewport.new()
    viewport.own_world_3d = true
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    add_child(viewport)
    display = TextureRect.new()
    display.mouse_filter = Control.MOUSE_FILTER_IGNORE
    display.texture = viewport.get_texture()
    display.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    display.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    palette_material = ShaderMaterial.new()
    palette_material.shader = preload("res://experiments/pawn/palette.gdshader")
    display.material = palette_material
    add_child(display)
    var world := WorldEnvironment.new()
    var environment := Environment.new()
    environment.background_mode = Environment.BG_COLOR
    environment.background_color = Color("101722")
    world.environment = environment
    viewport.add_child(world)
    camera = Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.current = true
    viewport.add_child(camera)
    splats = MeshInstance3D.new()
    splat_material = ShaderMaterial.new()
    splat_material.shader = preload("res://experiments/pawn/splat.gdshader")
    splats.material_override = splat_material
    viewport.add_child(splats)
    _make_tile()
    _make_controls()
    resized.connect(_resize_preview)
    _resize_preview()
    _generate(0.035)

func _make_tile() -> void:
    var tile := MeshInstance3D.new()
    var box := BoxMesh.new()
    box.size = Vector3(1.65, 0.14, 1.65)
    tile.mesh = box
    tile.position.y = -0.08
    var mat := StandardMaterial3D.new()
    mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
    mat.albedo_color = Color("34434a")
    tile.material_override = mat
    viewport.add_child(tile)
    # An inset top and a contact-shadow disc ground the splats without a mesh pawn.
    var top := MeshInstance3D.new()
    var top_box := BoxMesh.new()
    top_box.size = Vector3(1.57, 0.012, 1.57)
    top.mesh = top_box
    top.position.y = -0.002
    var top_mat := mat.duplicate() as StandardMaterial3D
    top_mat.albedo_color = Color("465455")
    top.material_override = top_mat
    viewport.add_child(top)
    var shadow := MeshInstance3D.new()
    var cylinder := CylinderMesh.new()
    cylinder.top_radius = 0.65
    cylinder.bottom_radius = 0.65
    cylinder.height = 0.002
    shadow.mesh = cylinder
    shadow.position.y = 0.006
    var shadow_mat := mat.duplicate() as StandardMaterial3D
    shadow_mat.albedo_color = Color("202a2f")
    shadow.material_override = shadow_mat
    viewport.add_child(shadow)

func _make_controls() -> void:
    var panel := PanelContainer.new()
    panel.set_anchors_and_offsets_preset(Control.PRESET_RIGHT_WIDE)
    panel.offset_left = -320
    var style := StyleBoxFlat.new()
    style.bg_color = Color("17212d")
    style.content_margin_left = 24
    style.content_margin_right = 24
    style.content_margin_top = 32
    panel.add_theme_stylebox_override("panel", style)
    add_child(panel)
    var column := VBoxContainer.new()
    column.add_theme_constant_override("separation", 18)
    panel.add_child(column)
    _label(column, "PAWN / 001", 28)
    _label(column, "SDF surface + Gaussian strokes", 16)
    _label(column, "Drag to orbit · Wheel to zoom", 15)
    count_label = _label(column, "Sampling…", 15)
    var pixels := CheckButton.new()
    pixels.text = "Pixel rendering"
    pixels.button_pressed = true
    pixels.toggled.connect(func(value: bool): pixelated = value; _resize_preview())
    column.add_child(pixels)
    var palette := CheckButton.new()
    palette.text = "12-color palette"
    palette.button_pressed = true
    palette.toggled.connect(func(value: bool): palette_material.set_shader_parameter("limited_palette", value))
    column.add_child(palette)
    var spin := CheckButton.new()
    spin.text = "Turntable"
    spin.toggled.connect(func(value: bool): spinning = value)
    column.add_child(spin)
    _label(column, "Stroke size", 16)
    var scale_slider := HSlider.new()
    scale_slider.min_value = 0.45
    scale_slider.max_value = 2.2
    scale_slider.step = 0.05
    scale_slider.value = 1.0
    scale_slider.value_changed.connect(func(value: float): stroke_scale = value; _rebuild_sorted_mesh())
    column.add_child(scale_slider)
    _label(column, "Surface sampling", 16)
    var density := OptionButton.new()
    density.add_item("Sparse / visible brushstrokes")
    density.add_item("Medium / default")
    density.add_item("Dense / smoother silhouette")
    density.selected = 1
    density.item_selected.connect(func(index: int): _generate([0.055, 0.035, 0.024][index]))
    column.add_child(density)
    var reset := Button.new()
    reset.text = "Reset camera"
    reset.pressed.connect(func(): yaw = 0.55; elevation = 0.25; zoom = 3.2; _update_camera())
    column.add_child(reset)
    var note := _label(column, "Study the silhouette first.\nThen try sparse strokes and\nswitch the pixel treatment off.", 15)
    note.modulate = Color("9aadb9")

func _label(parent: Node, text: String, font_size: int) -> Label:
    var label := Label.new()
    label.text = text
    label.add_theme_font_size_override("font_size", font_size)
    parent.add_child(label)
    return label

func _resize_preview() -> void:
    display.size = Vector2(maxf(size.x - 320, 160), maxf(size.y, 160))
    viewport.size = Vector2i(display.size / (3.0 if pixelated else 1.0))

func _generate(spacing: float) -> void:
    samples = Shape.sample_surface(spacing)
    cached_scale = -1.0
    count_label.text = "%s surface splats" % samples.size()
    print("[Pawn] samples=", samples.size(), " spacing=", spacing)
    _update_camera()

func _update_camera() -> void:
    var target := Vector3(0, 0.86, 0)
    camera.position = target + Vector3(sin(yaw) * cos(elevation), sin(elevation), cos(yaw) * cos(elevation)) * 5.0
    camera.size = zoom
    camera.look_at(target)
    _rebuild_sorted_mesh()

func _rebuild_sorted_mesh() -> void:
    if not is_equal_approx(cached_scale, stroke_scale):
        _cache_geometry()
    # Native integer sorting avoids a GDScript comparator per comparison.
    # Encode quantized depth and stable sample index in each key.
    var direction := camera.global_basis.z
    var keys := PackedInt64Array()
    keys.resize(samples.size())
    for i in samples.size():
        var sample_position: Vector3 = samples[i].position
        keys[i] = (roundi(sample_position.dot(direction) * 10000.0) + 100000) * samples.size() + i
    keys.sort()
    var indices := PackedInt32Array()
    indices.resize(samples.size() * 6)
    for i in keys.size():
        var first := int(keys[i] % samples.size()) * 6
        for j in 6:
            indices[i * 6 + j] = first + j
    cached_arrays[Mesh.ARRAY_INDEX] = indices
    var mesh := ArrayMesh.new()
    mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, cached_arrays)
    splats.mesh = mesh

func _cache_geometry() -> void:
    var vertices := PackedVector3Array()
    var colors := PackedColorArray()
    var uvs := PackedVector2Array()
    const CORNERS = [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, -1), Vector2(1, 1), Vector2(-1, 1)]
    for sample in samples:
        var n: Vector3 = sample.normal
        var tangent := n.cross(Vector3.UP if absf(n.y) < 0.95 else Vector3.RIGHT).normalized()
        var bitangent := n.cross(tangent).normalized()
        for corner in CORNERS:
            vertices.append(sample.position + (tangent * corner.x + bitangent * corner.y * 0.80) * sample.radius * stroke_scale)
            colors.append(sample.color)
            uvs.append((corner + Vector2.ONE) * 0.5)
    cached_arrays.resize(Mesh.ARRAY_MAX)
    cached_arrays[Mesh.ARRAY_VERTEX] = vertices
    cached_arrays[Mesh.ARRAY_COLOR] = colors
    cached_arrays[Mesh.ARRAY_TEX_UV] = uvs
    cached_scale = stroke_scale

func _input(event: InputEvent) -> void:
    if event is InputEventMouseMotion and event.button_mask & MOUSE_BUTTON_MASK_LEFT and event.position.x < size.x - 320:
        yaw -= event.relative.x * 0.008
        elevation = clampf(elevation + event.relative.y * 0.006, -0.12, 1.2)
        _update_camera()
    if event is InputEventMouseButton and event.pressed and event.position.x < size.x - 320:
        if event.button_index == MOUSE_BUTTON_WHEEL_UP:
            zoom = maxf(2.2, zoom - 0.2)
            _update_camera()
        elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
            zoom = minf(5.0, zoom + 0.2)
            _update_camera()

func _process(delta: float) -> void:
    if spinning:
        yaw += delta * 0.35
        _update_camera()
