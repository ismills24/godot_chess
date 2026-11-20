extends "res://experiments/pawn/pawn_preview.gd"

const Part = preload("res://experiments/pawn/splat_part.gd")
const Parts = preload("res://experiments/pawn/workshop_parts.gd")
const DURATION = 13.5
const SEAT = 1.25
const PITCH = 0.10
const TURNS = 3.0
var workshop: Node3D
var original_head: Node3D
var metal_head: Node3D
var moving_splats: Array[MeshInstance3D] = []
var timeline: HSlider
var phase_label: Label
var play_button: Button
var elapsed := 0.0
var playing := true

func _ready() -> void:
    yaw = 0.22
    elevation = 0.22
    zoom = 3.9
    super._ready()
    palette_material.shader = preload("res://experiments/pawn/workshop_palette.gdshader")

func _make_tile() -> void:
    workshop = Node3D.new()
    viewport.add_child(workshop)
    var light := DirectionalLight3D.new()
    light.rotation_degrees = Vector3(-45, -35, 0)
    light.light_energy = 1.25
    workshop.add_child(light)
    var fill := DirectionalLight3D.new()
    fill.rotation_degrees = Vector3(-20, 140, 0)
    fill.light_energy = 0.55
    workshop.add_child(fill)
    var wood := Parts.material(Color("49352a"))
    var grain := Parts.material(Color("2c251f"))
    Parts.box(workshop, Vector3(0, -0.13, 0), Vector3(4.2, 0.22, 1.85), wood)
    for z in [-0.59, 0.02, 0.63]:
        Parts.box(workshop, Vector3(0, -0.016, z), Vector3(4.18, 0.005, 0.014), grain)
    var rng := RandomNumberGenerator.new()
    rng.seed = 42
    for i in 32:
        Parts.box(workshop, Vector3(rng.randf_range(-2, 2), -0.012, rng.randf_range(-0.85, 0.85)),
            Vector3(rng.randf_range(0.08, 0.42), 0.003, 0.004), grain)
    var pad := Parts.material(Color("28383b"))
    for x in [-1.35, 1.35]:
        Parts.box(workshop, Vector3(x, 0.0, 0), Vector3(0.94, 0.026, 0.94), pad)
    # Small discarded fasteners on the bench establish scale and workshop context.
    var steel := Parts.material(Color("767d79"))
    for i in 3:
        Parts.ring(workshop, Vector3(0.73 + i * 0.13, 0.01, 0.60), 0.035, 0.055, steel)

func _make_controls() -> void:
    var panel := PanelContainer.new()
    panel.set_anchors_and_offsets_preset(Control.PRESET_RIGHT_WIDE)
    panel.offset_left = -320
    var style := StyleBoxFlat.new()
    style.bg_color = Color("17212d")
    style.content_margin_left = 24
    style.content_margin_right = 24
    style.content_margin_top = 30
    panel.add_theme_stylebox_override("panel", style)
    add_child(panel)
    var column := VBoxContainer.new()
    column.add_theme_constant_override("separation", 18)
    panel.add_child(column)
    _label(column, "BACKROOM / 002", 25)
    _label(column, "A less innocent pawn.", 18)
    _label(column, "Drag to orbit · Wheel to zoom", 15)
    phase_label = _label(column, "", 18)
    phase_label.modulate = Color("e4c38b")
    count_label = _label(column, "", 14)
    play_button = Button.new()
    play_button.text = "Pause"
    play_button.pressed.connect(_toggle_play)
    column.add_child(play_button)
    var replay := Button.new()
    replay.text = "Replay head swap"
    replay.pressed.connect(func(): elapsed = 0.0; playing = true; play_button.text = "Pause"; _apply_pose())
    column.add_child(replay)
    _label(column, "Scrub the swap", 16)
    timeline = HSlider.new()
    timeline.min_value = 0
    timeline.max_value = DURATION
    timeline.step = 0.01
    timeline.value_changed.connect(func(value: float): elapsed = value; playing = false; play_button.text = "Play"; _apply_pose())
    column.add_child(timeline)
    var expose := Button.new()
    expose.text = "Inspect exposed thread"
    expose.pressed.connect(func(): elapsed = 7.0; playing = false; play_button.text = "Play"; _apply_pose())
    column.add_child(expose)
    var pixels := CheckButton.new()
    pixels.text = "Pixel rendering"
    pixels.button_pressed = true
    pixels.toggled.connect(func(value: bool): pixelated = value; _resize_preview())
    column.add_child(pixels)
    var palette := CheckButton.new()
    palette.text = "Workshop palette"
    palette.button_pressed = true
    palette.toggled.connect(func(value: bool): palette_material.set_shader_parameter("limited_palette", value))
    column.add_child(palette)
    var reset := Button.new()
    reset.text = "Reset camera"
    reset.pressed.connect(func(): yaw = 0.22; elevation = 0.22; zoom = 3.9; _update_camera())
    column.add_child(reset)
    var note := _label(column, "Unscrew. Lift clear. Replace.\nThree turns lock the new head\nonto the same threaded neck.", 15)
    note.modulate = Color("9aadb9")

