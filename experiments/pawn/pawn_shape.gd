extends RefCounted
## Editable analytic pawn. Each profile segment becomes a conical surface
## of revolution; a sphere forms the head. No imported mesh or image model.

const PROFILE = [Vector2(0.0, 0.0), Vector2(0.54, 0.0),
    Vector2(0.60, 0.08), Vector2(0.60, 0.17), Vector2(0.53, 0.24),
    Vector2(0.48, 0.26), Vector2(0.47, 0.34), Vector2(0.38, 0.40),
    Vector2(0.28, 0.57), Vector2(0.21, 0.92), Vector2(0.22, 1.12),
    Vector2(0.36, 1.17), Vector2(0.36, 1.25), Vector2(0.26, 1.30),
    Vector2(0.0, 1.30)]

static func distance_at(p: Vector3) -> float:
    var q := Vector2(Vector2(p.x, p.z).length(), p.y)
    var distance_squared := INF
    var inside := false
    for i in PROFILE.size():
        var a: Vector2 = PROFILE[i]
        var b: Vector2 = PROFILE[(i + 1) % PROFILE.size()]
        var edge := b - a
        var offset := q - a
        var closest := offset - edge * clampf(offset.dot(edge) / edge.length_squared(), 0.0, 1.0)
        distance_squared = minf(distance_squared, closest.length_squared())
        if (a.y > q.y) != (b.y > q.y):
            if q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x:
                inside = not inside
    var body := sqrt(distance_squared) * (-1.0 if inside else 1.0)
    var head := (p - Vector3(0, 1.57, 0)).length() - 0.34
    return minf(body, head)

static func normal_at(p: Vector3) -> Vector3:
    const E = 0.001
    return Vector3(distance_at(p + Vector3(E, 0, 0)) - distance_at(p - Vector3(E, 0, 0)),
        distance_at(p + Vector3(0, E, 0)) - distance_at(p - Vector3(0, E, 0)),
        distance_at(p + Vector3(0, 0, E)) - distance_at(p - Vector3(0, 0, E))).normalized()

static func sample_surface(spacing: float) -> Array[Dictionary]:
    var samples: Array[Dictionary] = []
    var rng := RandomNumberGenerator.new()
    rng.seed = 7319
    var nx := ceili(1.30 / spacing)
    var ny := ceili(2.02 / spacing)
    var light := Vector3(-0.65, 0.85, 0.5).normalized()
    for x in nx:
        for y in ny:
            for z in nx:
                var p := Vector3(-0.65 + x * spacing, -0.04 + y * spacing, -0.65 + z * spacing)
                if absf(distance_at(p)) > spacing * 0.60:
                    continue
                p += Vector3(rng.randf_range(-0.3, 0.3), rng.randf_range(-0.3, 0.3), rng.randf_range(-0.3, 0.3)) * spacing
                for iteration in 3:
                    p -= normal_at(p) * distance_at(p)
                var n := normal_at(p)
                if n.length_squared() < 0.9 or absf(distance_at(p)) > 0.005:
                    continue
                var shade := 0.28 + 0.72 * maxf(n.dot(light), 0.0)
                shade *= rng.randf_range(0.94, 1.04)
                var color := Color(0.98, 0.88, 0.68) * shade
                color.a = 1.0
                samples.append({"position": p, "normal": n, "color": color,
                    "radius": spacing * 1.6 * rng.randf_range(0.90, 1.12)})
    return samples
