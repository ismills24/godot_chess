extends "res://common/scene_kit.gd"
## The inn: a low timber-and-plaster room. The fireplace on the back wall is the key light.
## Room space: x -5..5 (left..right wall), z +1 (front, by the door) .. -11 (back wall), y up.

const FIRE := preload("res://menu/fire.gdshader")
const MIST := preload("res://menu/mist.gdshader")
const OLD_WOOD := preload("res://inn/old_wood.gdshader")
const OLD_PLASTER := preload("res://inn/old_plaster.gdshader")
const OLD_BRICK := preload("res://inn/old_brick.gdshader")
const STONE_FLOOR := preload("res://inn/stone_floor.gdshader")
const COBWEB := preload("res://inn/cobweb.gdshader")
const ALE_STAIN := preload("res://inn/ale_stain.gdshader")
const HALO := preload("res://inn/halo.gdshader")
const MUG_STAVES := preload("res://inn/mug_staves.gdshader")
const FOAM := preload("res://inn/foam.gdshader")
const BENCH := preload("res://inn/props/bench.glb")
const WINE_BARREL := preload("res://inn/props/wine_barrel.glb")
const BARREL := preload("res://inn/props/barrel.glb")
const BOTTLES := preload("res://inn/props/bottles.glb")
const JUG := preload("res://inn/props/jug.glb")
const CANDLESTICK := preload("res://inn/props/candlestick.glb")
const BUCKET := preload("res://inn/props/bucket.glb")
const CRATE := preload("res://inn/props/crate.glb")
const CHANDELIER := preload("res://inn/props/chandelier.glb")
const BOWL := preload("res://inn/props/bowl.glb")
const PLATE := preload("res://inn/props/plate.glb")
const OIL_LAMP := preload("res://inn/props/oil_lamp.glb")
const Patron := preload("res://inn/patron.gd")
const Customer := preload("res://inn/customer.gd")
const ChessSet := preload("res://inn/chess_set.gd")
const Street := preload("res://inn/street.gd")

const LEFT_WALL := -5.0
const RIGHT_WALL := 3.2
const ROOM_MID := (LEFT_WALL + RIGHT_WALL) * 0.5
## The side walls stop at SPRING; a timber-boarded barrel vault rises from there to APEX.
const SPRING := 2.4
const APEX := 3.7
const BAR_X := RIGHT_WALL - 1.08
## Lanterns hang down the middle of the vault at these depths, all at one height, stopping
## short of the hearth.
## One great iron chandelier over the middle of the long table: the centre of its outer
## ring, and that ring's radius.
const CHANDELIER_AT := Vector3(ROOM_MID, 2.4, -4.55)
const CHANDELIER_RADIUS := 0.8
const BACK_WALL := -11.0
const HEARTH_AT := Vector3(ROOM_MID, 0, BACK_WALL)
## The doorway to the stairs in the back-right corner (x from..to, height). The stairs climb
## left behind the back wall, round the back of the chimney.
const STAIR_DOOR := Vector3(2.15, 3.05, 2.0)
const STAIRWELL_DEPTH := 1.15
const STAIR_RISE := 0.18
const STAIR_RUN := 0.26
## Top surface centre of the chess table, its short end against the left wall.
const CHESS_TABLE_AT := Vector3(-4.35, 0.76, -4.2)
const BARTENDER_SEAT := Vector3(-4.35, 0, -5.0)
const PLAYER_SEAT := Vector3(-4.35, 0, -3.42)
## The player's stool at the bar, the board's spot on the counter in front of it, and where
## the bartender stands behind the counter (serving facing -x, toward the stool).
const BAR_STOOL_AT := Vector3(BAR_X - 1.05, 0, -4.2)
## Where the stools stand tucked in under the counter; the player's starts here and is
## pulled back to BAR_STOOL_AT.
const BAR_STOOL_TUCKED := Vector3(BAR_X - 0.68, 0, -4.2)
const BAR_BOARD_AT := Vector3(BAR_X + 0.02, 1.105, -4.2)
const BARTENDER_POST := Vector3(BAR_X + 0.52, 0, -4.2)
## The lowest back shelf, where the board waits behind him.
const BOARD_SHELF_Y := 1.37
## The innkeeper's mug: just past the board's far left corner as you look at it.
const KEEPER_MUG_AT := Vector3(BAR_X + 0.08, 1.105, -4.2 - 0.37)
## The back bar against the right wall: a low cupboard, and above it a framed shelving unit
## running BACK_BAR_FROM to BACK_BAR_TO, its shelves at BACK_SHELVES (centre heights).
const BACK_BAR_FROM := -2.35
const BACK_BAR_TO := -7.45
const BACK_SHELVES: Array[float] = [1.35, 1.76, 2.17]
const BACK_DEPTH := 0.3
## The bay of the back shelves (counting from the door end) given over to chess: boards,
## folded sets and boxes of pieces. It's the bay behind the bartender, so the board he
## serves comes off its bottom shelf.
const CHESS_BAY := 1
## Empty stools along the bar either side of the player's.
const BAR_STOOLS: Array[float] = [-2.5, -3.35, -5.05]
## The hooded pair's table, just right of the hearth so the fire backlights them.
const HUDDLE_AT := Vector3(1.05, 0, -8.9)
const TABLE_HEIGHT := 0.76
## One more table behind the chess table, by the left wall; from the play seat it fills the
## gap between the bartender and the hearth without hiding the fire.
const BACK_TABLE_AT := Vector3(-3.8, 0, -7.9)
const CHAIR_REACH := 0.6
## Two small tables in the left aisle, leaving the lane between the long table and the bar
## clear; the hooded pair has another by the fire (HUDDLE_AT).
const TABLE_GRID: Array[Vector3] = [Vector3(-3.0, 0, -1.7), Vector3(-3.0, 0, -6.3)]
## The long table down the middle, on the hearth's line, from near the door to a few steps
## short of the fire. Seats are LONG_TABLE_PITCH apart down both sides.
const LONG_TABLE_X := ROOM_MID
const LONG_TABLE_FROM := -1.7
const LONG_TABLE_TO := -7.4
const LONG_TABLE_SEATS := 9
const LONG_TABLE_PITCH := 0.63
## Games in progress on the long table (FEN piece placement, then the pieces taken), by
## seat pair.
const LONG_TABLE_GAMES := {
	1: ["r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R", ""],
	2: ["r2q1rk1/pp2bppp/2n1pn2/3p4/3P4/2NBPN2/PP3PPP/R2Q1RK1", "Pp"],
	4: ["r1b2rk1/pp1nqppp/2p1p3/3n4/2BP4/2N1PN2/PP3PPP/R2Q1RK1", "Np"],
	6: ["4r1k1/pp3ppp/8/3p4/3P1q2/2P5/PP3PPP/R4RK1", "QBNPPbnnp"],
	7: ["6k1/5ppp/8/8/8/8/5PPP/3R2K1", "QRRBBNNPPPqrbbnnppppp"],
}

var _hearth_light: OmniLight3D
var _candle_light: OmniLight3D
var _bar_light: OmniLight3D
var _hearth_brick: ShaderMaterial
var _sparks: MultiMesh
var _patrons: Array[Node3D] = []
var _bar_regulars: Array[Node3D] = []
var _street: Node3D

## The regulars at the small tables, by "table index:side" (sides: 0 +z, 1 -z, 2 +x, 3 -x;
## tables as in _build_tables, 2 is the one behind the chess table). Most of them have seen
## better days.
var _seated := {
	# Asleep face-down by the door.
	"0:1": {"pose": "slump", "height": 0.95, "girth": 0.9, "skin": Color("a8806a"),
		"shirt": Color("4a5040"), "legs": Color("2b2a26"), "hair": Color("2a2018"), "beard": "full",
		"wear": ["hole", "torn_sleeve", "patch_knee"], "phase": 1.3},
	"0:0": {"pose": "sit", "height": 0.8, "girth": 1.25, "belly": 0.3, "skin": Color("8a5c40"),
		"shirt": Color("3e4a3a"), "legs": Color("2e2a24"), "hair": Color("1a1410"), "beard": "full",
		"wear": ["hat", "patch_shoulder", "red_nose"], "mug": true, "phase": 3.1},
	"1:1": {"pose": "slump", "height": 1.0, "girth": 1.2, "belly": 0.4, "skin": Color("c49a7c"),
		"shirt": Color("6b5d45"), "legs": Color("3a3028"), "beard": "stubble",
		"wear": ["stain", "frayed"], "phase": 0.9},
	# A thin old man with one eye behind the bartender, nursing a drink.
	"2:1": {"pose": "sit", "height": 1.14, "girth": 0.78, "skin": Color("b8a07c"),
		"shirt": Color("5a4a3a"), "legs": Color("3a3028"), "hair": Color("8a8478"), "bald": true,
		"beard": "stubble", "beard_color": Color("8a8478"),
		"wear": ["eyepatch", "bags", "frayed", "hole"], "mug": true, "phase": 2.2},
}
## The big man leaning back at the near end of the long table with his ale.
var _big_man := {"pose": "lean", "height": 1.0, "girth": 1.45, "belly": 1.0, "skin": Color("c08a70"),
	"shirt": Color("7a6a50"), "vest": Color("3a2a20"), "legs": Color("3a3028"),
	"hair": Color("4a3020"), "bald": true, "beard": "stubble",
	"wear": ["red_nose", "stain", "patch_shoulder", "frayed", "bags"], "mug": true, "phase": 0.4}
var _at_bar := [
	# Tall, wiry, bandaged and bruised from last night.
	{"pose": "stand", "height": 1.2, "girth": 0.85, "skin": Color("6e4a34"), "shirt": Color("5a3030"),
		"legs": Color("2e2a24"), "hair": Color("1a1410"),
		"wear": ["bandage", "torn_sleeve", "patch_knee", "black_eye"], "mug": true, "phase": 0.7},
	{"pose": "stand", "height": 0.84, "girth": 1.3, "belly": 0.5, "skin": Color("d0a890"),
		"shirt": Color("6a5a48"), "vest": Color("2e2620"), "legs": Color("3a3028"),
		"hair": Color("7a4020"), "beard": "full", "wear": ["bags", "stain"], "mug": true, "phase": 2.6},
]

var _plaster := _plaster_material(Color("3b342c"))
var _soot_plaster := _plaster_material(Color("2a241f"))
var _timber := _wood(Color("2a1d15"))
var _old_timber := _wood(Color("3a2a1e"))
var _iron := _paint(Color("2b2725"), 0.6, 0.5)
var _stone := _paint(Color("3a3733"), 0.93)
var _ash := _paint(Color("1a1716"), 1.0)
var _table_wood := _wood(Color("3e2a1c"))
var _chair_wood := _wood(Color("4f4033"))
var _chair_wood_dark := _wood(Color("3d3128"))
var _halo: ShaderMaterial
var _candle_wax := _paint(Color("cbb89a"), 0.7)
## Chandelier candles, lit from their own flames above any lamp.
var _lit_wax := _glowing(Color("c8a878"), 0.45)
var _mug_wood := _paint(Color("5a3a22"), 0.75)
## The player's bar stool, pulled out from under the counter before sitting.
var player_stool: Node3D
## The innkeeper's big mug, which he picks up to drink from.
var keeper_mug: Node3D
## Bottle glass: dark green, brown, amber, smoky clear, deep blue; seals and labels.
var _bottle_glass: Array[Material] = [
	_paint(Color("1f3a22"), 0.12, 0.2), _paint(Color("3e2410"), 0.12, 0.2), _paint(Color("7a4a16"), 0.12, 0.2),
	_paint(Color("6a7068"), 0.1, 0.25), _paint(Color("1c2440"), 0.12, 0.2),
]
var _seal := _paint(Color("6a1a14"), 0.6)
## Chess bay: board frames and their checkered faces, brass hinges and catches.
var _board_edge := _paint(Color("3a2416"), 0.6)
var _board_edge_dark := _paint(Color("24160e"), 0.6)
var _brass := _paint(Color("8a6a2a"), 0.4, 0.7)
var _checks := _checker_material()
var _label := _label_material()
## Clay: raw terracotta, a darker fired brown, a green glaze, a cream salt glaze.
var _clay: Array[Material] = [
	_paint(Color("8a4a2a"), 0.9), _paint(Color("5a3220"), 0.85), _paint(Color("3a4a2a"), 0.35),
	_paint(Color("a89878"), 0.5),
]
var _foam := _paint(Color("d8c8a0"), 0.9)
var _mug_handle_wood := _paint(Color("40281a"), 0.7)
var _candle_flame := _glowing(Color("ffd27a"), 7.0)


func build() -> void:
	_build_shell()
	_build_hearth()
	_build_bar()
	_build_chess_corner()
	_build_tables()
	_build_patrons()
	_build_customers_at_bar()
	_build_clutter()
	_build_cobwebs()
	_build_stains()
	_build_haze()
	_flush_boxes()
	_animate()


func set_reduced_motion(enabled: bool) -> void:
	super(enabled)
	_street.set_reduced_motion(enabled)
	for patron in _patrons:
		patron.set_reduced_motion(enabled)


