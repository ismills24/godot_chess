extends "res://common/scene_kit.gd"
## Someone sitting at a chess board waiting for a game: a modelled figure (rounded, tapering
## limbs, a face with eyes, brows, nose and lips, hands with fingers, cloth that creases), all
## generated from code so every place's opponent comes from one spec.
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

## Cloth: the colour broken by soft creases (which also bend the light), a fine weave, and
## optionally bands of stripes or the rings of mail. Back faces (inside a hood or a cuff) dark.
const CLOTH_CODE := """
shader_type spatial;
render_mode cull_disabled;

uniform vec4 albedo : source_color = vec4(0.5, 0.4, 0.3, 1.0);
uniform float roughness_value = 0.9;
uniform float metal = 0.0;
uniform float folds = 1.0;
uniform float strands = 0.0;
uniform float mail = 0.0;
uniform vec4 stripe_color : source_color = vec4(0.85, 0.85, 0.8, 1.0);
uniform float stripe_count = 0.0;
uniform float stripe_from = 0.0;
uniform float stripe_step = 0.1;
uniform float stripe_width = 0.035;

varying vec3 local_at;
varying vec3 local_normal;

float hash(vec2 p) {
	vec3 q = fract(p.xyx * vec3(0.1031, 0.11369, 0.13787));
	q += dot(q, q.yzx + 19.19);
	return fract((q.x + q.y) * q.z);
}

float noise(vec2 p) {
	vec2 c = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(c), hash(c + vec2(1, 0)), f.x), mix(hash(c + vec2(0, 1)), hash(c + vec2(1)), f.x), f.y);
}

float crease(vec3 p) {
	float broad = noise(vec2(p.x * 11.0 + p.z * 9.0, p.y * 3.0));
	float fine = noise(vec2(p.x * 27.0 - p.z * 23.0, p.y * 7.0 + 3.1));
	float hair = noise(vec2((p.x + p.z) * 110.0, p.y * 9.0)) * 0.6 + noise(vec2((p.x - p.z) * 47.0, p.y * 5.0)) * 0.4;
	return mix(broad * 0.65 + fine * 0.35, hair, strands);
}

void vertex() {
	local_at = VERTEX;
	local_normal = NORMAL;
}

void fragment() {
	vec3 p = local_at;
	float f = crease(p);
	float e = 0.003;
	vec3 slope = vec3(crease(p + vec3(e, 0, 0)) - f, crease(p + vec3(0, e, 0)) - f, crease(p + vec3(0, 0, e)) - f) / e;
	vec3 n = normalize(local_normal);
	slope -= n * dot(slope, n);
	vec3 bent = normalize(n - slope * 0.018 * folds * (1.0 - 0.8 * strands));
	if (!FRONT_FACING) {
		bent = -bent;
	}
	NORMAL = normalize((VIEW_MATRIX * (MODEL_MATRIX * vec4(bent, 0.0))).xyz);
	vec3 color = albedo.rgb * (0.86 + 0.24 * f * folds + (1.0 - folds) * 0.12);
	// A fine weave, too small to see as anything but texture.
	color *= 0.96 + 0.08 * noise(p.xy * 420.0 + p.z * 300.0);
	if (stripe_count > 0.0) {
		float k = clamp(floor((p.y - stripe_from) / stripe_step + 0.5), 0.0, stripe_count - 1.0);
		float d = abs(p.y - (stripe_from + k * stripe_step));
		color = mix(color, stripe_color.rgb * (0.9 + 0.15 * f), 1.0 - smoothstep(stripe_width * 0.5 - 0.002, stripe_width * 0.5 + 0.002, d));
	}
	float rough = roughness_value;
	if (mail > 0.0) {
		// Rings in offset rows: dark in each ring's hole, bright on its rim.
		vec2 q = vec2(atan(p.x, p.z) * 30.0, p.y * 160.0);
		q.x += 0.5 * mod(floor(q.y), 2.0);
		float r = length(fract(q) - 0.5);
		float ring = smoothstep(0.18, 0.26, r) * (1.0 - smoothstep(0.42, 0.5, r));
		color *= mix(0.5, 1.12, ring);
		rough = mix(0.7, roughness_value, ring);
	}
	if (!FRONT_FACING) {
		color *= 0.35;
	}
	ALBEDO = color;
	ROUGHNESS = rough;
	METALLIC = metal;
}
"""

static var _cloth_shader: Shader

var _phase := 0.0
var _hips: Node3D
var _torso: Node3D
var _head: Node3D
var _shoulders: Array[Node3D] = []
var _elbows: Array[Node3D] = []
var _wrists: Array[Node3D] = []
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

# The head: an egg narrowing to the jaw, centred HEAD_CENTRE above the head pivot.
const HEAD_CENTRE := Vector3(0, 0.13, 0)
const HEAD_RADII := Vector3(0.098, 0.128, 0.11)


