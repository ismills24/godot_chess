extends "res://common/scene_kit.gd"
## A regular of the inn, built from boxes, usually the worse for wear.
## Local space: origin on the floor under the seat (or between the feet when standing),
## facing +Z: toward the table or the bar.
##
## `build(spec)` takes a Dictionary:
##   pose    "sit" | "play" (hunched over a chess board, reaching for a piece) |
##           "slump" (asleep on the table) | "lean" (back, mug in hand) |
##           "stand" (at the bar) | "watch" (standing, arms folded, looking down at a game)
##   height, girth, belly   body proportions (about 0.85-1.15, 0.75-1.5, 0-1)
##   skin, shirt, legs      Colors; vest, hair, beard_color optional Colors
##   beard   "none" | "stubble" | "full"
##   bald    true for a horseshoe of hair round a bare crown
##   wear    Array of: "red_nose", "bags", "black_eye", "eyepatch", "bandage", "hat",
##           "torn_sleeve", "patch_shoulder", "patch_knee", "hole", "stain", "frayed"
##   mug     true to hold a tankard
##   phase   offset so customers don't breathe in step

const SEAT_HEIGHT := 0.47

var _pose := "sit"
var _phase := 0.0
var _torso: Node3D
var _head: Node3D
## A glance at someone: how far the head (and a little of the body) has turned to them.
var _glance := 0.0
var _glance_at: Node3D
var _torso_pitch := 0.0
## Arm pivots ([0] the right arm, the reaching / mug hand) and their posed angles.
var _shoulders: Array[Node3D] = []
var _elbows: Array[Node3D] = []
var _arm_pose: Array[Vector2] = []
var _head_pitch := 0.0


