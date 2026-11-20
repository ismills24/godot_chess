extends MeshInstance3D
## Shared geometry and depth order across identical modules, including both woods.
const Shapes = preload("res://assets/pieces/wood/wood_shapes.gd")
static var cache: Dictionary = {}
static var materials: Dictionary = {}
var cache_key := ""

func configure(kind: String, module: String, dark: bool, depth_tested: bool = false) -> void:
    cache_key = kind + "/" + module
    if not cache.has(cache_key):
        _build(cache_key, Shapes.samples(kind, module))
    var bishop_cut := kind == "bishop" and module == "head"
    var material_key := str(dark) + "/" + str(bishop_cut) + "/" + str(depth_tested)
    if not materials.has(material_key):
        var mat := ShaderMaterial.new()
        mat.shader = preload("res://assets/pieces/wood/wood_surface_depth.gdshader") if depth_tested else preload("res://assets/pieces/wood/wood_surface.gdshader")
        mat.set_shader_parameter("dark_wood", dark)
        mat.set_shader_parameter("bishop_cut", bishop_cut)
        materials[material_key] = mat
    material_override = materials[material_key]
    mesh = cache[cache_key].mesh

static func _build(key: String, points: Array[Dictionary]) -> void:
    var vertices := PackedVector3Array()
    var normals := PackedVector3Array()
    var uvs := PackedVector2Array()
    var centers := PackedVector3Array()
    const CORNERS = [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, -1), Vector2(1, 1), Vector2(-1, 1)]
    for point in points:
        var n: Vector3 = point.normal
        var tangent := n.cross(Vector3.UP if absf(n.y) < 0.95 else Vector3.RIGHT).normalized()
        var bitangent := n.cross(tangent).normalized()
        centers.append(point.position)
        for corner in CORNERS:
            vertices.append(point.position + (tangent * corner.x + bitangent * corner.y) * point.radius)
            normals.append(n)
            uvs.append((corner + Vector2.ONE) * 0.5)
    var arrays := []
    arrays.resize(Mesh.ARRAY_MAX)
    arrays[Mesh.ARRAY_VERTEX] = vertices
    arrays[Mesh.ARRAY_NORMAL] = normals
    arrays[Mesh.ARRAY_TEX_UV] = uvs
    var result := ArrayMesh.new()
    result.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
    cache[key] = {"arrays": arrays, "centers": centers, "mesh": result, "orders": {}}

func sort_for(camera: Camera3D) -> void:
    var data: Dictionary = cache[cache_key]
    var direction := (global_basis.inverse() * camera.global_basis.z).normalized()
    # Quantized direction bins share sorted meshes across same-facing pieces.
    var key := Vector3i(roundi(direction.x * 100), roundi(direction.y * 100), roundi(direction.z * 100))
    if not data.orders.has(key):
        var centers: PackedVector3Array = data.centers
        var keys := PackedInt64Array()
        keys.resize(centers.size())
        for i in centers.size():
            keys[i] = (roundi(centers[i].dot(direction) * 10000) + 100000) * centers.size() + i
        keys.sort()
        var indices := PackedInt32Array()
        indices.resize(centers.size() * 6)
        for i in keys.size():
            var first := int(keys[i] % centers.size()) * 6
            for j in 6:
                indices[i * 6 + j] = first + j
        var arrays: Array = data.arrays.duplicate()
        arrays[Mesh.ARRAY_INDEX] = indices
        var result := ArrayMesh.new()
        result.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
        # Bound cached orientations; this is a preview renderer, not an LRU atlas.
        if data.orders.size() >= 4:
            data.orders.clear()
        data.orders[key] = result
    mesh = data.orders[key]