func _animate() -> void:
	super()
	_hearth_light.light_energy = 4.6 + 0.7 * sin(_elapsed * 7.3) + 0.3 * sin(_elapsed * 15.9)
	_candle_light.light_energy = 0.55 + 0.06 * sin(_elapsed * 11.0) + 0.03 * sin(_elapsed * 23.0)
	for i in _sparks.instance_count:
		var life := fposmod(_elapsed * (0.37 + 0.021 * i) + 0.413 * i, 1.0)
		var drift := Vector3(sin(i * 4.0 + life * 3.2) * (0.12 + life * 0.25),
			life * (0.9 + 0.04 * i), cos(i * 3.8 + life * 3.1) * 0.12)
		var radius := (0.012 + 0.004 * (i % 3)) * (1.0 - life)
		_sparks.set_instance_transform(i, Transform3D(Basis.from_scale(Vector3.ONE * radius), drift))


func _build_shell() -> void:
	# A floor of big, irregular, badly laid stones with grimy joints.
	var flags := ShaderMaterial.new()
	flags.shader = STONE_FLOOR
	_box(flags, Vector3(ROOM_MID, -0.02, -5.0), Vector3(RIGHT_WALL - LEFT_WALL + 0.25, 0.04, 12.0))
	# Walls: soot-stained plaster with dark timber framing, up to the wall plates.
	for wall: float in [LEFT_WALL, RIGHT_WALL]:
		var out := signf(wall - ROOM_MID)
		_box(_plaster, Vector3(wall + out * 0.05, SPRING * 0.5, -5.0), Vector3(0.1, SPRING, 12.0))
		for post in 7:
			_box(_timber, Vector3(wall - out * 0.04, SPRING * 0.5, 0.8 - post * 1.95), Vector3(0.12, SPRING, 0.2))
		_box(_timber, Vector3(wall - out * 0.04, 1.05, -5.0), Vector3(0.1, 0.14, 12.0))
		_box(_soot_plaster, Vector3(wall - out * 0.01, SPRING - 0.3, -5.0), Vector3(0.04, 0.6, 12.0))
		_box(_old_timber, Vector3(wall - out * 0.08, SPRING - 0.08, -5.0), Vector3(0.24, 0.18, 12.2))
	var width := RIGHT_WALL - LEFT_WALL + 0.2
	var wall_left := LEFT_WALL - 0.1
	var wall_right := RIGHT_WALL + 0.1
	# The back wall, broken by the stair doorway in the right-hand corner.
	_box(_plaster, Vector3((wall_left + STAIR_DOOR.x) * 0.5, APEX * 0.5, BACK_WALL - 0.05),
		Vector3(STAIR_DOOR.x - wall_left, APEX, 0.1))
	_box(_plaster, Vector3((STAIR_DOOR.y + wall_right) * 0.5, APEX * 0.5, BACK_WALL - 0.05),
		Vector3(wall_right - STAIR_DOOR.y, APEX, 0.1))
	_box(_plaster, Vector3((STAIR_DOOR.x + STAIR_DOOR.y) * 0.5, (STAIR_DOOR.z + APEX) * 0.5, BACK_WALL - 0.05),
		Vector3(STAIR_DOOR.y - STAIR_DOOR.x, APEX - STAIR_DOOR.z, 0.1))
	# The front wall, with the street door.
	var door_left := Street.DOOR_X - Street.DOOR_WIDTH * 0.5
	var door_right := Street.DOOR_X + Street.DOOR_WIDTH * 0.5
	_box(_plaster, Vector3((wall_left + door_left) * 0.5, APEX * 0.5, 1.05), Vector3(door_left - wall_left, APEX, 0.1))
	_box(_plaster, Vector3((door_right + wall_right) * 0.5, APEX * 0.5, 1.05), Vector3(wall_right - door_right, APEX, 0.1))
	_box(_plaster, Vector3(Street.DOOR_X, (Street.DOOR_HEIGHT + APEX) * 0.5, 1.05),
		Vector3(Street.DOOR_WIDTH, APEX - Street.DOOR_HEIGHT, 0.1))
	for jamb: float in [door_left, door_right]:
		_box(_old_timber, Vector3(jamb, Street.DOOR_HEIGHT * 0.5, 1.0), Vector3(0.14, Street.DOOR_HEIGHT, 0.2))
	_box(_old_timber, Vector3(Street.DOOR_X, Street.DOOR_HEIGHT + 0.08, 1.0), Vector3(Street.DOOR_WIDTH + 0.4, 0.16, 0.22))
	# The door itself stands open into the room, hung on the right-hand jamb.
	var leaf := Vector3(door_right - 0.06, Street.DOOR_HEIGHT * 0.5 - 0.02, 1.0 - Street.DOOR_WIDTH * 0.5 + 0.02)
	for plank in 5:
		_box(_old_timber, leaf + Vector3(0, 0, (plank - 2) * 0.2), Vector3(0.05, Street.DOOR_HEIGHT - 0.06, 0.19))
	for brace: float in [-0.7, 0.7]:
		_box(_timber, leaf + Vector3(-0.035, brace, 0), Vector3(0.025, 0.1, Street.DOOR_WIDTH - 0.1))
	_box(_iron, leaf + Vector3(-0.05, 0.05, -0.4), Vector3(0.03, 0.12, 0.05))
	_street = Street.new()
	add_child(_street)
	_street.build()
	for post in 4:
		var x := LEFT_WALL + 1.0 + post * 2.0
		var tall := _vault_height(x)
		_box(_timber, Vector3(x, tall * 0.5, BACK_WALL + 0.04), Vector3(0.2, tall, 0.12))
	_build_vault()
	_build_window(Vector3(LEFT_WALL, 1.75, -4.2))
	_build_stairs()


## The stairs up to the rooms: a doorway in the back-right corner, and behind the back wall a
## narrow well where worn steps climb to the left, round the back of the chimney, lit by one
## candle in a sconce on the end wall.
func _build_stairs() -> void:
	var front := BACK_WALL - 0.1
	var back := BACK_WALL - 0.1 - STAIRWELL_DEPTH
	var mid_z := (front + back) * 0.5
	var steps := int((RIGHT_WALL - (HEARTH_AT.x - 0.6)) / STAIR_RUN)
	var left_end := RIGHT_WALL - steps * STAIR_RUN - 0.2
	var span := RIGHT_WALL + 0.1 - left_end
	var mid_x := (RIGHT_WALL + 0.1 + left_end) * 0.5
	var top := STAIR_RISE * steps + 2.3
	# No real light back here (the room is at its light limit): the well's surfaces carry a
	# faint glow of their own, warmest low down by the candle.
	var wall_glow := _glowing(Color("4a2c18"), 1.0)
	var riser_glow := _glowing(Color("3a2214"), 0.8)
	var tread_glow := _glowing(Color("7a4e2a"), 1.1)
	# The well: floor, back and end walls, a ceiling well above the top step.
	_box(riser_glow, Vector3(mid_x, -0.02, mid_z), Vector3(span, 0.04, STAIRWELL_DEPTH))
	_box(wall_glow, Vector3(mid_x, top * 0.5, back - 0.05), Vector3(span, top, 0.1))
	_box(wall_glow, Vector3(RIGHT_WALL + 0.15, top * 0.5, mid_z), Vector3(0.1, top, STAIRWELL_DEPTH + 0.2))
	_box(wall_glow, Vector3(left_end - 0.05, top * 0.5, mid_z), Vector3(0.1, top, STAIRWELL_DEPTH + 0.2))
	_box(_timber, Vector3(mid_x, top, mid_z), Vector3(span, 0.1, STAIRWELL_DEPTH + 0.2))
	# The steps, from the corner up to the left: solid blocks with a worn tread on each.
	for k in steps:
		var x := RIGHT_WALL - (k + 0.5) * STAIR_RUN
		var height := (k + 1) * STAIR_RISE
		_box(riser_glow, Vector3(x, height * 0.5, mid_z), Vector3(STAIR_RUN, height, STAIRWELL_DEPTH))
		_box(tread_glow, Vector3(x - 0.01, height + 0.012, mid_z + 0.01 * sin(k * 2.1)),
			Vector3(STAIR_RUN + 0.03, 0.025, STAIRWELL_DEPTH - 0.04))
	# A handrail on pegs along the back wall, following the pitch.
	var pitch := atan2(STAIR_RISE, STAIR_RUN)
	var rail_length := steps * STAIR_RUN / cos(pitch)
	var rail_mid := Vector3(RIGHT_WALL - steps * STAIR_RUN * 0.5, steps * STAIR_RISE * 0.5 + 0.9, back + 0.06)
	_box(_old_timber, rail_mid, Vector3(rail_length, 0.05, 0.05), Vector3(0, 0, -pitch))
	# The doorway: heavy jambs and lintel, a worn threshold.
	var door_mid := (STAIR_DOOR.x + STAIR_DOOR.y) * 0.5
	var door_width := STAIR_DOOR.y - STAIR_DOOR.x
	for jamb: float in [STAIR_DOOR.x, STAIR_DOOR.y]:
		_box(_old_timber, Vector3(jamb, STAIR_DOOR.z * 0.5, BACK_WALL), Vector3(0.14, STAIR_DOOR.z, 0.24))
	_box(_old_timber, Vector3(door_mid, STAIR_DOOR.z + 0.08, BACK_WALL), Vector3(door_width + 0.4, 0.16, 0.26))
	_box(_old_timber, Vector3(door_mid, 0.015, BACK_WALL - 0.05), Vector3(door_width, 0.03, 0.3))
	# A candle in a sconce on the end wall, the only light on the stairs.
	var sconce := Vector3(RIGHT_WALL + 0.08, 1.45, mid_z - 0.2)
	_box(_iron, sconce + Vector3(-0.03, 0, 0), Vector3(0.06, 0.04, 0.1))
	_box(_iron, sconce + Vector3(-0.06, 0.03, 0), Vector3(0.08, 0.015, 0.08))
	_cylinder(self, sconce + Vector3(-0.06, 0.09, 0), 0.02, 0.018, 0.1, _candle_wax)
	_box(_candle_flame, sconce + Vector3(-0.06, 0.16, 0), Vector3(0.014, 0.03, 0.014))
	var glow := QuadMesh.new()
	glow.size = Vector2(0.55, 0.55)
	_mesh(self, glow, _halo, sconce + Vector3(-0.08, 0.16, 0)).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## A leaded window of small square panes in a timber lattice, dark night behind the glass.
## `at` is its centre on the left wall.
func _build_window(at: Vector3) -> void:
	const PANES := 4
	const PANE := 0.16
	var night: Array[StandardMaterial3D] = [_glowing(Color("1b3a44"), 0.35),
		_glowing(Color("21434f"), 0.4), _glowing(Color("142f37"), 0.3)]
	var span := PANES * PANE
	for row in PANES:
		for column in PANES:
			var pane := at + Vector3(0.01, (row - (PANES - 1) * 0.5) * PANE, (column - (PANES - 1) * 0.5) * PANE)
			_box(night[(row * 3 + column * 2) % 3], pane, Vector3(0.02, PANE, PANE))
	# One pane cracked across.
	var crack := _glowing(Color("4a6e78"), 0.5)
	var cracked := at + Vector3(0.025, 0.5 * PANE, -0.5 * PANE)
	_box(crack, cracked, Vector3(0.005, 0.006, PANE * 1.3), Vector3(PI * 0.25, 0, 0))
	_box(crack, cracked + Vector3(0, 0.03, 0.02), Vector3(0.005, 0.005, PANE * 0.5), Vector3(-0.5, 0, 0))
	# The lattice: thin bars between the panes, a heavy frame, sill and lintel.
	for line in PANES - 1:
		var offset := (line - (PANES - 2) * 0.5) * PANE
		_box(_timber, at + Vector3(0.035, 0, offset), Vector3(0.03, span, 0.025))
		_box(_timber, at + Vector3(0.035, offset, 0), Vector3(0.03, 0.025, span))
	for side: int in [-1, 1]:
		_box(_timber, at + Vector3(0.04, 0, side * (span * 0.5 + 0.03)), Vector3(0.08, span + 0.12, 0.06))
		_box(_timber, at + Vector3(0.04, side * (span * 0.5 + 0.03), 0), Vector3(0.08, 0.06, span + 0.12))
	_box(_timber, at + Vector3(0.06, -span * 0.5 - 0.07, 0), Vector3(0.12, 0.06, span + 0.2))


## Height of the vault's underside at world x.
static func _vault_height(x: float) -> float:
	var half := (RIGHT_WALL - LEFT_WALL) * 0.5
	var rise := APEX - SPRING
	var radius := (half * half + rise * rise) / (2.0 * rise)
	var dx := clampf(x - ROOM_MID, -half, half)
	return APEX - radius + sqrt(radius * radius - dx * dx)