func build(spec: Dictionary) -> void:
	_pose = spec.get("pose", "sit")
	_phase = spec.get("phase", 0.0)
	var h: float = spec.get("height", 1.0)
	var g: float = spec.get("girth", 1.0)
	var belly: float = spec.get("belly", 0.0)
	var wear: Array = spec.get("wear", [])
	var skin: Color = spec.get("skin", Color("b08870"))
	var shirt_color: Color = spec.get("shirt", Color("6b5d45"))
	var shirt := _paint(shirt_color, 0.95)
	var legs := _paint(spec.get("legs", Color("3a3028")), 0.95)
	var flesh := _paint(skin, 0.8)
	var boots := _paint(Color("1e1712"), 0.7)
	var dark := _paint(Color("140c0a"), 0.6)
	var standing := _pose == "stand" or _pose == "watch"
	_torso_pitch = {"sit": 0.12, "play": 0.32, "slump": 0.95, "lean": -0.2, "stand": 0.25, "watch": 0.1}[_pose]

	# Legs: seated thighs run forward to the knee, standing legs hang straight.
	var hip_y := 0.92 * h if standing else SEAT_HEIGHT + 0.06
	var hips := _pivot(self, Vector3(0, hip_y, 0.0 if standing else -0.02))
	for side: int in [-1, 1]:
		var x := side * 0.1 * g
		if standing:
			_box_mesh(hips, legs, Vector3(x, -0.23 * h, 0), Vector3(0.15 * g, 0.46 * h, 0.16))
			_box_mesh(hips, legs, Vector3(x, -0.66 * h, 0.01), Vector3(0.12 * g, 0.42 * h, 0.13))
			_box_mesh(hips, boots, Vector3(x, -hip_y + 0.04, 0.04), Vector3(0.13, 0.08, 0.24))
		else:
			_box_mesh(hips, legs, Vector3(x, 0, 0.2), Vector3(0.15 * g, 0.15, 0.44))
			_box_mesh(hips, legs, Vector3(x, -0.25, 0.4), Vector3(0.12 * g, 0.5, 0.13))
			_box_mesh(hips, boots, Vector3(x, -hip_y + 0.04, 0.45), Vector3(0.13, 0.08, 0.24))
	if wear.has("patch_knee"):
		var knee := Vector3(0.1 * g, -0.45 * h, 0.08) if standing else Vector3(0.1 * g, 0.03, 0.4)
		_box_mesh(hips, _paint(shirt_color.lerp(Color("4a3d2c"), 0.6).lightened(0.15), 0.95), knee,
			Vector3(0.1, 0.1 if standing else 0.012, 0.012 if standing else 0.1) + Vector3(0, 0, 0.0))

	# Torso, with a belly pushing the shirt out, an optional vest and a belt.
	_torso = _pivot(hips, Vector3.ZERO)
	_torso.rotation.x = _torso_pitch
	var depth := 0.22 * g
	_box_mesh(_torso, shirt, Vector3(0, 0.29 * h, 0), Vector3(0.36 * g, 0.58 * h, depth))
	if belly > 0.0:
		_box_mesh(_torso, shirt, Vector3(0, 0.17 * h, depth * 0.5 + 0.03 * belly),
			Vector3(0.32 * g, 0.28 * h, 0.06 + 0.16 * belly))
	_box_mesh(_torso, _paint(Color("1e1510"), 0.6), Vector3(0, 0.05, 0.02 * belly),
		Vector3(0.37 * g, 0.05, depth + 0.01 + 0.16 * belly))
	if spec.has("vest"):
		var vest := _paint(spec["vest"], 0.9)
		for side: int in [-1, 1]:
			_box_mesh(_torso, vest, Vector3(side * 0.11 * g, 0.33 * h, 0.005 + 0.02 * belly),
				Vector3(0.15 * g, 0.42 * h, depth + 0.012 + 0.1 * belly))
		if wear.has("patch_shoulder"):
			_box_mesh(_torso, _paint(Color(spec["vest"]).lerp(Color("6a5a40"), 0.5), 0.95),
				Vector3(-0.12 * g, 0.42 * h, depth * 0.5 + 0.012 + 0.05 * belly), Vector3(0.09, 0.09, 0.01))
	elif wear.has("patch_shoulder"):
		_box_mesh(_torso, _paint(shirt_color.lerp(Color("2e2a22"), 0.55), 0.95),
			Vector3(-0.12 * g, 0.47 * h, depth * 0.5 + 0.004), Vector3(0.1, 0.09, 0.01))
	if wear.has("stain"):
		_box_mesh(_torso, _paint(shirt_color.darkened(0.45), 0.95),
			Vector3(0.04 * g, 0.3 * h, depth * 0.5 + 0.004 + 0.16 * belly), Vector3(0.11, 0.14, 0.008))
	if wear.has("hole"):
		_box_mesh(_torso, dark, Vector3(0.1 * g, 0.2 * h, depth * 0.5 + 0.005 + 0.16 * belly), Vector3(0.035, 0.03, 0.008))
		_box_mesh(_torso, flesh, Vector3(-0.08 * g, 0.38 * h, -depth * 0.5 - 0.004), Vector3(0.04, 0.035, 0.008))
	if wear.has("frayed"):
		for tatter in 5:
			var x := (tatter - 2) * 0.07 * g
			var drop := 0.03 + 0.04 * fposmod(tatter * 0.61 + _phase, 1.0)
			_box_mesh(_torso, shirt, Vector3(x, -drop * 0.5, depth * 0.5 + 0.02 * belly), Vector3(0.04, drop, 0.02))
	_box_mesh(_torso, flesh, Vector3(0, 0.6 * h, 0.01), Vector3(0.09, 0.07, 0.09))

	# Arms, posed per stance; the right hand may hold a tankard.
	var arms := {
		"sit": [Vector2(-0.45, -1.12), Vector2(-0.45, -1.12)],
		"slump": [Vector2(-0.95 - 1.45, -0.12), Vector2(-0.95 - 1.45, -0.12)],
		"lean": [Vector2(0.2 - 0.6, -0.97), Vector2(-0.1, -1.3)],
		"stand": [Vector2(-0.3 - 0.25, -1.27), Vector2(-0.3 - 0.25, -1.27)],
		# One hand out over the board, the other propping the chin... near enough.
		"play": [Vector2(-1.05 - 0.32, -0.45), Vector2(-0.5 - 0.32, -1.05)],
		# Arms folded across the chest.
		"watch": [Vector2(-0.35 - 0.1, -1.9), Vector2(-0.4 - 0.1, -1.8)],
	}
	if _pose == "sit":
		arms["sit"] = [Vector2(-0.45 - 0.12, -1.12), Vector2(-0.45 - 0.12, -1.12)]
	for i in 2:
		var side := 1 if i == 0 else -1
		var pose: Vector2 = arms[_pose][i]
		var shoulder := _pivot(_torso, Vector3(side * (0.18 * g + 0.035), 0.53 * h, 0))
		shoulder.rotation = Vector3(pose.x, 0, side * (0.06 + 0.05 * (g - 1.0)))
		if _pose == "lean" and side == -1:
			shoulder.rotation.z = -0.35
		if _pose == "watch":
			shoulder.rotation.z = side * -0.25
		_box_mesh(shoulder, shirt, Vector3(0, -0.15 * h, 0), Vector3(0.1 * sqrt(g), 0.31 * h, 0.11))
		var elbow := _pivot(shoulder, Vector3(0, -0.3 * h, 0))
		elbow.rotation.x = pose.y
		_shoulders.append(shoulder)
		_elbows.append(elbow)
		_arm_pose.append(pose)
		var torn := wear.has("torn_sleeve") and side == 1
		_box_mesh(elbow, flesh if torn else shirt, Vector3(0, -0.135 * h, 0),
			Vector3(0.075 if torn else 0.09 * sqrt(g), 0.27 * h, 0.085 if torn else 0.1))
		if torn:
			for rag in 3:
				_box_mesh(elbow, shirt, Vector3((rag - 1) * 0.03, -0.015 - 0.02 * (rag % 2), 0.01),
					Vector3(0.035, 0.05, 0.1))
		_box_mesh(elbow, flesh, Vector3(0, -0.29 * h, 0), Vector3(0.07, 0.08, 0.08))
		if side == 1 and spec.get("mug", false) and _pose != "slump" and _pose != "play" and _pose != "watch":
			# A wooden beer mug in hand: staves, iron hoops, foam. The forearm lies forward,
			# so its local +Z is up.
			var staves := _paint(Color("5a3a22"), 0.75)
			var hoop := _paint(Color("4a4642"), 0.35, 0.8)
			var mug := Vector3(-0.08, -0.29 * h, 0.07)
			_box_mesh(elbow, staves, mug, Vector3(0.095, 0.095, 0.14))
			_box_mesh(elbow, hoop, mug + Vector3(0, 0, -0.045), Vector3(0.1, 0.1, 0.012))
			_box_mesh(elbow, hoop, mug + Vector3(0, 0, 0.045), Vector3(0.1, 0.1, 0.012))
			_box_mesh(elbow, _paint(Color("d8c8a0"), 0.9), mug + Vector3(0, 0, 0.074), Vector3(0.085, 0.085, 0.012))
			_box_mesh(elbow, staves, mug + Vector3(0.065, 0, 0.005), Vector3(0.02, 0.02, 0.08))

	# Head: neck up, face on +Z.
	_head = _pivot(_torso, Vector3(0, 0.63 * h, 0.01))
	match _pose:
		"slump":
			_head.rotation = Vector3(0.55, 0.2, 0.75)
		"lean":
			_head.rotation.x = 0.12
		"stand":
			_head.rotation.x = -0.15
		"play":
			_head.rotation.x = 0.35
		"watch":
			_head.rotation.x = 0.5
	_head_pitch = _head.rotation.x
	_build_head(spec, wear, skin, flesh, dark)
	_animate()