func build(spec: Dictionary) -> void:
	_phase = spec.get("phase", 0.0)
	var h: float = spec.get("height", 1.0)
	var g: float = spec.get("girth", 1.0)
	var extras: Array = spec.get("extras", [])
	var skin: Color = spec.get("skin", Color("b08870"))
	var coat_color: Color = spec.get("coat", Color("5a4a3a"))
	var coat := _cloth(coat_color, 0.92)
	var sleeves := _cloth(spec.get("sleeves", coat_color), 0.92)
	if extras.has("mail"):
		sleeves = _cloth(Color("6a6c70"), 0.45, 0.7, 0.3)
		sleeves.set_shader_parameter("mail", 1.0)
	var legs := _cloth(spec.get("legs", Color("3a3028")), 0.95)
	var flesh := _skin(skin)
	var boots := _paint(Color("1e1712"), 0.55)
	var leather := _paint(Color("3a2618"), 0.6)
	var robe: bool = spec.get("robe", false)
	var ground: bool = spec.get("seat", "chair") == "ground"
	var seat_height: float = 0.1 if ground else spec.get("seat_height", 0.47)

	# Legs: on a seat the thighs run forward and the shins drop; on the ground they cross.
	_hips = _pivot(self, Vector3(0, seat_height + 0.06, -0.02))
	_soft(_hips, legs, Vector3(0, 0.0, 0.0), Vector3(0.36 * g, 0.17, 0.25 * g), 0.55)
	if ground:
		for side: int in [-1, 1]:
			var hip := Vector3(side * 0.11 * g, -0.02, 0.02)
			var knee := Vector3(side * 0.31 * g, -0.07, 0.24)
			var ankle := Vector3(-side * 0.12 * g, -0.1, 0.33 + side * 0.02)
			_limb(_hips, legs, hip, knee, 0.078 * g, 0.06 * g)
			_limb(_hips, legs, knee, ankle, 0.056 * g, 0.045 * g)
			var boot := _soft(_hips, boots, ankle + Vector3(-side * 0.07, 0.0, 0.0), Vector3(0.2, 0.08, 0.1), 0.5,
				Vector4(1.0, 0.85, 1.0, 0.85))
			boot.rotation.z = side * 1.5708
	else:
		for side: int in [-1, 1]:
			var x := side * 0.1 * g
			var knee := Vector3(x, -0.01, 0.4)
			var ankle := Vector3(x, -seat_height + 0.06, 0.41)
			_limb(_hips, legs, Vector3(x, 0.0, 0.02), knee, 0.08 * g, 0.064 * g)
			_limb(_hips, legs, knee, ankle, 0.058 * g, 0.044 * g)
			_boot(_hips, boots, Vector3(x, -seat_height - 0.02, 0.45))
	if robe:
		# The robe falls over the knees to the ground.
		if ground:
			_soft(_hips, coat, Vector3(0, -0.04, 0.14), Vector3(0.7 * g, 0.17, 0.6), 0.5, Vector4(1.0, 0.85, 1.0, 0.85))
		else:
			_soft(_hips, coat, Vector3(0, 0.01, 0.2), Vector3(0.4 * g, 0.18, 0.5), 0.5)
			_soft(_hips, coat, Vector3(0, -seat_height * 0.5 - 0.02, 0.43), Vector3(0.4 * g, seat_height + 0.08, 0.14),
				0.5, Vector4(1.12, 0.94, 1.1, 0.95))

	# Torso: a rounded trunk widening to the shoulders, settling into the hips.
	_torso = _pivot(_hips, Vector3.ZERO)
	var depth := 0.22 * g
	var trunk_height := 0.62 * h
	var torso_material := coat
	if extras.has("stripes"):
		torso_material = _cloth(coat_color, 0.92)
		torso_material.set_shader_parameter("stripe_color", spec.get("stripe_color", Color("d8d4c8")))
		torso_material.set_shader_parameter("stripe_count", 5.0)
		torso_material.set_shader_parameter("stripe_from", 0.12 - 0.3 * h)
		torso_material.set_shader_parameter("stripe_step", 0.09 * h)
		torso_material.set_shader_parameter("stripe_width", 0.035)
	_soft(_torso, torso_material, Vector3(0, 0.3 * h, 0), Vector3(0.43 * g, trunk_height, depth + 0.03), 0.5,
		Vector4(0.86, 1.0, 1.0, 0.86), 0.55, _trunk)
	if robe:
		_soft(_torso, coat, Vector3(0, 0.05, 0), Vector3(0.42 * g, 0.16, depth + 0.06), 0.5)
	if extras.has("tabard"):
		var tabard := _cloth(spec.get("tabard_color", Color("8a1e1a")), 0.9)
		var trim := _paint(spec.get("trim_color", Color("c8a040")), 0.4, 0.6)
		for face: int in [-1, 1]:
			var panel := _soft(_torso, tabard, Vector3(0, 0.22 * h, face * (depth * 0.5 + 0.012)), Vector3(0.31 * g, 0.64 * h, 0.014),
				0.3, Vector4(1.0, 0.9, 1.0, 1.0))
			panel.rotation.x = -face * 0.04
		# A tower on the chest, the town's badge.
		var badge_z := depth * 0.5 + 0.026
		_soft(_torso, trim, Vector3(0, 0.36 * h, badge_z), Vector3(0.1, 0.13, 0.008), 0.3)
		for merlon: int in [-1, 0, 1]:
			_soft(_torso, trim, Vector3(merlon * 0.035, 0.36 * h + 0.08, badge_z), Vector3(0.022, 0.032, 0.008), 0.3)
		_soft(_torso, _paint(Color("1a1410"), 0.6), Vector3(0, 0.36 * h - 0.025, badge_z + 0.004), Vector3(0.026, 0.05, 0.004), 0.4)
		_soft(_torso, trim, Vector3(0, 0.08, depth * 0.5 + 0.022), Vector3(0.31 * g, 0.026, 0.008), 0.3)
		# The tabard's skirt over the thighs.
		if not ground:
			var skirt := _soft(_hips, tabard, Vector3(0, 0.1, 0.17), Vector3(0.3 * g, 0.014, 0.36), 0.3, Vector4(0.95, 1.0, 1.0, 1.0))
			skirt.rotation.x = 0.06
	# Belt and buckle.
	_soft(_torso, leather, Vector3(0, 0.05, 0), Vector3(0.38 * g, 0.055, depth + 0.035), 0.45)
	_soft(_torso, _paint(Color("b09050"), 0.35, 0.8), Vector3(0, 0.05, depth * 0.5 + 0.02), Vector3(0.045, 0.045, 0.012), 0.4)
	if extras.has("belt_pouches"):
		for side: int in [-1, 1]:
			var pouch := Vector3(side * 0.14 * g, -0.005, depth * 0.5 + 0.03)
			_soft(_torso, leather, pouch, Vector3(0.08, 0.09, 0.05), 0.45, Vector4(1.0, 0.92, 1.0, 0.8))
			_soft(_torso, _paint(Color("2e1e12"), 0.6), pouch + Vector3(0, 0.03, 0.006), Vector3(0.084, 0.035, 0.05), 0.4)
	if extras.has("scarf"):
		var scarf := _cloth(spec.get("scarf_color", Color("7a2a20")), 0.95, 0.0, 1.4)
		_ring(_torso, scarf, Vector3(0, 0.6 * h, 0.0), 0.062, 0.135, 0.55)
		var tail := _soft(_torso, scarf, Vector3(0.07, 0.47 * h, depth * 0.5 + 0.03), Vector3(0.07, 0.21, 0.022), 0.45,
			Vector4(1.15, 0.9, 1.0, 1.0))
		tail.rotation.z = 0.12
	if extras.has("beads"):
		var bead := _paint(Color("4a2a18"), 0.4)
		for k in 11:
			var angle := (k - 5) * 0.22
			_ball(_torso, bead, Vector3(sin(angle) * 0.115, 0.5 * h - cos(angle) * 0.1 + 0.08, depth * 0.5 + 0.014 - absf(sin(angle)) * 0.02), 0.013)
		var pendant := _soft(_torso, _paint(Color("8a6a3a"), 0.35, 0.5), Vector3(0, 0.39 * h, depth * 0.5 + 0.018), Vector3(0.04, 0.05, 0.012), 0.6)
		pendant.rotation.z = 0.785
	# Neck and collar.
	_limb(_torso, flesh, Vector3(0, 0.55 * h, 0.0), Vector3(0, 0.65 * h, 0.012), 0.05, 0.046)
	if not extras.has("scarf"):
		_ring(_torso, coat, Vector3(0, 0.6 * h, 0.0), 0.05, 0.085, 0.45)

	_head = _pivot(_torso, Vector3(0, 0.62 * h, 0.01))
	_build_head(spec, extras, skin, flesh)

	# Arms: shoulder and elbow pivots; at rest the forearms come forward toward the board.
	for side: int in [1, -1]:
		var shoulder := _pivot(_torso, Vector3(side * 0.2 * g, 0.53 * h, 0))
		var wide := 1.35 if robe else 1.0
		var upper := 0.28 * h
		var fore := 0.22 * h
		_limb(shoulder, sleeves, Vector3.ZERO, Vector3(0, -upper, 0), 0.066 * g * wide, 0.052 * g * wide)
		var elbow := _pivot(shoulder, Vector3(0, -upper, 0))
		_limb(elbow, sleeves, Vector3(0, 0.005, 0), Vector3(0, -fore + 0.02, 0), 0.052 * g * wide, 0.043 * g * wide)
		if robe:
			# Wide cuffs hanging open.
			var cuff := CylinderMesh.new()
			cuff.top_radius = 0.05 * g
			cuff.bottom_radius = 0.085
			cuff.height = 0.11
			cuff.radial_segments = 32
			cuff.rings = 2
			cuff.cap_top = false
			cuff.cap_bottom = false
			_mesh(elbow, cuff, sleeves, Vector3(0, -fore + 0.02, 0.0))
		else:
			_ring(elbow, sleeves, Vector3(0, -fore + 0.015, 0), 0.034, 0.05, 0.9).rotation.x = 0.0
		var wrist := _pivot(elbow, Vector3(0, -fore, 0))
		_hand(wrist, flesh, Vector3.ZERO, side)
		_shoulders.append(shoulder)
		_elbows.append(elbow)
		_wrists.append(wrist)
		_rest_arm.append(Vector2(-0.55, -0.95))
	_animate()


