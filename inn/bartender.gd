extends "res://common/scene_kit.gd"
## The bartender, built from boxes: a towering, barrel-chested brute with a dwarf's look about
## him (shaved head, a huge dark beard in iron-ringed braids, leather jerkin, bracers, rune
## tattoos), plus the wooden board he serves.
## Local space: origin between the feet, facing +Z. Limbs hang from pivot nodes so poses
## are plain rotations: shoulders/hips swing on X, the torso leans on X from the hips.
## He starts with his back to the customer, hands on the board where it rests on the back
## shelf; serve() lifts it, turns him round, sets it on the counter and leaves him leaning on
## it, hands planted, ready to play; then emits `served`.

signal served
## He's put his mug back down after a drink.
signal drank
## The mug has just hit the counter (mid-way through `drink`, before his arm folds back).
signal mug_down

const ChessSet := preload("res://inn/chess_set.gd")

const HIP_HEIGHT := 1.02
## How far in front of him the board rides, and its height in his hands.
const CARRY := Vector3(0, 1.42, 0.42)
const TURN_SECONDS := 1.6
## Facing the customer with the board held up, a beat before setting it down.
const BEFORE_SETTING := 0.45

var _hips: Node3D
var _torso: Node3D
var _head: Node3D
var _shoulders: Array[Node3D] = []
var _elbows: Array[Node3D] = []
var _legs: Array[Node3D] = []
var _knees: Array[Node3D] = []
var _board: Node3D
var _post: Vector3
var _facing: float
var _board_target: Vector3
var _is_ready := false
## A look over his shoulder at someone coming in: how far he's turned to them.
var _glance := 0.0
var _glance_at: Node3D

var _shirt := _paint(Color("2c2926"), 0.95)
var _cuff := _paint(Color("221f1d"), 0.95)
var _leather := _paint(Color("4a3020"), 0.7)
var _apron := _paint(Color("33261c"), 0.9)
var _trousers := _paint(Color("272322"), 0.95)
var _boots := _paint(Color("191412"), 0.7)
var _skin := _paint(Color("b07a58"), 0.55)
var _skin_shade := _paint(Color("8e5e42"), 0.7)
var _beard := _paint(Color("1c1410"), 1.0)
var _grey := _paint(Color("57504a"), 1.0)
var _iron := _paint(Color("4a4642"), 0.35, 0.8)
var _ink := _paint(Color("2a3040"), 0.8)
var _eye := _paint(Color("0d0a09"), 0.4)


## `post` is the floor point behind the bar where he serves, facing `facing` (a yaw: his +Z
## turns toward the customer); `board_target` is the counter-top spot for the board; `shelf_y`
## is the height of the back shelf the board waits on, behind him.
func build(post: Vector3, facing: float, board_target: Vector3, shelf_y: float) -> void:
	_post = post
	_facing = facing
	_board_target = board_target
	position = post
	rotation.y = facing + PI
	_build_body()
	_build_board()
	_hold_board_pose()
	_board.position.y = shelf_y
	_animate()


## Where his eyes are, for the customer to look at.
func head_position() -> Vector3:
	return _head.global_position + Vector3(0, 0.1, 0)


