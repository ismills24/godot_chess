extends "res://common/scene_kit.gd"
## Someone sitting at a chess board waiting for a game, built from boxes like the inn's regulars.
## Local space: origin on the ground under the seat, facing +Z (toward the board and you).
##
## `build(spec)` takes a Dictionary:
##   seat         "chair" (sitting on something seat_height high) | "ground" (cross-legged)
##   seat_height  for "chair", default 0.47
##   height, girth             body proportions (about 0.85-1.15)
##   skin, coat, legs          Colors; sleeves optional (defaults to coat)
##   robe         true for a long robe hiding the legs
##   hat          "none" | "hood" | "wizard_hood" | "straw" | "cap" | "helm" | "bandana"
##   hat_color    Color
##   hair, beard_color         Colors; bald  true for a horseshoe of hair round a bare crown
##   beard        "none" | "stubble" | "full" | "long"
##   extras       Array of "spectacles", "scarf", "tabard", "beads", "stripes", "pipe",
##                "earring", "eyepatch", "mail", "belt_pouches"
##   scarf_color, tabard_color, trim_color, stripe_color   Colors for those extras
##   phase        offset so idle motion differs between people
## Motion: they study the board, breathing; look_at_target() turns their head to follow
## something (you, walking up); nod() greets you; settle() puts their eyes back on the board;
## reach(point) stretches their right hand out to a point on the board and back.

var _phase := 0.0
var _hips: Node3D
var _torso: Node3D
var _head: Node3D
var _shoulders: Array[Node3D] = []
var _elbows: Array[Node3D] = []
var _rest_arm: Array[Vector2] = []
var _torso_pitch := 0.18
## Studying the board: how far down the head is tipped.
var _study_pitch := 0.45
## Looking at something: how far they've turned to it (0 studying the board, 1 on it).
var _look := 0.0
var _look_target: Node3D
var _nod := 0.0
var _reach := 0.0
var _reach_point := Vector3.ZERO
var _pipe_ember: StandardMaterial3D


