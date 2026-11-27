extends Node3D
## One scene where you walk up to someone and play them at chess. It works the same way as the
## inn: you walk in on foot, sit down across the board from them, they look up at you, you
## look down at the board and lean in over it to play, your White against their Black.
## Rendered low-res through the menu's pixel finish.
##
## The world script (Stage/World, extends place_world.gd) builds the place and says where
## everything is: the path walked in, what you look at before setting off, where your eyes
## are once seated, the board and the opponent sitting at it.

const ChessSet := preload("res://inn/chess_set.gd")
const InnMatch := preload("res://inn/InnMatch.cs")
const SharpLayer := preload("res://common/sharp_layer.gd")
const MENU_SCENE := "res://menu/MainMenu.tscn"
const SETTINGS_FILE := "user://menu_settings.cfg"
const FPS_CAP := 60
const PIXEL_LONG_EDGE := 704
const WALK_START_DELAY := 0.6
const WALK_SPEED := 1.25
const STRIDE := 0.72
## The eyes lead the feet: they look at the point this far ahead along the path, slightly
## down at the ground.
const LOOK_AHEAD := 1.4
const LOOK_DROP := 0.22
## Setting off, the eyes leave what they were looking at over this distance.
const LEAVE_FIRST_LOOK := 1.8
## The opponent looks up at you when you're this far from your seat.
const NOTICED_AT := 3.2
## At the end of the path: sit down, looking at the opponent; hold their eye, they nod at the
## board, you look down at it and lean in.
const SIT_SECONDS := 1.5
const FACE_HOLD_SECONDS := 0.9
const TO_BOARD_SECONDS := 0.9
const PLAY_ZOOM := 0.86
const LEAN_AFTER := 0.25
const LEAN_SECONDS := 1.1
const PLAY_PITCH := 65.0
const PLAY_DISTANCE := 0.9
const LEAN_ZOOM := 0.72
const FOLLOW_RATE := 5.0
const AUTOPLAY_ARG := "--inn-autoplay"
const REACH_SECONDS := 0.32

## Start seated at the board, leaned in to play (previews, tools).
@export var skip_intro := false

@onready var _stage: SubViewport = $Stage
@onready var _camera: Camera3D = $Stage/Camera
@onready var _world: Node3D = $Stage/World

var _previous_fps_cap := 0
var _base_fov := 50.0
var _zoom := 1.0
var _reduced_motion := false
var _walk: Curve3D
var _watching := false
var _noticed := false
var _gaze := Vector3.ZERO
var _match: Node
var _board: Node3D
var _hud: CanvasLayer
var _status: Label
var _chosen := -1
var _reach: Array = []
var _threat: Array = []
var _last: Array = []


func _ready() -> void:
	_previous_fps_cap = Engine.max_fps
	if _previous_fps_cap == 0 or _previous_fps_cap > FPS_CAP:
		Engine.max_fps = FPS_CAP
	($Display.material as ShaderMaterial).set_shader_parameter("stage_texture", _stage.get_texture())
	_base_fov = _camera.fov
	var config := ConfigFile.new()
	if config.load(SETTINGS_FILE) == OK:
		_reduced_motion = config.get_value("display", "reduced_motion", false) == true
	_world.build()
	_world.set_reduced_motion(_reduced_motion)
	_board = _world.board
	_build_walk()
	_camera.transform = _walk_shot(0.0)
	get_viewport().size_changed.connect(_frame_stage)
	_frame_stage()
	if _reduced_motion or skip_intro:
		_zoom = LEAN_ZOOM
		_camera.transform = _lean_shot()
		_frame_stage()
		_begin_play()
	else:
		get_tree().create_timer(WALK_START_DELAY).timeout.connect(_walk_in)


func _process(delta: float) -> void:
	if not _noticed and _walk and _camera.position.distance_to(_walk_end() + Vector3(0, _world.eye_height, 0)) < NOTICED_AT:
		_noticed = true
		_world.opponent.look_at_target(_camera)
	if _watching:
		_gaze = _gaze.lerp(_world.opponent.head_position(), 1.0 - exp(-FOLLOW_RATE * delta))
		_camera.transform = _shot(_world.seat_eye, _gaze)


func _exit_tree() -> void:
	Engine.max_fps = _previous_fps_cap


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		get_tree().change_scene_to_file(MENU_SCENE)
		return
	if not _match:
		return
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		get_viewport().set_input_as_handled()
		_click(_square_under(event.position))
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_RIGHT:
		_chosen = -1
		_refresh_marks()
	elif event is InputEventMouseMotion:
		var square := _square_under(event.position)
		var live: bool = square >= 0 and (square in _reach or _match.IsMine(square))
		Input.set_default_cursor_shape(Input.CURSOR_POINTING_HAND if live else Input.CURSOR_ARROW)