## Plays the serve. With `instant`, jumps straight to the ready pose (reduced motion).
func serve(instant := false) -> void:
	if instant:
		rotation.y = _facing
		_board.position = _board_target
		_board.rotation.y = _facing
		_ready_pose()
		_finish_serving()
		return
	var tween := create_tween()
	# Lift the board off the shelf, then turn round with it to face the customer.
	tween.tween_property(_board, "position:y", position.y + CARRY.y, 0.35).set_trans(Tween.TRANS_SINE)
	tween.tween_method(_turn_step, 0.0, 1.0, TURN_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_interval(BEFORE_SETTING)
	# Lean in and set the board down on the counter.
	tween.tween_property(_torso, "rotation:x", 0.55, 0.8).set_trans(Tween.TRANS_SINE)
	for side in 2:
		tween.parallel().tween_property(_shoulders[side], "rotation:x", -1.1, 0.8)
		tween.parallel().tween_property(_elbows[side], "rotation:x", -0.2, 0.8)
	tween.parallel().tween_property(_board, "position", _board_target + Vector3(0, 0.06, 0), 0.8)
	tween.tween_property(_board, "position", _board_target, 0.2)
	tween.tween_interval(0.2)
	# Ease back onto his planted hands, ready.
	tween.tween_property(_torso, "rotation:x", 0.3, 0.6).set_trans(Tween.TRANS_SINE)
	for side in 2:
		tween.parallel().tween_property(_shoulders[side], "rotation:x", -0.55, 0.6)
		tween.parallel().tween_property(_shoulders[side], "rotation:z", 0.3 if side == 0 else -0.3, 0.6)
		tween.parallel().tween_property(_elbows[side], "rotation:x", -0.45, 0.6)
	tween.tween_callback(_finish_serving)


## Turning on the spot: a half turn, shuffling his feet round as he goes.
func _turn_step(t: float) -> void:
	rotation.y = _facing + PI * (1.0 - t)
	var shuffle := sin(t * TAU * 1.5) * sin(t * PI)
	for side in 2:
		var swing := shuffle * (1.0 if side == 0 else -1.0)
		_legs[side].rotation.x = 0.25 * swing
		_knees[side].rotation.x = 0.25 * maxf(swing, 0.0)
	_carry_board()


## The carried board rides in his hands, its light side away from him.
func _carry_board() -> void:
	var basis := Basis(Vector3.UP, rotation.y)
	_board.position = position + basis * CARRY
	_board.rotation.y = rotation.y


func _finish_serving() -> void:
	_is_ready = true
	served.emit()


func _animate() -> void:
	super()
	if not _torso:
		return
	# Breathing and a slow look around; no head turn once he's facing the player.
	_torso.scale = Vector3(1.0 + 0.012 * sin(_elapsed * 1.6), 1.0, 1.0 + 0.018 * sin(_elapsed * 1.6))
	var glance := 0.0 if _is_ready else 0.18 * sin(_elapsed * 0.5)
	_head.rotation.y = glance + 0.04 * sin(_elapsed * 0.9)
	_torso.rotation.y = 0.0
	if _glance > 0.0 and _glance_at:
		# Over the shoulder: the head turns as far as it goes, the shoulders follow a little.
		var seen := to_local(_glance_at.global_position)
		var turn := atan2(seen.x, seen.z)
		var side := signf(turn) if absf(turn) > 0.01 else 1.0
		_head.rotation.y = lerpf(_head.rotation.y, side * minf(absf(turn), 1.55), _glance)
		_torso.rotation.y = side * minf(absf(turn), 0.65) * _glance


## The board he served, which the game is played on.
func board() -> Node3D:
	return _board


## Straightens up off the counter and folds his arms across his chest, out of the way of the
## board, to wait on your moves.
func fold_arms(seconds := 0.9) -> void:
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT).set_parallel()
	tween.tween_property(_torso, "rotation:x", 0.1, seconds)
	for side in 2:
		tween.tween_property(_shoulders[side], "rotation", _folded(side), seconds)
		tween.tween_property(_elbows[side], "rotation:x", -1.65, seconds)


## A folded arm's shoulder: the upper arm at his side, turned in so the forearm crosses his
## chest; the right arm a little lower, over the left.
func _folded(side: int) -> Vector3:
	return Vector3(-0.15 if side == 0 else -0.3, 1.0 if side == 0 else -1.0, 0.0)


