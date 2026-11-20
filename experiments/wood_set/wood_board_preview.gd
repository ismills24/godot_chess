extends "res://experiments/wood_set/wood_set_preview.gd"
const Board = preload("res://assets/boards/wood/wood_board.gd")
var board: Node3D
var shadows: Node3D
var auto_orbit := false
var orbit_toggle: CheckButton

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
    column.add_theme_constant_override("separation", 20)
    panel.add_child(column)
    _label(column, "THE WOOD BOARD", 23)
    _label(column, "Maple / Walnut", 20)
    _label(column, "A simple frame.\nA full starting set.", 16)
    status = _label(column, "", 14)
    var reset := Button.new()
    reset.text = "White's view"
    reset.pressed.connect(_reset_camera)
    column.add_child(reset)
    var reverse := Button.new()
    reverse.text = "Black's view"
    reverse.pressed.connect(func(): _stop_orbit(); yaw = PI; elevation = 1.0; zoom = 13.5; _update_camera())
    column.add_child(reverse)
    var top := Button.new()
    top.text = "Overhead view"
    top.pressed.connect(func(): _stop_orbit(); yaw = 0; elevation = 1.55; zoom = 14.0; _update_camera())
    column.add_child(top)
    orbit_toggle = CheckButton.new()
    orbit_toggle.text = "Auto orbit"
    orbit_toggle.toggled.connect(func(value: bool): auto_orbit = value)
    column.add_child(orbit_toggle)
    var pixel := CheckButton.new()
    pixel.text = "Pixel rendering"
    pixel.button_pressed = true
    pixel.toggled.connect(func(value: bool): pixelated = value; _resize())
    column.add_child(pixel)
    var show_pieces := CheckButton.new()
    show_pieces.text = "Show pieces"
    show_pieces.button_pressed = true
    show_pieces.toggled.connect(func(value: bool):
        for piece in pieces:
            piece.visible = value
        shadows.visible = value)
    column.add_child(show_pieces)
    _label(column, "Drag to orbit\nScroll to zoom", 15)
    var note := _label(column, "Quiet grain. A walnut frame.\nRoom around every piece.", 15)
    note.modulate = Color("adbdc6")

func _populate() -> void:
    collection = Node3D.new()
    viewport.add_child(collection)
    board = Board.new()
    board.name = "Board"
    collection.add_child(board)
    shadows = Node3D.new()
    shadows.name = "ContactShadows"
    collection.add_child(shadows)
    var shadow_mesh := PlaneMesh.new()
    shadow_mesh.size = Vector2(1.45, 1.45)
    var shadow_mat := ShaderMaterial.new()
    shadow_mat.shader = preload("res://experiments/wood_set/contact_shadow.gdshader")
    var back_rank = ["rook", "knight", "bishop", "queen", "king", "bishop", "knight", "rook"]
    for dark in [false, true]:
        var home_rank := 7 if dark else 0
        var pawn_rank := 6 if dark else 1
        for file in 8:
            for rank in [home_rank, pawn_rank]:
                var kind: String = back_rank[file] if rank == home_rank else "pawn"
                var piece := Piece.new()
                piece.name = ("Dark" if dark else "Light") + kind.capitalize() + str(file)
                piece.piece_kind = kind
                piece.dark_wood = dark
                piece.depth_tested_splats = true
                piece.position = Board.square_center(file, rank)
                piece.set_meta("file", file)
                piece.set_meta("rank", rank)
                piece.rotation.y = (-PI / 2.0 if dark else PI / 2.0) if kind == "knight" else (PI if dark else 0.0)
                collection.add_child(piece)
                pieces.append(piece)
                var shadow := MeshInstance3D.new()
                shadow.mesh = shadow_mesh
                shadow.material_override = shadow_mat
                shadow.position = piece.position + Vector3(0, 0.004, 0)
                var radius_scale: float = Shapes.RADII[kind] / 0.55
                shadow.scale = Vector3(radius_scale, 1, radius_scale)
                shadows.add_child(shadow)
    status.text = "64 squares · 32 wooden pieces"
    _reset_camera()
    print("[WoodBoard] Starting position ready: 32 pieces")

func _reset_camera() -> void:
    _stop_orbit()
    yaw = 0.0
    elevation = 1.0
    zoom = 13.5
    _update_camera()

func _stop_orbit() -> void:
    auto_orbit = false
    if orbit_toggle != null:
        orbit_toggle.set_pressed_no_signal(false)

func _update_camera() -> void:
    var target := Vector3(0, 0.35, 0)
    camera.position = target + Vector3(sin(yaw) * cos(elevation), sin(elevation), cos(yaw) * cos(elevation)) * 22.0
    camera.size = zoom
    camera.look_at(target)

func _input(event: InputEvent) -> void:
    if event is InputEventMouseMotion and event.button_mask & MOUSE_BUTTON_MASK_LEFT and event.position.x < size.x - 300:
        yaw -= event.relative.x * 0.006
        elevation = clampf(elevation + event.relative.y * 0.005, 0.22, 1.55)
        _update_camera()

    if event is InputEventMouseButton and event.pressed and event.position.x < size.x - 300:
        if event.button_index == MOUSE_BUTTON_WHEEL_UP:
            zoom = maxf(8.0, zoom - 0.4)
        elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
            zoom = minf(20.0, zoom + 0.4)
        _update_camera()

func _process(delta: float) -> void:
    if auto_orbit:
        yaw += delta * 0.4
        _update_camera()
