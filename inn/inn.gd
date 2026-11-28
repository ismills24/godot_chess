extends Node3D
## The inn: you stand in the rainy street under THE HANGING PAWN's sign, walk in through the
## door and down the lane between the long table and the bar, and take a stool at the bar. The
## bartender brings the board along behind the counter and sets it down in front of you; you
## watch him do it, hold his eye a moment, then look down at the board and lean in over it to
## play him: a game of chess on the counter, your White against his Black. Crossing the threshold, the street's cold night
## gives way to the bar room's warm haze. Rendered at full resolution, graded like the menu's pixel finish.

const InnWorld := preload("res://inn/inn_world.gd")
const Bartender := preload("res://inn/bartender.gd")
const ChessSet := preload("res://inn/chess_set.gd")
const InnMatch := preload("res://inn/InnMatch.cs")
const SharpLayer := preload("res://common/sharp_layer.gd")
const MENU_SCENE := "res://menu/MainMenu.tscn"
const SETTINGS_FILE := "user://menu_settings.cfg"
const FPS_CAP := 60
## The walk in, on the floor (x, z): across the street to the door, inside, down the lane
## between the long table and the bar, to the stool.
const WALK_PATH: Array[Vector2] = [
	Vector2(1.7, 7.4), Vector2(1.1, 4.6), Vector2(0.65, 2.5), Vector2(0.6, 1.3), Vector2(0.6, 0.5),
	Vector2(0.6, -1.4), Vector2(0.6, -3.1), Vector2(0.6, -3.75), Vector2(0.62, -4.2),
]
## A moment in the street taking in the sign before setting off.
const WALK_START_DELAY := 0.6
## Standing in the street, the eyes are on the sign over the door.
const SIGN_LOOK := Vector3(0.6, 2.3, 1.6)
## The street's night (the menu alley's) and the threshold over which it fades to the room's.
const NIGHT_FOG := Color(0.106, 0.243, 0.275)
const NIGHT_FOG_DENSITY := 0.068
const NIGHT_AMBIENT := Color(0.36, 0.55, 0.64)
const NIGHT_AMBIENT_ENERGY := 0.21
const NIGHT_BACKGROUND := Color(0.03, 0.06, 0.07)
const THRESHOLD := Vector2(0.7, 2.2)
const WALK_SPEED := 1.25
## At the end of the path, beside the stool: look down at it, pull it back out from under the
## counter, then sit, looking up at the bartender's back; he turns round with the board this
## long after you've sat down.
const LOOK_AT_STOOL_SECONDS := 1.0
const PULL_SECONDS := 1.15
const SIT_SECONDS := 1.5
const SERVE_AFTER := 0.3
const STOOL_SEAT := 0.76
const EYE_HEIGHT := 1.64
const STRIDE := 0.72
## The eyes lead the feet: they look at the point this far ahead along the path, so the
## head turns into each bend a step before the body does, and slightly down at the floor.
const LOOK_AHEAD := 1.4
const LOOK_DROP := 0.22
## Setting off, the eyes leave the sign over this distance.
const LEAVE_SIGN := 1.6
## Seated, you watch the bartender bring the board; once it's down you hold his eye a beat,
## then look down at the board.
const FACE_HOLD_SECONDS := 0.35
const TO_BOARD_SECONDS := 0.9
## How quickly the eyes catch up with him as he moves.
const FOLLOW_RATE := 5.0
const PLAY_FROM := Vector3(InnWorld.BAR_STOOL_AT.x - 0.2, 1.62, InnWorld.BAR_STOOL_AT.z)
const PLAY_AT := Vector3(InnWorld.BAR_BOARD_AT.x, 1.4, InnWorld.BAR_BOARD_AT.z)
## Looking down at the board, the view closes in on it.
const PLAY_ZOOM := 0.86
## Then you lean in over the board to play, looking down on it so every square reads.
const LEAN_AFTER := 0.25
const LEAN_SECONDS := 1.1
## The play view looks down on the board's centre from PLAY_PITCH degrees (90 straight
## down, 0 level), PLAY_DISTANCE away, close enough that the board fills most of the screen.
const PLAY_PITCH := 65.0
const PLAY_DISTANCE := 0.9
const BOARD_CENTRE := Vector3(InnWorld.BAR_BOARD_AT.x - 0.015, InnWorld.BAR_BOARD_AT.y + 0.03, InnWorld.BAR_BOARD_AT.z)
const LEAN_ZOOM := 0.72
## User arg (after "--") that gives your side to an AI too, for unattended test renders.
const AUTOPLAY_ARG := "--inn-autoplay"
## How long the bartender's hand takes to get to his piece.
const REACH_SECONDS := 0.28
## He drinks whenever the beer-spill board effect fires: a pull from his mug, then he slams it
## down and beer slops out of it onto the squares that turned Slippery. He starts this long after
## a move (his hand back from the board after his own, a beat after yours).
const DRINK_AFTER_HIS_MOVE := 1.0
const DRINK_AFTER_YOUR_MOVE := 0.5
## The lip of his mug, above where it stands (inn_world.gd builds it at size 2: 0.26 tall).
const MUG_LIP := Vector3(0, 0.27, 0)
## The tile id the beer spill leaves.
const BEER_TILE := "slippery"