func build(spec: Dictionary) -> void:
	_phase = spec.get("phase", 0.0)
	var h: float = spec.get("height", 1.0)
	var g: float = spec.get("girth", 1.0)
	var extras: Array = spec.get("extras", [])
	var skin: Color = spec.get("skin", Color("b08870"))
	var coat_color: Color = spec.get("coat", Color("5a4a3a"))
	var coat := _paint(coat_color, 0.92)
	var sleeves := _paint(spec.get("sleeves", coat_color), 0.92)
	if extras.has("mail"):
		sleeves = _paint(Color("6a6c70"), 0.45, 0.7)
	var legs := _paint(spec.get("legs", Color("3a3028")), 0.95)
	var flesh := _paint(skin, 0.75)
	var boots := _paint(Color("1e1712"), 0.7)
	var leather := _paint(Color("3a2618"), 0.7)
	var robe: bool = spec.get("robe", false)
	var ground: bool = spec.get("seat", "chair") == "ground"
	var seat_height: float = 0.1 if ground else spec.get("seat_height", 0.47)

	# Legs: on a seat the thighs run forward and the shins drop; on the ground they cross.
	_hips = _pivot(self, Vector3(0, seat_height + 0.06, -0.02))
	if ground:
		for side: int in [-1, 1]:
			var thigh := _box_mesh(_hips, legs, Vector3(side * 0.19 * g, -0.05, 0.12), Vector3(0.15 * g, 0.13, 0.42))
			thigh.rotation.y = side * 0.55
			var shin := _box_mesh(_hips, legs, Vector3(side * 0.06, -0.09, 0.3), Vector3(0.44 * g, 0.11, 0.12))
			shin.rotation.y = side * 0.25
			_box_mesh(_hips, boots, Vector3(side * -0.2 * g, -0.1, 0.32 + side * 0.02), Vector3(0.11, 0.09, 0.2))
	else:
		for side: int in [-1, 1]:
			var x := side * 0.1 * g
			_box_mesh(_hips, legs, Vector3(x, 0, 0.2), Vector3(0.15 * g, 0.15, 0.44))
			_box_mesh(_hips, legs, Vector3(x, -seat_height * 0.5 - 0.02, 0.4), Vector3(0.12 * g, seat_height + 0.02, 0.13))
			_box_mesh(_hips, boots, Vector3(x, -seat_height - 0.02, 0.45), Vector3(0.13, 0.08, 0.24))
	if robe:
		# The robe falls over the knees to the ground.
		if ground:
			_box_mesh(_hips, coat, Vector3(0, -0.04, 0.14), Vector3(0.66 * g, 0.16, 0.56))
		else:
			_box_mesh(_hips, coat, Vector3(0, 0.01, 0.2), Vector3(0.38 * g, 0.17, 0.48))
			_box_mesh(_hips, coat, Vector3(0, -seat_height * 0.5 - 0.02, 0.43), Vector3(0.36 * g, seat_height + 0.06, 0.12))

	# Torso.
	_torso = _pivot(_hips, Vector3.ZERO)
	var depth := 0.22 * g
	_box_mesh(_torso, coat, Vector3(0, 0.29 * h, 0), Vector3(0.36 * g, 0.58 * h, depth))
	_box_mesh(_torso, coat, Vector3(0, 0.53 * h, 0), Vector3(0.42 * g, 0.1, depth + 0.01))
	if robe:
		_box_mesh(_torso, coat, Vector3(0, 0.05, 0), Vector3(0.4 * g, 0.14, depth + 0.04))
	if extras.has("stripes"):
		var stripe := _paint(spec.get("stripe_color", Color("d8d4c8")), 0.9)
		for band in 5:
			_box_mesh(_torso, stripe, Vector3(0, 0.12 + band * 0.09 * h, 0), Vector3(0.365 * g, 0.035, depth + 0.006))
	if extras.has("tabard"):
		var tabard := _paint(spec.get("tabard_color", Color("8a1e1a")), 0.9)
		var trim := _paint(spec.get("trim_color", Color("c8a040")), 0.5, 0.3)
		for face: int in [-1, 1]:
			_box_mesh(_torso, tabard, Vector3(0, 0.22 * h, face * (depth * 0.5 + 0.008)), Vector3(0.3 * g, 0.64 * h, 0.014))
		# A tower on the chest, the town's badge.
		_box_mesh(_torso, trim, Vector3(0, 0.36 * h, depth * 0.5 + 0.017), Vector3(0.1, 0.13, 0.006))
		for merlon: int in [-1, 0, 1]:
			_box_mesh(_torso, trim, Vector3(merlon * 0.035, 0.44 * h, depth * 0.5 + 0.017), Vector3(0.022, 0.03, 0.006))
		_box_mesh(_torso, trim, Vector3(0, 0.08, depth * 0.5 + 0.016), Vector3(0.31 * g, 0.03, 0.006))
		# The tabard's skirt over the thighs.
		if not ground:
			_box_mesh(_hips, tabard, Vector3(0, 0.09, 0.18), Vector3(0.3 * g, 0.012, 0.36))
	_box_mesh(_torso, leather, Vector3(0, 0.05, 0), Vector3(0.37 * g, 0.05, depth + 0.012))
	if extras.has("belt_pouches"):
		for side: int in [-1, 1]:
			_box_mesh(_torso, leather, Vector3(side * 0.13 * g, 0.0, depth * 0.5 + 0.02), Vector3(0.08, 0.09, 0.05))
	if extras.has("scarf"):
		var scarf := _paint(spec.get("scarf_color", Color("7a2a20")), 0.95)
		_box_mesh(_torso, scarf, Vector3(0, 0.59 * h, 0.0), Vector3(0.24, 0.07, depth + 0.04))
		var tail := _box_mesh(_torso, scarf, Vector3(0.07, 0.47 * h, depth * 0.5 + 0.025), Vector3(0.07, 0.2, 0.02))
		tail.rotation.z = 0.12
	if extras.has("beads"):
		var bead := _paint(Color("4a2a18"), 0.5)
		for k in 9:
			var angle := (k - 4) * 0.26
			_box_mesh(_torso, bead, Vector3(sin(angle) * 0.11, 0.5 * h - cos(angle) * 0.1 + 0.08, depth * 0.5 + 0.012),
				Vector3(0.024, 0.024, 0.02))
		_box_mesh(_torso, _paint(Color("8a6a3a"), 0.4, 0.4), Vector3(0, 0.39 * h, depth * 0.5 + 0.016), Vector3(0.04, 0.05, 0.01))
	_box_mesh(_torso, flesh, Vector3(0, 0.6 * h, 0), Vector3(0.1, 0.06, 0.1))

	_head = _pivot(_torso, Vector3(0, 0.62 * h, 0.01))
	_build_head(spec, extras, skin, flesh)

	# Arms: shoulder and elbow pivots; at rest the forearms come forward toward the board.
	for side: int in [1, -1]:
		var shoulder := _pivot(_torso, Vector3(side * 0.21 * g, 0.53 * h, 0))
		var wide := 1.35 if robe else 1.0
		_box_mesh(shoulder, sleeves, Vector3(0, -0.14 * h, 0), Vector3(0.11 * g * wide, 0.3 * h, 0.12 * wide))
		var elbow := _pivot(shoulder, Vector3(0, -0.28 * h, 0))
		_box_mesh(elbow, sleeves, Vector3(0, -0.1 * h, 0), Vector3(0.1 * g * wide, 0.22 * h, 0.1 * wide))
		if robe:
			# Wide cuffs hanging open.
			_box_mesh(elbow, sleeves, Vector3(0, -0.22 * h, 0.0), Vector3(0.15, 0.06, 0.15))
		_box_mesh(elbow, flesh, Vector3(0, -0.26 * h, 0.0), Vector3(0.075, 0.09, 0.08))
		_shoulders.append(shoulder)
		_elbows.append(elbow)
		_rest_arm.append(Vector2(-0.55, -0.95))
	_animate()


