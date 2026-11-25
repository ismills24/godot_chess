extends RefCounted
## Geometry in local units: +Y up, knight faces +X, common 0.132-radius dowel.
const KINDS = ["pawn", "rook", "knight", "bishop", "queen", "king"]
const SEATS = {"pawn": 0.91, "rook": 1.02, "knight": 0.48, "bishop": 1.16, "queen": 1.34, "king": 1.48}
const RADII = {"pawn": 0.43, "rook": 0.48, "knight": 0.49, "bishop": 0.49, "queen": 0.52, "king": 0.55}
# One broad cut-out silhouette. The throat's negative space suggests a horse;
# no separate eyes, teeth or mane grooves. A single swept ear and an arched crest
# give the profile its gesture; the muzzle tapers in width (see head_distance).
const HORSE = [Vector2(-0.33, 0.04), Vector2(0.29, 0.04), Vector2(0.25, 0.19),
    Vector2(0.14, 0.34), Vector2(0.06, 0.47), Vector2(0.10, 0.56),
    Vector2(0.24, 0.60), Vector2(0.38, 0.62), Vector2(0.46, 0.67),
    Vector2(0.47, 0.76), Vector2(0.41, 0.84), Vector2(0.28, 0.92),
    Vector2(0.17, 1.01), Vector2(0.11, 1.10), Vector2(0.07, 1.25),
    Vector2(-0.01, 1.15), Vector2(-0.09, 1.12), Vector2(-0.21, 1.04),
    Vector2(-0.31, 0.90), Vector2(-0.37, 0.70), Vector2(-0.38, 0.45),
    Vector2(-0.36, 0.22)]

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
            # Broad at the neck, narrowing toward the poll and the muzzle.
            var width := lerpf(0.20, 0.13, clampf(p.y / 1.22, 0, 1)) * lerpf(1.0, 0.80, smoothstep(0.05, 0.47, p.x))
            const ROUND = 0.05
            var q := Vector2(planar + ROUND, absf(p.z) - width + ROUND)
            d = q.max(Vector2.ZERO).length() + minf(maxf(q.x, q.y), 0.0) - ROUND
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
    var spacing: float = {"bishop": 0.018, "rook": 0.022, "knight": 0.022}.get(kind, 0.027)
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
                var wear := surface_wear(p, n, kind)
                result.append({"position": p, "normal": n, "radius": spacing * (1.20 if curvature < 0.8 else 1.48),
                    "edge": wear.x, "cavity": wear.y})
    return result

## Baked per splat: x = exposed convex edge (handled, varnish worn), y = cavity (grime, occlusion).
static func surface_wear(p: Vector3, n: Vector3, kind: String) -> Vector2:
    # The Laplacian of a distance field is its summed principal curvature.
    const E = 0.02
    var center := head_distance(p, kind)
    var laplacian := 0.0
    for axis: Vector3 in [Vector3.RIGHT, Vector3.UP, Vector3.BACK]:
        laplacian += head_distance(p + axis * E, kind) + head_distance(p - axis * E, kind) - 2.0 * center
    laplacian /= E * E
    var occlusion := 0.0
    for step in 3:
        var h := 0.035 * (step + 1)
        occlusion += clampf((h - head_distance(p + n * h, kind)) / h, 0, 1) / (step + 1.0)
    return Vector2(smoothstep(12.0, 40.0, laplacian),
        clampf(occlusion * 0.75 + smoothstep(-8.0, -30.0, laplacian) * 0.6, 0, 1))

static func lathe_samples(profile: Array, spacing: float = 0.036, occluded_below := -1.0) -> Array[Dictionary]:
    var result: Array[Dictionary] = []
    # Signed turn at each profile vertex: positive is a convex lip, negative a groove.
    var turns: Array[float] = []
    for i in profile.size():
        if i == 0 or i == profile.size() - 1:
            turns.append(0.0)
            continue
        var before: Vector2 = (profile[i] - profile[i - 1]).normalized()
        var after: Vector2 = (profile[i + 1] - profile[i]).normalized()
        turns.append(before.angle_to(after))
    for segment in range(profile.size() - 1):
        var a: Vector2 = profile[segment]
        var b: Vector2 = profile[segment + 1]
        var edge := b - a
        var steps := maxi(1, ceili(edge.length() / spacing))
        var n2 := Vector2(edge.y, -edge.x).normalized()
        # Spread each vertex's turn over about two splats either side.
        var reach := spacing * 2.2
        for row in steps:
            var t := (row + 0.5) / steps
            var p := a.lerp(b, t)
            var turn := turns[segment] * clampf(1.0 - t * edge.length() / reach, 0, 1) \
                + turns[segment + 1] * clampf(1.0 - (1.0 - t) * edge.length() / reach, 0, 1)
            var edge_wear := clampf(turn * 1.6, 0, 1)
            var cavity := clampf(-turn * 1.4, 0, 1)
            if p.y < occluded_below:
                cavity = maxf(cavity, 0.55 * (1.0 - p.y / occluded_below) + 0.25)
            var count := maxi(6, ceili(TAU * p.x / spacing))
            for i in count:
                var angle := TAU * (i + 0.5 * (row % 2)) / count
                result.append({"position": Vector3(p.x * cos(angle), p.y, p.x * sin(angle)),
                    "normal": Vector3(n2.x * cos(angle), n2.y, n2.x * sin(angle)), "radius": spacing * 1.40,
                    "edge": edge_wear, "cavity": cavity})
    return result

## Turned body: one foot seam, a concave Staunton-like sweep, and a small collar
## bead under the head. The silhouette stays plain; the curve does the work.
static func body_profile(r: float, seat: float) -> Array:
    const NECK = 0.185
    var bottom := 0.145
    var collar := seat - 0.075
    var points := [Vector2(r * 0.90, bottom), Vector2(r * 0.93, bottom + 0.025)]
    var steps := 16
    for i in range(1, steps + 1):
        var t := float(i) / steps
        var y := lerpf(bottom + 0.025, collar, t)
        points.append(Vector2(NECK + 0.02 + (r * 0.93 - NECK - 0.02) * pow(1.0 - t, 2.1), y))
    points.append_array([Vector2(0.232, seat - 0.055), Vector2(0.246, seat - 0.035),
        Vector2(0.232, seat - 0.014), Vector2(NECK + 0.004, seat - 0.004), Vector2(NECK, seat)])
    return points

static func samples(kind: String, module: String) -> Array[Dictionary]:
    var r: float = RADII[kind]
    var seat: float = SEATS[kind]
    if module == "head":
        return sample_head(kind)
    if module == "base":
        # Weighted plinth with a rounded shoulder; the lowest band is the felt pad.
        return lathe_samples([Vector2(0, 0), Vector2(r * 0.97, 0), Vector2(r, 0.018),
            Vector2(r, 0.062), Vector2(r * 0.985, 0.088), Vector2(r * 0.95, 0.108),
            Vector2(r * 0.88, 0.122), Vector2(0, 0.124)], 0.024)
    return lathe_samples(body_profile(r, seat), 0.026, 0.19)
