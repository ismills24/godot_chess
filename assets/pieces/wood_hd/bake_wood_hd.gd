extends SceneTree
## Bakes the high-resolution wood set: the same shapes as assets/pieces/wood (its lathe
## profiles and carved head distance fields, from wood_shapes.gd), as smooth triangle meshes
## instead of splat clouds. Writes meshes/<kind>_<module>.res.
## Run: Godot --headless --path . --script res://assets/pieces/wood_hd/bake_wood_hd.gd

const Shapes = preload("res://assets/pieces/wood/wood_shapes.gd")
const OUT := "res://assets/pieces/wood_hd/meshes/"
## Turned parts: segments around the axis.
const AROUND := 72
## Carved heads: grid spacing of the surface extraction (heads are about 0.6 units wide).
const CELL := 0.009
## Profile corners sharper than this keep a crisp edge instead of a smoothed normal.
const CREASE := 0.7

const HEAD_MIN := Vector3(-0.52, -0.04, -0.52)
const HEAD_SIZE := Vector3(1.04, 1.36, 1.04)


func _init() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT))
	var only := OS.get_cmdline_user_args()
	for kind: String in Shapes.KINDS:
		if not only.is_empty() and not kind in only:
			continue
		var started := Time.get_ticks_msec()
		_save(_lathe(_base_profile(kind)), kind + "_base")
		_save(_lathe(_body_profile(kind), 0.19), kind + "_body")
		_save(_head(kind), kind + "_head")
		print("[wood_hd] %s baked in %.1f s" % [kind, (Time.get_ticks_msec() - started) / 1000.0])
	quit()


func _save(mesh: ArrayMesh, name: String) -> void:
	var error := ResourceSaver.save(mesh, OUT + name + ".res", ResourceSaver.FLAG_COMPRESS)
	if error != OK:
		push_error("Could not save %s: %s" % [name, error_string(error)])
	var lods: Array = RenderingServer.mesh_get_surface(mesh.get_rid(), 0).get("lods", [])
	print("[wood_hd]   %s: %d triangles, %d LODs" % [name, mesh.surface_get_array_index_len(0) / 3, lods.size()])


## The plinth, exactly as wood_shapes.gd samples it.
func _base_profile(kind: String) -> Array:
	var r: float = Shapes.RADII[kind]
	return [Vector2(0, 0), Vector2(r * 0.97, 0), Vector2(r, 0.018),
		Vector2(r, 0.062), Vector2(r * 0.985, 0.088), Vector2(r * 0.95, 0.108),
		Vector2(r * 0.88, 0.122), Vector2(0, 0.124)]


## The turned body, tucked down into the plinth (splats overlapped the small gap there).
func _body_profile(kind: String) -> Array:
	var r: float = Shapes.RADII[kind]
	var profile: Array = Shapes.body_profile(r, Shapes.SEATS[kind])
	profile.push_front(Vector2(r * 0.86, 0.11))
	return profile


