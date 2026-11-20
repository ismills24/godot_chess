extends Control
const Piece = preload("res://assets/pieces/wood/wood_piece.gd")
const Shapes = preload("res://assets/pieces/wood/wood_shapes.gd")
var viewport: SubViewport
var display: TextureRect
var camera: Camera3D
var collection: Node3D
var pieces: Array[Node3D] = []
var pixelated := true
var yaw := 0.04
var elevation := 0.65
var zoom := 8.0
var focused := -1
var full_armies := false
var lifted := false
var detail_choice: OptionButton
var status: Label

func _ready() -> void:
    var background := ColorRect.new()
    background.color = Color("111a23")
    background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    add_child(background)
    viewport = SubViewport.new()
    viewport.own_world_3d = true
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    add_child(viewport)
    display = TextureRect.new()
    display.texture = viewport.get_texture()
    display.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    display.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    display.mouse_filter = Control.MOUSE_FILTER_IGNORE
    add_child(display)
    var environment := WorldEnvironment.new()
    environment.environment = Environment.new()
    environment.environment.background_mode = Environment.BG_COLOR
    environment.environment.background_color = Color("111a23")
    viewport.add_child(environment)
    var light := DirectionalLight3D.new()
    light.rotation_degrees = Vector3(-45, -35, 0)
    light.light_energy = 1.25
    viewport.add_child(light)
    var fill := DirectionalLight3D.new()
    fill.rotation_degrees = Vector3(-25, 150, 0)
    fill.light_energy = 0.6
    viewport.add_child(fill)
    camera = Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    camera.current = true
    viewport.add_child(camera)
    _controls()
    resized.connect(_resize)
    _resize()
    _populate()

func _controls() -> void:
    var panel := PanelContainer.new()
    panel.set_anchors_and_offsets_preset(Control.PRESET_RIGHT_WIDE)
    panel.offset_left = -300
    var style := StyleBoxFlat.new()
    style.bg_color = Color("1b2831")
    style.content_margin_left = 22
    style.content_margin_right = 22
    style.content_margin_top = 28
    panel.add_theme_stylebox_override("panel", style)
    add_child(panel)
    var column := VBoxContainer.new()
    column.add_theme_constant_override("separation", 18)
    panel.add_child(column)
    _label(column, "THE WOOD SET", 25)
    _label(column, "Maple / Walnut", 20)
    _label(column, "Six carvings. Two woods.\nInterchangeable heads.", 16)
    status = _label(column, "Generating carvings…", 14)
    detail_choice = OptionButton.new()
    detail_choice.add_item("Whole collection")
    for kind in Shapes.KINDS:
        detail_choice.add_item(kind.capitalize() + " / inspect pair")
    detail_choice.item_selected.connect(func(index: int): focused = index - 1; full_armies = false; _populate())
    column.add_child(detail_choice)
    var army := Button.new()
    army.text = "Show all 32 pieces"
    army.pressed.connect(func(): focused = -1; full_armies = true; detail_choice.select(0); _populate())
    column.add_child(army)
    var gallery := Button.new()
    gallery.text = "Show 12 designs"
    gallery.pressed.connect(func(): focused = -1; full_armies = false; detail_choice.select(0); _populate())
    column.add_child(gallery)
    var lift := CheckButton.new()
    lift.text = "Lift heads / inspect joints"
    lift.toggled.connect(func(value: bool): lifted = value; _lift_heads())
    column.add_child(lift)
    var pixel := CheckButton.new()
    pixel.text = "Pixel rendering"
    pixel.button_pressed = true
    pixel.toggled.connect(func(value: bool): pixelated = value; _resize())
    column.add_child(pixel)
    var reset := Button.new()
    reset.text = "Reset camera"
    reset.pressed.connect(func(): _reset_camera())
    column.add_child(reset)
    _label(column, "Drag to orbit\nScroll to zoom", 15)
    var note := _label(column, "Pale maple. Warm walnut.\n\nSimple silhouettes. Quiet grain.\nSmooth joints, wooden dowels.", 15)
    note.modulate = Color("adbdc6")