@export var skip_intro := false

@onready var _stage: SubViewport = $Stage
@onready var _camera: Camera3D = $Stage/Camera
@onready var _world: InnWorld = $Stage/World
@onready var _bartender: Bartender = $Stage/Bartender

var _previous_fps_cap := 0
var _base_fov := 50.0
## Narrows the view for the play shot: 1 while walking in, PLAY_ZOOM once on the board.
var _zoom := 1.0
var _reduced_motion := false
var _walk: Curve3D
## While seated and watching the bartender: where the eyes are on him.
var _watching := false
## Whether the regulars at the bar have looked round at you coming in.
var _noticed := false
## Whether the bartender has looked round at you yet, and where along the lane he does.
var _bartender_noticed := false
const BARTENDER_GLANCE_Z := -0.3
var _gaze := Vector3.ZERO
var _environment: Environment
var _room_fog: Color
var _room_fog_density: float
var _room_fog_height_density: float
var _room_ambient: Color
var _room_ambient_energy: float
var _room_background: Color
## The game on the counter, once you've leaned in to play.
var _match: Node
var _board: Node3D
var _hud: CanvasLayer
var _status: Label
## What's under the cursor (the piece and its abilities, a special square).
var _hint: Label
## Whether you're seated and the game takes input (the match exists from the start, so the
## board he serves already has the game's size and armies on it).
var _playing := false
var _undo_button: Button
var _leave_button: Button
## Your picked-up piece's square, where it can go and which of those are captures; the
## last move's squares.
var _chosen := -1
var _reach: Array = []
var _threat: Array = []
var _last: Array = []
## Slippery squares the game already has that he hasn't poured out of his mug yet.
var _pending_beer: Array = []
var _drinking := false


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
	_bartender.build(InnWorld.BARTENDER_POST, -PI * 0.5, InnWorld.BAR_BOARD_AT, InnWorld.BOARD_SHELF_Y)
	# The game decides the board's size and what stands on it before he brings it over.
	_board = _bartender.board()
	_match = InnMatch.new()
	add_child(_match)
	_match.Begin(false)
	_board.resize(_match.Width, _match.Height)
	_board.show_pieces(_match.Pieces(), false)
	_show_tiles()
	_world.set_reduced_motion(_reduced_motion)
	_bartender.set_reduced_motion(_reduced_motion)
	_build_walk()
	# A private copy: the threshold blend below rewrites it every frame.
	var world_environment := $Stage/WorldEnvironment as WorldEnvironment
	_environment = world_environment.environment.duplicate()
	world_environment.environment = _environment
	_room_fog = _environment.fog_light_color
	_room_fog_density = _environment.fog_density
	_room_fog_height_density = _environment.fog_height_density
	_room_ambient = _environment.ambient_light_color
	_room_ambient_energy = _environment.ambient_light_energy
	_room_background = _environment.background_color
	_camera.transform = _walk_shot(0.0)
	get_viewport().size_changed.connect(_frame_stage)
	_frame_stage()
	if _reduced_motion or skip_intro:
		_bartender.serve(true)
		_world.player_stool.position = InnWorld.BAR_STOOL_AT
		_zoom = LEAN_ZOOM
		_camera.transform = _lean_shot()
		_frame_stage()
		_bartender.fold_arms(0.0)
		_begin_play()
	else:
		_bartender.served.connect(_on_served)
		get_tree().create_timer(WALK_START_DELAY).timeout.connect(_walk_in)


