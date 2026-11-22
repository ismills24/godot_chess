extends Node3D

const AlleyWorld := preload("res://menu/alley_world.gd")
const MenuSign := preload("res://menu/menu_sign.gd")
const GAME_SCENE := "res://sandbox/Sandbox.tscn"
const SETTINGS_FILE := "user://menu_settings.cfg"
const GAME_ARGUMENTS := ["scenario", "encounter", "seed", "white", "black"]
const MENU_FPS_CAP := 60
const PIXEL_LONG_EDGE := 704

@onready var _stage: SubViewport = $Stage
@onready var _camera: Camera3D = $Stage/Camera
@onready var _alley: AlleyWorld = $Stage/Alley
@onready var _sign: MenuSign = $Stage/MenuSign

var _config := ConfigFile.new()
var _settings_open := false
var _reduced_motion := false
var _selected := 0
var _pressed := -1
var _previous_fps_cap := 0
var _base_fov := 46.0


func _ready() -> void:
	_previous_fps_cap = Engine.max_fps
	if _has_game_argument():
		_open_game.call_deferred()
		return

	if _previous_fps_cap == 0 or _previous_fps_cap > MENU_FPS_CAP:
		Engine.max_fps = MENU_FPS_CAP
	($Display.material as ShaderMaterial).set_shader_parameter("stage_texture", _stage.get_texture())
	_base_fov = _camera.fov
	_alley.build($Stage/MistBacklight.position)
	_sign.build()
	var load_error := _config.load(SETTINGS_FILE)
	if load_error != OK and load_error != ERR_FILE_NOT_FOUND:
		push_warning("Could not load menu settings: " + error_string(load_error))
		_config.clear()
	if _config.has_section_key("display", "fullscreen"):
		_apply_fullscreen(_config.get_value("display", "fullscreen") == true)
	_reduced_motion = _config.get_value("display", "reduced_motion", false) == true
	_alley.set_reduced_motion(_reduced_motion)
	_sign.set_reduced_motion(_reduced_motion)
	get_viewport().size_changed.connect(_frame_alley)
	_frame_alley()
	_show_home()


func _exit_tree() -> void:
	Engine.max_fps = _previous_fps_cap
	Input.set_default_cursor_shape(Input.CURSOR_ARROW)


func _has_game_argument() -> bool:
	for argument in OS.get_cmdline_user_args():
		var fields := argument.split("=", true, 1)
		if fields.size() == 2 and fields[0].lstrip("-").to_lower() in GAME_ARGUMENTS:
			return true
	return false


func _frame_alley() -> void:
	# Keep both the sign and chess table in frame in narrow windows.
	var viewport_size := get_viewport().get_visible_rect().size
	var aspect := viewport_size.x / maxf(viewport_size.y, 1.0)
	var scale_to_pixels := PIXEL_LONG_EDGE / maxf(maxf(viewport_size.x, viewport_size.y), 1.0)
	_stage.size = Vector2i((viewport_size * scale_to_pixels).round()).max(Vector2i(2, 2))
	_camera.fov = maxf(_base_fov, rad_to_deg(2.0 * atan(0.75 / maxf(aspect, 0.5))))


func _pick_plaque(screen_at: Vector2) -> int:
	var screen := get_viewport().get_visible_rect().size
	return _sign.pick(_camera, screen_at * Vector2(_stage.size) / screen.max(Vector2.ONE))


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		var hit := _pick_plaque(event.position)
		Input.set_default_cursor_shape(Input.CURSOR_POINTING_HAND if hit >= 0 else Input.CURSOR_ARROW)
		if hit >= 0 and hit != _selected:
			_select(hit)
		return

	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		var hit := _pick_plaque(event.position)
		if event.pressed:
			_pressed = hit
			if hit >= 0:
				_select(hit)
		else:
			if hit >= 0 and _pressed == hit:
				_activate(hit)
			_pressed = -1
		return

	var count := 3 if _settings_open else 2
	if event.is_action_pressed("ui_cancel") and _settings_open:
		_show_home(1)
	elif event.is_action_pressed("ui_up", true) or event.is_action_pressed("ui_focus_prev", true):
		_select(posmod(_selected - 1, count))
	elif event.is_action_pressed("ui_down", true) or event.is_action_pressed("ui_focus_next", true):
		_select(posmod(_selected + 1, count))
	elif event.is_action_pressed("ui_accept") or event.is_action_pressed("ui_select"):
		_activate(_selected)
	else:
		return
	get_viewport().set_input_as_handled()


func _select(index: int) -> void:
	_selected = index
	_sign.show_focus(index)


func _activate(index: int) -> void:
	if not _settings_open:
		if index == 0:
			_open_game()
		else:
			_show_settings(0)
		return
	match index:
		0:
			var enabled := not _is_fullscreen()
			_apply_fullscreen(enabled)
			_save_setting("fullscreen", enabled)
			_show_settings(0)
		1:
			_reduced_motion = not _reduced_motion
			_alley.set_reduced_motion(_reduced_motion)
			_sign.set_reduced_motion(_reduced_motion)
			_save_setting("reduced_motion", _reduced_motion)
			_show_settings(1)
		2:
			_show_home(1)


func _show_home(focus := 0) -> void:
	_settings_open = false
	_pressed = -1
	_sign.set_entries(["Play", "Settings"], [])
	_select(focus)


func _show_settings(focus: int) -> void:
	_settings_open = true
	_pressed = -1
	var fullscreen := "On" if _is_fullscreen() else "Off"
	var motion := "On" if _reduced_motion else "Off"
	_sign.set_entries(["Fullscreen", "Reduced motion", "Back"], [fullscreen, motion])
	_select(focus)


func _open_game() -> void:
	var error := get_tree().change_scene_to_file(GAME_SCENE)
	if error != OK:
		_sign.set_entries(["Retry Play", "Settings"], [])
		_select(0)
		push_error("Could not open the chess scene: " + error_string(error))


func _is_fullscreen() -> bool:
	var mode := get_window().mode
	return mode == Window.MODE_FULLSCREEN or mode == Window.MODE_EXCLUSIVE_FULLSCREEN


func _apply_fullscreen(enabled: bool) -> void:
	get_window().mode = Window.MODE_FULLSCREEN if enabled else Window.MODE_WINDOWED


func _save_setting(key: String, enabled: bool) -> void:
	_config.set_value("display", key, enabled)
	var error := _config.save(SETTINGS_FILE)
	if error != OK:
		push_warning("Could not save menu settings: " + error_string(error))
