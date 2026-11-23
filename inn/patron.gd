extends "res://common/scene_kit.gd"
## A hooded patron hunched over a table, face lost in the hood's shadow. Built from boxes.
## Local space: origin on the floor under the stool seat, facing +Z (toward the table).
## The hood drifts and now and then turns toward the chess corner; a smoker's pipe ember
## brightens on each draw and lights the chin from below.

const SEAT_HEIGHT := 0.47

var _hood: Node3D
var _torso: Node3D
var _ember: StandardMaterial3D
var _ember_light: OmniLight3D
var _phase := 0.0
var _glance_yaw := 0.0


## `glance_yaw` is the hood turn (radians) that looks toward the chess table.
func build(cloak: Color, smoking: bool, phase: float, glance_yaw: float) -> void:
	_phase = phase
	_glance_yaw = glance_yaw
	var cloth := _paint(cloak, 0.97)
	var hem := _paint(cloak.darkened(0.3), 1.0)
	var shadow := _paint(Color("040303"), 1.0)
	var skin := _paint(Color("6a4634"), 0.85)
	var hips := _pivot(self, Vector3(0, SEAT_HEIGHT + 0.05, 0))
	# Thighs forward on the stool, the cloak hem hanging over the shins.
	_box_mesh(hips, cloth, Vector3(0, 0, 0.18), Vector3(0.38, 0.15, 0.42))
	_box_mesh(hips, hem, Vector3(0, -0.25, 0.36), Vector3(0.36, 0.46, 0.12))
	_box_mesh(hips, shadow, Vector3(0, -0.49, 0.42), Vector3(0.3, 0.06, 0.2))
	_torso = _pivot(hips, Vector3.ZERO)
	_torso.rotation.x = 0.3
	var robe := CylinderMesh.new()
	robe.top_radius = 0.17
	robe.bottom_radius = 0.28
	robe.height = 0.62
	robe.radial_segments = 12
	robe.rings = 0
	_mesh(_torso, robe, cloth, Vector3(0, 0.31, 0))
	_box_mesh(_torso, cloth, Vector3(0, 0.6, 0), Vector3(0.46, 0.11, 0.27))
	# The hood's cowl drapes down the back in folds, so the cloak never reads as one plane.
	_box_mesh(_torso, hem, Vector3(0, 0.46, -0.15), Vector3(0.32, 0.3, 0.05))
	_box_mesh(_torso, cloth, Vector3(0, 0.3, -0.2), Vector3(0.12, 0.42, 0.04))
	# Sleeves reach forward, hands together on the table.
	for side: int in [-1, 1]:
		var arm := _pivot(_torso, Vector3(side * 0.2, 0.58, 0.03))
		arm.rotation = Vector3(-0.85, 0, side * 0.32)
		_box_mesh(arm, cloth, Vector3(0, -0.16, 0), Vector3(0.13, 0.34, 0.14))
		var forearm := _pivot(arm, Vector3(0, -0.32, 0))
		forearm.rotation.x = -0.85
		_box_mesh(forearm, cloth, Vector3(0, -0.12, 0), Vector3(0.14, 0.26, 0.15))
		_box_mesh(forearm, skin, Vector3(0, -0.28, 0), Vector3(0.07, 0.07, 0.08))
	# The hood: a deep cowl with a peak, its opening a pit of shadow.
	_hood = _pivot(_torso, Vector3(0, 0.66, 0.02))
	_box_mesh(_hood, cloth, Vector3(0, 0.14, -0.01), Vector3(0.29, 0.31, 0.31))
	_box_mesh(_hood, cloth, Vector3(0, 0.27, -0.1), Vector3(0.18, 0.12, 0.2))
	_box_mesh(_hood, cloth, Vector3(0, 0.12, 0.16), Vector3(0.27, 0.27, 0.02))
	_box_mesh(_hood, shadow, Vector3(0, 0.1, 0.172), Vector3(0.17, 0.19, 0.01))
	_box_mesh(_hood, skin, Vector3(0, 0.025, 0.168), Vector3(0.09, 0.035, 0.012))
	if smoking:
		var stem := _paint(Color("1a120d"), 0.6)
		_box_mesh(_hood, stem, Vector3(0.02, 0.035, 0.22), Vector3(0.012, 0.012, 0.1))
		_box_mesh(_hood, stem, Vector3(0.02, 0.05, 0.27), Vector3(0.032, 0.035, 0.032))
		_ember = _glowing(Color("ff5a1e"), 1.0)
		_box_mesh(_hood, _ember, Vector3(0.02, 0.07, 0.27), Vector3(0.024, 0.006, 0.024))
		_ember_light = OmniLight3D.new()
		_ember_light.position = Vector3(0.02, 0.1, 0.3)
		_ember_light.light_color = Color("ff6a2a")
		_ember_light.omni_range = 0.7
		_hood.add_child(_ember_light)
	_animate()


func _animate() -> void:
	super()
	if not _hood:
		return
	# Slow drift, with a long, suspicious look toward the chess corner every so often.
	var look := smoothstep(0.55, 0.85, sin(_elapsed * 0.21 + _phase))
	_hood.rotation.y = lerpf(0.12 * sin(_elapsed * 0.37 + _phase), _glance_yaw, look)
	_hood.rotation.x = 0.06 * sin(_elapsed * 0.53 + _phase * 2.0) - 0.1 * look
	_torso.scale = Vector3(1.0 + 0.01 * sin(_elapsed * 1.3 + _phase), 1.0, 1.0)
	if _ember:
		var draw := pow(maxf(0.0, sin(_elapsed * 0.8 + _phase)), 10.0)
		_ember.emission_energy_multiplier = 0.8 + 4.5 * draw
		_ember_light.light_energy = 0.08 + 0.55 * draw