func _process(delta: float) -> void:
	if not _noticed and _camera.position.z < WALK_PATH[4].y:
		_noticed = true
		_world.bar_regulars_glance(_camera)
	if not _bartender_noticed and _camera.position.z < BARTENDER_GLANCE_Z:
		_bartender_noticed = true
		_bartender.glance_at(_camera, 0.9)
	if _watching:
		# On the stool, the eyes follow the bartender as he brings the board.
		_gaze = _gaze.lerp(_bartender.head_position(), 1.0 - exp(-FOLLOW_RATE * delta))
		_camera.transform = _shot(PLAY_FROM, _gaze)
	# Out in the street the night is the menu alley's; it gives way to the bar room's haze
	# as the camera crosses the threshold.
	var night := smoothstep(THRESHOLD.x, THRESHOLD.y, _camera.position.z)
	_environment.fog_light_color = _room_fog.lerp(NIGHT_FOG, night)
	_environment.fog_density = lerpf(_room_fog_density, NIGHT_FOG_DENSITY, night)
	# Indoors the smoke gathers under the vault; the street has no ceiling to hold it.
	_environment.fog_height_density = lerpf(_room_fog_height_density, 0.0, night)
	_environment.ambient_light_color = _room_ambient.lerp(NIGHT_AMBIENT, night)
	_environment.ambient_light_energy = lerpf(_room_ambient_energy, NIGHT_AMBIENT_ENERGY, night)
	_environment.background_color = _room_background.lerp(NIGHT_BACKGROUND, night)


func _exit_tree() -> void:
	Engine.max_fps = _previous_fps_cap


func _unhandled_input(event: InputEvent) -> void:
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		if _playing:
			_match.Leave()
		elif get_parent().has_method("LeaveEncounter"):
			get_parent().LeaveEncounter()
		else:
			get_tree().change_scene_to_file(MENU_SCENE)
		return
	if not _playing:
		return
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		get_viewport().set_input_as_handled()
		_click(_square_under(event.position))
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_RIGHT:
		_chosen = -1
		_refresh_marks()
	elif event is InputEventMouseMotion:
		# A hand over anything you can pick up or move to.
		var square := _square_under(event.position)
		var live: bool = square >= 0 and (square in _reach or _match.IsMine(square))
		Input.set_default_cursor_shape(Input.CURSOR_POINTING_HAND if live else Input.CURSOR_ARROW)
		if _hint:
			_hint.text = _match.Describe(square)


func _frame_stage() -> void:
	var viewport_size := get_viewport().get_visible_rect().size
	var aspect := viewport_size.x / maxf(viewport_size.y, 1.0)
	# Full window resolution, to match the sharp board and pieces drawn over it.
	_stage.size = Vector2i(viewport_size.round()).max(Vector2i(2, 2))
	_camera.fov = maxf(_base_fov, rad_to_deg(2.0 * atan(0.75 / maxf(aspect, 0.5)))) * _zoom


func _build_walk() -> void:
	# A smooth floor path through the waypoints (Catmull-Rom handles), ending at the seat.
	_walk = Curve3D.new()
	_walk.bake_interval = 0.05
	var points: Array[Vector3] = []
	for point in WALK_PATH:
		points.append(Vector3(point.x, 0, point.y))
	for k in points.size():
		var before := points[maxi(k - 1, 0)]
		var after := points[mini(k + 1, points.size() - 1)]
		var handle := (after - before) * 0.22
		_walk.add_point(points[k], -handle, handle)