## A hand hanging from `wrist` along -Y, palm toward -Z (down onto the table when the forearm
## rests forward), fingers slightly curled, the thumb on the body's side (`side` is the arm's x).
func _hand(parent: Node3D, flesh: Material, wrist: Vector3, side: int) -> void:
	_limb(parent, flesh, wrist + Vector3(0, 0.012, 0), wrist + Vector3(0, -0.012, 0), 0.03, 0.032)
	var palm_at := wrist + Vector3(0, -0.045, -0.002)
	var nails := _skin((flesh as StandardMaterial3D).albedo_color.lightened(0.18))
	nails.roughness = 0.35
	_soft(parent, flesh, palm_at, Vector3(0.074, 0.08, 0.03), 0.55, Vector4(1.05, 0.92, 1.0, 0.85))
	var lengths := [0.047, 0.054, 0.051, 0.041]
	for k in 4:
		var x := (k - 1.5) * 0.0166 * -side
		var knuckle := palm_at + Vector3(x, -0.036, 0.002)
		var length: float = lengths[k]
		var mid := knuckle + Vector3(x * 0.02, -length * 0.5, -0.017)
		var tip := mid + Vector3(0.0, -length * 0.2, -0.032)
		_limb(parent, flesh, knuckle, mid, 0.0086, 0.0076)
		_limb(parent, flesh, mid, tip, 0.0076, 0.0064)
		# A nail on the back of each fingertip.
		var nail := _soft(parent, nails, tip.lerp(mid, 0.25) + (tip - mid).cross(Vector3.RIGHT).normalized() * 0.0058, Vector3(0.011, 0.012, 0.003), 0.6)
		nail.basis = _along(tip - mid)
	var thumb_root := palm_at + Vector3(-side * 0.03, 0.02, -0.006)
	var thumb_mid := thumb_root + Vector3(-side * 0.02, -0.03, -0.012)
	_limb(parent, flesh, thumb_root, thumb_mid, 0.013, 0.0105)
	_limb(parent, flesh, thumb_mid, thumb_mid + Vector3(-side * 0.008, -0.028, -0.008), 0.0105, 0.0085)