func _label(parent: Node, text: String, font_size: int) -> Label:
    var label := Label.new()
    label.text = text
    label.add_theme_font_size_override("font_size", font_size)
    parent.add_child(label)
    return label

func _resize() -> void:
    display.size = Vector2(maxf(size.x - 300, 160), maxf(size.y, 160))
    viewport.size = Vector2i(display.size / (2.0 if pixelated else 1.0))

func _populate() -> void:
    if collection != null:
        viewport.remove_child(collection)
        collection.queue_free()
    pieces.clear()
    collection = Node3D.new()
    viewport.add_child(collection)
    if focused >= 0:
        _add_piece(Shapes.KINDS[focused], false, Vector3(-0.95, 0, 0))
        _add_piece(Shapes.KINDS[focused], true, Vector3(0.95, 0, 0))
    elif full_armies:
        var rank = ["rook", "knight", "bishop", "queen", "king", "bishop", "knight", "rook"]
        for side in 2:
            for file in 8:
                var z := -3.0 if side == 1 else 2.1
                _add_piece(rank[file], side == 1, Vector3((file - 3.5) * 1.35, 0, z))
                _add_piece("pawn", side == 1, Vector3((file - 3.5) * 1.35, 0, z + 1.35))
    else:
        for side in 2:
            for i in 6:
                _add_piece(Shapes.KINDS[i], side == 1, Vector3((i - 2.5) * 1.65, 0, -2.35 if side == 1 else 2.35))
    _lift_heads()
    _reset_camera()
    status.text = "%s pieces · modular surface splats" % pieces.size()
    print("[WoodSet] Loaded ", pieces.size(), " pieces")

func _add_piece(kind: String, dark: bool, at: Vector3) -> void:
    var piece := Piece.new()
    piece.piece_kind = kind
    piece.dark_wood = dark
    piece.position = at
    collection.add_child(piece)
    pieces.append(piece)
    var stand := MeshInstance3D.new()
    var cylinder := CylinderMesh.new()
    cylinder.top_radius = 0.64
    cylinder.bottom_radius = 0.64
    cylinder.height = 0.06
    stand.mesh = cylinder
    stand.position = at + Vector3(0, -0.045, 0)
    var mat := StandardMaterial3D.new()
    mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
    mat.albedo_color = Color("243440")
    stand.material_override = mat
    collection.add_child(stand)
    if not full_armies:
        var label := Label3D.new()
        label.text = ("WALNUT  /  " if dark else "MAPLE  /  ") + kind.to_upper()
        label.font_size = 32
        label.pixel_size = 0.005
        label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
        label.modulate = Color("afc0ca")
        label.position = at + Vector3(0, -0.12, 0.81)
        collection.add_child(label)

func _lift_heads() -> void:
    for piece in pieces:
        piece.set_head_lift(0.65 if lifted else 0.0)

func _reset_camera() -> void:
    yaw = 0.04
    elevation = 0.65 if focused < 0 else 0.26
    zoom = (9.3 if full_armies else 8.0) if focused < 0 else 3.9
    _update_camera()

func _update_camera() -> void:
    var target := Vector3(0, 1.15, 0)
    camera.position = target + Vector3(sin(yaw) * cos(elevation), sin(elevation), cos(yaw) * cos(elevation)) * 15.0
    camera.size = zoom
    camera.look_at(target)

func _input(event: InputEvent) -> void:
    if event is InputEventMouseMotion and event.button_mask & MOUSE_BUTTON_MASK_LEFT and event.position.x < size.x - 300:
        yaw -= event.relative.x * 0.006
        elevation = clampf(elevation + event.relative.y * 0.005, 0.12, 1.2)
        _update_camera()
    if event is InputEventMouseButton and event.pressed and event.position.x < size.x - 300:
        if event.button_index == MOUSE_BUTTON_WHEEL_UP:
            zoom = maxf(2.5, zoom - 0.3)
        elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
            zoom = minf(13, zoom + 0.3)
        _update_camera()