func _frame_stage() -> void:
	var viewport_size := get_viewport().get_visible_rect().size
	var aspect := viewport_size.x / maxf(viewport_size.y, 1.0)
	var scale_to_pixels := PIXEL_LONG_EDGE / maxf(maxf(viewport_size.x, viewport_size.y), 1.0)
	_stage.size = Vector2i((viewport_size * scale_to_pixels).round()).max(Vector2i(2, 2))
	_camera.fov = maxf(_base_fov, rad_to_deg(2.0 * atan(0.75 / maxf(aspect, 0.5)))) * _zoom


func _walk_end() -> Vector3:
	return _walk.sample_baked(_walk.get_baked_length())


func _build_walk() -> void:
	_walk = Curve3D.new()
	_walk.bake_interval = 0.05
	var points: Array[Vector3] = []
	for point: Vector2 in _world.walk_path:
		points.append(Vector3(point.x, _world.ground_at(point.x, point.y), point.y))
	for k in points.size():
		var before := points[maxi(k - 1, 0)]
		var after := points[mini(k + 1, points.size() - 1)]
		var handle := (after - before) * 0.22
		_walk.add_point(points[k], -handle, handle)


func _walk_in() -> void:
	var length := _walk.get_baked_length()
	var tween := create_tween()
	tween.tween_method(func(d: float) -> void: _camera.transform = _walk_shot(d),
		0.0, length, length / WALK_SPEED).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	# Sit down, coming round to face the opponent, with a little drop onto the seat.
	tween.tween_method(func(t: float) -> void:
		var shot := _walk_shot(length).interpolate_with(_shot(_world.seat_eye, _world.opponent.head_position()), t)
		shot.origin.y -= 0.1 * sin(t * PI)
		_camera.transform = shot,
		0.0, 1.0, SIT_SECONDS).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN_OUT)
	tween.tween_callback(func() -> void:
		_gaze = _world.opponent.head_position()
		_watching = true
		_world.opponent.nod())
	tween.tween_interval(FACE_HOLD_SECONDS)
	tween.tween_callback(func() -> void: _watching = false)
	var play_at := _board_centre() + Vector3(0, 0.02, 0)
	tween.tween_method(func(t: float) -> void:
		_camera.transform = _shot(_world.seat_eye, _gaze).interpolate_with(_shot(_world.seat_eye, play_at), t)
		_set_zoom(lerpf(1.0, PLAY_ZOOM, t)),
		0.0, 1.0, TO_BOARD_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_interval(LEAN_AFTER)
	tween.tween_callback(func() -> void: _world.opponent.settle())
	tween.tween_method(func(t: float) -> void:
		_camera.transform = _shot(_world.seat_eye, play_at).interpolate_with(_lean_shot(), t)
		_set_zoom(lerpf(PLAY_ZOOM, LEAN_ZOOM, t)),
		0.0, 1.0, LEAN_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_callback(_begin_play)


func _begin_play() -> void:
	_light_board()
	# The board and pieces in play, drawn at full resolution over the pixel stage; the
	# opponent's arm still shows over them when it reaches across.
	var sharp := SharpLayer.new()
	sharp.setup(_stage, _camera, $Display)
	add_child(sharp)
	sharp.punch_through(_world.opponent, _board)
	sharp.reveal(_board)
	_match = InnMatch.new()
	add_child(_match)
	_match.connect("Moved", _on_moved)
	_match.connect("Thinking", func() -> void: _say(_world.opponent_name + " considers the board..."))
	_match.connect("Finished", _on_finished)
	_match.Begin(AUTOPLAY_ARG in OS.get_cmdline_user_args())
	_board.show_pieces(_match.Pieces(), false)
	_build_hud()
	_say("Your move.")


func _on_moved(from: int, to: int, by_opponent: bool) -> void:
	if by_opponent:
		# Their piece moves as their hand gets to it.
		_world.opponent.reach(_board.to_global(ChessSet.square_center(from % 8, from / 8)))
		await get_tree().create_timer(REACH_SECONDS).timeout
	_board.show_pieces(_match.Pieces())
	_last = [from, to]
	_chosen = -1
	_refresh_marks()
	var check: int = _match.CheckedKing()
	if _match.IsPlayerTurn():
		_say("Check. Your move." if check >= 0 else "Your move.")


func _on_finished(winner: String) -> void:
	var line: String = {
		"player": "Checkmate. " + _world.opponent_name + " tips their king to you.",
		"bartender": "Checkmate. " + _world.opponent_name + " wins.",
	}.get(winner, "A draw.")
	_say(line + "   Esc to leave")


func _click(square: int) -> void:
	if not _match.IsPlayerTurn():
		return
	if _chosen >= 0 and square in _reach:
		var from := _chosen
		_chosen = -1
		_match.TryMove(from, square)
		return
	if square >= 0 and square != _chosen and _match.IsMine(square):
		_chosen = square
		_reach = Array(_match.Targets(square))
		_threat = _reach.filter(func(to: int) -> bool: return _match.IsCapture(square, to))
	else:
		_chosen = -1
	_refresh_marks()


func _refresh_marks() -> void:
	if _chosen < 0:
		_reach = []
		_threat = []
	_board.mark(_chosen, _reach, _threat, _last, _match.CheckedKing())


func _square_under(point: Vector2) -> int:
	var window := get_viewport().get_visible_rect().size
	var on_stage := point / window * Vector2(_stage.size)
	var origin := _camera.project_ray_origin(on_stage)
	var along := _camera.project_ray_normal(on_stage)
	var surface := _board.global_position.y + ChessSet.SURFACE
	if absf(along.y) < 0.0001:
		return -1
	var hit := origin + along * ((surface - origin.y) / along.y)
	var square: Vector2i = ChessSet.square_at(_board.to_local(hit))
	return -1 if square.x < 0 else square.y * 8 + square.x


## A soft light over the board while you play, in the place's own colour.
func _light_board() -> void:
	var light := OmniLight3D.new()
	light.light_color = _world.board_light_color
	light.omni_range = 1.1
	light.omni_attenuation = 1.4
	light.shadow_enabled = false
	light.position = _board_centre() + _toward_player() * 0.25 + Vector3(0, 0.55, 0)
	light.light_energy = 0.0
	_world.add_child(light)
	create_tween().tween_property(light, "light_energy", _world.board_light_energy, 0.8)


func _build_hud() -> void:
	_hud = CanvasLayer.new()
	add_child(_hud)
	var font := SystemFont.new()
	font.font_names = PackedStringArray(["Georgia", "Liberation Serif", "Noto Serif", "DejaVu Serif"])
	font.font_weight = 700
	_status = Label.new()
	_status.add_theme_font_override("font", font)
	_status.add_theme_font_size_override("font_size", 26)
	_status.add_theme_color_override("font_color", Color("f0d6a0"))
	_status.add_theme_color_override("font_outline_color", Color("140c08"))
	_status.add_theme_constant_override("outline_size", 8)
	_status.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_status.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	_status.offset_left = -500
	_status.offset_right = 500
	_status.offset_top = -64
	_status.offset_bottom = -28
	_status.modulate.a = 0.0
	_hud.add_child(_status)
	create_tween().tween_property(_status, "modulate:a", 1.0, 0.6)


func _say(text: String) -> void:
	if _status:
		_status.text = text


func _set_zoom(zoom: float) -> void:
	_zoom = zoom
	_frame_stage()


func _board_centre() -> Vector3:
	return _board.global_position + Vector3(0, ChessSet.SURFACE, 0)


## Across the board toward your side (the board's light side, its local +Z), level.
func _toward_player() -> Vector3:
	var along: Vector3 = _board.global_basis.z
	along.y = 0
	return along.normalized()


func _walk_shot(distance: float) -> Transform3D:
	var length := _walk.get_baked_length()
	var foot := _walk.sample_baked(clampf(distance, 0.0, length))
	var ahead := _walk.sample_baked(clampf(distance + LOOK_AHEAD, 0.0, length))
	if distance + LOOK_AHEAD > length:
		var along := (_walk.sample_baked(length) - _walk.sample_baked(length - 0.2)).normalized()
		ahead += along * (distance + LOOK_AHEAD - length)
	var stepping := smoothstep(0.0, 0.6, distance) * smoothstep(0.0, 0.5, length - distance)
	var phase := distance / STRIDE * PI
	var bob := Vector3(0.012 * sin(phase), 0.022 * absf(sin(phase)) - 0.011, 0) * stepping
	var height: float = _world.eye_height
	var eye := foot + Vector3(0, height, 0)
	var look := ahead + Vector3(0, height - LOOK_DROP, 0)
	look = _world.first_look.lerp(look, smoothstep(0.0, LEAVE_FIRST_LOOK, distance))
	var shot := _shot(eye, look)
	shot.origin += shot.basis * bob
	return shot


func _lean_shot() -> Transform3D:
	var pitch := deg_to_rad(PLAY_PITCH)
	var centre := _board_centre()
	var eye := centre + _toward_player() * cos(pitch) * PLAY_DISTANCE + Vector3(0, sin(pitch) * PLAY_DISTANCE, 0)
	return _shot(eye, centre)


static func _shot(from: Vector3, at: Vector3) -> Transform3D:
	return Transform3D(Basis.IDENTITY, from).looking_at(at, Vector3.UP)
