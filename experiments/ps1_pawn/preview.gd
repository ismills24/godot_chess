extends Control
var viewport: SubViewport
var camera: Camera3D
var pawn: Node3D
var old: Node3D
var materials: Array[ShaderMaterial] = []
var yaw := 0.0
var pitch := 0.24
var zoom := 4.5
var exploded := false
var auto_orbit := false
var pixelated := true
var capture_frames := 0

func _ready() -> void:
    var bg := ColorRect.new()
    bg.color = Color("13171c")
    bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    add_child(bg)
    viewport = SubViewport.new()
    viewport.size = Vector2i(480, 270)
    viewport.own_world_3d = true
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    add_child(viewport)
    var display := TextureRect.new()
    display.texture = viewport.get_texture()
    display.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
    display.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
    display.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
    display.mouse_filter = Control.MOUSE_FILTER_IGNORE
    add_child(display)
    var env := WorldEnvironment.new()
    env.environment = Environment.new()
    env.environment.background_mode = Environment.BG_COLOR
    env.environment.background_color = Color("232831")
    viewport.add_child(env)
    camera = Camera3D.new()
    camera.projection = Camera3D.PROJECTION_PERSPECTIVE
    camera.fov = 36
    viewport.add_child(camera)
    old = load("res://assets/pieces/wood/LightPawn.tscn").instantiate()
    old.depth_tested_splats = true
    old.position.x = -0.9
    viewport.add_child(old)
    pawn = load("res://experiments/ps1_pawn/PawnPS1.tscn").instantiate()
    pawn.position.x = 0.9
    viewport.add_child(pawn)
    restyle(pawn)
    for x in [-0.9,0.9]:
        var stand := MeshInstance3D.new()
        var mesh := CylinderMesh.new()
        mesh.top_radius = 0.64
        mesh.bottom_radius = 0.64
        mesh.height = 0.06
        mesh.radial_segments = 12
        stand.mesh = mesh
        stand.position = Vector3(x,-0.045,0)
        var mat := StandardMaterial3D.new()
        mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
        mat.albedo_color = Color("353a43")
        stand.material_override = mat
        viewport.add_child(stand)
    var panel := VBoxContainer.new()
    panel.position = Vector2(24,20)
    add_child(panel)
    var title := Label.new()
    title.text = "PAWN / TEXTURED PS1 STUDY"
    title.add_theme_font_size_override("font_size",26)
    panel.add_child(title)
    var note := Label.new()
    note.text = "Original wood: left   ·   TRELLIS + low-poly texture pass: right"
    panel.add_child(note)
    var row := HBoxContainer.new()
    panel.add_child(row)
    button(row,"Separate modules",func(): exploded = not exploded; apply_explode())
    button(row,"Pixel / raw view",func(): pixelated = not pixelated; viewport.size = Vector2i(480,270) if pixelated else Vector2i(1440,810))
    button(row,"Vertex snap",func():
        for mat in materials: mat.set_shader_parameter("vertex_snap",not mat.get_shader_parameter("vertex_snap")))
    button(row,"Auto orbit",func(): auto_orbit = not auto_orbit)
    button(row,"Reset view",func(): yaw = 0.0; pitch = 0.24; zoom = 4.5; update_camera())
    var footer := Label.new()
    footer.text = "Drag to orbit · Wheel to zoom · Three detachable modules · 64px point-filtered textures · Preview only"
    footer.position = Vector2(24,size.y-38)
    add_child(footer)
    update_camera()

func restyle(node: Node) -> void:
    if node is MeshInstance3D:
        for surface in node.mesh.get_surface_count():
            var original = node.get_active_material(surface)
            if original is StandardMaterial3D:
                var mat := ShaderMaterial.new()
                mat.shader = load("res://experiments/ps1_pawn/ps1_material.gdshader")
                if original.albedo_texture:
                    mat.set_shader_parameter("albedo_tex",original.albedo_texture)
                else:
                    var img := Image.create(1,1,false,Image.FORMAT_RGBA8)
                    img.fill(original.albedo_color)
                    mat.set_shader_parameter("albedo_tex",ImageTexture.create_from_image(img))
                mat.set_shader_parameter("vertex_snap",true)
                materials.append(mat)
                node.set_surface_override_material(surface,mat)
    for child in node.get_children(): restyle(child)

func button(parent: Node, text: String, action: Callable) -> void:
    var b := Button.new()
    b.text = text
    b.pressed.connect(action)
    parent.add_child(b)

func apply_explode() -> void:
    pawn.get_node("Body").position.y = 0.23 if exploded else 0.0
    pawn.get_node("Head").position.y = 1.61 if exploded else 0.91
    update_camera()

func update_camera() -> void:
    var center := Vector3(0,1.05 if exploded else 0.87,0)
    camera.position = center + Vector3(sin(yaw)*cos(pitch),sin(pitch),cos(yaw)*cos(pitch))*(zoom+0.7 if exploded else zoom)
    camera.look_at(center)

func _process(delta: float) -> void:
    if auto_orbit:
        yaw += delta * 0.4
        update_camera()
    if "--capture-pawn" in OS.get_cmdline_user_args():
        capture_frames += 1
        if capture_frames == 20:
            await RenderingServer.frame_post_draw
            get_viewport().get_texture().get_image().save_png("res://artifacts/asset_harness/ps1_pawn/comparison.png")
            exploded = true
            apply_explode()
        if capture_frames == 35:
            await RenderingServer.frame_post_draw
            get_viewport().get_texture().get_image().save_png("res://artifacts/asset_harness/ps1_pawn/exploded.png")
            get_tree().quit()

func _unhandled_input(event: InputEvent) -> void:
    if event is InputEventMouseMotion and event.button_mask & (MOUSE_BUTTON_MASK_LEFT | MOUSE_BUTTON_MASK_RIGHT):
        yaw -= event.relative.x * 0.007
        pitch = clampf(pitch+event.relative.y*0.005,-0.25,1.3)
        update_camera()
    if event is InputEventMouseButton and event.pressed:
        if event.button_index == MOUSE_BUTTON_WHEEL_UP: zoom = maxf(2.4,zoom*0.9)
        if event.button_index == MOUSE_BUTTON_WHEEL_DOWN: zoom = minf(6,zoom*1.1)
        update_camera()