## Takes a long pull from `mug`: unfolds his left arm to grab it off the counter, lifts it to
## his mouth with his head tipped back, sets it down where it was and folds his arms again.
## Emits `mug_down` as it lands and `drank` when done. With `slam` he brings it down hard and
## fast, the way that slops beer out of it.
func drink(mug: Node3D, slam := false) -> void:
	var home := mug.get_parent()
	var arm := 0
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	# Reach down to it.
	tween.tween_property(_torso, "rotation:x", 0.22, 0.5)
	tween.parallel().tween_property(_shoulders[arm], "rotation", Vector3(-0.45, 0.0, 0.08), 0.5)
	tween.parallel().tween_property(_elbows[arm], "rotation:x", -0.55, 0.5)
	# Take hold: from here it rides in his hand.
	tween.tween_callback(func() -> void: mug.reparent(_elbows[arm], true))
	# Up to his mouth, head going back as it tips.
	tween.tween_property(_torso, "rotation:x", -0.05, 0.65)
	tween.parallel().tween_property(_shoulders[arm], "rotation", Vector3(-1.55, 0.35, 0.0), 0.65)
	tween.parallel().tween_property(_elbows[arm], "rotation:x", -1.75, 0.65)
	tween.parallel().tween_property(_head, "rotation:x", -0.35, 0.65)
	tween.tween_interval(0.8)
	# And back down onto the counter.
	var down := 0.6
	if slam:
		# A beat up high, then down with a thump.
		down = 0.24
		tween.tween_property(_shoulders[arm], "rotation", Vector3(-1.2, 0.2, 0.05), 0.25)
		tween.parallel().tween_property(_head, "rotation:x", 0.0, 0.25)
		tween.tween_interval(0.12)
		tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_IN)
	tween.tween_property(_torso, "rotation:x", 0.22, down)
	tween.parallel().tween_property(_shoulders[arm], "rotation", Vector3(-0.45, 0.0, 0.08), down)
	tween.parallel().tween_property(_elbows[arm], "rotation:x", -0.55, down)
	tween.parallel().tween_property(_head, "rotation:x", 0.0, down)
	tween.tween_callback(func() -> void:
		mug.reparent(home, true)
		mug_down.emit())
	if slam:
		tween.set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tween.tween_interval(0.35)
	tween.tween_property(_torso, "rotation:x", 0.1, 0.55)
	tween.parallel().tween_property(_shoulders[arm], "rotation", _folded(arm), 0.55)
	tween.parallel().tween_property(_elbows[arm], "rotation:x", -1.65, 0.55)
	tween.tween_callback(func() -> void: drank.emit())


## Unfolds his right arm to reach over his half of the board and back, as he makes his move.
func reach() -> void:
	if not _is_ready:
		return
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	# In toward the middle of the board, not straight out at the side of it.
	tween.tween_property(_torso, "rotation:x", 0.24, 0.3)
	tween.parallel().tween_property(_shoulders[1], "rotation", Vector3(-0.62, -0.55, 0.0), 0.3)
	tween.parallel().tween_property(_elbows[1], "rotation:x", -0.6, 0.3)
	tween.tween_interval(0.25)
	tween.tween_property(_torso, "rotation:x", 0.1, 0.45)
	tween.parallel().tween_property(_shoulders[1], "rotation", _folded(1), 0.45)
	tween.parallel().tween_property(_elbows[1], "rotation:x", -1.65, 0.45)


## Glances over his shoulder at `target` for `hold` seconds, then back to what he's doing.
func glance_at(target: Node3D, hold: float) -> void:
	_glance_at = target
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(self, "_glance", 1.0, 0.45)
	tween.tween_interval(hold)
	tween.tween_property(self, "_glance", 0.0, 0.6)