func _build_head(spec: Dictionary, extras: Array, skin: Color, flesh: StandardMaterial3D) -> void:
	var shade := _paint(skin.darkened(0.3), 0.85)
	var dark := _paint(Color("140c0a"), 0.6)
	_box_mesh(_head, flesh, Vector3(0, 0.12, 0), Vector3(0.2, 0.24, 0.22))
	for side: int in [-1, 1]:
		_box_mesh(_head, flesh, Vector3(side * 0.105, 0.12, 0), Vector3(0.02, 0.06, 0.04))
		_box_mesh(_head, dark, Vector3(side * 0.045, 0.15, 0.111), Vector3(0.03, 0.02, 0.01))
	_box_mesh(_head, shade, Vector3(0, 0.178, 0.112), Vector3(0.15, 0.02, 0.01))
	_box_mesh(_head, _paint(skin.darkened(0.08), 0.6), Vector3(0, 0.11, 0.126), Vector3(0.035, 0.06, 0.035))
	_box_mesh(_head, _paint(Color("2a1512"), 0.6), Vector3(0, 0.058, 0.111), Vector3(0.07, 0.012, 0.01))
	var hair_color: Color = spec.get("hair", Color("2a2018"))
	var hair := _paint(hair_color, 0.95)
	if spec.has("hair"):
		_box_mesh(_head, hair, Vector3(0, 0.16, -0.105), Vector3(0.21, 0.17, 0.03))
		for side: int in [-1, 1]:
			_box_mesh(_head, hair, Vector3(side * 0.104, 0.18, -0.03), Vector3(0.02, 0.08, 0.15))
		if not spec.get("bald", false):
			_box_mesh(_head, hair, Vector3(0, 0.25, -0.01), Vector3(0.21, 0.04, 0.23))
	var beard: String = spec.get("beard", "none")
	var whiskers := _paint(spec.get("beard_color", hair_color), 0.95)
	match beard:
		"stubble":
			_box_mesh(_head, _paint(skin.lerp(spec.get("beard_color", hair_color), 0.45), 0.9),
				Vector3(0, 0.045, 0.004), Vector3(0.205, 0.08, 0.215))
		"full":
			_box_mesh(_head, whiskers, Vector3(0, 0.04, 0.05), Vector3(0.21, 0.1, 0.13))
			_box_mesh(_head, whiskers, Vector3(0, -0.02, 0.09), Vector3(0.15, 0.08, 0.06))
		"long":
			# A long beard spilling down onto the chest.
			_box_mesh(_head, whiskers, Vector3(0, 0.04, 0.05), Vector3(0.21, 0.1, 0.13))
			_box_mesh(_head, whiskers, Vector3(0, -0.06, 0.1), Vector3(0.17, 0.14, 0.07))
			_box_mesh(_head, whiskers, Vector3(0, -0.19, 0.13), Vector3(0.12, 0.16, 0.05))
			_box_mesh(_head, whiskers, Vector3(0.01, -0.3, 0.14), Vector3(0.06, 0.1, 0.04))
			_box_mesh(_head, whiskers, Vector3(0, 0.085, 0.118), Vector3(0.12, 0.025, 0.02))
	if beard != "none":
		_box_mesh(_head, _paint(Color("2a1512"), 0.6), Vector3(0, 0.062, 0.116), Vector3(0.06, 0.01, 0.01))
	if extras.has("spectacles"):
		var rim := _paint(Color("8a6a2a"), 0.35, 0.8)
		var lens := _glowing(Color("9fd8ff"), 0.6)
		for side: int in [-1, 1]:
			_box_mesh(_head, rim, Vector3(side * 0.047, 0.15, 0.122), Vector3(0.05, 0.044, 0.006))
			_box_mesh(_head, lens, Vector3(side * 0.047, 0.15, 0.126), Vector3(0.036, 0.03, 0.004))
		_box_mesh(_head, rim, Vector3(0, 0.155, 0.124), Vector3(0.03, 0.008, 0.005))
	if extras.has("eyepatch"):
		_box_mesh(_head, dark, Vector3(0.045, 0.15, 0.115), Vector3(0.05, 0.045, 0.012))
		var strap := _box_mesh(_head, dark, Vector3(0, 0.17, 0.114), Vector3(0.22, 0.012, 0.012))
		strap.rotation.z = -0.3
	if extras.has("earring"):
		_box_mesh(_head, _paint(Color("d0a040"), 0.3, 0.9), Vector3(-0.11, 0.085, 0.0), Vector3(0.014, 0.025, 0.014))
	if extras.has("pipe"):
		var pipe_wood := _paint(Color("3a2014"), 0.6)
		var stem := _box_mesh(_head, pipe_wood, Vector3(0.04, 0.05, 0.16), Vector3(0.012, 0.012, 0.1))
		stem.rotation.x = 0.25
		_box_mesh(_head, pipe_wood, Vector3(0.04, 0.05, 0.215), Vector3(0.035, 0.045, 0.035))
		_pipe_ember = _glowing(Color("ff6a20"), 2.0)
		_box_mesh(_head, _pipe_ember, Vector3(0.04, 0.074, 0.215), Vector3(0.024, 0.006, 0.024))
	var hat_color: Color = spec.get("hat_color", Color("2a241c"))
	var felt := _paint(hat_color, 0.95)
	match spec.get("hat", "none"):
		"hood", "wizard_hood":
			# A deep hood framing the face, open at the front, falling onto the shoulders.
			_box_mesh(_head, felt, Vector3(0, 0.27, -0.01), Vector3(0.27, 0.06, 0.3))
			_box_mesh(_head, felt, Vector3(0, 0.12, -0.135), Vector3(0.27, 0.34, 0.05))
			for side: int in [-1, 1]:
				_box_mesh(_head, felt, Vector3(side * 0.125, 0.11, 0.01), Vector3(0.035, 0.32, 0.27))
			_box_mesh(_head, felt, Vector3(0, -0.04, -0.05), Vector3(0.36, 0.06, 0.26))
			_box_mesh(_head, _paint(hat_color.darkened(0.4), 0.95), Vector3(0, 0.24, 0.13), Vector3(0.22, 0.02, 0.02))
			if spec["hat"] == "wizard_hood":
				# Its peak flops back and over to one side.
				var peak := _pivot(_head, Vector3(0, 0.29, -0.04))
				peak.rotation = Vector3(-0.5, 0, 0.35)
				_box_mesh(peak, felt, Vector3(0, 0.06, 0), Vector3(0.17, 0.13, 0.18))
				_box_mesh(peak, felt, Vector3(0, 0.15, -0.02), Vector3(0.1, 0.1, 0.1))
				var tip := _box_mesh(peak, felt, Vector3(-0.03, 0.22, -0.06), Vector3(0.05, 0.08, 0.05))
				tip.rotation.z = 0.6
		"straw":
			var straw := _paint(hat_color, 0.95)
			var band := _paint(Color("5a2418"), 0.9)
			var hat := _pivot(_head, Vector3(0, 0.25, -0.01))
			hat.rotation = Vector3(-0.12, 0, 0.06)
			_cylinder(hat, Vector3.ZERO, 0.24, 0.24, 0.016, straw)
			_cylinder(hat, Vector3(0, 0.05, 0), 0.115, 0.1, 0.09, straw)
			_cylinder(hat, Vector3(0, 0.02, 0), 0.118, 0.118, 0.025, band)
		"cap":
			var cap := _pivot(_head, Vector3(0, 0.25, 0))
			cap.rotation = Vector3(-0.08, 0, -0.05)
			_cylinder(cap, Vector3(0, 0.01, -0.01), 0.13, 0.12, 0.06, felt)
			_box_mesh(cap, felt, Vector3(0, -0.005, 0.12), Vector3(0.18, 0.015, 0.08))
		"helm":
			var iron := _paint(hat_color, 0.35, 0.8)
			var helm := _pivot(_head, Vector3(0, 0.22, 0))
			_cylinder(helm, Vector3(0, 0.0, 0), 0.22, 0.22, 0.018, iron)
			_cylinder(helm, Vector3(0, 0.06, 0), 0.125, 0.115, 0.11, iron)
			var dome := SphereMesh.new()
			dome.radius = 0.115
			dome.height = 0.13
			dome.radial_segments = 10
			dome.rings = 4
			_mesh(helm, dome, iron, Vector3(0, 0.11, 0))
			_box_mesh(helm, _paint(hat_color.darkened(0.4), 0.5, 0.6), Vector3(0, 0.12, 0), Vector3(0.02, 0.05, 0.25))
		"bandana":
			_box_mesh(_head, felt, Vector3(0, 0.235, -0.005), Vector3(0.215, 0.07, 0.235))
			var knot := _box_mesh(_head, felt, Vector3(0, 0.2, -0.13), Vector3(0.05, 0.1, 0.03))
			knot.rotation.x = 0.4


