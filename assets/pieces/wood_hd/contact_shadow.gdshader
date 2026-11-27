shader_type spatial;
// A piece's shadow on the board: a flat quad under the piece (piece-local units, the foot's
// centre at the origin) that darkens what is beneath it. Each of up to two lights throws
// the piece's silhouette away from it, as a tapering streak from the foot: `cast_a` and
// `cast_b` are (direction x, direction z, length, darkness), set by wood_hd_piece.gd from
// the lights near the piece. A faint ring round the foot grounds it under any light.
render_mode blend_mul, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform float foot = 0.5;
uniform float ground = 0.3;
uniform vec4 cast_a = vec4(0.0);
uniform vec4 cast_b = vec4(0.0);
varying vec2 spot;

void vertex() {
    spot = VERTEX.xz;
}

float cast(vec4 c, vec2 p) {
    if (c.w <= 0.0) {
        return 0.0;
    }
    vec2 dir = c.xy;
    float t = clamp(dot(p, dir) / max(c.z, 0.001), 0.0, 1.0);
    float off = length(p - dir * t * c.z);
    // The silhouette narrows from the foot to the head, and softens and fades with distance.
    float radius = foot * mix(0.95, 0.5, t);
    float soft = foot * (0.15 + 0.5 * t);
    float shade = 1.0 - smoothstep(radius - soft, radius + soft, off);
    return shade * mix(1.0, 0.45, t) * c.w;
}

void fragment() {
    float d = length(spot) / foot;
    float ring = (1.0 - smoothstep(0.85, 1.35, d)) * ground;
    float dark = max(ring, 1.0 - (1.0 - cast(cast_a, spot)) * (1.0 - cast(cast_b, spot)));
    ALBEDO = vec3(1.0 - clamp(dark, 0.0, 0.85));
}