func _build_head(spec: Dictionary, wear: Array, skin: Color, flesh: StandardMaterial3D, dark: StandardMaterial3D) -> void:
	var shade := _paint(skin.darkened(0.3), 0.85)
	_box_mesh(_head, flesh, Vector3(0, 0.12, 0), Vector3(0.2, 0.24, 0.22))
	for side: int in [-1, 1]:
		_box_mesh(_head, flesh, Vector3(side * 0.105, 0.12, 0), Vector3(0.02, 0.06, 0.04))
		_box_mesh(_head, dark, Vector3(side * 0.045, 0.15, 0.111), Vector3(0.03, 0.02, 0.01))
		if wear.has("bags"):
			_box_mesh(_head, _paint(skin.darkened(0.45).lerp(Color("4a3040"), 0.3), 0.85),
				Vector3(side * 0.045, 0.132, 0.111), Vector3(0.036, 0.014, 0.008))
	_box_mesh(_head, shade, Vector3(0, 0.178, 0.112), Vector3(0.15, 0.02, 0.01))
	var nose := _paint(Color("b04a3e") if wear.has("red_nose") else skin.darkened(0.08), 0.6)
	_box_mesh(_head, nose, Vector3(0, 0.11, 0.126), Vector3(0.04 if wear.has("red_nose") else 0.035, 0.06, 0.035))
	_box_mesh(_head, _paint(Color("2a1512"), 0.6), Vector3(0, 0.058, 0.111), Vector3(0.07, 0.012, 0.01))
	if wear.has("black_eye"):
		_box_mesh(_head, _paint(Color("3a2032"), 0.7), Vector3(-0.045, 0.148, 0.1105), Vector3(0.052, 0.044, 0.006))
	var hair_color: Color = spec.get("hair", Color("2a2018"))
	var hair := _paint(hair_color, 0.95)
	if spec.has("hair"):
		_box_mesh(_head, hair, Vector3(0, 0.16, -0.105), Vector3(0.21, 0.17, 0.03))
		for side: int in [-1, 1]:
			_box_mesh(_head, hair, Vector3(side * 0.104, 0.18, -0.03), Vector3(0.02, 0.08, 0.15))
		if not spec.get("bald", false):
			_box_mesh(_head, hair, Vector3(0, 0.25, -0.01), Vector3(0.21, 0.04, 0.23))
			# A couple of tufts sticking up: nobody here owns a comb.
			for tuft in 3:
				var tuft_box := _box_mesh(_head, hair, Vector3((tuft - 1) * 0.06, 0.275, -0.02 + tuft * 0.03),
					Vector3(0.04, 0.05, 0.03))
				tuft_box.rotation = Vector3(0.3 * (tuft - 1), 0, 0.4 * (1 - tuft))
	var beard: String = spec.get("beard", "none")
	var beard_color: Color = spec.get("beard_color", hair_color)
	if beard == "stubble":
		_box_mesh(_head, _paint(skin.lerp(beard_color, 0.45), 0.9), Vector3(0, 0.045, 0.004), Vector3(0.205, 0.08, 0.215))
	elif beard == "full":
		var whiskers := _paint(beard_color, 0.95)
		_box_mesh(_head, whiskers, Vector3(0, 0.04, 0.05), Vector3(0.21, 0.1, 0.13))
		_box_mesh(_head, whiskers, Vector3(0, -0.02, 0.09), Vector3(0.15, 0.08, 0.06))
		_box_mesh(_head, _paint(Color("2a1512"), 0.6), Vector3(0, 0.062, 0.116), Vector3(0.06, 0.01, 0.01))
	if wear.has("eyepatch"):
		_box_mesh(_head, dark, Vector3(0.045, 0.15, 0.115), Vector3(0.05, 0.045, 0.012))
		var strap := _box_mesh(_head, dark, Vector3(0, 0.17, 0.114), Vector3(0.22, 0.012, 0.012))
		strap.rotation.z = -0.3
		_box_mesh(_head, dark, Vector3(0, 0.2, -0.112), Vector3(0.21, 0.012, 0.01))
	if wear.has("bandage"):
		var linen := _paint(Color("9a8e78"), 0.95)
		var band := _box_mesh(_head, linen, Vector3(0, 0.2, 0), Vector3(0.215, 0.055, 0.235))
		band.rotation.z = 0.18
		_box_mesh(_head, _paint(Color("5a1a14"), 0.8), Vector3(-0.06, 0.2, 0.119), Vector3(0.035, 0.03, 0.005))
	if wear.has("hat"):
		var felt := _paint(Color("2a241c"), 0.95)
		var hat := _pivot(_head, Vector3(0, 0.25, 0))
		hat.rotation = Vector3(-0.1, 0, 0.12)
		_box_mesh(hat, felt, Vector3.ZERO, Vector3(0.3, 0.02, 0.3))
		_box_mesh(hat, felt, Vector3(0, 0.055, 0), Vector3(0.2, 0.1, 0.2))
		_box_mesh(hat, _paint(Color("1a1510"), 0.9), Vector3(0, 0.02, 0), Vector3(0.205, 0.025, 0.205))