func _walk_in() -> void:
	var length := _walk.get_baked_length()
	var tween := create_tween()
	# Walk at a steady pace with a gentle start.
	tween.tween_method(func(d: float) -> void: _camera.transform = _walk_shot(d),
		0.0, length, length / WALK_SPEED).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	# Beside the stool: look down at it where it's tucked under the counter.
	var standing := _walk_shot(length)
	var eye := standing.origin
	var stool := _world.player_stool
	# Eyes on the stool, not straight down at the floor: aim just past its seat.
	var seat := func() -> Vector3: return stool.position + Vector3(0.12, STOOL_SEAT + 0.04, 0)
	var at_stool := _shot(eye, seat.call())
	tween.tween_method(func(t: float) -> void: _camera.transform = standing.interpolate_with(at_stool, t),
		0.0, 1.0, LOOK_AT_STOOL_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	# Pull it back out, leaning in to grab it, eyes on it as it comes.
	var reach := eye + Vector3(0.12, -0.12, 0)
	tween.tween_property(stool, "position", InnWorld.BAR_STOOL_AT, PULL_SECONDS).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.parallel().tween_method(func(t: float) -> void:
		_camera.transform = _shot(eye.lerp(reach, sin(t * PI)), seat.call()),
		0.0, 1.0, PULL_SECONDS)
	# Sit, coming up to face the bar and the bartender's back.
	tween.tween_method(func(t: float) -> void:
		var from := _shot(eye, seat.call())
		var to := _shot(PLAY_FROM, _bartender.head_position())
		var shot := from.interpolate_with(to, t)
		# Drop onto the stool rather than glide: a little dip mid-way.
		shot.origin.y -= 0.12 * sin(t * PI)
		_camera.transform = shot,
		0.0, 1.0, SIT_SECONDS).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN_OUT)
	tween.tween_callback(func() -> void:
		_gaze = _bartender.head_position()
		_watching = true)
	# A moment watching his back, then he turns round with the board.
	tween.tween_interval(SERVE_AFTER)
	tween.tween_callback(_bartender.serve)