## A boot standing at `at` (its sole), toe toward +Z.
func _boot(parent: Node3D, leather: Material, at: Vector3) -> void:
	_soft(parent, leather, at + Vector3(0, 0.045, 0.0), Vector3(0.12, 0.09, 0.25), 0.45, Vector4(1.0, 0.92, 1.0, 0.8))
	_soft(parent, leather, at + Vector3(0, 0.1, -0.04), Vector3(0.11, 0.12, 0.12), 0.5)
	_soft(parent, _paint(Color("120d0a"), 0.8), at + Vector3(0, 0.006, 0.0), Vector3(0.125, 0.018, 0.255), 0.4)


func _build_head(spec: Dictionary, extras: Array, skin: Color, flesh: Material) -> void:
	var shade := _skin(skin.darkened(0.14))
	var dark := _paint(Color("140c0a"), 0.5)
	var hair_color: Color = spec.get("hair", Color("2a2018"))
	var brow_color: Color = spec.get("beard_color", hair_color) if spec.has("beard_color") or spec.has("hair") else Color("3a2a20")
	_soft(_head, flesh, HEAD_CENTRE, HEAD_RADII * 2.0, 0.92, Vector4(1, 1, 1, 1), -1.0, _jaw)
	# Ears.
	for side: int in [-1, 1]:
		var ear := _soft(_head, flesh, Vector3(side * 0.1, 0.13, -0.008), Vector3(0.02, 0.056, 0.036), 0.8)
		ear.rotation.y = side * 0.3
		_soft(_head, shade, Vector3(side * 0.106, 0.13, -0.004), Vector3(0.008, 0.034, 0.02), 0.8)
	# Eyes: whites, irises, lids and brows, set into the face.
	var white := _paint(Color("e8e0d4"), 0.25)
	for side: int in [-1, 1]:
		var x := side * 0.039
		var y := 0.148
		var z := _face_z(x, y) - 0.009
		var eye := _ball(_head, white, Vector3(x, y, z), 0.016)
		eye.scale = Vector3(1.0, 0.72, 0.8)
		_ball(_head, dark, Vector3(x, y, z + 0.0095), 0.0068)
		var lid := _soft(_head, shade, Vector3(x, y + 0.008, z + 0.004), Vector3(0.036, 0.013, 0.026), 0.8)
		lid.rotation.x = -0.25
		_soft(_head, shade, Vector3(x, y - 0.013, z + 0.004), Vector3(0.032, 0.007, 0.022), 0.85)
		var brow := _soft(_head, _cloth(brow_color, 0.95, 0.0, 0.6), Vector3(x + side * 0.003, 0.172, _face_z(x, 0.172) + 0.002),
			Vector3(0.042, 0.01, 0.014), 0.6, Vector4(1.0, 0.75, 1.0, 1.0))
		brow.rotation.z = -side * 0.12
	# Brow ridge and cheekbones give the face some planes.
	_soft(_head, flesh, Vector3(0, 0.168, _face_z(0, 0.168) - 0.012), Vector3(0.13, 0.025, 0.03), 0.85)
	for side: int in [-1, 1]:
		_soft(_head, flesh, Vector3(side * 0.05, 0.112, _face_z(side * 0.05, 0.112) - 0.014), Vector3(0.05, 0.035, 0.035), 0.9)
	# Nose: bridge, tip and the wings of the nostrils.
	var nose_z := _face_z(0, 0.12)
	_soft(_head, flesh, Vector3(0, 0.125, nose_z + 0.004), Vector3(0.022, 0.058, 0.028), 0.75, Vector4(1.15, 0.7, 1.2, 0.6))
	_ball(_head, flesh, Vector3(0, 0.1, nose_z + 0.017), 0.0135)
	for side: int in [-1, 1]:
		_ball(_head, shade, Vector3(side * 0.013, 0.096, nose_z + 0.009), 0.0095)
	# Mouth.
	var mouth_z := _face_z(0, 0.064)
	var lips := _skin(skin.lerp(Color("8a3a32"), 0.3).darkened(0.08))
	_soft(_head, lips, Vector3(0, 0.07, mouth_z + 0.002), Vector3(0.05, 0.01, 0.012), 0.6, Vector4(1.0, 0.85, 1.0, 1.0))
	_soft(_head, lips, Vector3(0, 0.058, mouth_z + 0.001), Vector3(0.044, 0.012, 0.013), 0.6)
	_soft(_head, _paint(Color("2a1512"), 0.6), Vector3(0, 0.0645, mouth_z + 0.004), Vector3(0.052, 0.004, 0.008), 0.5)
	# Chin.
	_ball(_head, flesh, Vector3(0, 0.03, _face_z(0, 0.03) - 0.012), 0.024).scale = Vector3(1.2, 0.9, 1.0)

	var hair := _cloth(hair_color, 0.85, 0.0, 1.0)
	hair.set_shader_parameter("strands", 1.0)
	if spec.has("hair"):
		if spec.get("bald", false):
			# A horseshoe round the back and sides, the crown bare.
			_soft(_head, hair, HEAD_CENTRE + Vector3(0, -0.006, -0.008), HEAD_RADII * 2.0 * Vector3(1.07, 1.0, 1.05), 0.9,
				Vector4(1, 1, 1, 1), -1.0, _jaw, func(d: Vector3) -> bool: return d.y > 0.35 or d.z > 0.2 or d.y < -0.35)
		else:
			_soft(_head, hair, HEAD_CENTRE + Vector3(0, 0.012, -0.012), HEAD_RADII * 2.0 * Vector3(1.08, 1.05, 1.07), 0.9,
				Vector4(1, 1, 1, 1), -1.0, _jaw, func(d: Vector3) -> bool: return d.y < -0.45 or (d.z > 0.35 and d.y < 0.45))
	var beard: String = spec.get("beard", "none")
	var whiskers := _cloth(spec.get("beard_color", hair_color), 0.9, 0.0, 1.0)
	whiskers.set_shader_parameter("strands", 1.0)
	var lower_face := func(d: Vector3) -> bool: return d.y > -0.05 or d.z < -0.25
	match beard:
		"stubble":
			var rough := _skin(skin.lerp(spec.get("beard_color", hair_color), 0.45))
			_soft(_head, rough, HEAD_CENTRE, HEAD_RADII * 2.0 * 1.02, 0.92, Vector4(1, 1, 1, 1), -1.0, _jaw, lower_face)
		"full":
			_soft(_head, whiskers, HEAD_CENTRE + Vector3(0, -0.004, 0.006), HEAD_RADII * 2.0 * Vector3(1.1, 1.06, 1.12), 0.88,
				Vector4(1, 1, 1, 1), -1.0, _jaw, lower_face)
			_soft(_head, whiskers, Vector3(0, -0.005, 0.078), Vector3(0.14, 0.09, 0.07), 0.8, Vector4(0.7, 1.0, 0.8, 1.0))
		"long":
			# A long beard spilling down onto the chest.
			_soft(_head, whiskers, HEAD_CENTRE + Vector3(0, -0.004, 0.006), HEAD_RADII * 2.0 * Vector3(1.1, 1.06, 1.12), 0.88,
				Vector4(1, 1, 1, 1), -1.0, _jaw, lower_face)
			_soft(_head, whiskers, Vector3(0, -0.07, 0.1), Vector3(0.16, 0.16, 0.075), 0.75, Vector4(0.75, 1.0, 0.85, 1.0))
			_soft(_head, whiskers, Vector3(0, -0.2, 0.125), Vector3(0.12, 0.16, 0.055), 0.75, Vector4(0.6, 1.0, 0.8, 1.0))
			_soft(_head, whiskers, Vector3(0.008, -0.31, 0.135), Vector3(0.065, 0.11, 0.04), 0.8, Vector4(0.4, 1.0, 0.6, 1.0))
	if beard == "full" or beard == "long":
		# A moustache sweeping out from under the nose, the mouth showing beneath.
		for side: int in [-1, 1]:
			_limb(_head, whiskers, Vector3(side * 0.004, 0.083, mouth_z + 0.014), Vector3(side * 0.035, 0.066, mouth_z + 0.004), 0.009, 0.005)
		_soft(_head, _paint(Color("2a1512"), 0.6), Vector3(0, 0.062, mouth_z + 0.012), Vector3(0.05, 0.008, 0.006), 0.5)
	if extras.has("spectacles"):
		var rim := _paint(Color("8a6a2a"), 0.3, 0.85)
		var lens := _glowing(Color("9fd8ff"), 0.6)
		lens.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		lens.albedo_color.a = 0.45
		for side: int in [-1, 1]:
			var at := Vector3(side * 0.042, 0.148, _face_z(side * 0.039, 0.148) + 0.016)
			_ring(_head, rim, at, 0.019, 0.023, 1.0).rotation.x = PI / 2
			var glass := CylinderMesh.new()
			glass.top_radius = 0.02
			glass.bottom_radius = 0.02
			glass.height = 0.002
			glass.radial_segments = 24
			_mesh(_head, glass, lens, at).rotation.x = PI / 2
			# Arms back to the ears.
			_limb(_head, rim, at + Vector3(side * 0.022, 0.0, -0.004), Vector3(side * 0.1, 0.15, -0.02), 0.0022, 0.0022)
		_limb(_head, rim, Vector3(-0.02, 0.152, _face_z(0, 0.15) + 0.014), Vector3(0.02, 0.152, _face_z(0, 0.15) + 0.014), 0.0025, 0.0025)
	if extras.has("eyepatch"):
		var patch := _soft(_head, dark, Vector3(0.042, 0.148, _face_z(0.04, 0.148) + 0.004), Vector3(0.05, 0.045, 0.016), 0.6)
		patch.rotation.z = -0.3
		var strap := _ring(_head, dark, HEAD_CENTRE + Vector3(0, 0.03, 0), 0.104, 0.108, 1.0)
		strap.scale = Vector3(1.0, 1.0, 1.13)
		strap.rotation = Vector3(0.1, 0.0, -0.3)
	if extras.has("earring"):
		_ring(_head, _paint(Color("d0a040"), 0.25, 0.95), Vector3(-0.104, 0.085, 0.0), 0.006, 0.009, 1.0).rotation.z = PI / 2
	if extras.has("pipe"):
		var pipe_wood := _paint(Color("3a2014"), 0.45)
		var bowl_at := Vector3(0.04, 0.05, mouth_z + 0.11)
		_limb(_head, pipe_wood, Vector3(0.012, 0.064, mouth_z + 0.004), bowl_at + Vector3(0, -0.005, -0.012), 0.005, 0.0055)
		_round(_head, bowl_at, 0.014, 0.018, 0.045, pipe_wood)
		_pipe_ember = _glowing(Color("ff6a20"), 2.0)
		_round(_head, bowl_at + Vector3(0, 0.0215, 0), 0.0145, 0.0145, 0.003, _pipe_ember)
	var hat_color: Color = spec.get("hat_color", Color("2a241c"))
	var felt := _cloth(hat_color, 0.95, 0.0, 1.3)
	match spec.get("hat", "none"):
		"hood", "wizard_hood":
			# A deep hood framing the face, open at the front, falling onto the shoulders.
			_soft(_head, felt, HEAD_CENTRE + Vector3(0, 0.015, -0.02), Vector3(0.28, 0.33, 0.31), 0.85, Vector4(0.95, 1.0, 1.0, 1.0),
				-1.0, Callable(), func(d: Vector3) -> bool: return (d.z > 0.28 and absf(d.x) < 0.8 and d.y < 0.62) or d.y < -0.7)
			_soft(_head, felt, Vector3(0, -0.03, -0.04), Vector3(0.4, 0.11, 0.34), 0.6, Vector4(1.0, 0.65, 1.0, 0.7))
			if spec["hat"] == "wizard_hood":
				# Its peak flops back and over to one side.
				var peak := _pivot(_head, Vector3(0, 0.27, -0.05))
				peak.rotation = Vector3(-0.5, 0, 0.35)
				var a := Vector3.ZERO
				var b := Vector3(0, 0.12, -0.02)
				var c := Vector3(-0.03, 0.21, -0.07)
				var d := Vector3(-0.09, 0.25, -0.12)
				_limb(peak, felt, a, b, 0.085, 0.06)
				_limb(peak, felt, b, c, 0.06, 0.035)
				_limb(peak, felt, c, d, 0.035, 0.012)
		"straw":
			var straw := _cloth(hat_color, 0.95, 0.0, 0.5)
			straw.set_shader_parameter("strands", 0.6)
			var band := _cloth(Color("5a2418"), 0.9)
			var hat := _pivot(_head, Vector3(0, 0.25, -0.01))
			hat.rotation = Vector3(-0.12, 0, 0.06)
			_round(hat, Vector3.ZERO, 0.24, 0.235, 0.012, straw)
			_ring(hat, straw, Vector3(0, 0.002, 0), 0.226, 0.246, 0.5)
			_round(hat, Vector3(0, 0.05, 0), 0.118, 0.1, 0.09, straw)
			_soft(hat, straw, Vector3(0, 0.095, 0), Vector3(0.2, 0.025, 0.2), 0.9)
			_round(hat, Vector3(0, 0.02, 0), 0.121, 0.117, 0.026, band)
		"cap":
			var cap := _pivot(_head, Vector3(0, 0.245, 0))
			cap.rotation = Vector3(-0.08, 0, -0.05)
			_soft(cap, felt, Vector3(0, 0.0, -0.005), Vector3(0.25, 0.1, 0.26), 0.7, Vector4(1.0, 0.9, 1.0, 0.9))
			_ring(cap, felt, Vector3(0, -0.035, -0.005), 0.112, 0.13, 0.7)
			var peak := _soft(cap, felt, Vector3(0, -0.03, 0.13), Vector3(0.18, 0.014, 0.085), 0.5)
			peak.rotation.x = 0.15
		"helm":
			var iron := _paint(hat_color, 0.32, 0.85)
			var helm := _pivot(_head, Vector3(0, 0.22, 0))
			_round(helm, Vector3(0, 0.0, 0), 0.215, 0.205, 0.016, iron)
			_ring(helm, iron, Vector3(0, -0.006, 0), 0.2, 0.22, 0.6)
			_round(helm, Vector3(0, 0.055, 0), 0.125, 0.117, 0.1, iron)
			_ring(helm, _paint(hat_color.darkened(0.25), 0.4, 0.8), Vector3(0, 0.012, 0), 0.122, 0.132, 0.8)
			var dome := SphereMesh.new()
			dome.radius = 0.117
			dome.height = 0.13
			dome.radial_segments = 40
			dome.rings = 20
			_mesh(helm, dome, iron, Vector3(0, 0.105, 0))
			_soft(helm, _paint(hat_color.darkened(0.4), 0.45, 0.7), Vector3(0, 0.125, 0), Vector3(0.02, 0.06, 0.25), 0.6)
			for k in 8:
				var angle := TAU * k / 8.0
				_ball(helm, iron, Vector3(sin(angle) * 0.126, 0.012, cos(angle) * 0.126), 0.007)
		"bandana":
			_soft(_head, felt, HEAD_CENTRE + Vector3(0, 0.02, -0.004), HEAD_RADII * 2.0 * 1.07, 0.9, Vector4(1, 1, 1, 1), -1.0,
				_jaw, func(d: Vector3) -> bool: return d.y < 0.3 - 0.25 * maxf(0.0, -d.z))
			var knot := _ball(_head, felt, Vector3(0, 0.19, -0.115), 0.024)
			knot.scale = Vector3(1.3, 1.0, 0.8)
			for side: int in [-1, 1]:
				var tail := _soft(_head, felt, Vector3(side * 0.02, 0.15, -0.125), Vector3(0.035, 0.09, 0.01), 0.5, Vector4(0.6, 1.0, 1.0, 1.0))
				tail.rotation = Vector3(0.35, 0, side * 0.3)


