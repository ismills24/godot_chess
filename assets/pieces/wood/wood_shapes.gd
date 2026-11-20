extends RefCounted
## Geometry in local units: +Y up, knight faces +X, common 0.132-radius dowel.
const KINDS = ["pawn", "rook", "knight", "bishop", "queen", "king"]
const SEATS = {"pawn": 0.91, "rook": 1.02, "knight": 0.48, "bishop": 1.16, "queen": 1.34, "king": 1.48}
const RADII = {"pawn": 0.43, "rook": 0.48, "knight": 0.49, "bishop": 0.49, "queen": 0.52, "king": 0.55}
# One broad cut-out silhouette. The throat's negative space suggests a horse;
# no separate eyes, teeth, mane grooves, or protruding ear ellipsoids.
const HORSE = [Vector2(-0.30, 0.04), Vector2(0.29, 0.04), Vector2(0.19, 0.24),
    Vector2(-0.045, 0.49), Vector2(0.10, 0.70), Vector2(0.34, 0.65),
    Vector2(0.43, 0.69), Vector2(0.42, 0.84), Vector2(0.21, 0.93),
    Vector2(0.08, 1.08), Vector2(0.035, 1.22), Vector2(-0.075, 1.16),
    Vector2(-0.08, 1.07), Vector2(-0.22, 1.00), Vector2(-0.31, 0.87),
    Vector2(-0.36, 0.64), Vector2(-0.35, 0.30)]

static func polygon_distance(q: Vector2, polygon: Array) -> float:
    var d := INF
    var inside := false
    for i in polygon.size():
        var a: Vector2 = polygon[i]
        var b: Vector2 = polygon[(i + 1) % polygon.size()]
        var edge := b - a
        var v := q - a
        d = minf(d, (v - edge * clampf(v.dot(edge) / maxf(edge.length_squared(), 0.000001), 0, 1)).length_squared())
        if (a.y > q.y) != (b.y > q.y):
            if q.x < edge.x * (q.y - a.y) / edge.y + a.x:
                inside = not inside
    return sqrt(d) * (-1.0 if inside else 1.0)

static func box(p: Vector3, half: Vector3) -> float:
    var q := p.abs() - half
    return q.max(Vector3.ZERO).length() + minf(maxf(q.x, maxf(q.y, q.z)), 0.0)

static func cylinder(p: Vector3, radius: float, half_height: float) -> float:
    var d := Vector2(Vector2(p.x, p.z).length() - radius, absf(p.y) - half_height)
    return d.max(Vector2.ZERO).length() + minf(maxf(d.x, d.y), 0.0)

static func ellipsoid(p: Vector3, radii: Vector3) -> float:
    var k0 := (p / radii).length()
    var k1 := (p / (radii * radii)).length()
    return k0 * (k0 - 1.0) / maxf(k1, 0.00001)

static func head_distance(p: Vector3, kind: String) -> float:
    var d := INF
    match kind:
        "pawn":
            d = (p - Vector3(0, 0.31, 0)).length() - 0.28
        "rook":
            var profile = [Vector2(0, 0), Vector2(0.19, 0), Vector2(0.34, 0.43), Vector2(0.34, 0.65), Vector2(0, 0.65)]
            d = polygon_distance(Vector2(Vector2(p.x, p.z).length(), p.y), profile)
            d = maxf(d, -cylinder(p - Vector3(0, 0.68, 0), 0.19, 0.22))
            for i in 3:
                var q := p.rotated(Vector3.UP, -(i * TAU / 3.0 + PI / 6.0))
                d = maxf(d, -box(q - Vector3(0.28, 0.62, 0), Vector3(0.18, 0.16, 0.075)))
        "knight":
            var planar := polygon_distance(Vector2(p.x, p.y), HORSE)
            var width := lerpf(0.195, 0.115, clampf(p.y / 1.22, 0, 1))
            var q := Vector2(planar, absf(p.z) - width)
            d = q.max(Vector2.ZERO).length() + minf(maxf(q.x, q.y), 0.0) - 0.028
        "bishop":
            d = ellipsoid(p - Vector3(0, 0.35, 0), Vector3(0.255, 0.43, 0.255))
            var slit := (p - Vector3(0.13, 0.64, 0)).rotated(Vector3.FORWARD, -0.65)
            d = maxf(d, -box(slit, Vector3(0.028, 0.18, 0.36)))
        "queen":
            var profile = [Vector2(0, 0), Vector2(0.19, 0), Vector2(0.33, 0.39),
                Vector2(0.34, 0.44), Vector2(0.12, 0.56), Vector2(0, 0.56)]
            d = polygon_distance(Vector2(Vector2(p.x, p.z).length(), p.y), profile)
            d = minf(d, (p - Vector3(0, 0.64, 0)).length() - 0.105)
        "king":
            var profile = [Vector2(0, 0), Vector2(0.19, 0), Vector2(0.34, 0.39),
                Vector2(0.35, 0.43), Vector2(0.09, 0.56), Vector2(0, 0.56)]
            d = polygon_distance(Vector2(Vector2(p.x, p.z).length(), p.y), profile)
            d = minf(d, box(p - Vector3(0, 0.72, 0), Vector3(0.065, 0.24, 0.07)) - 0.008)
            d = minf(d, box(p - Vector3(0, 0.78, 0), Vector3(0.205, 0.065, 0.07)) - 0.008)
    # A short hidden socket blends into each carving instead of reading as jewelry.
    d = minf(d, cylinder(p - Vector3(0, 0.08, 0), 0.185, 0.08))
    # Common hollow entry for the dowel, continuing into each head's interior.
    return maxf(d, -cylinder(p - Vector3(0, 0.03, 0), 0.14, 0.29))