func _generate(_spacing: float) -> void:
    samples.clear()
    for sample in Shape.sample_surface(0.028):
        # Keep the original base and shaft, replacing the fuzzy collar with
        # manufactured mating surfaces and a genuinely separate head.
        if sample.position.y < 1.14:
            samples.append(sample)
    cached_scale = -1.0
    var dark_brass := Parts.material(Color("67513c"))
    var ivory := Parts.material(Color("cdbb94"))
    Parts.cylinder(workshop, Vector3(0, 1.175, 0), 0.32, 0.10, ivory)
    Parts.ring(workshop, Vector3(0, 1.21, 0), 0.24, 0.34, ivory)
    Parts.cylinder(workshop, Vector3(0, 1.235, 0), 0.235, 0.035, dark_brass)
    Parts.wooden_dowel(workshop, SEAT, TURNS * PITCH, PITCH)
    original_head = Node3D.new()
    workshop.add_child(original_head)
    var old_part := Part.new()
    original_head.add_child(old_part)
    old_part.build(Part.sphere_samples(Vector3(0, 0.43, 0), 0.34, 2500, Color("efdab2"), 71))
    moving_splats.append(old_part)
    Parts.socket(original_head, ivory)
    # A nick and a longitudinal mark make rotation legible on the round head.
    Parts.box(original_head, Vector3(0, 0.07, 0.19), Vector3(0.025, 0.10, 0.015), dark_brass)
    var scratch := PackedVector3Array([Vector3(0.04, 0.31, 0.319), Vector3(0.06, 0.39, 0.334), Vector3(0.08, 0.46, 0.329)])
    Parts.tube(original_head, scratch, 0.007, dark_brass)
    metal_head = Node3D.new()
    workshop.add_child(metal_head)
    var metal_part := Part.new()
    metal_head.add_child(metal_part)
    metal_part.build(Part.sphere_samples(Vector3(0, 0.43, 0), 0.37, 3300, Color("8195a4"), 93))
    moving_splats.append(metal_part)
    var steel := Parts.material(Color("84949f"))
    Parts.socket(metal_head, steel)
    metal_part.material_override.set_shader_parameter("metal_finish", 1.0)
    count_label.text = "Solid steel head · Tier I"
    _update_camera()
    _apply_pose()

func _update_camera() -> void:
    var target := Vector3(0, 1.30, 0)
    camera.position = target + Vector3(sin(yaw) * cos(elevation), sin(elevation), cos(yaw) * cos(elevation)) * 6.0
    camera.size = zoom
    camera.look_at(target)
    _rebuild_sorted_mesh()
    for part in moving_splats:
        part.sort_for(camera)

func _toggle_play() -> void:
    if elapsed >= DURATION:
        elapsed = 0.0
    playing = not playing
    play_button.text = "Pause" if playing else "Play"

func _apply_pose() -> void:
    if original_head == null:
        return
    var old_y := SEAT
    var old_x := 0.0
    var old_angle := 0.0
    var new_y := 0.025
    var new_x := 1.35
    var new_angle := 0.0
    if elapsed < 1.0:
        phase_label.text = "01 / Original fitting"
    elif elapsed < 4.0:
        phase_label.text = "02 / Unscrewing"
    elif elapsed < 6.5:
        phase_label.text = "03 / Lift clear & set aside"
    elif elapsed < 9.5:
        phase_label.text = "04 / Align replacement"
    elif elapsed < 12.5:
        phase_label.text = "05 / Screw onto neck"
    else:
        phase_label.text = "06 / Modified pawn"
    if elapsed >= 1.0:
        var fraction := clampf((elapsed - 1.0) / 3.0, 0, 1)
        old_angle = fraction * TURNS * TAU
        old_y = SEAT + fraction * TURNS * PITCH
    if elapsed >= 4.0:
        old_y = lerpf(SEAT + TURNS * PITCH, 2.12, smoothstep(4.0, 4.8, elapsed))
    if elapsed >= 4.8:
        old_x = lerpf(0, -1.35, smoothstep(4.8, 5.6, elapsed))
    if elapsed >= 5.6:
        old_y = lerpf(2.12, 0.025, smoothstep(5.6, 6.5, elapsed))
    if elapsed >= 6.5:
        new_y = lerpf(0.025, 2.12, smoothstep(6.5, 7.5, elapsed))
    if elapsed >= 7.5:
        new_x = lerpf(1.35, 0, smoothstep(7.5, 8.5, elapsed))
    if elapsed >= 8.5:
        new_y = lerpf(2.12, SEAT + TURNS * PITCH, smoothstep(8.5, 9.5, elapsed))
    if elapsed >= 9.5:
        var fraction := clampf((elapsed - 9.5) / 3.0, 0, 1)
        new_angle = -fraction * TURNS * TAU
        new_y = SEAT + (1.0 - fraction) * TURNS * PITCH
    original_head.position = Vector3(old_x, old_y, 0)
    original_head.rotation.y = old_angle
    metal_head.position = Vector3(new_x, new_y, 0)
    metal_head.rotation.y = new_angle
    timeline.set_value_no_signal(elapsed)
    for part in moving_splats:
        part.sort_for(camera)

func _process(delta: float) -> void:
    if playing and original_head != null:
        elapsed = minf(elapsed + delta, DURATION)
        _apply_pose()
        if elapsed >= DURATION:
            playing = false
            play_button.text = "Play"