## The trunk's shape up its height: the chest filling out, the shoulders sloping to the neck.
static func _trunk(t: float) -> Vector2:
	var chest := 1.0 + 0.06 * sin(clampf((t - 0.35) / 0.5, 0.0, 1.0) * PI)
	var slope := 1.0 - 0.3 * smoothstep(0.84, 1.0, t)
	return Vector2(slope, chest * (1.0 - 0.15 * smoothstep(0.85, 1.0, t)))


## How the head narrows to the jaw and chin: width and depth multipliers at height t (0 chin, 1 crown).
static func _jaw(t: float) -> Vector2:
	return Vector2(lerpf(0.68, 1.0, smoothstep(0.0, 0.6, t)), lerpf(0.84, 1.0, smoothstep(0.0, 0.55, t)))


## The front of the face (head local z) at (x, y), so features sit on it.
static func _face_z(x: float, y: float) -> float:
	var v := clampf((y - HEAD_CENTRE.y) / HEAD_RADII.y, -1.0, 1.0)
	var taper := _jaw((v + 1.0) * 0.5)
	var u := x / (HEAD_RADII.x * taper.x)
	return HEAD_CENTRE.z + sqrt(maxf(0.0, 1.0 - u * u - v * v)) * HEAD_RADII.z * taper.y


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
			# The wrist lifts so the fingers point at the piece.
			_wrists[side].rotation.x = -0.7 * _reach
		else:
			# Now and then a hand comes up to rub the chin.
			var chin := 0.0
			if side == 1:
				var into := fposmod(t + _phase * 2.0, 9.0)
				chin = sin(into / 2.6 * PI) if into < 2.6 else 0.0
				chin *= 1.0 - _look
			shoulder.quaternion = rest.slerp(Quaternion(Basis.from_euler(Vector3(-0.75, 0.35, -0.1))), chin)
			_elbows[side].rotation.x = lerpf(elbow_rest, -2.2, chin)
			_wrists[side].rotation.x = 0.0