## Lays `count` boxes edge to edge along the vault's arc at depth z. `offset` moves their
## centres off the boarding's underside (+ up into it, - down into the room).
func _arc(material: Material, count: int, offset: float, z: float, thickness: float, run: float) -> void:
	var half := (RIGHT_WALL - LEFT_WALL) * 0.5
	var rise := APEX - SPRING
	var radius := (half * half + rise * rise) / (2.0 * rise)
	var reach := asin(half / radius)
	var centre := Vector3(ROOM_MID, APEX - radius, z)
	var middle := radius + offset
	for i in count:
		var angle := lerpf(-reach, reach, (i + 0.5) / count)
		var chord := 2.0 * middle * sin(reach / count) * 1.06
		var at := centre + Vector3(sin(angle), cos(angle), 0) * middle
		_box(material, at, Vector3(chord, thickness, run), Vector3(0, 0, -angle))


func _build_vault() -> void:
	# Dark boarding over the whole room, heavy ribs landing on the wall plates, a ridge
	# beam, and the kitchen's leftovers hung from the ribs to dry.
	var boarding := _wood(Color("241912"))
	_arc(boarding, 26, 0.025, -5.0, 0.05, 12.2)
	for rib in 6:
		_arc(_old_timber, 14, -0.09, 0.6 - rib * 2.1, 0.18, 0.2)
	_box(_old_timber, Vector3(ROOM_MID, APEX - 0.07, -5.0), Vector3(0.18, 0.14, 12.2))
	_build_chandelier(CHANDELIER_AT)


func _build_hearth() -> void:
	var at := HEARTH_AT
	# Chimney breast juts 0.7 from the back wall: two brick pillars and a breast above a
	# 1.2 x 1.05 opening. The firebox behind it is a sooty recess where the fire sits.
	const OPENING := Vector2(1.2, 1.05)
	const BREAST := Vector2(2.6, APEX)
	var pillar := (BREAST.x - OPENING.x) * 0.5
	for side: int in [-1, 1]:
		var x := side * (OPENING.x + pillar) * 0.5
		_box(_stone, at + Vector3(x, OPENING.y * 0.5, 0.35), Vector3(pillar, OPENING.y, 0.7))
		_brick_face(at + Vector3(x, OPENING.y * 0.5, 0.705), Vector2(pillar, OPENING.y))
	var upper := BREAST.y - OPENING.y
	_box(_stone, at + Vector3(0, OPENING.y + upper * 0.5, 0.35), Vector3(BREAST.x, upper, 0.7))
	_brick_face(at + Vector3(0, OPENING.y + upper * 0.5, 0.705), Vector2(BREAST.x, upper))
	_box(_ash, at + Vector3(0, OPENING.y * 0.5, 0.06), Vector3(OPENING.x, OPENING.y, 0.12))
	_box(_ash, at + Vector3(0, OPENING.y - 0.03, 0.35), Vector3(OPENING.x, 0.06, 0.7))
	_box(_old_timber, at + Vector3(0, OPENING.y + 0.08, 0.8), Vector3(2.0, 0.14, 0.24))
	_box(_stone, at + Vector3(0, 0.04, 0.95), Vector3(2.2, 0.08, 0.6))
	_box(_ash, at + Vector3(0, 0.01, 0.35), Vector3(OPENING.x, 0.02, 0.7))
	# Logs and embers.
	var ember := _glowing(Color("d9531e"), 2.4)
	var charcoal := _paint(Color("241b17"), 0.95)
	for i in 3:
		_box(charcoal, at + Vector3(-0.25 + 0.25 * i, 0.08 + 0.04 * (i % 2), 0.4), Vector3(0.6, 0.11, 0.11),
			Vector3(0, 0.4 - 0.4 * i, 0))
	for i in 8:
		var angle := i * 2.4
		var spot := at + Vector3(cos(angle) * 0.32 * fposmod(i * 0.61, 1.0), 0.05, 0.42 + sin(angle) * 0.08)
		_box(ember if i % 3 != 1 else charcoal, spot, Vector3(0.09, 0.03, 0.07), Vector3(0.1, angle, 0.1))
	var fire := MultiMesh.new()
	fire.transform_format = MultiMesh.TRANSFORM_3D
	fire.use_custom_data = true
	fire.mesh = _flame_mesh()
	fire.instance_count = 9
	fire.custom_aabb = AABB(Vector3(-0.7, -0.1, -0.4), Vector3(1.4, 1.4, 0.8))
	for i in fire.instance_count:
		var x := -0.42 + 0.105 * i
		var growth := 0.5 + 0.5 * fposmod(i * 0.61, 1.0)
		var width := 0.17 + 0.1 * fposmod(i * 0.39, 1.0)
		var basis := Basis(Vector3.UP, i * 2.4) * Basis.from_scale(Vector3(width, growth, width))
		fire.set_instance_transform(i, Transform3D(basis, Vector3(x, 0, 0.04 * sin(i * 1.7))))
		fire.set_instance_custom_data(i, Color(fposmod(i * 0.382, 1.0), fposmod(i * 0.17, 1.0), 1.0 - i / 9.0))
	var flames := MultiMeshInstance3D.new()
	flames.multimesh = fire
	flames.material_override = _animated_material(FIRE)
	flames.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	flames.position = at + Vector3(0, 0.1, 0.4)
	flames.scale = Vector3(1.2, 1.1, 1.0)
	add_child(flames)
	_hearth_light = _omni(at + Vector3(0, 0.75, 1.25), Color("ff7a38"), 4.6, 9.5)
	_hearth_light.shadow_enabled = true
	_hearth_light.shadow_bias = 0.05
	_sparks = MultiMesh.new()
	_sparks.transform_format = MultiMesh.TRANSFORM_3D
	var spark := BoxMesh.new()
	spark.material = _glowing(Color("ffae4c"), 4.0)
	_sparks.mesh = spark
	_sparks.instance_count = 10
	var sparks := MultiMeshInstance3D.new()
	sparks.multimesh = _sparks
	sparks.position = at + Vector3(0, 0.25, 0.42)
	sparks.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(sparks)
	# Mantel clutter.
	_place_prop(JUG, at + Vector3(-0.75, 1.2, 0.8), 0.22, 0.4)
	_place_prop(CANDLESTICK, at + Vector3(0.7, 1.2, 0.8), 0.24, 0.0)
	_place_prop(BOWL, at + Vector3(0.25, 1.2, 0.78), 0.07, 0.0)
	_deer_trophy(at + Vector3(0, 2.3, 0.705))
	# Firewood stacked to the side of the hearth.
	for i in 7:
		_box(_old_timber, at + Vector3(1.75 + 0.14 * (i % 3), 0.07 + 0.12 * int(i / 3.0), 0.55),
			Vector3(0.12, 0.12, 0.62), Vector3(0, 0.05 * i, 0))


## A mounted stag's head on a dark wooden shield, antlers spreading above; `at` is the
## shield's centre on the wall. Separate meshes that cast no shadow, so the fire below
## doesn't throw a long shadow of it up the chimney.
func _deer_trophy(at: Vector3) -> void:
	var trophy := _pivot(self, at)
	trophy.scale = Vector3.ONE * 1.3
	var plaque := _wood(Color("150c07"))
	var hide := _paint(Color("7a5236"), 0.9)
	var pale := _paint(Color("c8b090"), 0.9)
	var dark := _paint(Color("140b09"), 0.4)
	var antler := _paint(Color("b49a72"), 0.6)
	var parts := [
		[plaque, Vector3(0, 0, 0.025), Vector3(0.46, 0.56, 0.05), Vector3.ZERO],
		[plaque, Vector3(0, -0.33, 0.025), Vector3(0.28, 0.1, 0.05), Vector3.ZERO],
		[_iron, Vector3(0, -0.33, 0.052), Vector3(0.14, 0.035, 0.005), Vector3.ZERO],
		# Neck out from the shield, head on top, the long muzzle tipped down.
		[hide, Vector3(0, -0.07, 0.12), Vector3(0.2, 0.3, 0.18), Vector3(0.25, 0, 0)],
		[pale, Vector3(0, -0.12, 0.2), Vector3(0.1, 0.18, 0.03), Vector3(0.25, 0, 0)],
		[hide, Vector3(0, 0.08, 0.24), Vector3(0.17, 0.17, 0.2), Vector3.ZERO],
		[hide, Vector3(0, 0.02, 0.38), Vector3(0.11, 0.1, 0.2), Vector3(0.3, 0, 0)],
		[pale, Vector3(0, -0.025, 0.38), Vector3(0.1, 0.03, 0.17), Vector3(0.3, 0, 0)],
		[dark, Vector3(0, -0.015, 0.48), Vector3(0.07, 0.055, 0.03), Vector3(0.3, 0, 0)],
		[dark, Vector3(-0.087, 0.1, 0.3), Vector3(0.02, 0.03, 0.035), Vector3.ZERO],
		[dark, Vector3(0.087, 0.1, 0.3), Vector3(0.02, 0.03, 0.035), Vector3.ZERO],
		[hide, Vector3(-0.14, 0.15, 0.2), Vector3(0.05, 0.15, 0.03), Vector3(0, 0, 0.95)],
		[hide, Vector3(0.14, 0.15, 0.2), Vector3(0.05, 0.15, 0.03), Vector3(0, 0, -0.95)],
	]
	for side: int in [-1, 1]:
		parts.append_array([
			[antler, Vector3(side * 0.1, 0.3, 0.21), Vector3(0.03, 0.3, 0.03), Vector3(0, 0, -side * 0.5)],
			[antler, Vector3(side * 0.21, 0.53, 0.19), Vector3(0.028, 0.26, 0.028), Vector3(-0.15, 0, -side * 0.2)],
			[antler, Vector3(side * 0.26, 0.7, 0.17), Vector3(0.022, 0.14, 0.022), Vector3(0, 0, -side * 0.55)],
			[antler, Vector3(side * 0.13, 0.38, 0.27), Vector3(0.022, 0.13, 0.022), Vector3(0.7, 0, 0)],
			[antler, Vector3(side * 0.2, 0.5, 0.25), Vector3(0.02, 0.13, 0.02), Vector3(0.55, 0, -side * 0.3)],
			[antler, Vector3(side * 0.18, 0.66, 0.2), Vector3(0.02, 0.12, 0.02), Vector3(0.2, 0, side * 0.35)],
		])
	for part: Array in parts:
		var piece := _box_mesh(trophy, part[0], part[1], part[2])
		piece.rotation = part[3]
		piece.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _brick_face(at: Vector3, size: Vector2) -> void:
	# The brick is laid in world space, so every face shares one material and the courses
	# line up across the pillars and the breast.
	if not _hearth_brick:
		_hearth_brick = ShaderMaterial.new()
		_hearth_brick.shader = OLD_BRICK
		_hearth_brick.set_shader_parameter("soot_origin", Vector2(HEARTH_AT.x, 1.05))
	var face := QuadMesh.new()
	face.size = size
	_mesh(self, face, _hearth_brick, at)


func _build_bar() -> void:
	# Counter along the right wall, its open end toward the fireplace.
	var counter := _wood(Color("3a2416"))
	_box(_timber, Vector3(BAR_X + 0.03, 0.52, -4.8), Vector3(0.45, 1.04, 5.6))
	# The top: three long planks with hairline gaps, each its own tone and grain, on a dark
	# bed that shows in the joints.
	_box(_timber, Vector3(BAR_X, 1.064, -4.8), Vector3(0.6, 0.056, 5.78))
	for plank in 3:
		_box(counter, Vector3(BAR_X - 0.206 + plank * 0.206, 1.07, -4.8 + 0.017 * (plank - 1) + 0.006),
			Vector3(0.203, 0.07 - 0.002 * (plank % 2), 5.8))
	for panel in 7:
		_box(_old_timber, Vector3(BAR_X - 0.2, 0.52, -2.3 - panel * 0.83), Vector3(0.04, 0.8, 0.06))
	_box(_iron, Vector3(BAR_X - 0.26, 0.12, -4.8), Vector3(0.05, 0.05, 5.6))
	_build_back_bar()
	# A cask against the back wall beside the stair door, clear of the way in.
	_place_prop(WINE_BARREL, Vector3(1.45, 0, -10.45), 0.8, 0.0)
	_place_prop(BARREL, Vector3(RIGHT_WALL - 0.5, 0, -1.2), 0.9, 0.3)
	for mug in 3:
		_wooden_mug(Vector3(BAR_X - 0.08 + 0.06 * (mug % 2), 1.105, -2.45 - mug * 0.16), mug * 1.9, mug != 1)
	# Beside the board: the player's mug, and the bartender's own, twice anyone else's.
	_wooden_mug(BAR_BOARD_AT + Vector3(-0.15, 0, 0.42), PI, true)
	# The innkeeper's own big mug, by the board's far left corner, his side: he drinks from it
	# as you play.
	keeper_mug = Node3D.new()
	keeper_mug.position = KEEPER_MUG_AT
	add_child(keeper_mug)
	_mug_parts(keeper_mug, Vector3.ZERO, 0.4, true, 2.0, true)
	# The player's stool is its own node so it can be pulled out from the counter.
	player_stool = Node3D.new()
	player_stool.position = BAR_STOOL_TUCKED
	add_child(player_stool)
	_movable_stool(player_stool, 0.1)
	for z: float in BAR_STOOLS:
		_bar_stool(Vector3(BAR_STOOL_TUCKED.x + 0.05 * sin(z * 3.0), 0, z), z)
	_place_prop(OIL_LAMP, Vector3(BAR_X, 1.105, -6.2), 0.32, 0.2)
	_box(_glowing(Color("ffb35c"), 2.0), Vector3(BAR_X, 1.27, -6.2), Vector3(0.04, 0.06, 0.04))
	_bar_light = _omni(Vector3(BAR_X - 0.2, 1.55, -6.1), Color("ffa04e"), 1.1, 3.6)
	# The chandelier (built with the vault) lights the room through two lamps either side of
	# its hub, at candle height.
	for side: int in [-1, 1]:
		_omni(CHANDELIER_AT + Vector3(side * 0.5, 0.15, 0), Color("ff9a4a"), 1.25, 6.5)


