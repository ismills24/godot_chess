extends RefCounted
## Conventional geometry gives manufactured interfaces clean, readable edges.

static func material(color: Color) -> StandardMaterial3D:
    var mat := StandardMaterial3D.new()
    mat.albedo_color = color
    mat.roughness = 0.76
    return mat

static func mesh_node(parent: Node3D, mesh: Mesh, at: Vector3, mat: Material) -> MeshInstance3D:
    var node := MeshInstance3D.new()
    node.mesh = mesh
    node.position = at
    node.material_override = mat
    parent.add_child(node)
    return node

static func cylinder(parent: Node3D, at: Vector3, radius: float, height: float, mat: Material) -> MeshInstance3D:
    var mesh := CylinderMesh.new()
    mesh.top_radius = radius
    mesh.bottom_radius = radius
    mesh.height = height
    mesh.radial_segments = 48
    return mesh_node(parent, mesh, at, mat)

static func box(parent: Node3D, at: Vector3, dimensions: Vector3, mat: Material) -> MeshInstance3D:
    var mesh := BoxMesh.new()
    mesh.size = dimensions
    return mesh_node(parent, mesh, at, mat)

static func ring(parent: Node3D, at: Vector3, inner: float, outer: float, mat: Material) -> MeshInstance3D:
    var mesh := TorusMesh.new()
    mesh.inner_radius = inner
    mesh.outer_radius = outer
    mesh.rings = 48
    mesh.ring_segments = 12
    return mesh_node(parent, mesh, at, mat)

static func tube(parent: Node3D, path: PackedVector3Array, radius: float, mat: Material) -> MeshInstance3D:
    var surface := SurfaceTool.new()
    surface.begin(Mesh.PRIMITIVE_TRIANGLES)
    for i in range(path.size() - 1):
        var direction := (path[i + 1] - path[i]).normalized()
        var a := direction.cross(Vector3.UP if absf(direction.y) < 0.95 else Vector3.RIGHT).normalized()
        var b := direction.cross(a).normalized()
        for j in 8:
            var n0 := a * cos(j * TAU / 8.0) + b * sin(j * TAU / 8.0)
            var n1 := a * cos((j + 1) * TAU / 8.0) + b * sin((j + 1) * TAU / 8.0)
            var v := [path[i] + n0 * radius, path[i] + n1 * radius,
                path[i + 1] + n1 * radius, path[i + 1] + n0 * radius]
            for k in [0, 2, 1, 0, 3, 2]:
                surface.set_normal(n0 if k == 0 or k == 3 else n1)
                surface.add_vertex(v[k])
    var mat_double := mat.duplicate() as StandardMaterial3D
    mat_double.cull_mode = BaseMaterial3D.CULL_DISABLED
    return mesh_node(parent, surface.commit(), Vector3.ZERO, mat_double)

static func helix(parent: Node3D, radius: float, bottom: float, turns: float, pitch: float, thickness: float, mat: Material) -> void:
    var path := PackedVector3Array()
    var steps := ceili(turns * 64)
    for i in range(steps + 1):
        var t := turns * float(i) / steps
        path.append(Vector3(cos(t * TAU) * radius, bottom + t * pitch, sin(t * TAU) * radius))
    tube(parent, path, thickness, mat)

static func socket(parent: Node3D, mat: Material) -> void:
    # Continuous smooth outer wall, inner bore, and flat annular end faces.
    var surface := SurfaceTool.new()
    surface.begin(Mesh.PRIMITIVE_TRIANGLES)
    for i in 64:
        var a := Vector3(cos(i * TAU / 64.0), 0, sin(i * TAU / 64.0))
        var b := Vector3(cos((i + 1) * TAU / 64.0), 0, sin((i + 1) * TAU / 64.0))
        for section in 4:
            var vertices: Array[Vector3]
            var normals: Array[Vector3]
            if section < 2:
                var radius := 0.185 if section == 0 else 0.138
                var sign_value := 1.0 if section == 0 else -1.0
                vertices = [a * radius, b * radius, b * radius + Vector3.UP * 0.18, a * radius + Vector3.UP * 0.18]
                normals = [a * sign_value, b * sign_value, b * sign_value, a * sign_value]
            else:
                var offset := Vector3.UP * (0.18 if section == 2 else 0.0)
                vertices = [a * 0.138 + offset, b * 0.138 + offset, b * 0.185 + offset, a * 0.185 + offset]
                var n := Vector3.UP if section == 2 else Vector3.DOWN
                normals = [n, n, n, n]
            for k in [0, 2, 1, 0, 3, 2]:
                surface.set_normal(normals[k])
                surface.add_vertex(vertices[k])
    var sleeve_material := mat.duplicate() as StandardMaterial3D
    sleeve_material.cull_mode = BaseMaterial3D.CULL_DISABLED
    mesh_node(parent, surface.commit(), Vector3.ZERO, sleeve_material)

static func wooden_dowel(parent: Node3D, bottom: float, height: float, pitch: float) -> void:
    var mesh := CylinderMesh.new()
    mesh.top_radius = 0.132
    mesh.bottom_radius = 0.132
    mesh.height = height
    mesh.radial_segments = 64
    mesh.rings = 96
    var mat := ShaderMaterial.new()
    mat.shader = preload("res://experiments/pawn/wooden_dowel.gdshader")
    mat.set_shader_parameter("pitch", pitch)
    mat.set_shader_parameter("height", height)
    mesh_node(parent, mesh, Vector3(0, bottom + height * 0.5, 0), mat)

static func screw(parent: Node3D, at: Vector3, mat: Material) -> void:
    var cap := cylinder(parent, at, 0.033, 0.022, mat)
    cap.rotation.x = PI / 2.0
    box(parent, at + Vector3(0, 0, 0.013), Vector3(0.043, 0.009, 0.005), material(Color("202329")))