## A surface of revolution from `profile` (radius, height) points. Wear goes in the vertex
## colour like the splats' (red exposed edge, green cavity): from the profile's turns, and
## shadowed low on the body (`occluded_below`).
func _lathe(profile: Array, occluded_below := -1.0) -> ArrayMesh:
	var count := profile.size()
	var turns: Array[float] = []
	var normals: Array[Vector2] = []
	for i in count - 1:
		var edge: Vector2 = profile[i + 1] - profile[i]
		normals.append(Vector2(edge.y, -edge.x).normalized())
	for i in count:
		if i == 0 or i == count - 1:
			turns.append(0.0)
		else:
			turns.append((profile[i] - profile[i - 1]).normalized().angle_to((profile[i + 1] - profile[i]).normalized()))
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	for segment in count - 1:
		var a: Vector2 = profile[segment]
		var b: Vector2 = profile[segment + 1]
		if a.distance_to(b) < 0.0001:
			continue
		var ends := [a, b]
		var end_normals: Array[Vector2] = []
		var end_wear: Array[Color] = []
		for side in 2:
			var vertex := segment + side
			var n: Vector2 = normals[segment]
			var neighbour := segment - 1 if side == 0 else segment + 1
			if neighbour >= 0 and neighbour < normals.size() and absf(turns[vertex]) < CREASE:
				n = (n + normals[neighbour]).normalized()
			end_normals.append(n)
			var turn := turns[vertex]
			var cavity := clampf(-turn * 1.4, 0, 1)
			var y: float = ends[side].y
			if y < occluded_below:
				cavity = maxf(cavity, 0.55 * (1.0 - y / occluded_below) + 0.25)
			end_wear.append(Color(clampf(turn * 1.6, 0, 1), cavity, 0, 1))
		for step in AROUND:
			var corners: Array[Vector3] = []
			var corner_normals: Array[Vector3] = []
			var corner_wear: Array[Color] = []
			for s: int in [step, step + 1]:
				var angle := TAU * s / AROUND
				var c := cos(angle)
				var si := sin(angle)
				for side in 2:
					var p: Vector2 = ends[side]
					corners.append(Vector3(p.x * c, p.y, p.x * si))
					corner_normals.append(Vector3(end_normals[side].x * c, end_normals[side].y, end_normals[side].x * si))
					corner_wear.append(end_wear[side])
			# corners: 0 = (step, a), 1 = (step, b), 2 = (step+1, a), 3 = (step+1, b)
			for index: int in [0, 1, 2, 2, 1, 3]:
				tool.set_normal(corner_normals[index])
				tool.set_color(corner_wear[index])
				tool.add_vertex(corners[index])
	tool.index()
	return _outward(tool.commit())


## A carved head: surface nets over head_distance, each vertex pulled onto the true surface.
func _head(kind: String) -> ArrayMesh:
	var n := Vector3i((HEAD_SIZE / CELL).ceil()) + Vector3i.ONE
	var field := PackedFloat32Array()
	field.resize(n.x * n.y * n.z)
	# Coarse blocks first: a block whose centre is far from the surface is all one side.
	const BLOCK := 6
	var reach := CELL * BLOCK * 0.87 + 0.03
	for bx in range(0, n.x, BLOCK):
		for by in range(0, n.y, BLOCK):
			for bz in range(0, n.z, BLOCK):
				var centre := HEAD_MIN + Vector3(bx + BLOCK * 0.5, by + BLOCK * 0.5, bz + BLOCK * 0.5) * CELL
				var coarse := _distance(centre, kind)
				var exact := absf(coarse) < reach
				for x in range(bx, mini(bx + BLOCK, n.x)):
					for y in range(by, mini(by + BLOCK, n.y)):
						for z in range(bz, mini(bz + BLOCK, n.z)):
							field[(x * n.y + y) * n.z + z] = _distance(HEAD_MIN + Vector3(x, y, z) * CELL, kind) if exact else coarse
	var cell_vertex := {}
	var positions := PackedVector3Array()
	var vertex_normals := PackedVector3Array()
	var colors := PackedColorArray()
	var indices := PackedInt32Array()
	var axes := [Vector3i(1, 0, 0), Vector3i(0, 1, 0), Vector3i(0, 0, 1)]
	for x in n.x:
		for y in n.y:
			for z in n.z:
				var here := field[(x * n.y + y) * n.z + z]
				for a in 3:
					var step: Vector3i = axes[a]
					var o := Vector3i(x, y, z) + step
					if o.x >= n.x or o.y >= n.y or o.z >= n.z:
						continue
					var there := field[(o.x * n.y + o.y) * n.z + o.z]
					if (here < 0.0) == (there < 0.0):
						continue
					# The four cells round this edge, in order about the axis.
					var u: Vector3i = axes[(a + 1) % 3]
					var v: Vector3i = axes[(a + 2) % 3]
					var base := Vector3i(x, y, z)
					var quad: Array[int] = []
					for offset: Vector3i in [Vector3i.ZERO, -u, -u - v, -v]:
						var cell := base + offset
						if cell.x < 0 or cell.y < 0 or cell.z < 0 or cell.x >= n.x - 1 or cell.y >= n.y - 1 or cell.z >= n.z - 1:
							quad.clear()
							break
						var key := (cell.x * n.y + cell.y) * n.z + cell.z
						if not cell_vertex.has(key):
							cell_vertex[key] = positions.size()
							var p := _cell_point(field, n, cell, kind)
							var normal := _normal(p, kind)
							var wear := Shapes.surface_wear(p, normal, kind)
							positions.append(p)
							vertex_normals.append(normal)
							colors.append(Color(wear.x, wear.y, 0, 1))
						quad.append(cell_vertex[key])
					if quad.size() < 4:
						continue
					# Wind so the face looks out of the solid (inside is negative).
					if here < 0.0:
						quad.reverse()
					for index: int in [0, 1, 2, 0, 2, 3]:
						indices.append(quad[index])
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = positions
	arrays[Mesh.ARRAY_NORMAL] = vertex_normals
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return _outward(mesh)