## The back bar: a low plank cupboard with kegs and crocks on top, and over it a framed
## shelving unit crammed with bottles in rows, small casks, clay pots, jugs and stacked bowls.
## The bottom shelf keeps a gap behind the bartender where the board waits.
func _build_back_bar() -> void:
	var x := RIGHT_WALL - BACK_DEPTH * 0.5 - 0.02
	var length := BACK_BAR_FROM - BACK_BAR_TO
	var mid_z := (BACK_BAR_FROM + BACK_BAR_TO) * 0.5
	var front := x - BACK_DEPTH * 0.5
	# Dark back boards behind the whole unit.
	_box(_timber, Vector3(RIGHT_WALL - 0.015, 1.55, mid_z), Vector3(0.03, 1.5, length))
	# The cupboard: a carcass, a top, plank doors with iron pulls, a kick board.
	_box(_old_timber, Vector3(x, 0.45, mid_z), Vector3(BACK_DEPTH, 0.86, length))
	_box(_wood(Color("3e2a1a")), Vector3(x - 0.01, 0.9, mid_z), Vector3(BACK_DEPTH + 0.04, 0.05, length + 0.06))
	_box(_timber, Vector3(front - 0.005, 0.05, mid_z), Vector3(0.02, 0.1, length))
	var doors := int(length / 0.62)
	for d in doors:
		var z := BACK_BAR_FROM - (d + 0.5) * length / doors
		_box(_wood(Color("33231a")), Vector3(front - 0.012, 0.48, z), Vector3(0.02, 0.66, length / doors - 0.05))
		_box(_iron, Vector3(front - 0.03, 0.62, z + (0.2 if d % 2 == 0 else -0.2)), Vector3(0.02, 0.07, 0.02))
	# The shelving unit: uprights dividing it into bays, the shelves, a crown board on top.
	var bays := 4
	for b in bays + 1:
		var z := BACK_BAR_FROM - b * length / bays
		_box(_timber, Vector3(x, 1.6, z), Vector3(BACK_DEPTH, 1.4, 0.06))
	for shelf_y: float in BACK_SHELVES:
		_box(_old_timber, Vector3(x, shelf_y, mid_z), Vector3(BACK_DEPTH, 0.04, length))
		_box(_timber, Vector3(front + 0.01, shelf_y - 0.03, mid_z), Vector3(0.025, 0.05, length))
	_box(_timber, Vector3(x - 0.02, 2.33, mid_z), Vector3(BACK_DEPTH + 0.06, 0.08, length + 0.1))

	var rng := RandomNumberGenerator.new()
	rng.seed = 9031
	# On the cupboard: a row of kegs with taps and clay crocks between them.
	var z := BACK_BAR_FROM - 0.25
	var k := 0
	while z > BACK_BAR_TO + 0.25:
		if absf(z - BARTENDER_POST.z) < 0.3:
			z -= 0.3
			continue
		match k % 3:
			0:
				_keg(Vector3(x, 0.925, z), 0.3, rng.randi() % 3)
				z -= 0.38
			1:
				_crock(Vector3(x, 0.925, z), 0.22 + rng.randf() * 0.06, rng.randi() % 4)
				z -= 0.3
			2:
				_crock(Vector3(x, 0.925, z), 0.18 + rng.randf() * 0.05, rng.randi() % 4)
				_crock(Vector3(x, 0.925, z - 0.2), 0.14, rng.randi() % 4)
				z -= 0.42
		k += 1
	# The shelves: runs of bottles in two staggered rows, broken up by casks, pots, jugs,
	# bowls and the odd stack of plates; never more than a hand's width bare.
	for shelf in BACK_SHELVES.size():
		var top := BACK_SHELVES[shelf] + 0.02
		var headroom := 0.36 if shelf < 2 else 0.2
		z = BACK_BAR_FROM - 0.06
		var run := 0
		while z > BACK_BAR_TO + 0.08:
			if shelf == 0 and absf(z - BARTENDER_POST.z) < 0.34:
				z = BARTENDER_POST.z - 0.4
				continue
			# The chess bay is dressed on its own.
			if floori((BACK_BAR_FROM - z) / (length / bays)) == CHESS_BAY:
				z = BACK_BAR_FROM - (CHESS_BAY + 1) * length / bays - 0.06
				continue
			# Skip over the uprights.
			var bay := fmod(BACK_BAR_FROM - z, length / bays)
			if bay < 0.05 or bay > length / bays - 0.05:
				z -= 0.06
				continue
			var pick := rng.randf()
			if run < 3 or pick < 0.55:
				z -= _bottle_cluster(Vector3(x, top, z), rng, headroom)
				run += 1
				continue
			run = 0
			if pick < 0.68 and headroom > 0.3:
				_keg(Vector3(x, top, z - 0.12), 0.24, rng.randi() % 3)
				z -= 0.28
			elif pick < 0.8:
				_crock(Vector3(x, top, z - 0.1), minf(headroom - 0.04, 0.16 + rng.randf() * 0.12), rng.randi() % 4)
				z -= 0.24
			elif pick < 0.9 and headroom > 0.25:
				_place_prop(JUG, Vector3(x, top, z - 0.1), 0.2, rng.randf() * TAU)
				z -= 0.22
			else:
				for bowl in 3:
					_place_prop(BOWL, Vector3(x, top + bowl * 0.045, z - 0.1), 0.05, bowl * 0.7)
				z -= 0.24
	_build_chess_bay(x, BACK_BAR_FROM - CHESS_BAY * length / bays, BACK_BAR_FROM - (CHESS_BAY + 1) * length / bays)


## The chess bay, between `from_z` and `to_z` (from_z > to_z) on shelves centred at `x`.
## Bottom shelf: a little set standing mid-game, and a pile of boards lying flat. Middle
## shelf: open travel boards leaning back against the wall, and a row of folded boards
## standing like books. Top shelf: stacked boxes of pieces.
func _build_chess_bay(x: float, from_z: float, to_z: float) -> void:
	var front := x - BACK_DEPTH * 0.5
	var bottom := BACK_SHELVES[0] + 0.02
	var middle := BACK_SHELVES[1] + 0.02
	var upper := BACK_SHELVES[2] + 0.02
	# Bottom: the board to serve waits in the middle (BARTENDER_POST.z), boxes of pieces on
	# one side of it and a pile of boards on the other.
	for n in 2:
		_piece_box(Vector3(x - 0.02, bottom + n * 0.065, from_z - 0.19 + n * 0.02), n * 0.08)
	var pile_at := Vector3(x, bottom, BARTENDER_POST.z - 0.45)
	for n in 6:
		var turn := 0.06 * sin(n * 2.1)
		_box(_board_edge if n % 2 == 0 else _board_edge_dark, pile_at + Vector3(0.004 * (n % 3), 0.016 + n * 0.03, 0),
			Vector3(0.28, 0.028, 0.28), Vector3(0, turn, 0))
	_checks_face(pile_at + Vector3(0.008, 0.18, 0), 0.26, Vector3(0, 0.06 * sin(5 * 2.1), 0))
	# Middle: three open travel boards leaning back, overlapping, then folded boards on end.
	for n in 3:
		var pivot := Node3D.new()
		pivot.position = Vector3(front + 0.06 + n * 0.02, middle, from_z - 0.24 - n * 0.2)
		pivot.rotation = Vector3(0, 0.08 * (n - 1), PI * 0.5 - 0.3 + 0.04 * n)
		add_child(pivot)
		_box_mesh(pivot, _board_edge, Vector3(0.155, 0.01, 0), Vector3(0.31, 0.02, 0.31))
		var face := PlaneMesh.new()
		face.size = Vector2(0.28, 0.28)
		_mesh(pivot, face, _checks, Vector3(0.155, 0.0205, 0))
	for n in 6:
		var z := from_z - 0.78 - n * 0.06
		var lean := 0.0 if n < 5 else -0.25
		_box(_board_edge if n % 3 != 1 else _board_edge_dark, Vector3(x, middle + 0.13, z),
			Vector3(0.26, 0.26, 0.05), Vector3(lean, 0, 0))
		for hinge: float in [-0.07, 0.07]:
			_box(_brass, Vector3(front + 0.018, middle + 0.13 + hinge, z), Vector3(0.006, 0.03, 0.04), Vector3(lean, 0, 0))
	# Top: a small set standing mid-game, white toward the room, and boxes of pieces
	# stacked two and three high.
	var shown := ChessSet.new()
	shown.position = Vector3(x, upper, from_z - 0.3)
	shown.rotation.y = -PI * 0.5
	shown.scale = Vector3.ONE * 0.55
	add_child(shown)
	shown.build("r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/3P1N2/PPP2PPP/RNBQK2R")
	for stack in 2:
		for n in 2 + (stack % 2):
			_piece_box(Vector3(x, upper + n * 0.065, from_z - 0.7 - stack * 0.36), 0.1 * sin(stack * 3.0 + n))


## A board's checkered face, lying flat at `at`, `size` metres square.
func _checks_face(at: Vector3, size: float, angles: Vector3) -> void:
	var face := PlaneMesh.new()
	face.size = Vector2(size, size)
	var shown := _mesh(self, face, _checks, at)
	shown.rotation = angles


## A sliding-lid wooden box of pieces, lying on `at`.
func _piece_box(at: Vector3, yaw: float) -> void:
	_box(_board_edge, at + Vector3(0, 0.03, 0), Vector3(0.12, 0.06, 0.24), Vector3(0, yaw, 0))
	_box(_board_edge_dark, at + Vector3(-0.002, 0.061, 0), Vector3(0.1, 0.004, 0.22), Vector3(0, yaw, 0))
	_box(_brass, at + Vector3(-0.061, 0.03, 0), Vector3(0.004, 0.02, 0.02), Vector3(0, yaw, 0))


## A cluster of 3-6 bottles in two staggered rows, at shelf height `at.y`, starting at
## `at.z` and running toward -z. Returns the length used.
func _bottle_cluster(at: Vector3, rng: RandomNumberGenerator, headroom: float) -> float:
	var count := 3 + rng.randi() % 4
	var kind := rng.randi() % _bottle_glass.size()
	var tall := minf(headroom - 0.03, 0.2 + rng.randf() * 0.1)
	var used := 0.0
	for n in count:
		var radius := 0.03 + rng.randf() * 0.01
		var row := -0.06 if n % 2 == 0 else 0.06
		var glass := _bottle_glass[kind if rng.randf() < 0.75 else rng.randi() % _bottle_glass.size()]
		var height := tall * (0.9 + rng.randf() * 0.15)
		_bottle(at + Vector3(row, 0, -used - radius), radius, height, glass, rng.randf() < 0.5)
		used += radius * (1.35 if n % 2 == 0 else 1.0)
	return used + 0.05


## One bottle standing on `at`: body, sloped shoulder, neck, a cork or wax seal, sometimes
## a paper label on the side facing the room.
func _bottle(at: Vector3, radius: float, height: float, glass: Material, label: bool) -> void:
	var body := height * 0.62
	_cylinder(self, at + Vector3(0, body * 0.5, 0), radius, radius, body, glass)
	_cylinder(self, at + Vector3(0, body + height * 0.06, 0), radius, radius * 0.38, height * 0.12, glass)
	_cylinder(self, at + Vector3(0, body + height * 0.25, 0), radius * 0.36, radius * 0.32, height * 0.26, glass)
	_cylinder(self, at + Vector3(0, height + 0.008, 0), radius * 0.4, radius * 0.4, 0.02, _seal)
	if label:
		var paper := QuadMesh.new()
		paper.size = Vector2(radius * 1.4, body * 0.5)
		var shown := _mesh(self, paper, _label, at + Vector3(-radius - 0.003, body * 0.5, 0))
		shown.rotation.y = -PI * 0.5