func _build_body() -> void:
	_hips = _pivot(self, Vector3(0, HIP_HEIGHT, 0))
	for side in 2:
		var x := -0.15 if side == 0 else 0.15
		var leg := _pivot(_hips, Vector3(x, 0, 0))
		_box_mesh(leg, _trousers, Vector3(0, -0.25, 0), Vector3(0.23, 0.52, 0.25))
		var knee := _pivot(leg, Vector3(0, -0.5, 0))
		_box_mesh(knee, _trousers, Vector3(0, -0.22, 0), Vector3(0.2, 0.46, 0.21))
		_box_mesh(knee, _boots, Vector3(0, -0.47, 0.05), Vector3(0.23, 0.13, 0.36))
		_box_mesh(knee, _boots, Vector3(0, -0.33, 0), Vector3(0.22, 0.18, 0.22))
		_legs.append(leg)
		_knees.append(knee)
	_torso = _pivot(_hips, Vector3.ZERO)
	# Barrel chest over a solid gut, a dark shirt under an open leather jerkin.
	_box_mesh(_torso, _shirt, Vector3(0, 0.2, 0.02), Vector3(0.62, 0.38, 0.4))
	_box_mesh(_torso, _shirt, Vector3(0, 0.52, 0), Vector3(0.72, 0.42, 0.42))
	_box_mesh(_torso, _shirt, Vector3(0, 0.72, -0.01), Vector3(0.8, 0.12, 0.38))
	for side: int in [-1, 1]:
		_box_mesh(_torso, _leather, Vector3(side * 0.22, 0.43, 0.215), Vector3(0.26, 0.62, 0.025))
		_box_mesh(_torso, _leather, Vector3(side * 0.37, 0.43, 0), Vector3(0.025, 0.62, 0.42))
		for stud in 3:
			_box_mesh(_torso, _iron, Vector3(side * 0.12, 0.3 + stud * 0.15, 0.23), Vector3(0.025, 0.025, 0.01))
	_box_mesh(_torso, _leather, Vector3(0, 0.43, -0.215), Vector3(0.74, 0.62, 0.025))
	_box_mesh(_torso, _apron, Vector3(0, 0.06, 0.235), Vector3(0.56, 0.46, 0.03))
	_box_mesh(_torso, _leather, Vector3(0, 0.02, 0.0), Vector3(0.68, 0.09, 0.46))
	_box_mesh(_torso, _iron, Vector3(0, 0.02, 0.235), Vector3(0.13, 0.11, 0.02))
	_box_mesh(_torso, _leather, Vector3(0, 0.02, 0.247), Vector3(0.07, 0.05, 0.01))
	_box_mesh(_torso, _skin, Vector3(0, 0.8, 0.01), Vector3(0.22, 0.12, 0.22))
	_head = _pivot(_torso, Vector3(0, 0.84, 0.02))
	_build_head()
	# Huge arms: sleeves rolled to the elbow, inked forearms, leather bracers, big hands.
	for side in 2:
		var x := -0.45 if side == 0 else 0.45
		var shoulder := _pivot(_torso, Vector3(x, 0.7, 0))
		_box_mesh(shoulder, _shirt, Vector3(0, -0.18, 0), Vector3(0.2, 0.4, 0.21))
		_box_mesh(shoulder, _cuff, Vector3(0, -0.36, 0), Vector3(0.22, 0.07, 0.23))
		var elbow := _pivot(shoulder, Vector3(0, -0.37, 0))
		_box_mesh(elbow, _skin, Vector3(0, -0.15, 0), Vector3(0.16, 0.3, 0.16))
		_box_mesh(elbow, _ink, Vector3(0, -0.07, 0), Vector3(0.163, 0.035, 0.163))
		_box_mesh(elbow, _ink, Vector3(0, -0.11, 0), Vector3(0.163, 0.015, 0.163))
		_box_mesh(elbow, _leather, Vector3(0, -0.24, 0), Vector3(0.168, 0.11, 0.168))
		for stud: int in [-1, 1]:
			_box_mesh(elbow, _iron, Vector3(stud * 0.04, -0.24, 0.086), Vector3(0.022, 0.022, 0.008))
		_box_mesh(elbow, _skin, Vector3(0, -0.36, 0.01), Vector3(0.12, 0.12, 0.13))
		_shoulders.append(shoulder)
		_elbows.append(elbow)