## The head's distance field with its hidden dowel hole filled back in: it's never seen, and
## its inside walls would double the triangles.
func _distance(p: Vector3, kind: String) -> float:
	return minf(Shapes.head_distance(p, kind), Shapes.cylinder(p - Vector3(0, 0.16, 0), 0.14, 0.16))


func _normal(p: Vector3, kind: String) -> Vector3:
	const E = 0.001
	return Vector3(_distance(p + Vector3(E, 0, 0), kind) - _distance(p - Vector3(E, 0, 0), kind),
		_distance(p + Vector3(0, E, 0), kind) - _distance(p - Vector3(0, E, 0), kind),
		_distance(p + Vector3(0, 0, E), kind) - _distance(p - Vector3(0, 0, E), kind)).normalized()


## Where a cell's vertex goes: the average of its edge crossings, then onto the surface.
func _cell_point(field: PackedFloat32Array, n: Vector3i, cell: Vector3i, kind: String) -> Vector3:
	var sum := Vector3.ZERO
	var crossings := 0
	var values: Array[float] = []
	for corner in 8:
		var c := cell + Vector3i(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1)
		values.append(field[(c.x * n.y + c.y) * n.z + c.z])
	for corner in 8:
		for bit: int in [1, 2, 4]:
			if corner & bit:
				continue
			var other := corner | bit
			var d0 := values[corner]
			var d1 := values[other]
			if (d0 < 0.0) == (d1 < 0.0):
				continue
			var t := clampf(d0 / (d0 - d1), 0.0, 1.0)
			var p0 := Vector3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1)
			var p1 := Vector3(other & 1, (other >> 1) & 1, (other >> 2) & 1)
			sum += p0.lerp(p1, t)
			crossings += 1
	var p := HEAD_MIN + (Vector3(cell) + sum / maxf(crossings, 1)) * CELL
	var start := p
	for iteration in 4:
		p -= _normal(p, kind) * _distance(p, kind)
	# Keep vertices in their cell's neighbourhood so creases don't fold over.
	if p.distance_to(start) > CELL * 1.5:
		p = start + (p - start).limit_length(CELL * 1.5)
	return p


## Godot's front faces wind clockwise; flip every triangle if this mesh came out the other
## way (checked against the stored outward normals).
func _outward(mesh: ArrayMesh) -> ArrayMesh:
	var arrays := mesh.surface_get_arrays(0)
	var positions: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	var agree := 0.0
	for i in range(0, indices.size(), 3):
		var a := positions[indices[i]]
		var face := (positions[indices[i + 1]] - a).cross(positions[indices[i + 2]] - a)
		agree += face.dot(normals[indices[i]] + normals[indices[i + 1]] + normals[indices[i + 2]])
	# Clockwise seen from outside gives a face normal (b-a)x(c-a) pointing inward.
	if agree > 0.0:
		for i in range(0, indices.size(), 3):
			var swap := indices[i + 1]
			indices[i + 1] = indices[i + 2]
			indices[i + 2] = swap
		arrays[Mesh.ARRAY_INDEX] = indices
	# Automatic LODs: full detail close up, far fewer triangles at board distance.
	var importer := ImporterMesh.new()
	importer.add_surface(Mesh.PRIMITIVE_TRIANGLES, arrays)
	importer.generate_lods(60.0, 25.0, [])
	return importer.get_mesh()