## Once the board is down: hold his eye a beat, then look down at the board.
func _on_served() -> void:
	var tween := create_tween()
	tween.tween_interval(FACE_HOLD_SECONDS)
	tween.tween_callback(func() -> void: _watching = false)
	tween.tween_method(func(t: float) -> void:
		_camera.transform = _shot(PLAY_FROM, _gaze).interpolate_with(_shot(PLAY_FROM, PLAY_AT), t)
		_set_zoom(lerpf(1.0, PLAY_ZOOM, t)),
		0.0, 1.0, TO_BOARD_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	# Lean in over the board, and the game begins. He stands back and folds his arms.
	tween.tween_interval(LEAN_AFTER)
	tween.tween_callback(_bartender.fold_arms)
	tween.tween_method(func(t: float) -> void:
		_camera.transform = _shot(PLAY_FROM, PLAY_AT).interpolate_with(_lean_shot(), t)
		_set_zoom(lerpf(PLAY_ZOOM, LEAN_ZOOM, t)),
		0.0, 1.0, LEAN_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_callback(_begin_play)


## Starts the game on the served board: your White, the bartender's Black.
func _begin_play() -> void:
	_light_board()
	# The board and pieces in play, drawn at full resolution over the pixel stage; the
	# opponent's arm still shows over them when it reaches across.
	var sharp := SharpLayer.new()
	sharp.setup(_stage, _camera, $Display)
	add_child(sharp)
	sharp.punch_through(_bartender, _board)
	sharp.reveal(_board)
	_match.connect("Moved", _on_moved)
	_match.connect("Thinking", func() -> void:
		_say("The bartender considers his move...")
		_undo_button.disabled = true
		_leave_button.disabled = true)
	_match.connect("Finished", _on_finished)
	_match.connect("Refreshed", _on_refreshed)
	_match.connect("Failed", func(message: String) -> void:
		_on_refreshed()
		_say(message))
	if AUTOPLAY_ARG in OS.get_cmdline_user_args():
		_match.Begin(true)
	_board.show_pieces(_match.Pieces(), false)
	_playing = true
	_build_hud()
	_say("Your move.")


func _on_refreshed() -> void:
	_board.show_pieces(_match.Pieces(), false)
	_show_tiles()
	_chosen = -1
	_refresh_marks()
	_undo_button.disabled = not _match.CanUndo()
	_leave_button.disabled = false
	if _match.IsPlayerTurn():
		_say("Check. Your move." if _match.CheckedKing() >= 0 else "Your move.")


func _on_moved(from: int, to: int, by_bartender: bool) -> void:
	if by_bartender:
		# His piece moves as his hand gets to it.
		_bartender.reach()
		await get_tree().create_timer(REACH_SECONDS).timeout
	_board.show_pieces(_match.Pieces())
	_show_tiles(DRINK_AFTER_HIS_MOVE if by_bartender else DRINK_AFTER_YOUR_MOVE)
	_last = [from, to]
	_chosen = -1
	_refresh_marks()
	var check: int = _match.CheckedKing()
	if _match.IsPlayerTurn():
		_say("Check. Your move." if check >= 0 else "Your move.")


## Shows the game's special squares, except Slippery ones that just appeared during play: those
## are held back until the bartender has had his drink and slops them out of his mug, `delay`
## seconds from now.
func _show_tiles(delay := 0.0) -> void:
	var tiles: Array = _match.Tiles()
	if _playing:
		var shown: Array = _board.beer_squares()
		for entry: Dictionary in tiles:
			var square := int(entry["square"])
			if entry.get("id", "") == BEER_TILE and square not in shown and square not in _pending_beer:
				_pending_beer.append(square)
		if not _pending_beer.is_empty() and not _drinking:
			_spill_from_mug(delay)
	_board.show_tiles(tiles.filter(func(entry: Dictionary) -> bool:
		return not (entry.get("id", "") == BEER_TILE and int(entry["square"]) in _pending_beer)))


## He drinks, slams the mug down and the beer in `_pending_beer` slops out of it onto the board.
## His next reply waits until the mug is back on the counter.
func _spill_from_mug(delay: float) -> void:
	_drinking = true
	_match.Busy = true
	await get_tree().create_timer(delay).timeout
	_bartender.drink(_world.keeper_mug, true)
	await _bartender.mug_down
	# Only what's still slippery (an undo may have taken some back).
	var live: Array = _match.Tiles().filter(func(entry: Dictionary) -> bool:
		return entry.get("id", "") == BEER_TILE).map(func(entry: Dictionary) -> int: return int(entry["square"]))
	var squares := _pending_beer.filter(func(square: int) -> bool: return square in live)
	_pending_beer.clear()
	_board.spill_beer(squares, true, _world.keeper_mug.global_position + MUG_LIP)
	_show_tiles()
	await _bartender.drank
	_drinking = false
	_match.Busy = false
	# Your move was held while he drank.
	_refresh_marks()
	if _match.IsPlayerTurn():
		_say("Check. Your move." if _match.CheckedKing() >= 0 else "Your move.")


func _on_finished(winner: String) -> void:
	var line: String = {
		"player": "You take his king. The bartender tips his hat to you.",
		"bartender": "He takes your king. The bartender wins.",
	}.get(winner, "A draw.")
	_say(line)
	_leave_button.text = "Return to map"
	_leave_button.disabled = false
	_undo_button.disabled = true


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


## The board square under `point` (window pixels), or -1: a ray from the camera through the
## same spot of the low-res stage, onto the board's surface.
func _square_under(point: Vector2) -> int:
	var window := get_viewport().get_visible_rect().size
	var on_stage := point / window * Vector2(_stage.size)
	var origin := _camera.project_ray_origin(on_stage)
	var along := _camera.project_ray_normal(on_stage)
	var surface := _board.global_position.y + ChessSet.SURFACE
	if absf(along.y) < 0.0001:
		return -1
	var hit := origin + along * ((surface - origin.y) / along.y)
	return _board.square_index_at(_board.to_local(hit))


## A low candle-warm light hung over the board while you play, so both sides' pieces read.
func _light_board() -> void:
	var light := OmniLight3D.new()
	light.light_color = Color("ffc890")
	light.omni_range = 1.1
	light.omni_attenuation = 1.4
	light.shadow_enabled = false
	light.position = _board.global_position + Vector3(-0.25, 0.55, 0)
	light.light_energy = 0.0
	_world.add_child(light)
	create_tween().tween_property(light, "light_energy", 0.9, 0.8)


## A single line of text low on the screen saying whose move it is.
func _build_hud() -> void:
	_hud = CanvasLayer.new()
	add_child(_hud)
	var actions := HBoxContainer.new()
	actions.position = Vector2(22, 42)
	actions.add_theme_constant_override("separation", 12)
	_hud.add_child(actions)
	_undo_button = Button.new()
	_undo_button.text = "Undo move"
	_undo_button.disabled = not _match.CanUndo()
	_undo_button.pressed.connect(func() -> void: _match.Undo())
	actions.add_child(_undo_button)
	_leave_button = Button.new()
	_leave_button.text = "Concede and leave"
	_leave_button.pressed.connect(func() -> void: _match.Leave())
	actions.add_child(_leave_button)
	if get_parent().has_method("SaveNow"):
		var save := Button.new()
		save.text = "Save run"
		save.pressed.connect(func() -> void: get_parent().SaveNow())
		actions.add_child(save)
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
	_hint = _status.duplicate()
	_hint.add_theme_font_size_override("font_size", 20)
	_hint.add_theme_color_override("font_color", Color("d8c8a8"))
	_hint.offset_top = -100
	_hint.offset_bottom = -68
	_hint.text = ""
	_hud.add_child(_hint)
	create_tween().tween_property(_hint, "modulate:a", 1.0, 0.6)
	create_tween().tween_property(_status, "modulate:a", 1.0, 0.6)


func _say(text: String) -> void:
	if _status:
		_status.text = text


func _set_zoom(zoom: float) -> void:
	_zoom = zoom
	_frame_stage()


## The walking eye `distance` metres along the path: head bobbing with each step, looking
## ahead down the path.
func _walk_shot(distance: float) -> Transform3D:
	# First person: the eyes ride on the walker's head and look where the walker is going.
	var length := _walk.get_baked_length()
	var foot := _walk.sample_baked(clampf(distance, 0.0, length))
	var ahead := _walk.sample_baked(clampf(distance + LOOK_AHEAD, 0.0, length))
	if distance + LOOK_AHEAD > length:
		var along := (_walk.sample_baked(length) - _walk.sample_baked(length - 0.2)).normalized()
		ahead += along * (distance + LOOK_AHEAD - length)
	# Ease the bob in and out so it starts and stops with the feet.
	var stepping := smoothstep(0.0, 0.6, distance) * smoothstep(0.0, 0.5, length - distance)
	var phase := distance / STRIDE * PI
	var bob := Vector3(0.012 * sin(phase), 0.022 * absf(sin(phase)) - 0.011, 0) * stepping
	var eye := foot + Vector3(0, EYE_HEIGHT, 0)
	var look := ahead + Vector3(0, EYE_HEIGHT - LOOK_DROP, 0)
	look = SIGN_LOOK.lerp(look, smoothstep(0.0, LEAVE_SIGN, distance))
	var shot := _shot(eye, look)
	shot.origin += shot.basis * bob
	return shot


## Over the board to play: from your side of it, PLAY_PITCH degrees down onto its centre.
static func _lean_shot() -> Transform3D:
	var pitch := deg_to_rad(PLAY_PITCH)
	var eye := BOARD_CENTRE + Vector3(-cos(pitch), sin(pitch), 0) * PLAY_DISTANCE
	return _shot(eye, BOARD_CENTRE)


static func _shot(from: Vector3, at: Vector3) -> Transform3D:
	return Transform3D(Basis.IDENTITY, from).looking_at(at, Vector3.UP)