## Where their eyes are, for you to look at.
func head_position() -> Vector3:
	return _head.global_position + Vector3(0, 0.15, 0)


## Looks up from the board and follows `target` with their eyes.
func look_at_target(target: Node3D) -> void:
	_look_target = target
	create_tween().tween_property(self, "_look", 1.0, 0.7).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


## A slow nod: sit down, then.
func nod() -> void:
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_interval(0.15)
	tween.tween_property(self, "_nod", 1.0, 0.3)
	tween.tween_property(self, "_nod", 0.0, 0.4)


## Eyes back down on the board, to play.
func settle() -> void:
	create_tween().tween_property(self, "_look", 0.0, 0.8).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)


## Reaches out to `point` (a piece on the board) and back.
func reach(point: Vector3) -> void:
	_reach_point = point
	var tween := create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	tween.tween_property(self, "_reach", 1.0, 0.32)
	tween.tween_interval(0.3)
	tween.tween_property(self, "_reach", 0.0, 0.5)


func _animate() -> void:
	super()
	if not _torso:
		return
	var t := _elapsed + _phase
	_torso.scale = Vector3(1.0 + 0.012 * sin(t * 1.5), 1.0, 1.0 + 0.016 * sin(t * 1.5))
	_torso.rotation.x = _torso_pitch + 0.03 * sin(t * 0.4) + 0.22 * _reach - 0.1 * _look
	_torso.rotation.z = 0.02 * sin(t * 0.33)
	# Studying the board, eyes wandering over it.
	var study := Vector2(0.18 * sin(t * 0.37) + 0.08 * sin(t * 0.91), _study_pitch + 0.06 * sin(t * 0.6))
	var looking := study
	if _look_target:
		var seen := _torso.global_basis.inverse() * (_look_target.global_position - _head.global_position)
		var across := Vector2(seen.x, seen.z).length()
		looking = Vector2(clampf(atan2(seen.x, seen.z), -1.2, 1.2), clampf(atan2(-seen.y, across), -0.6, 0.8))
	var aim := study.lerp(looking, _look)
	_head.rotation = Vector3(aim.y + 0.35 * _nod, aim.x, 0.0)
	if _pipe_ember:
		_pipe_ember.emission_energy_multiplier = 1.6 + 0.9 * maxf(0.0, sin(t * 0.8)) + 0.2 * sin(t * 9.0)
	for side in 2:
		var shoulder := _shoulders[side]
		var rest := Quaternion(Basis.from_euler(Vector3(_rest_arm[side].x, 0, (0.12 if side == 0 else -0.12))))
		var elbow_rest := _rest_arm[side].y
		if side == 0 and _reach > 0.0:
			# Hang the arm straight from the shoulder toward the piece.
			var toward: Vector3 = (shoulder.get_parent() as Node3D).global_basis.inverse() * (_reach_point - shoulder.global_position)
			var aim_arm := Quaternion(Vector3.DOWN, toward.normalized())
			shoulder.quaternion = rest.slerp(aim_arm, _reach)
			_elbows[side].rotation.x = lerpf(elbow_rest, -0.12, _reach)
		else:
			# Now and then a hand comes up to rub the chin.
			var chin := 0.0
			if side == 1:
				var into := fposmod(t + _phase * 2.0, 9.0)
				chin = sin(into / 2.6 * PI) if into < 2.6 else 0.0
				chin *= 1.0 - _look
			shoulder.quaternion = rest.slerp(Quaternion(Basis.from_euler(Vector3(-0.75, 0.35, -0.1))), chin)
			_elbows[side].rotation.x = lerpf(elbow_rest, -2.2, chin)