# --- Building blocks -------------------------------------------------------------------------

static func _cloth(color: Color, roughness: float, metallic := 0.0, folds := 1.0) -> ShaderMaterial:
	if not _cloth_shader:
		_cloth_shader = Shader.new()
		_cloth_shader.code = CLOTH_CODE
	var material := ShaderMaterial.new()
	material.shader = _cloth_shader
	material.set_shader_parameter("albedo", color)
	material.set_shader_parameter("roughness_value", roughness)
	material.set_shader_parameter("metal", metallic)
	material.set_shader_parameter("folds", folds)
	return material


static func _skin(color: Color) -> StandardMaterial3D:
	var material := _paint(color, 0.62)
	material.rim_enabled = true
	material.rim = 0.25
	material.rim_tint = 0.6
	return material


## A basis whose +Y runs along `direction`.
static func _along(direction: Vector3) -> Basis:
	var y := direction.normalized()
	var x := Vector3.RIGHT if absf(y.x) < 0.9 else Vector3.BACK
	var z := x.cross(y).normalized()
	x = y.cross(z).normalized()
	return Basis(x, y, z)


## A rounded solid `size` across, made from a sphere pushed toward a box: `square` 1 is an
## ellipsoid, lower is boxier (sides) and `square_y` the same for the ends (defaults to
## `square`). `taper` scales width (x bottom, y top) and depth (z bottom, w top) up its height;
## `profile` (t -> Vector2 width, depth; t 0 bottom 1 top) shapes it further. `skip` (unit
## direction -> bool) cuts away triangles, for shells like hoods and hair.
static func _soft_mesh(size: Vector3, square := 0.5, taper := Vector4(1, 1, 1, 1), square_y := -1.0,
		profile := Callable(), skip := Callable(), segments := 32, rings := 18) -> ArrayMesh:
	var sphere := SphereMesh.new()
	sphere.radius = 1.0
	sphere.height = 2.0
	sphere.radial_segments = segments
	sphere.rings = rings
	var arrays := sphere.get_mesh_arrays()
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	var ey := square if square_y < 0.0 else square_y
	var shaped := PackedVector3Array()
	shaped.resize(vertices.size())
	for k in vertices.size():
		var v := vertices[k]
		var q := Vector3(signf(v.x) * pow(absf(v.x), square), signf(v.y) * pow(absf(v.y), ey), signf(v.z) * pow(absf(v.z), square))
		var t := (q.y + 1.0) * 0.5
		var scale := Vector2(lerpf(taper.x, taper.y, t), lerpf(taper.z, taper.w, t))
		if profile.is_valid():
			scale *= profile.call(t)
		shaped[k] = Vector3(q.x * scale.x * size.x * 0.5, q.y * size.y * 0.5, q.z * scale.y * size.z * 0.5)
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_smooth_group(0)
	for k in range(0, indices.size(), 3):
		if skip.is_valid():
			var centre := (vertices[indices[k]] + vertices[indices[k + 1]] + vertices[indices[k + 2]]) / 3.0
			if skip.call(centre.normalized()):
				continue
		for corner in 3:
			var index := indices[k + corner]
			tool.set_uv(Vector2(vertices[index].x, vertices[index].y))
			tool.add_vertex(shaped[index])
	tool.generate_normals()
	return tool.commit()