static func head_normal(p: Vector3, kind: String) -> Vector3:
    const E = 0.001
    return Vector3(head_distance(p + Vector3(E, 0, 0), kind) - head_distance(p - Vector3(E, 0, 0), kind),
        head_distance(p + Vector3(0, E, 0), kind) - head_distance(p - Vector3(0, E, 0), kind),
        head_distance(p + Vector3(0, 0, E), kind) - head_distance(p - Vector3(0, 0, E), kind)).normalized()

static func sample_head(kind: String) -> Array[Dictionary]:
    var result: Array[Dictionary] = []
    var spacing := 0.018 if kind == "bishop" else 0.027
    var rng := RandomNumberGenerator.new()
    rng.seed = 118 + KINDS.find(kind)
    for x in ceili(1.08 / spacing):
        for y in ceili(1.38 / spacing):
            for z in ceili(1.08 / spacing):
                var p := Vector3(-0.52 + x * spacing, -0.02 + y * spacing, -0.52 + z * spacing)
                if absf(head_distance(p, kind)) > spacing * 0.58:
                    continue
                p += Vector3(rng.randf_range(-0.2, 0.2), rng.randf_range(-0.2, 0.2), rng.randf_range(-0.2, 0.2)) * spacing
                for iteration in 3:
                    p -= head_normal(p, kind) * head_distance(p, kind)
                var n := head_normal(p, kind)
                if n.length_squared() < 0.9 or absf(head_distance(p, kind)) > 0.004:
                    continue
                # Clip the footprint at sharp transitions to reduce wispy edges.
                var curvature := n.dot(head_normal(p + Vector3(0.012, 0.008, 0.007), kind))
                result.append({"position": p, "normal": n, "radius": spacing * (1.20 if curvature < 0.8 else 1.48)})
    return result

static func lathe_samples(profile: Array, spacing: float = 0.036) -> Array[Dictionary]:
    var result: Array[Dictionary] = []
    for segment in range(profile.size() - 1):
        var a: Vector2 = profile[segment]
        var b: Vector2 = profile[segment + 1]
        var edge := b - a
        var steps := maxi(1, ceili(edge.length() / spacing))
        var n2 := Vector2(edge.y, -edge.x).normalized()
        for row in steps:
            var p := a.lerp(b, (row + 0.5) / steps)
            var count := maxi(6, ceili(TAU * p.x / spacing))
            for i in count:
                var angle := TAU * (i + 0.5 * (row % 2)) / count
                result.append({"position": Vector3(p.x * cos(angle), p.y, p.x * sin(angle)),
                    "normal": Vector3(n2.x * cos(angle), n2.y, n2.x * sin(angle)), "radius": spacing * 1.40})
    return result

static func samples(kind: String, module: String) -> Array[Dictionary]:
    var r: float = RADII[kind]
    var seat: float = SEATS[kind]
    if module == "head":
        return sample_head(kind)
    if module == "base":
        return lathe_samples([Vector2(0, 0), Vector2(r * 0.96, 0), Vector2(r, 0.025),
            Vector2(r, 0.10), Vector2(r * 0.96, 0.12), Vector2(0, 0.12)], 0.028)
    # Single quiet taper and one foot seam, following the minimalist reference.
    return lathe_samples([Vector2(r * 0.94, 0.145), Vector2(r * 0.95, 0.18),
        Vector2(0.19, seat - 0.015), Vector2(0.185, seat)], 0.028)
