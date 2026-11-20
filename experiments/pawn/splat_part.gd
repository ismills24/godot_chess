extends MeshInstance3D
## A rigid module owns its local samples. Moving it never resamples its shape.
var points: Array[Dictionary] = []
var arrays: Array = []
var last_direction := Vector3.ZERO

func build(source: Array[Dictionary]) -> void:
    points = source
    var material := ShaderMaterial.new()
    material.shader = preload("res://experiments/pawn/splat.gdshader")
    material_override = material
    var vertices := PackedVector3Array()
    var colors := PackedColorArray()
    var uvs := PackedVector2Array()
    var normals := PackedVector3Array()
    const CORNERS = [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, -1), Vector2(1, 1), Vector2(-1, 1)]
    for point in points:
        var n: Vector3 = point.normal
        var tangent := n.cross(Vector3.UP if absf(n.y) < 0.95 else Vector3.RIGHT).normalized()
        var bitangent := n.cross(tangent).normalized()
        for corner in CORNERS:
            vertices.append(point.position + (tangent * corner.x + bitangent * corner.y) * point.radius)
            colors.append(point.color)
            normals.append(n)
            uvs.append((corner + Vector2.ONE) * 0.5)
    arrays.resize(Mesh.ARRAY_MAX)
    arrays[Mesh.ARRAY_VERTEX] = vertices
    arrays[Mesh.ARRAY_COLOR] = colors
    arrays[Mesh.ARRAY_NORMAL] = normals
    arrays[Mesh.ARRAY_TEX_UV] = uvs
    # Real world-space lighting on moving parts, with variation stored as albedo.
    material.shader = preload("res://experiments/pawn/workshop_splat.gdshader")

func sort_for(camera: Camera3D) -> void:
    var direction := global_basis.inverse() * camera.global_basis.z
    if direction.distance_squared_to(last_direction) < 0.000001:
        return
    last_direction = direction
    var keys := PackedInt64Array()
    keys.resize(points.size())
    for i in points.size():
        var p: Vector3 = points[i].position
        keys[i] = (roundi(p.dot(direction) * 10000.0) + 100000) * points.size() + i
    keys.sort()
    var indices := PackedInt32Array()
    indices.resize(points.size() * 6)
    for i in keys.size():
        var first := int(keys[i] % points.size()) * 6
        for j in 6:
            indices[i * 6 + j] = first + j
    arrays[Mesh.ARRAY_INDEX] = indices
    var result := ArrayMesh.new()
    result.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
    mesh = result

static func sphere_samples(center: Vector3, radius: float, count: int, base_color: Color, seed_value: int) -> Array[Dictionary]:
    var result: Array[Dictionary] = []
    var rng := RandomNumberGenerator.new()
    rng.seed = seed_value
    for i in count:
        var y := 1.0 - 2.0 * (i + 0.5) / count
        var angle := i * 2.3999632297
        var radial := sqrt(1.0 - y * y)
        var n := Vector3(cos(angle) * radial, y, sin(angle) * radial)
        var p := center + n * radius
        # Open the underside for the female socket instead of filling its bore.
        if p.y < center.y and Vector2(p.x, p.z).length() < 0.135:
            continue
        var color := base_color * rng.randf_range(0.89, 1.06)
        color.a = 1.0
        result.append({"position": p, "normal": n, "color": color,
            "radius": sqrt(4.0 * PI * radius * radius / count) * 1.45})
    return result