func _soft(parent: Node3D, material: Material, at: Vector3, size: Vector3, square := 0.5, taper := Vector4(1, 1, 1, 1),
		square_y := -1.0, profile := Callable(), skip := Callable()) -> MeshInstance3D:
	var small := maxf(size.x, maxf(size.y, size.z)) < 0.06
	var mesh := _soft_mesh(size, square, taper, square_y, profile, skip, 16 if small else 32, 10 if small else 18)
	return _mesh(parent, mesh, material, at)


## A limb from `from` to `to`, `from_radius` thick tapering to `to_radius`, its ends rounded.
func _limb(parent: Node3D, material: Material, from: Vector3, to: Vector3, from_radius: float, to_radius: float) -> MeshInstance3D:
	var length := from.distance_to(to)
	var widest := maxf(from_radius, to_radius)
	var total := length + from_radius + to_radius
	var small := widest < 0.02
	var mesh := _soft_mesh(Vector3(widest * 2.0, total, widest * 2.0), 1.0,
		Vector4(from_radius / widest, to_radius / widest, from_radius / widest, to_radius / widest), 0.55,
		Callable(), Callable(), 14 if small else 28, 10 if small else 16)
	var node := _mesh(parent, mesh, material, from.lerp(to, 0.5) + (to - from).normalized() * (to_radius - from_radius) * 0.5)
	node.basis = _along(to - from)
	return node