## A small cask standing on end, iron-hooped, with a wooden tap on the side facing the room.
func _keg(at: Vector3, height: float, tone: int) -> void:
	var radius := height * 0.42
	var staves: Material = [_mug_wood, _wood(Color("4a3020")), _wood(Color("5e3e24"))][tone]
	_cylinder(self, at + Vector3(0, height * 0.5, 0), radius * 0.88, radius * 0.88, height, staves)
	_cylinder(self, at + Vector3(0, height * 0.5, 0), radius, radius, height * 0.6, staves)
	for hoop: float in [0.12, 0.3, 0.7, 0.88]:
		var girth := radius * (1.02 if hoop > 0.2 and hoop < 0.8 else 0.92)
		_cylinder(self, at + Vector3(0, height * hoop, 0), girth, girth, 0.014, _iron)
	_box(_timber, at + Vector3(-radius - 0.03, height * 0.25, 0), Vector3(0.06, 0.025, 0.025))
	_box(_timber, at + Vector3(-radius - 0.055, height * 0.25 - 0.02, 0), Vector3(0.02, 0.04, 0.02))


## A clay pot: a fat belly narrowing to a short neck and lipped rim.
func _crock(at: Vector3, height: float, tone: int) -> void:
	var clay: Material = _clay[tone]
	var radius := height * (0.38 + 0.05 * (tone % 2))
	_cylinder(self, at + Vector3(0, height * 0.22, 0), radius * 0.7, radius, height * 0.44, clay)
	_cylinder(self, at + Vector3(0, height * 0.6, 0), radius, radius * 0.6, height * 0.32, clay)
	_cylinder(self, at + Vector3(0, height * 0.86, 0), radius * 0.55, radius * 0.55, height * 0.2, clay)
	_cylinder(self, at + Vector3(0, height * 0.97, 0), radius * 0.65, radius * 0.62, height * 0.06, clay)


## The house label: aged paper with red bands top and bottom and a pawn inked in the
## middle.
static func _label_material() -> StandardMaterial3D:
	const ART := [
		"RRRRRRRRRRRR",
		"............",
		"............",
		".....##.....",
		"....####....",
		"....####....",
		".....##.....",
		"....####....",
		".....##.....",
		".....##.....",
		"....####....",
		"...######...",
		"...######...",
		"............",
		"............",
		"RRRRRRRRRRRR",
	]
	var art := Image.create(12, 16, false, Image.FORMAT_RGB8)
	for y in ART.size():
		for x in 12:
			var mark: String = ART[y][x]
			art.set_pixel(x, y, Color("2a1a10") if mark == "#" else (Color("6a1a14") if mark == "R" else Color("b8a47c")))
	var material := StandardMaterial3D.new()
	material.albedo_texture = _crisp_texture(art, 16)
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	material.roughness = 0.95
	return material


## A tiny pixel image blown up `factor` times with hard edges and given mipmaps, so it stays
## sharp up close and smooth (not shimmering) further off at full resolution.
static func _crisp_texture(image: Image, factor: int) -> ImageTexture:
	var big := image.duplicate() as Image
	big.resize(image.get_width() * factor, image.get_height() * factor, Image.INTERPOLATE_NEAREST)
	big.generate_mipmaps()
	return ImageTexture.create_from_image(big)


static func _checker_material() -> StandardMaterial3D:
	var checks := Image.create(8, 8, false, Image.FORMAT_RGB8)
	for file in 8:
		for rank in 8:
			checks.set_pixel(file, rank, Color("b48c5e") if (file + rank) % 2 == 0 else Color("4a3021"))
	var material := StandardMaterial3D.new()
	material.albedo_texture = _crisp_texture(checks, 32)
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS_ANISOTROPIC
	material.roughness = 0.55
	return material


## A tall bar stool: a round worn seat on four splayed legs with a footrest ring.
func _bar_stool(at: Vector3, twist: float) -> void:
	const SEAT := 0.76
	_cylinder(self, at + Vector3(0, SEAT - 0.025, 0), 0.2, 0.19, 0.05, _chair_wood)
	for k in 4:
		var a := twist + TAU * (k + 0.5) / 4.0
		var foot := at + Vector3(cos(a) * 0.22, 0, sin(a) * 0.22)
		var top := at + Vector3(cos(a) * 0.12, SEAT - 0.05, sin(a) * 0.12)
		_strut(_chair_wood_dark, foot, top, 0.045)
		var low := foot.lerp(top, 0.32)
		var next := TAU * (k + 1.5) / 4.0 + twist
		var low_next := (at + Vector3(cos(next) * 0.22, 0, sin(next) * 0.22)).lerp(at + Vector3(cos(next) * 0.12, SEAT - 0.05, sin(next) * 0.12), 0.32)
		_strut(_iron, low, low_next, 0.02)


## The same stool as _bar_stool, built under `parent` (at its origin) as plain meshes so it
## can be moved.
func _movable_stool(parent: Node3D, twist: float) -> void:
	const SEAT := 0.76
	_cylinder(parent, Vector3(0, SEAT - 0.025, 0), 0.2, 0.19, 0.05, _chair_wood)
	for k in 4:
		var a := twist + TAU * (k + 0.5) / 4.0
		var foot := Vector3(cos(a) * 0.22, 0, sin(a) * 0.22)
		var top := Vector3(cos(a) * 0.12, SEAT - 0.05, sin(a) * 0.12)
		_strut_mesh(parent, _chair_wood_dark, foot, top, 0.045)
		var next := TAU * (k + 1.5) / 4.0 + twist
		var low_next := Vector3(cos(next) * 0.22, 0, sin(next) * 0.22).lerp(Vector3(cos(next) * 0.12, SEAT - 0.05, sin(next) * 0.12), 0.32)
		_strut_mesh(parent, _iron, foot.lerp(top, 0.32), low_next, 0.02)


## A square bar from `from` to `to` as its own mesh under `parent`.
func _strut_mesh(parent: Node3D, material: Material, from: Vector3, to: Vector3, thickness: float) -> void:
	var along := to - from
	var up := along.normalized()
	var side := up.cross(Vector3.FORWARD if absf(up.z) < 0.9 else Vector3.RIGHT).normalized()
	var bar := _box_mesh(parent, material, (from + to) * 0.5, Vector3(thickness, along.length(), thickness))
	bar.basis = Basis(side, up, side.cross(up))


func _build_chess_corner() -> void:
	# A heavy plank table, short end hard against the left wall under the shuttered window.
	var at := CHESS_TABLE_AT
	var top := ShaderMaterial.new()
	top.shader = OLD_WOOD
	top.set_shader_parameter("wood_color", Color("4a3020"))
	for plank in 4:
		_box(top, at + Vector3(0, -0.03, -0.3 + plank * 0.2), Vector3(1.2, 0.06, 0.195))
	for x: int in [-1, 1]:
		for z: int in [-1, 1]:
			_box(_timber, at + Vector3(x * 0.5, -0.4, z * 0.32), Vector3(0.09, 0.74, 0.09))
	_box(_timber, at + Vector3(0, -0.6, 0), Vector3(1.0, 0.07, 0.07))
	_chair(BARTENDER_SEAT + Vector3(0, 0, -0.06), 0.04)
	_chair(PLAYER_SEAT + Vector3(0, 0, 0.06), PI - 0.06)
	# A stub of candle on a plate at the wall end, and a forgotten tankard.
	_place_prop(PLATE, at + Vector3(-0.42, 0.0, -0.18), 0.025, 0.0)
	var wax := _paint(Color("cbb89a"), 0.7)
	_cylinder(self, at + Vector3(-0.42, 0.06, -0.18), 0.022, 0.02, 0.1, wax)
	_box(_glowing(Color("ffc06a"), 3.0), at + Vector3(-0.42, 0.125, -0.18), Vector3(0.012, 0.025, 0.012))
	_candle_light = _omni(at + Vector3(-0.38, 0.32, -0.12), Color("ffb066"), 0.55, 2.4)
	# A smoky wall lamp over the corner so the bartender reads from across the room.
	_box(_iron, Vector3(-4.88, 2.05, -5.15), Vector3(0.22, 0.04, 0.04))
	_box(_glowing(Color("ffb35c"), 2.2), Vector3(-4.78, 1.95, -5.15), Vector3(0.07, 0.12, 0.07))
	_omni(Vector3(-4.6, 1.9, -5.0), Color("ff9d52"), 0.9, 3.2)
	_wooden_mug(at + Vector3(0.45, 0.0, 0.2), 2.1, false)


func _build_tables() -> void:
	# Small tables: chairs pushed in or left askew, one knocked over, the night's leftovers
	# on the tops. Each entry: centre, then the sides that keep a
	# chair (+z, -z, +x, -x), and what was left on the top.
	var tables := [
		[TABLE_GRID[0], [true, true, false, false], "goblets"],
		[TABLE_GRID[1], [true, true, false, false], "plate"],
		[BACK_TABLE_AT, [false, true, true, true], "plate"],
	]
	for i in tables.size():
		var entry: Array = tables[i]
		var at: Vector3 = entry[0]
		_square_table(at)
		var sides: Array = entry[1]
		for side in 4:
			if not sides[side]:
				continue
			var yaw: float = [PI, 0.0, -PI * 0.5, PI * 0.5][side]
			var spot := at + Basis(Vector3.UP, yaw) * Vector3(0, 0, -CHAIR_REACH)
			var jitter := 0.09 * sin(i * 3.1 + side * 1.7)
			spot += Basis(Vector3.UP, yaw) * Vector3(0, 0, -0.08 * absf(sin(i + side * 2.3)))
			if i == 1 and side == 0:
				_fallen_chair(spot + Vector3(0.2, 0, 0.35), yaw + 0.5)
			else:
				_chair(spot, yaw + jitter)
				var key := "%d:%d" % [i, side]
				if _seated.has(key):
					_add_customer(_seated[key], spot, yaw + jitter)
		match entry[2]:
			"goblets":
				_wooden_mug(at + Vector3(0.12, TABLE_HEIGHT, -0.05), i * 0.9, false)
				_wooden_mug(at + Vector3(0.22, TABLE_HEIGHT, 0.1), i * 1.7, true)
			"plate":
				_place_prop(PLATE, at + Vector3(-0.15, 0.765, 0.12), 0.025, i)
				_place_prop(BOWL, at + Vector3(0.2, 0.77, 0.18), 0.07, 0.0)
		# A candle on every table, burning only where someone is drinking.
		var occupied := false
		for key: String in _seated:
			occupied = occupied or key.begins_with("%d:" % i)
		_table_candle(at + Vector3(-0.22, TABLE_HEIGHT, -0.22), occupied, i)
	_place_prop(BENCH, Vector3(-4.55, 0, -8.6), 0.45, PI * 0.5)
	_place_prop(BENCH, Vector3(-4.55, 0, -1.3), 0.45, PI * 0.5)
	_build_long_table()


## The long table on the hearth's line: chess games down its length, players hunched over
## them, drinkers in between, a couple of onlookers standing behind.
func _build_long_table() -> void:
	var length := LONG_TABLE_FROM - LONG_TABLE_TO
	var mid := Vector3(LONG_TABLE_X, 0, (LONG_TABLE_FROM + LONG_TABLE_TO) * 0.5)
	for plank in 4:
		_box(_table_wood, mid + Vector3(-0.3375 + plank * 0.225, TABLE_HEIGHT - 0.03, 0),
			Vector3(0.22, 0.055, length))
	for leg_z: float in [-length * 0.5 + 0.2, 0.0, length * 0.5 - 0.2]:
		for x: int in [-1, 1]:
			_box(_timber, mid + Vector3(x * 0.36, TABLE_HEIGHT * 0.5 - 0.03, leg_z),
				Vector3(0.09, TABLE_HEIGHT - 0.06, 0.09))
		_box(_timber, mid + Vector3(0, 0.16, leg_z), Vector3(0.72, 0.06, 0.05))
	for x: int in [-1, 1]:
		_box(_timber, mid + Vector3(x * 0.36, TABLE_HEIGHT - 0.1, 0), Vector3(0.03, 0.07, length - 0.3))
	_box(_timber, mid + Vector3(0, 0.16, 0), Vector3(0.06, 0.06, length - 0.3))

	var rng := RandomNumberGenerator.new()
	rng.seed = 2207
	# Per seat pair: who sits on the -x and +x sides ("" leaves the chair empty).
	var layout := [
		["lean", ""], ["play", "play"], ["play", "play"], ["sit", "lean"], ["play", "play"],
		["", "sit"], ["play", "play"], ["play", "play"], ["slump", ""],
	]
	for k in LONG_TABLE_SEATS:
		var z := LONG_TABLE_FROM - 0.35 - k * LONG_TABLE_PITCH
		for side in 2:
			var out := -1.0 if side == 0 else 1.0
			var yaw := PI * 0.5 if side == 0 else -PI * 0.5
			var jitter := 0.07 * sin(k * 2.3 + side * 1.9)
			var spot := Vector3(LONG_TABLE_X + out * (CHAIR_REACH + 0.04 * absf(sin(k + side * 3.1))), 0, z)
			_chair(spot, yaw + jitter)
			var pose: String = layout[k][side]
			if pose == "":
				continue
			var spec := _regular(rng, pose)
			if k == 0 and side == 0:
				spec = _big_man
			_add_customer(spec, spot, yaw + jitter)
			if spec.get("mug", false) or pose == "play":
				_wooden_mug(Vector3(LONG_TABLE_X + out * 0.3, TABLE_HEIGHT, z + 0.2 * out), rng.randf() * TAU,
					rng.randf() < 0.7)
		if LONG_TABLE_GAMES.has(k):
			var board := ChessSet.new()
			board.position = Vector3(LONG_TABLE_X, TABLE_HEIGHT, z)
			board.rotation.y = -PI * 0.5 + 0.05 * sin(k * 1.7)
			add_child(board)
			board.build(LONG_TABLE_GAMES[k][0], LONG_TABLE_GAMES[k][1])
	for candle_k: int in [0, 3, 5, 8]:
		_table_candle(Vector3(LONG_TABLE_X + 0.05, TABLE_HEIGHT, LONG_TABLE_FROM - 0.35 - candle_k * LONG_TABLE_PITCH + 0.3),
			true, candle_k + 11)
	# Onlookers standing behind two of the games on the fire side (the bar side is the way
	# in), arms folded.
	for watcher: Array in [[2, 0], [6, 0]]:
		var out := -1.0 if watcher[1] == 0 else 1.0
		var z: float = LONG_TABLE_FROM - 0.35 - watcher[0] * LONG_TABLE_PITCH + 0.25
		_add_customer(_regular(rng, "watch"), Vector3(LONG_TABLE_X + out * 1.15, 0, z),
			PI * 0.5 if watcher[1] == 0 else -PI * 0.5)