## Turns to look at `target` (followed as it moves) for `hold` seconds, then back.
func glance_at(target: Node3D, hold: float) -> void:
	_glance_at = target
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(self, "_glance", 1.0, 0.4)
	tween.tween_interval(hold)
	tween.tween_property(self, "_glance", 0.0, 0.6)


func _animate() -> void:
	super()
	if not _torso:
		return
	_animate_pose()
	if _glance > 0.0 and _glance_at:
		var seen := to_local(_glance_at.global_position)
		var turn := clampf(atan2(seen.x, seen.z), -1.5, 1.5) * _glance
		_head.rotation.y = lerpf(_head.rotation.y, turn * 0.7, _glance)
		_torso.rotation.y = turn * 0.3


## 0 most of the time, rising to 1 and back over `length` seconds once every `period`
## seconds (offset by `offset`): the shape of an occasional gesture.
static func _now_and_then(t: float, period: float, length: float, offset: float) -> float:
	var into := fposmod(t + offset, period)
	return sin(into / length * PI) if into < length else 0.0


func _animate_pose() -> void:
	var t := _elapsed + _phase
	# Each regular keeps their own rhythm.
	var period := 5.5 + 3.0 * fposmod(_phase * 0.37, 1.0)
	match _pose:
		"slump":
			# Deep, slow snoring breaths.
			_torso.rotation.x = _torso_pitch + 0.02 * sin(t * 0.9)
			_torso.scale = Vector3(1.0 + 0.02 * sin(t * 0.9), 1.0, 1.0)
		"play":
			# Brooding over the position; every so often, leaning in to make a move: the hand
			# goes out over the board, sets a piece down and comes back.
			var move := _now_and_then(t, period, 1.7, _phase * 3.1)
			_head.rotation.y = 0.08 * sin(t * 0.3)
			_head.rotation.x = _head_pitch + 0.15 * move
			_torso.rotation.x = _torso_pitch + 0.12 * move
			_shoulders[0].rotation.x = _arm_pose[0].x - 0.3 * move
			_elbows[0].rotation.x = _arm_pose[0].y + 0.25 * move
			_torso.scale = Vector3(1.0 + 0.01 * sin(t * 1.2), 1.0, 1.0)
		"stand", "watch":
			# Unsteady on his feet.
			_torso.rotation.z = 0.035 * sin(t * 0.6)
			_head.rotation.z = 0.06 * sin(t * 0.45 + 1.0)
		"lean":
			# Leaning back with a mug; now and then a long pull from it, head tipped back.
			var drink := _now_and_then(t, period, 2.2, _phase * 2.3)
			_torso.rotation.x = _torso_pitch - 0.06 * drink
			_torso.rotation.z = 0.025 * sin(t * 0.5)
			_shoulders[0].rotation.x = _arm_pose[0].x - 0.75 * drink
			_elbows[0].rotation.x = _arm_pose[0].y - 0.55 * drink
			_head.rotation.x = _head_pitch - 0.45 * drink
			_head.rotation.y = 0.15 * sin(t * 0.23) * (1.0 - drink)
		_:
			# Sitting: every so often leaning right back in the chair for a stretch.
			var stretch := _now_and_then(t, period + 1.5, 2.6, _phase * 1.7)
			_torso.rotation.x = _torso_pitch - 0.3 * stretch
			_torso.rotation.z = 0.025 * sin(t * 0.5)
			_torso.scale = Vector3(1.0 + 0.012 * sin(t * 1.4), 1.0, 1.0)
			for arm in 2:
				_shoulders[arm].rotation.x = _arm_pose[arm].x - 0.5 * stretch
			_head.rotation.x = _head_pitch - 0.2 * stretch
			_head.rotation.y = 0.15 * sin(t * 0.23)
