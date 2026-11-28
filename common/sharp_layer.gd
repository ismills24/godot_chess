extends Node
## Draws one render layer (the board and pieces in play) at full window resolution over a scene's pixel
## stage, so they keep their detail while the room keeps its low-res look. The sharp view
## shares the stage's world, lights and camera; the stage camera stops drawing the layer, but
## its lights still do, so the pieces go on casting shadows on the board. The overlay is
## graded like menu/pixel_finish.gdshader (split tone and vignette), without the pixels.
##
## Use: var sharp := SharpLayer.new(); sharp.setup($Stage, $Stage/Camera, $Display); add_child(sharp)
## then sharp.reveal(board) once nothing stands between the camera and the board: the overlay
## draws over everything in the stage, so it is for when the board is in clear view. Anything
## that comes between the camera and the board later (an opponent reaching for a piece) is
## handed to punch_through(), and the stage shows through the overlay wherever it lies in front.

## Visual layer 11.
const LAYER := 1 << 10
const GRADE := preload("res://common/sharp_layer.gdshader")
const PUNCH := preload("res://common/punch_through.gdshader")

var _stage_camera: Camera3D
var _view: SubViewport
var _camera: Camera3D
var _overlay: TextureRect


func setup(stage: SubViewport, stage_camera: Camera3D, display: CanvasItem) -> void:
	_stage_camera = stage_camera
	stage_camera.cull_mask &= ~LAYER
	_view = SubViewport.new()
	_view.world_3d = stage.find_world_3d()
	_view.transparent_bg = true
	_view.msaa_3d = Viewport.MSAA_4X
	# SubViewports default to a 2048 shadow atlas whatever project.godot says; match the stage.
	_view.positional_shadow_atlas_size = stage.positional_shadow_atlas_size
	_view.gui_disable_input = true
	_view.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_view)
	_camera = Camera3D.new()
	_camera.cull_mask = LAYER
	_camera.current = true
	_view.add_child(_camera)
	_overlay = TextureRect.new()
	_overlay.texture = _view.get_texture()
	_overlay.set_anchors_preset(Control.PRESET_FULL_RECT)
	_overlay.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_overlay.stretch_mode = TextureRect.STRETCH_SCALE
	_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var grade := ShaderMaterial.new()
	grade.shader = GRADE
	var finish := display.material as ShaderMaterial
	if finish:
		for parameter: String in ["vignette", "shadow_tint", "highlight_tint"]:
			var value: Variant = finish.get_shader_parameter(parameter)
			if value != null:
				grade.set_shader_parameter(parameter, value)
	_overlay.material = grade
	display.add_sibling(_overlay)
	# A camera's cull mask filters lights by their layers too: without this the sharp view
	# draws the pieces unlit. (Lights added to the stage later aren't picked up.)
	for light in stage.find_children("*", "Light3D", true, false):
		(light as Light3D).layers |= LAYER
	# After the director has moved the camera this frame.
	process_priority = 1000


## Fades the chess set's board and pieces in sharp over their pixel versions, then takes them
## out of the stage.
func reveal(chess_set: Node3D, seconds := 0.5) -> void:
	chess_set.set_play_layers(1 | LAYER)
	_overlay.modulate.a = 0.0
	var tween := create_tween()
	tween.tween_property(_overlay, "modulate:a", 1.0, seconds)
	tween.tween_callback(func() -> void: chess_set.set_play_layers(LAYER))


## Lets every mesh under `node` (built before this call) cut through the sharp overlay where
## it is nearer the camera than the board, so a hand over the board isn't drawn beneath it.
## Meshes under `chess_set` (when the board is carried by the opponent) are left alone.
func punch_through(node: Node3D, chess_set: Node = null) -> void:
	var material := ShaderMaterial.new()
	material.shader = PUNCH
	for found in node.find_children("*", "MeshInstance3D", true, false):
		var mesh := found as MeshInstance3D
		if chess_set and (mesh == chess_set or chess_set.is_ancestor_of(mesh)):
			continue
		var hole := MeshInstance3D.new()
		hole.mesh = mesh.mesh
		hole.material_override = material
		hole.layers = LAYER
		hole.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mesh.add_child(hole)


func _ready() -> void:
	get_viewport().size_changed.connect(_fit)
	_fit()


func _fit() -> void:
	_view.size = Vector2i(get_viewport().get_visible_rect().size).max(Vector2i(2, 2))


func _process(_delta: float) -> void:
	_camera.global_transform = _stage_camera.global_transform
	_camera.fov = _stage_camera.fov
	_camera.near = _stage_camera.near
	_camera.far = _stage_camera.far