## A random regular for the long table, usually in a poor way: build, colouring, clothes
## and the night's damage all drawn from `rng`.
func _regular(rng: RandomNumberGenerator, pose: String) -> Dictionary:
	const SKINS := ["c49a7c", "b08870", "a8806a", "8a5c40", "6e4a34", "d0a890", "b8a07c", "9a6a4a", "5e3e2c"]
	const SHIRTS := ["6b5d45", "5a4a3a", "4a5040", "3e4a3a", "5a3030", "7a6a50", "4a3b2a", "665848", "3a3a44"]
	const LEGS := ["3a3028", "2b2a26", "2e2a24", "4a3d2c", "332c26"]
	const HAIR := ["2a2018", "1a1410", "4a3020", "7a4020", "8a8478", "3a2a1a", "5a4a3a"]
	const VESTS := ["3a2a20", "2e2620", "4a3a28"]
	const WEAR := ["red_nose", "bags", "black_eye", "torn_sleeve", "patch_shoulder", "patch_knee",
		"hole", "stain", "frayed", "hat", "bags", "red_nose", "eyepatch", "bandage"]
	var hair := Color(HAIR[rng.randi() % HAIR.size()])
	var spec := {
		"pose": pose,
		# Short, tall and everything between, so the room never reads as one body copied.
		"height": rng.randf_range(0.78, 1.2),
		"girth": rng.randf_range(0.78, 1.2),
		"belly": maxf(0.0, rng.randf_range(-0.4, 0.8)),
		"skin": Color(SKINS[rng.randi() % SKINS.size()]),
		"shirt": Color(SHIRTS[rng.randi() % SHIRTS.size()]),
		"legs": Color(LEGS[rng.randi() % LEGS.size()]),
		"beard": ["none", "stubble", "stubble", "full"][rng.randi() % 4],
		"beard_color": hair,
		"mug": pose == "sit" or pose == "lean",
		"phase": rng.randf() * TAU,
	}
	if rng.randf() < 0.85:
		spec["hair"] = hair
		spec["bald"] = rng.randf() < 0.3
	if rng.randf() < 0.35:
		spec["vest"] = Color(VESTS[rng.randi() % VESTS.size()])
	var wear: Array[String] = []
	for i in rng.randi_range(1, 4):
		var item: String = WEAR[rng.randi() % WEAR.size()]
		if not wear.has(item) and not (item == "hat" and wear.has("bandage")) and not (item == "bandage" and wear.has("hat")):
			wear.append(item)
	spec["wear"] = wear
	return spec


## A square plank table, 0.9 m a side, its top at TABLE_HEIGHT.
func _square_table(at: Vector3) -> void:
	const SIDE := 0.9
	for plank in 4:
		_box(_table_wood, at + Vector3(0, TABLE_HEIGHT - 0.03, -0.3375 + plank * 0.225),
			Vector3(SIDE, 0.055, 0.22))
	for x: int in [-1, 1]:
		for z: int in [-1, 1]:
			_box(_timber, at + Vector3(x * 0.37, TABLE_HEIGHT * 0.5 - 0.03, z * 0.37),
				Vector3(0.075, TABLE_HEIGHT - 0.06, 0.075))
	for axis in 2:
		var size := Vector3(0.66, 0.06, 0.03) if axis == 0 else Vector3(0.03, 0.06, 0.66)
		for side: int in [-1, 1]:
			var offset := Vector3(0, 0, side * 0.37) if axis == 0 else Vector3(side * 0.37, 0, 0)
			_box(_timber, at + Vector3(0, TABLE_HEIGHT - 0.1, 0) + offset, size)


## A worn ladder-back chair. `at` is on the floor under the seat; the sitter faces +Z
## rotated by `yaw`, with the back rest on the -Z side.
func _chair(at: Vector3, yaw: float) -> void:
	_chair_at(Transform3D(Basis(Vector3.UP, yaw), at), absi(int(at.x * 7.0 + at.z * 13.0)))


## The same chair tipped over onto its back.
func _fallen_chair(at: Vector3, yaw: float) -> void:
	var tipped := Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, -PI * 0.5)
	_chair_at(Transform3D(tipped, at + Vector3(0, 0.235, 0)), 3)


func _chair_at(frame: Transform3D, wear: int) -> void:
	const SEAT := 0.47
	const HALF := 0.2
	var parts := [
		# Seat boards, the back one split shorter on some chairs.
		[Vector3(0, SEAT - 0.02, 0.1), Vector3(0.42, 0.035, 0.2)],
		[Vector3(0, SEAT - 0.02, -0.1), Vector3(0.42 if wear % 3 else 0.36, 0.035, 0.19)],
		# Front legs, and the back legs running up into the back rest.
		[Vector3(-HALF + 0.02, SEAT * 0.5 - 0.02, HALF - 0.02), Vector3(0.04, SEAT - 0.04, 0.04)],
		[Vector3(HALF - 0.02, SEAT * 0.5 - 0.02, HALF - 0.02), Vector3(0.04, SEAT - 0.04, 0.04)],
		[Vector3(-HALF + 0.02, 0.49, -HALF + 0.02), Vector3(0.045, 0.98, 0.045)],
		[Vector3(HALF - 0.02, 0.49, -HALF + 0.02), Vector3(0.045, 0.98, 0.045)],
		# Ladder rails across the back, and stretchers low between the legs.
		[Vector3(0, 0.94, -HALF + 0.02), Vector3(0.4, 0.08, 0.03)],
		[Vector3(0, 0.78, -HALF + 0.02), Vector3(0.36, 0.05, 0.025)],
		[Vector3(0, 0.15, HALF - 0.02), Vector3(0.36, 0.03, 0.03)],
		[Vector3(-HALF + 0.02, 0.12, 0), Vector3(0.025, 0.025, 0.36)],
		[Vector3(HALF - 0.02, 0.12, 0), Vector3(0.025, 0.025, 0.36)],
	]
	if wear % 4 != 1:
		parts.append([Vector3(0, 0.63, -HALF + 0.02), Vector3(0.36, 0.05, 0.025)])
	var material := _chair_wood if wear % 2 else _chair_wood_dark
	for part: Array in parts:
		var local := Transform3D(Basis.from_scale(part[1]), part[0])
		_batches.get_or_add(material, []).append(frame * local)


## A wooden beer mug standing on a surface (`at` is on the surface): staves with two iron
## hoops and a handle on the `yaw` side, a head of foam when `full`. `size` scales it.
func _wooden_mug(at: Vector3, yaw: float, full: bool, size := 1.0) -> void:
	_mug_parts(self, at, yaw, full, size, false)


## A mug's parts under `parent`: batched into the room's meshes, or with `movable` as plain
## meshes of `parent` so the whole mug can be picked up.
func _mug_parts(parent: Node3D, at: Vector3, yaw: float, full: bool, size: float, movable: bool) -> void:
	var radius := 0.045 * size
	var tall := 0.13 * size
	var body := CylinderMesh.new()
	body.bottom_radius = radius
	body.top_radius = radius * 0.9
	body.height = tall
	body.radial_segments = 48
	body.rings = 0
	var shown := _mesh(parent, body, _mug_staves(size), at + Vector3(0, tall * 0.5, 0))
	# Turn the staves per mug so no two line up the same.
	shown.rotation.y = yaw * 1.7
	for hoop: float in [0.025 * size, tall - 0.025 * size]:
		var girth := radius * 1.04 - hoop * 0.03
		_cylinder(parent, at + Vector3(0, hoop, 0), girth, girth, 0.012 * size, _iron)
	# A bent wooden handle: a rounded C from below the top hoop to above the bottom one.
	var handle := _mesh(parent, _mug_handle(size), _mug_handle_wood, at)
	handle.rotation.y = yaw
	if full:
		var head := CylinderMesh.new()
		head.bottom_radius = radius * 0.92
		head.top_radius = radius * 0.85
		head.height = 0.016 * size
		head.radial_segments = 48
		head.rings = 1
		_mesh(parent, head, _foam_material(), at + Vector3(0, tall + 0.006 * size, 0))
		# The head heaped a little higher in the middle.
		var dome := SphereMesh.new()
		dome.radius = radius * 0.84
		dome.height = 0.02 * size
		dome.is_hemisphere = true
		dome.radial_segments = 48
		dome.rings = 8
		_mesh(parent, dome, _foam_material(), at + Vector3(0, tall + 0.0135 * size, 0))


var _mug_staves_by_size := {}
var _mug_handles_by_size := {}
var _foam_shader_material: ShaderMaterial


## Stave-built mug wood for a mug of `size` (the shader needs its cap radius and height).
func _mug_staves(size: float) -> ShaderMaterial:
	if not _mug_staves_by_size.has(size):
		var material := ShaderMaterial.new()
		material.shader = MUG_STAVES
		material.set_shader_parameter("wood_color", Color("5a3a22"))
		material.set_shader_parameter("top_radius", 0.045 * size * 0.9)
		material.set_shader_parameter("wall", 0.0055 * size)
		material.set_shader_parameter("height", 0.13 * size)
		material.set_shader_parameter("staves", 12.0 + size)
		_mug_staves_by_size[size] = material
	return _mug_staves_by_size[size]


func _foam_material() -> ShaderMaterial:
	if not _foam_shader_material:
		_foam_shader_material = ShaderMaterial.new()
		_foam_shader_material.shader = FOAM
		_foam_shader_material.set_shader_parameter("foam_color", Color("d8c8a0"))
	return _foam_shader_material


## A mug handle for a mug of `size`, standing on the origin and bowing out along +z: a tube
## of rounded-oblong section swept round a C, its ends sunk into the mug's wall.
func _mug_handle(size: float) -> ArrayMesh:
	if _mug_handles_by_size.has(size):
		return _mug_handles_by_size[size]
	const ALONG := 24
	const AROUND := 12
	var radius := 0.045 * size
	var tall := 0.13 * size
	var centre := Vector3(0, tall * 0.53, radius - 0.004 * size)
	var reach := 0.034 * size
	var drop := 0.042 * size
	var wide := 0.009 * size
	var thick := 0.0065 * size
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	var rings: Array = []
	for i in ALONG + 1:
		var t := PI * i / ALONG
		# A C bowed out along +z, a little fuller at the top where the thumb rests.
		var swell := 1.0 + 0.12 * cos(t)
		var spine := centre + Vector3(0, drop * cos(t) * swell, reach * sin(t))
		var tangent := Vector3(0, -drop * sin(t) * swell, reach * cos(t)).normalized()
		var side := Vector3.RIGHT
		var normal := tangent.cross(side).normalized()
		var ring: Array = []
		for k in AROUND:
			var a := TAU * k / AROUND
			ring.append(spine + side * cos(a) * wide + normal * sin(a) * thick)
		rings.append(ring)
	for i in ALONG:
		for k in AROUND:
			var k2 := (k + 1) % AROUND
			var a: Vector3 = rings[i][k]
			var b: Vector3 = rings[i + 1][k]
			var c: Vector3 = rings[i + 1][k2]
			var d: Vector3 = rings[i][k2]
			tool.set_uv(Vector2(float(k) / AROUND, float(i) / ALONG))
			for v: Vector3 in [a, c, b, a, d, c]:
				tool.add_vertex(v)
	tool.generate_normals()
	var mesh := tool.commit()
	_mug_handles_by_size[size] = mesh
	return mesh


## Round parts at full resolution: enough sides that a mug or a crock reads round, scaled by
## its size so the many small bottles stay cheap.
func _cylinder(parent: Node3D, at: Vector3, foot: float, top: float, height: float, material: Material) -> void:
	var cylinder := CylinderMesh.new()
	cylinder.bottom_radius = foot
	cylinder.top_radius = top
	cylinder.height = height
	cylinder.radial_segments = clampi(int(maxf(foot, top) * 500.0), 16, 56)
	cylinder.rings = 0
	_mesh(parent, cylinder, material, at)


