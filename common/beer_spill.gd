extends Node3D
## Beer spilled onto board squares: the splash, and the puddle it leaves on each square.
## A puddle is how a Slippery square is shown. Works at any board scale (`square_size`), so the
## inn's small wooden set (inn/chess_set.gd) and the sandbox's big board (BoardView3D) share it.
## Squares are identified by a key (the game's square index) and placed at a point in this
## node's parent space: the centre of the square on the playing surface.
##
## - `spill(key, at)` throws beer at a square: drops arc in from the far side (`from_side`),
##   splash out on landing, and the puddle spreads; it then stays until `dry(key)`.
## - `dry(key)` dries a puddle off: the foam goes, it shrinks a little and fades away.
## - `sync(points)` shows exactly the puddles in `points` ({key: centre}), spilling new ones and
##   drying the ones that went. It only ever dries puddles it made itself, so spills made by
##   hand (a demo, a scripted moment) are left alone.

## A puddle has stopped spreading (its splash has settled).
signal settled(key: int)
## A puddle has dried off and gone.
signal dried(key: int)

const PUDDLE_SHADER := preload("res://common/beer_puddle.gdshader")
const SPILL_SECONDS := 0.6
const DRY_SECONDS := 0.9
## Seconds between squares when several are spilled at once.
const STAGGER := 0.11

## The width of one board square.
var square_size := 1.0
## How far above the surface the puddles lie (under the pieces' contact shadows).
var lift := 0.0
## The direction, across the board, the beer is thrown from (the far side by default).
var from_side := Vector3(0, 0, -1)
## Visual layers for every puddle and drop.
var layers := 1:
	set = set_layers
## No flying drops: the puddle just spreads.
var reduced_motion := false

## key -> the puddle's MeshInstance3D.
var _puddles := {}
## Keys of the puddles `sync` made (and so may dry).
var _synced := {}
var _drop_material: StandardMaterial3D
var _foam_drop_material: StandardMaterial3D
var _drop_mesh: SphereMesh


## Throws beer at square `key`, centred at `at`, after `delay` seconds. With `animate` false the
## puddle is simply there. Spilling where there's already a puddle splashes it again.
func spill(key: int, at: Vector3, delay := 0.0, animate := true) -> void:
	var puddle: MeshInstance3D = _puddles.get(key)
	var fresh := puddle == null or puddle.has_meta("drying")
	if fresh:
		if puddle:
			_remove(key)
		puddle = _make_puddle(key, at)
	var material := puddle.material_override as ShaderMaterial
	if not animate:
		material.set_shader_parameter("spread", 1.0)
		material.set_shader_parameter("foam", 0.4)
		settled.emit(key)
		return
	if fresh:
		material.set_shader_parameter("spread", 0.0)
	var land := 0.0 if reduced_motion else _pour(at, delay)
	_stop_spill(puddle)
	var tween := puddle.create_tween()
	puddle.set_meta("spill", tween)
	tween.tween_interval(delay + land)
	if not reduced_motion:
		tween.tween_callback(_splash.bind(at))
	tween.set_parallel()
	tween.tween_method(func(v: float) -> void: material.set_shader_parameter("spread", v),
		material.get_shader_parameter("spread"), 1.0, SPILL_SECONDS).set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	tween.tween_method(func(v: float) -> void: material.set_shader_parameter("foam", v),
		1.0, 0.4, 2.2).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
	tween.chain().tween_callback(func() -> void: settled.emit(key))


## Spills on several squares at once, a beat apart: `points` is {key: centre}.
func spill_all(points: Dictionary, animate := true) -> void:
	var i := 0
	for key: int in points:
		spill(key, points[key], i * STAGGER, animate)
		i += 1