## Shaved head, heavy brow and a great dark beard spilling onto the chest in two
## iron-ringed braids. The beard hangs in front of the chest (torso front is z 0.21).
func _build_head() -> void:
	_box_mesh(_head, _skin, Vector3(0, 0.17, 0), Vector3(0.26, 0.28, 0.27))
	_box_mesh(_head, _skin, Vector3(0, 0.32, -0.01), Vector3(0.22, 0.05, 0.23))
	_box_mesh(_head, _skin_shade, Vector3(0, 0.215, 0.13), Vector3(0.24, 0.045, 0.04))
	for side: int in [-1, 1]:
		_box_mesh(_head, _skin, Vector3(side * 0.137, 0.17, -0.01), Vector3(0.03, 0.07, 0.06))
		var brow := _box_mesh(_head, _beard, Vector3(side * 0.06, 0.232, 0.152), Vector3(0.09, 0.032, 0.025))
		brow.rotation.z = side * -0.15
		_box_mesh(_head, _skin_shade, Vector3(side * 0.058, 0.19, 0.137), Vector3(0.06, 0.04, 0.005))
		_box_mesh(_head, _eye, Vector3(side * 0.058, 0.192, 0.14), Vector3(0.034, 0.022, 0.01))
		# Bushy sideburns running down into the beard.
		_box_mesh(_head, _beard, Vector3(side * 0.13, 0.08, 0.02), Vector3(0.05, 0.2, 0.2))
	_box_mesh(_head, _skin, Vector3(0, 0.15, 0.155), Vector3(0.075, 0.09, 0.06))
	_box_mesh(_head, _skin_shade, Vector3(0, 0.11, 0.17), Vector3(0.08, 0.025, 0.04))
	# Moustache over a great bushy beard, wider than the jaw, falling onto the chest, with
	# two short braids bound in iron rings.
	_box_mesh(_head, _beard, Vector3(0, 0.1, 0.165), Vector3(0.21, 0.045, 0.05))
	_box_mesh(_head, _beard, Vector3(0, 0.03, 0.08), Vector3(0.34, 0.18, 0.21))
	_box_mesh(_head, _eye, Vector3(0, 0.07, 0.186), Vector3(0.08, 0.015, 0.01))
	_box_mesh(_head, _beard, Vector3(0, -0.07, 0.15), Vector3(0.36, 0.14, 0.16))
	_box_mesh(_head, _beard, Vector3(0, -0.17, 0.235), Vector3(0.3, 0.12, 0.07))
	for streak: float in [-0.09, 0.06]:
		_box_mesh(_head, _grey, Vector3(streak, -0.09, 0.232), Vector3(0.03, 0.14, 0.006))
	for side: int in [-1, 1]:
		_box_mesh(_head, _beard, Vector3(side * 0.07, -0.28, 0.245), Vector3(0.06, 0.12, 0.06))
		_box_mesh(_head, _iron, Vector3(side * 0.07, -0.29, 0.245), Vector3(0.075, 0.035, 0.075))
		_box_mesh(_head, _beard, Vector3(side * 0.07, -0.35, 0.245), Vector3(0.045, 0.04, 0.045))


func _hold_board_pose() -> void:
	for side in 2:
		_shoulders[side].rotation.x = -0.75
		_elbows[side].rotation.x = -0.75
		_shoulders[side].rotation.z = 0.12 if side == 0 else -0.12
	_carry_board()


## Leaning on the counter with both hands planted, waiting for the first move.
func _ready_pose() -> void:
	_torso.rotation.x = 0.3
	for side in 2:
		_legs[side].rotation.x = 0.0
		_knees[side].rotation.x = 0.0
		_shoulders[side].rotation.x = -0.55
		_shoulders[side].rotation.z = 0.3 if side == 0 else -0.3
		_elbows[side].rotation.x = -0.45


func _build_board() -> void:
	# The board ignores the bartender's transform so it can be carried and left behind.
	var board := ChessSet.new()
	board.top_level = true
	add_child(board)
	board.build()
	_board = board