## A candle stub in an iron dish on a table top (`at` is on the top). Lit ones get a flame
## and a soft halo; cold ones a blackened wick. No light of their own: the room already has
## as many as the renderer will mix on one surface.
func _table_candle(at: Vector3, lit: bool, variant: int) -> void:
	var tall := 0.05 + 0.05 * fposmod(variant * 0.37, 1.0)
	_box(_iron, at + Vector3(0, 0.008, 0), Vector3(0.1, 0.012, 0.1))
	_cylinder(self, at + Vector3(0, 0.014 + tall * 0.5, 0), 0.02, 0.018, tall, _candle_wax)
	_box(_candle_wax, at + Vector3(0.021, 0.014 + tall * 0.35, 0.004), Vector3(0.008, tall * 0.5, 0.01))
	_box(_candle_wax, at + Vector3(0.03, 0.016, 0.01), Vector3(0.03, 0.006, 0.025))
	var wick := at + Vector3(0, 0.014 + tall, 0)
	_box(_ash, wick + Vector3(0, 0.006, 0), Vector3(0.004, 0.012, 0.004))
	if lit:
		_box(_candle_flame, wick + Vector3(0, 0.022, 0), Vector3(0.012, 0.028, 0.012))
		var glow := QuadMesh.new()
		glow.size = Vector2(0.26, 0.26)
		_mesh(self, glow, _halo, wick + Vector3(0, 0.02, 0)).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _add_customer(spec: Dictionary, at: Vector3, yaw: float) -> void:
	var customer := Node3D.new()
	customer.set_script(Customer)
	customer.position = at
	customer.rotation.y = yaw
	add_child(customer)
	customer.build(spec)
	_patrons.append(customer)


func _build_customers_at_bar() -> void:
	# Two drinkers propped on the counter, facing the bar.
	_add_customer(_at_bar[0], Vector3(BAR_X - 0.75, 0, -7.4), PI * 0.5 + 0.15)
	_bar_regulars.append(_patrons.back())
	_add_customer(_at_bar[1], Vector3(BAR_X - 0.75, 0, -6.1), PI * 0.5 - 0.2)
	_bar_regulars.append(_patrons.back())


## The two at the bar look round at whoever just came in, one a moment after the other.
func bar_regulars_glance(at: Node3D) -> void:
	for k in _bar_regulars.size():
		get_tree().create_timer(0.35 * k).timeout.connect(_bar_regulars[k].glance_at.bind(at, 1.1 - 0.2 * k))


func _build_patrons() -> void:
	# Two hooded figures hunched over one drink by the fire, faces in shadow.
	_square_table(HUDDLE_AT)
	_wooden_mug(HUDDLE_AT + Vector3(-0.2, TABLE_HEIGHT, 0.05), 0.4, true)
	_wooden_mug(HUDDLE_AT + Vector3(0.1, TABLE_HEIGHT, 0.22), 2.6, true)
	_place_prop(JUG, HUDDLE_AT + Vector3(-0.14, 0.77, -0.1), 0.2, 1.3)
	_table_candle(HUDDLE_AT + Vector3(0.22, TABLE_HEIGHT, -0.2), true, 7)
	var seats := [
		[Vector3(-0.68, 0, -0.04), Color("141210"), false, 0.0],
		[Vector3(0.06, 0, 0.68), Color("141811"), true, 2.4],
	]
	for seat: Array in seats:
		var at: Vector3 = HUDDLE_AT + seat[0]
		var facing := _yaw_toward(at, HUDDLE_AT)
		_chair(at + Basis(Vector3.UP, facing) * Vector3(0, 0, -0.08), facing)
		var patron := Node3D.new()
		patron.set_script(Patron)
		patron.position = at
		patron.rotation.y = facing
		add_child(patron)
		var glance := clampf(wrapf(_yaw_toward(at, BAR_STOOL_AT) - facing, -PI, PI), -1.1, 1.1)
		patron.build(seat[1], seat[2], seat[3], glance)
		_patrons.append(patron)
	# Their peg rail on the back wall: spare cloaks and a hat, breaking up the firelit plaster.
	var wall_z := BACK_WALL + 0.06
	_box(_old_timber, Vector3(1.4, 2.05, wall_z), Vector3(1.3, 0.08, 0.05))
	for peg in 4:
		_box(_old_timber, Vector3(0.9 + peg * 0.33, 2.03, wall_z + 0.07), Vector3(0.03, 0.03, 0.12))
	var wool := _paint(Color("1a1714"), 0.98)
	var oilcloth := _paint(Color("232118"), 0.85)
	_box(wool, Vector3(0.9, 1.55, wall_z + 0.1), Vector3(0.32, 0.95, 0.08), Vector3(0.05, 0, 0.03))
	_box(wool, Vector3(0.9, 1.98, wall_z + 0.13), Vector3(0.22, 0.14, 0.12))
	_box(oilcloth, Vector3(1.56, 1.6, wall_z + 0.1), Vector3(0.36, 0.85, 0.09), Vector3(0.04, 0, -0.04))
	_box(oilcloth, Vector3(1.89, 1.94, wall_z + 0.12), Vector3(0.24, 0.05, 0.22))
	_box(oilcloth, Vector3(1.89, 2.0, wall_z + 0.12), Vector3(0.15, 0.09, 0.14))


static func _plaster_material(color: Color) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = OLD_PLASTER
	material.set_shader_parameter("plaster_color", color)
	material.set_shader_parameter("top", SPRING)
	return material


static func _wood(color: Color) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.shader = OLD_WOOD
	material.set_shader_parameter("wood_color", color)
	return material


static func _yaw_toward(from: Vector3, to: Vector3) -> float:
	return atan2(to.x - from.x, to.z - from.z)


func _build_clutter() -> void:
	_place_prop(CRATE, Vector3(2.6, 0, -10.4), 0.5, 0.3)
	_place_prop(CRATE, Vector3(2.75, 0.5, -10.35), 0.42, 0.9)
	_place_prop(BUCKET, Vector3(2.0, 0, -10.5), 0.34, 0.0)
	# Barrels piled in the back-left corner, one up on top.
	_place_prop(BARREL, Vector3(-4.45, 0, -10.35), 0.88, 1.1)
	_place_prop(BARREL, Vector3(-3.7, 0, -10.4), 0.88, 0.4)
	_place_prop(BARREL, Vector3(-4.5, 0, -9.6), 0.84, 2.3)
	_place_prop(BARREL, Vector3(-4.1, 0.88, -10.35), 0.72, 0.8)
	_place_prop(WINE_BARREL, Vector3(-3.55, 0, -9.55), 0.62, 0.3)
	# Straw on the boards and a rug before the hearth.
	var straw := _paint(Color("6b5a35"), 0.9)
	var rng := RandomNumberGenerator.new()
	rng.seed = 1662
	for i in 40:
		var at := Vector3(rng.randf_range(LEFT_WALL + 0.2, RIGHT_WALL - 0.2), 0.006, rng.randf_range(-10.6, 0.6))
		_box(straw, at, Vector3(0.12, 0.005, 0.008), Vector3(0, rng.randf() * TAU, 0))
	var rug := _paint(Color("4a2117"), 0.98)
	var weave := _paint(Color("6a4a2a"), 0.98)
	var rug_at := Vector3(HEARTH_AT.x, 0.012, BACK_WALL + 1.85)
	_box(rug, rug_at, Vector3(1.9, 0.012, 1.05), Vector3(0, 0.05, 0))
	for stripe: float in [-0.75, 0.75]:
		_box(weave, rug_at + Vector3(stripe, 0.002, 0.04 * stripe), Vector3(0.12, 0.012, 1.0), Vector3(0, 0.05, 0))
	_box(weave, rug_at + Vector3(0, 0.002, 0), Vector3(1.0, 0.012, 0.1), Vector3(0, 0.05, 0))


## A small iron lantern: hanging loop, cap, glass lit by the flame inside, a soft halo
## and a base plate.
## The great chandelier: a wrought-iron wheel of candles hung from the ridge. A scrolled outer
## ring of twelve candles on curved spokes, a raised inner ring of six, a turned hub with a drop
## finial, link chains to a hook plate on the ridge beam. Years of dust lie on every top
## surface, with small cobwebs between the chains and stray strands hanging below.
## `at` is the centre of the outer ring.
func _build_chandelier(at: Vector3) -> void:
	if not _halo:
		_halo = _animated_material(HALO)
	var wrought := _paint(Color("1f1c1a"), 0.55, 0.6)
	var dust := _paint(Color("5f5952"), 1.0)
	var strand := _paint(Color("7a746c"), 1.0)
	const R := CHANDELIER_RADIUS
	const INNER := 0.42
	const INNER_RISE := 0.3
	# Outer ring: a broad band with a thin twisted rail below, tied by short bars, dusty on top.
	_iron_ring(wrought, at, R, 28, Vector3(0, 0.05, 0.03))
	_iron_ring(dust, at + Vector3(0, 0.027, 0), R, 28, Vector3(0, 0.006, 0.028))
	_iron_ring(wrought, at + Vector3(0, -0.11, 0), R - 0.01, 28, Vector3(0, 0.018, 0.018))
	for k in 28:
		var a := TAU * (k + 0.5) / 28.0
		_box(wrought, at + Vector3(cos(a) * R, -0.055, sin(a) * R), Vector3(0.01, 0.1, 0.01))
	# Raised inner ring.
	var upper := at + Vector3(0, INNER_RISE, 0)
	_iron_ring(wrought, upper, INNER, 18, Vector3(0, 0.04, 0.025))
	_iron_ring(dust, upper + Vector3(0, 0.022, 0), INNER, 18, Vector3(0, 0.005, 0.023))
	# The hub: a turned shaft with collars, a bulb below and a drop finial.
	_cylinder(self, at + Vector3(0, 0.1, 0), 0.04, 0.035, 0.75, wrought)
	for collar: float in [-0.02, INNER_RISE, 0.42]:
		_cylinder(self, at + Vector3(0, collar, 0), 0.07, 0.07, 0.035, wrought)
		_cylinder(self, at + Vector3(0, collar + 0.019, 0), 0.066, 0.066, 0.005, dust)
	_cylinder(self, at + Vector3(0, -0.14, 0), 0.05, 0.11, 0.16, wrought)
	_cylinder(self, at + Vector3(0, -0.28, 0), 0.02, 0.05, 0.12, wrought)
	_cylinder(self, at + Vector3(0, -0.37, 0), 0.005, 0.02, 0.07, wrought)
	# Eight curved spokes from hub to rim, each ending in a curl outside the ring, and struts
	# rising from the rim to the inner ring.
	for k in 8:
		var a := TAU * k / 8.0
		var out := Vector3(cos(a), 0, sin(a))
		var path: Array[Vector3] = [at + out * 0.06, at + out * 0.3 + Vector3(0, -0.12, 0),
			at + out * 0.6 + Vector3(0, -0.1, 0), at + out * R]
		for n in path.size() - 1:
			_strut(wrought, path[n], path[n + 1], 0.022)
		_strut(wrought, at + out * (R - 0.02), upper + out * INNER, 0.016)
		# The curl: a small loop in the spoke's vertical plane, hanging out past the rim.
		var curl := at + out * (R + 0.07) + Vector3(0, -0.03, 0)
		var last := at + out * R
		for n in 7:
			var t := TAU * (n + 1) / 7.0 + PI
			var next := curl + out * (0.06 * cos(t)) + Vector3(0, 0.06 * sin(t), 0)
			_strut(wrought, last, next, 0.012)
			last = next
	# Candles: twelve on the outer ring, six on the inner, of uneven length; a few burnt out.
	var rng := RandomNumberGenerator.new()
	rng.seed = 6113
	for k in 18:
		var outer := k < 12
		var a := TAU * (k / 12.0 if outer else (k - 12 + 0.5) / 6.0)
		var foot := (at + Vector3(cos(a) * R, 0.025, sin(a) * R)) if outer \
			else (upper + Vector3(cos(a) * INNER, 0.02, sin(a) * INNER))
		_chandelier_candle(foot, rng, k % 7 != 3, wrought, dust)
	# Chains: four from the outer ring and one from the hub, up to a hook plate on the ridge.
	var hook := Vector3(at.x, APEX - 0.2, at.z)
	_box(wrought, hook + Vector3(0, 0.03, 0), Vector3(0.22, 0.03, 0.22))
	_box(dust, hook + Vector3(0, 0.047, 0), Vector3(0.2, 0.005, 0.2))
	_cylinder(self, hook + Vector3(0, -0.03, 0), 0.03, 0.03, 0.07, wrought)
	var anchors: Array[Vector3] = [at + Vector3(0, 0.48, 0)]
	for k in 4:
		var a := TAU * (k + 0.5) / 4.0
		anchors.append(at + Vector3(cos(a) * R, 0.03, sin(a) * R))
	for anchor in anchors:
		_chain(wrought, anchor, hook + Vector3(0, -0.06, 0))
	# Small cobwebs strung between two of the chains and the rim, one across the inner ring.
	_web_sheet(at + Vector3(0, 0.62, 0), 0.5, 0.42, PI * 0.25, 2.3, 0.85)
	_web_sheet(at + Vector3(0.3, 0.3, -0.3), 0.36, 0.26, -PI * 0.25, 5.1, 0.8)
	_web_sheet(upper + Vector3(-0.2, 0.18, 0.2), 0.3, 0.24, PI * 0.75, 8.7, 0.75)
	# Stray strands of web and dust hanging from the rims.
	for k in 14:
		var a := rng.randf() * TAU
		var radius := R if k < 10 else INNER
		var from := (at if k < 10 else upper) + Vector3(cos(a) * radius, -0.02, sin(a) * radius)
		var length := rng.randf_range(0.06, 0.24)
		_box(strand, from + Vector3(0, -length * 0.5, 0), Vector3(0.005, length, 0.005), Vector3(0, 0, rng.randf_range(-0.15, 0.15)))
	# The whole wheel glows warm through the smoke.
	var bloom := QuadMesh.new()
	bloom.size = Vector2(2.4, 1.3)
	_mesh(self, bloom, _halo, at + Vector3(0, 0.12, 0)).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## A ring of `count` boxes around `centre` (horizontal), `size` giving (unused, height, width).