## Dries the puddle on `key` off (fading, or at once with `animate` false).
func dry(key: int, animate := true) -> void:
	_synced.erase(key)
	var puddle: MeshInstance3D = _puddles.get(key)
	if puddle == null or puddle.has_meta("drying"):
		return
	if not animate:
		_remove(key)
		dried.emit(key)
		return
	puddle.set_meta("drying", true)
	_stop_spill(puddle)
	var spread: float = (puddle.material_override as ShaderMaterial).get_shader_parameter("spread")
	var material := puddle.material_override as ShaderMaterial
	var tween := puddle.create_tween().set_parallel()
	tween.tween_method(func(v: float) -> void: material.set_shader_parameter("foam", v),
		material.get_shader_parameter("foam"), 0.0, DRY_SECONDS * 0.4)
	tween.tween_method(func(v: float) -> void: material.set_shader_parameter("spread", v),
		spread, spread * 0.82, DRY_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)
	tween.tween_method(func(v: float) -> void: material.set_shader_parameter("amount", v),
		1.0, 0.0, DRY_SECONDS).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN)
	tween.chain().tween_callback(func() -> void:
		if _puddles.get(key) == puddle:
			_remove(key)
		dried.emit(key))


## Dries every puddle.
func dry_all(animate := true) -> void:
	for key: int in _puddles.keys():
		dry(key, animate)


func has_puddle(key: int) -> bool:
	return _puddles.has(key) and not (_puddles[key] as Node).has_meta("drying")


func puddle_keys() -> Array:
	return _puddles.keys().filter(has_puddle)


## Shows a puddle on exactly the squares in `points` ({key: centre}): new ones are spilled
## (animated, a beat apart), ones no longer listed dry off. Puddles spilled by hand are kept.
func sync(points: Dictionary, animate := true) -> void:
	for key: int in _synced.keys():
		if not points.has(key):
			dry(key, animate)
	var fresh := {}
	for key: int in points:
		if not has_puddle(key):
			fresh[key] = points[key]
		_synced[key] = true
	spill_all(fresh, animate)


func set_layers(value: int) -> void:
	layers = value
	for child in get_children():
		if child is VisualInstance3D:
			(child as VisualInstance3D).layers = value


func _make_puddle(key: int, at: Vector3) -> MeshInstance3D:
	var material := ShaderMaterial.new()
	material.shader = PUDDLE_SHADER
	# Under the pieces' contact shadows and the move marks.
	material.render_priority = -1
	material.set_shader_parameter("seed", float(key) * 1.618 + randf() * 6.0)
	# Set every animated uniform, so tweens can start from them before they first change.
	material.set_shader_parameter("spread", 1.0)
	material.set_shader_parameter("foam", 0.4)
	material.set_shader_parameter("amount", 1.0)
	var plane := PlaneMesh.new()
	plane.size = Vector2(square_size, square_size)
	var puddle := MeshInstance3D.new()
	puddle.name = "Puddle_%d" % key
	puddle.mesh = plane
	puddle.material_override = material
	puddle.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	puddle.layers = layers
	puddle.position = at + Vector3(0, lift, 0)
	puddle.rotation.y = randf() * TAU
	add_child(puddle)
	_puddles[key] = puddle
	return puddle


## Stops a puddle's spill from spreading it any further (a dry or a new spill takes over).
func _stop_spill(puddle: Node) -> void:
	if not puddle.has_meta("spill"):
		return
	var tween: Tween = puddle.get_meta("spill")
	if tween and tween.is_valid():
		tween.kill()
	puddle.remove_meta("spill")


func _remove(key: int) -> void:
	var puddle: Node = _puddles.get(key)
	_puddles.erase(key)
	if puddle:
		puddle.queue_free()