func _ball(parent: Node3D, material: Material, at: Vector3, radius: float) -> MeshInstance3D:
	var sphere := SphereMesh.new()
	sphere.radius = radius
	sphere.height = radius * 2.0
	sphere.radial_segments = 16 if radius < 0.02 else 28
	sphere.rings = 8 if radius < 0.02 else 14
	return _mesh(parent, sphere, material, at)


## A flat ring (collar, cuff, brim lip, rim), lying level, `flatten` squashing its height.
func _ring(parent: Node3D, material: Material, at: Vector3, inner: float, outer: float, flatten := 1.0) -> MeshInstance3D:
	var torus := TorusMesh.new()
	torus.inner_radius = inner
	torus.outer_radius = outer
	torus.rings = 48
	torus.ring_segments = 12
	var node := _mesh(parent, torus, material, at)
	node.scale = Vector3(1.0, flatten, 1.0)
	return node


## A smooth upright cylinder or cone (brims, crowns, pipe bowls).
func _round(parent: Node3D, at: Vector3, foot: float, top: float, height: float, material: Material) -> MeshInstance3D:
	var cylinder := CylinderMesh.new()
	cylinder.bottom_radius = foot
	cylinder.top_radius = top
	cylinder.height = height
	cylinder.radial_segments = 48
	cylinder.rings = 1
	return _mesh(parent, cylinder, material, at)