func _iron_ring(material: Material, centre: Vector3, radius: float, count: int, size: Vector3) -> void:
	var chord := TAU * radius / count * 1.06
	for k in count:
		var a := TAU * (k + 0.5) / count
		_box(material, centre + Vector3(cos(a) * radius, 0, sin(a) * radius),
			Vector3(chord, size.y, size.z), Vector3(0, -a - PI * 0.5, 0))


## A square bar of `thickness` from `from` to `to`.
func _strut(material: Material, from: Vector3, to: Vector3, thickness: float, twist := 0.0) -> void:
	var along := to - from
	var up := along.normalized()
	var side := up.cross(Vector3.FORWARD if absf(up.z) < 0.9 else Vector3.RIGHT).normalized()
	var basis := Basis(side, up, side.cross(up)).rotated(up, twist) if twist != 0.0 else Basis(side, up, side.cross(up))
	if not _batches.has(material):
		_batches[material] = []
	_batches[material].append(Transform3D(basis * Basis.from_scale(Vector3(thickness, along.length(), thickness)),
		(from + to) * 0.5))


## A chain of open links from `from` up to `to`, each link turned a quarter to the last.
func _chain(material: Material, from: Vector3, to: Vector3) -> void:
	const LINK := 0.055
	var along := to - from
	var count := int(along.length() / LINK)
	var step := along / count
	for n in count:
		var a := from + step * n
		var b := a + step * 1.15
		var turn := 0.0 if n % 2 == 0 else PI * 0.5
		# Two side bars per link: offset across the link, in the link's own plane.
		var up := step.normalized()
		var across := up.cross(Vector3.FORWARD if absf(up.z) < 0.9 else Vector3.RIGHT).normalized()
		across = across.rotated(up, turn)
		for side: int in [-1, 1]:
			_strut(material, a + across * side * 0.011, b + across * side * 0.011, 0.007)


## One chandelier candle standing on `foot`: an iron drip pan, the candle (some burnt down to
## stubs), wax run down over the pan's edge, dust in the pan; flame and glow when `lit`.
func _chandelier_candle(foot: Vector3, rng: RandomNumberGenerator, lit: bool, iron: Material, dust: Material) -> void:
	_cylinder(self, foot + Vector3(0, 0.008, 0), 0.04, 0.045, 0.016, iron)
	_cylinder(self, foot + Vector3(0, 0.017, 0), 0.04, 0.04, 0.003, dust)
	_cylinder(self, foot + Vector3(0, -0.02, 0), 0.008, 0.02, 0.04, iron)
	var tall := rng.randf_range(0.05, 0.17) if lit else rng.randf_range(0.025, 0.05)
	_cylinder(self, foot + Vector3(0, 0.016 + tall * 0.5, 0), 0.019, 0.017, tall, _lit_wax)
	for drip in rng.randi_range(1, 3):
		var a := rng.randf() * TAU
		var hang := rng.randf_range(0.02, 0.07)
		_box(_lit_wax, foot + Vector3(cos(a) * 0.043, 0.01 - hang * 0.5, sin(a) * 0.043),
			Vector3(0.008, hang, 0.008))
	var wick := foot + Vector3(0, 0.016 + tall, 0)
	_box(_ash, wick + Vector3(0, 0.006, 0), Vector3(0.004, 0.012, 0.004))
	if lit:
		_box(_candle_flame, wick + Vector3(0, 0.022, 0), Vector3(0.012, 0.028, 0.012))
		var glow := QuadMesh.new()
		glow.size = Vector2(0.24, 0.24)
		_mesh(self, glow, _halo, wick + Vector3(0, 0.02, 0)).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


## A small cobweb sheet hung from `top` (the middle of its top edge), facing along `yaw`.
func _web_sheet(top: Vector3, width: float, drop: float, yaw: float, seed: float, opacity: float) -> void:
	var web := ShaderMaterial.new()
	web.shader = COBWEB
	web.set_shader_parameter("seed", seed)
	web.set_shader_parameter("opacity", opacity)
	var quad := QuadMesh.new()
	quad.size = Vector2(width, drop)
	var sheet := _mesh(self, quad, web, top + Vector3(0, -drop * 0.5, 0))
	sheet.rotation.y = yaw
	sheet.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _build_cobwebs() -> void:
	# Webs slung across both back corners where the walls meet the vault, and one further
	# forward in the angle of each side wall and a rib.
	for corner: float in [LEFT_WALL, RIGHT_WALL]:
		var inward := -signf(corner - ROOM_MID)
		var shift := 0.0 if inward > 0 else 3.0
		_cobweb(Vector3(corner, SPRING + 0.25, BACK_WALL), inward, 1.0, 1.1, 0.4 + shift)
		_cobweb(Vector3(corner, SPRING - 0.05, BACK_WALL), inward, 0.55, 0.75, 1.7 + shift)
		_cobweb(Vector3(corner, SPRING + 0.1, 0.6 - 4 * 2.1 + 0.1), inward, 0.6, 0.7, 7.0 + shift)


## A web across the angle between a side wall (x = `corner.x`) and the plane z = `corner.z`
## behind it, `reach` metres along each; `inward` is +1 on the left wall, -1 on the right.
func _cobweb(corner: Vector3, inward: float, reach: float, drop: float, seed: float) -> void:
	var web := ShaderMaterial.new()
	web.shader = COBWEB
	web.set_shader_parameter("seed", seed)
	var quad := QuadMesh.new()
	quad.size = Vector2(reach * sqrt(2.0), drop)
	var sheet := _mesh(self, quad, web, corner + Vector3(inward * reach * 0.5, -drop * 0.5, reach * 0.5))
	sheet.rotation.y = PI * 0.25 * inward
	sheet.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _build_stains() -> void:
	# Spilled ale: wet puddles near the bar and the busy tables, old dried blots everywhere,
	# mug rings on the table tops.
	var rng := RandomNumberGenerator.new()
	rng.seed = 4417
	var spots: Array[Vector3] = [
		Vector3(BAR_X - 0.75, 0, -3.3), Vector3(BAR_X - 0.65, 0, -5.4), Vector3(BAR_X - 0.9, 0, -6.6),
		Vector3(-3.6, 0, -5.4), Vector3(-3.5, 0, -3.0), HUDDLE_AT + Vector3(0.3, 0, 0.5),
		Vector3(-1.2, 0, -6.4), Vector3(-1.0, 0, -3.1), Vector3(-2.9, 0, -7.4),
		# The open floor between the chess table and the fire, in full view of the play seat.
		Vector3(-1.9, 0, -6.0), Vector3(-0.7, 0, -7.4), Vector3(-2.5, 0, -8.4),
		Vector3(-0.5, 0, -5.7), Vector3(-1.5, 0, -8.0), Vector3(-2.8, 0, -6.3),
	]
	for table: Vector3 in TABLE_GRID:
		spots.append(table + Vector3(rng.randf_range(-0.5, 0.5), 0, rng.randf_range(-0.5, 0.5)))
	for i in spots.size():
		var wet := 1.0 if i < 3 or i == 9 or i == 12 or rng.randf() < 0.25 else 0.0
		_stain(spots[i] + Vector3(0, 0.009 + 0.0005 * i, 0), rng.randf_range(0.6, 1.1), i * 1.37, wet, false)
		if rng.randf() < 0.6:
			var splash := spots[i] + Vector3(rng.randf_range(-0.5, 0.5), 0.0095 + 0.0005 * i, rng.randf_range(-0.5, 0.5))
			_stain(splash, rng.randf_range(0.2, 0.4), i * 2.9, wet, false)
	for table: Vector3 in TABLE_GRID:
		for ring in 2:
			var at := table + Vector3(rng.randf_range(-0.3, 0.3), TABLE_HEIGHT + 0.002, rng.randf_range(-0.3, 0.3))
			_stain(at, 0.11, rng.randf() * 10.0, 0.0, true)
	_stain(CHESS_TABLE_AT + Vector3(0.36, 0.002, -0.28), 0.11, 4.2, 0.0, true)
	_stain(CHESS_TABLE_AT + Vector3(0.44, 0.002, -0.06), 0.1, 6.1, 0.0, true)
	_stain(HUDDLE_AT + Vector3(-0.25, TABLE_HEIGHT + 0.002, 0.2), 0.12, 1.9, 0.0, true)


func _stain(at: Vector3, size: float, seed: float, wet: float, ring: bool) -> void:
	var stain := ShaderMaterial.new()
	stain.shader = ALE_STAIN
	stain.set_shader_parameter("seed", seed)
	stain.set_shader_parameter("wet", wet)
	stain.set_shader_parameter("ring", 1.0 if ring else 0.0)
	stain.set_shader_parameter("strength", 0.55 if ring else 0.85)
	var quad := QuadMesh.new()
	quad.size = Vector2(size, size * (1.0 if ring else 0.75))
	var decal := _mesh(self, quad, stain, at)
	decal.rotation = Vector3(-PI * 0.5, seed, 0)
	decal.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _build_haze() -> void:
	# Smoke the chimney doesn't draw: plumes rolling out over the top of the fireplace opening,
	# up the face of the chimney breast and drifting out into the room, lit from below.
	for plume in 4:
		var smoke := _animated_material(MIST)
		smoke.set_shader_parameter("smoke", true)
		smoke.set_shader_parameter("phase", 1.3 + plume * 1.7)
		smoke.set_shader_parameter("opacity", 0.62 - plume * 0.08)
		smoke.set_shader_parameter("mist_color", Color("9a8270").lerp(Color("6e5c4e"), plume / 3.0))
		var drift := Vector3((plume - 1.5) * 0.28, 0.9 + plume * 0.12 + 0.95, 0.85 + plume * 0.32)
		var sheet := _mist_sheet(smoke, HEARTH_AT + drift, Vector2(1.3 + plume * 0.3, 2.2))
		sheet.rotation = Vector3(-0.12 - plume * 0.06, 0.2 * sin(plume * 2.3), 0.12 * cos(plume * 1.9))
	# The smoke that escapes hangs thickest round the fire: banks of haze across the room's
	# back end, densest against the chimney breast and thinning toward the long table.
	for bank in 5:
		var near := 1.0 - bank / 4.0
		var murk := _animated_material(MIST)
		murk.set_shader_parameter("phase", 0.7 + bank * 1.9)
		murk.set_shader_parameter("opacity", 0.16 + 0.3 * near)
		murk.set_shader_parameter("mist_color", Color("7a6250").lerp(Color("5a4a3e"), 1.0 - near))
		var sheet := _mist_sheet(murk, Vector3(ROOM_MID + 0.3 * sin(bank * 2.2), 0.9 + 0.3 * (bank % 3), HEARTH_AT.z + 1.2 + bank * 0.55),
			Vector2(RIGHT_WALL - LEFT_WALL - 0.4, 2.4))
		sheet.rotation.y = 0.06 * sin(bank * 1.7)
	# Pipe smoke and hearth haze pooled under the ceiling, lit warm by the fire.
	for layer in 3:
		var haze := _animated_material(MIST)
		haze.set_shader_parameter("smoke", true)
		haze.set_shader_parameter("phase", layer * 2.1)
		haze.set_shader_parameter("opacity", 0.16 + layer * 0.05)
		haze.set_shader_parameter("mist_color", Color("4a3a2e").lerp(Color("6a4a32"), layer / 2.0))
		# Kept behind the chess table so no sheet edge crosses the seated play view.
		var sheet := _mist_sheet(haze, Vector3(ROOM_MID, SPRING + 0.1 - layer * 0.12, -9.9 + layer * 1.2),
			Vector2(RIGHT_WALL - LEFT_WALL - 0.3, 1.2))
		sheet.rotation.y = 0.08 * cos(layer * 2.0)