## A short stream of drops arcing in from `from_side` onto `at`. Returns when (after `delay`)
## the first one lands.
func _pour(at: Vector3, delay: float) -> float:
	var s := square_size
	var side := from_side.normalized()
	var across := Vector3(-side.z, 0, side.x)
	var flight := 0.3
	var from := at + side * s * 1.5 + Vector3(0, s * 2.0, 0)
	for i in 24:
		var start := from + across * s * randf_range(-0.12, 0.12) + side * s * randf_range(-0.1, 0.1)
		var land := at + Vector3(randf_range(-0.15, 0.15), 0, randf_range(-0.15, 0.15)) * s
		_fly(start, land, s * randf_range(0.5, 0.65), flight, delay + i * 0.011,
			s * randf_range(0.025, 0.045), i % 5 == 4)
	return flight


## The crown of drops thrown up where the beer lands.
func _splash(at: Vector3) -> void:
	var s := square_size
	for i in 18:
		var angle := TAU * i / 18.0 + randf_range(-0.2, 0.2)
		var out := Vector3(cos(angle), 0, sin(angle)) * s * randf_range(0.3, 0.6)
		_fly(at + out * 0.25, at + out, s * randf_range(0.15, 0.35), randf_range(0.22, 0.34),
			randf() * 0.06, s * randf_range(0.015, 0.032), i % 3 == 0)


## One drop of beer (or foam) flying from `from` to `to` over `seconds`, peaking `height` above
## the straight line, after `delay`; it shrinks into the puddle on landing.
func _fly(from: Vector3, to: Vector3, height: float, seconds: float, delay: float, radius: float,
		frothy: bool) -> void:
	_make_drop_materials()
	var drop := MeshInstance3D.new()
	drop.mesh = _drop_mesh
	drop.material_override = _foam_drop_material if frothy else _drop_material
	drop.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	drop.layers = layers
	drop.position = from + Vector3(0, lift, 0)
	drop.visible = false
	add_child(drop)
	var size := Vector3.ONE * radius
	var tween := drop.create_tween()
	tween.tween_interval(delay)
	tween.tween_callback(func() -> void: drop.visible = true)
	tween.tween_method(func(t: float) -> void:
		var at := from.lerp(to, t) + Vector3(0, height * 4.0 * t * (1.0 - t), 0)
		var velocity := (to - from) + Vector3(0, height * 4.0 * (1.0 - 2.0 * t), 0)
		drop.position = at + Vector3(0, lift, 0)
		# Stretched along its flight.
		if velocity.length() > 0.0001:
			drop.basis = Basis.looking_at(velocity.normalized(),
				Vector3.UP if absf(velocity.normalized().y) < 0.99 else Vector3.RIGHT)
		drop.scale = size * Vector3(1, 1, 3.2),
		0.0, 1.0, seconds).set_trans(Tween.TRANS_LINEAR)
	tween.tween_method(func(t: float) -> void:
		drop.basis = Basis.IDENTITY
		drop.scale = size * Vector3(1.0 + t * 0.6, maxf(1.0 - t, 0.05) * 0.4, 1.0 + t * 0.6),
		0.0, 1.0, 0.14)
	tween.tween_callback(drop.queue_free)


func _make_drop_materials() -> void:
	if _drop_material:
		return
	_drop_mesh = SphereMesh.new()
	_drop_mesh.radius = 1.0
	_drop_mesh.height = 2.0
	_drop_mesh.radial_segments = 10
	_drop_mesh.rings = 6
	_drop_material = StandardMaterial3D.new()
	_drop_material.albedo_color = Color(1.0, 0.66, 0.16, 0.82)
	_drop_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_drop_material.roughness = 0.06
	_drop_material.metallic_specular = 0.9
	_drop_material.emission_enabled = true
	_drop_material.emission = Color(0.55, 0.3, 0.05)
	_drop_material.emission_energy_multiplier = 0.4
	_foam_drop_material = StandardMaterial3D.new()
	_foam_drop_material.albedo_color = Color(1.0, 0.95, 0.84)
	_foam_drop_material.roughness = 0.6
	_foam_drop_material.emission_enabled = true
	_foam_drop_material.emission = Color(0.4, 0.37, 0.3)
	_foam_drop_material.emission_energy_multiplier = 0.4
