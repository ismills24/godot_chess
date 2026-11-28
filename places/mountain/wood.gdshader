shader_type spatial;
// Turned wood (the hermit's bowl): growth rings across the lathe axis, fine open grain along
// it, and an oiled sheen worn dull at the rim.

uniform vec4 light_color : source_color = vec4(0.5, 0.34, 0.2, 1.0);
uniform vec4 dark_color : source_color = vec4(0.3, 0.19, 0.11, 1.0);

varying vec3 local_at;

float hash2(vec2 p) {
	vec3 q = fract(vec3(p.xyx) * vec3(0.1031, 0.103, 0.0973));
	q += dot(q, q.yzx + 33.33);
	return fract((q.x + q.y) * q.z);
}

float noise2(vec2 x) {
	vec2 i = floor(x);
	vec2 f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash2(i), hash2(i + vec2(1, 0)), f.x), mix(hash2(i + vec2(0, 1)), hash2(i + vec2(1, 1)), f.x), f.y);
}

void vertex() {
	local_at = VERTEX;
}

void fragment() {
	// The blank was cut with its grain running along x.
	vec2 across = local_at.yz * 140.0;
	float wobble = noise2(vec2(local_at.x * 30.0, 0.0) + across * 0.05) * 1.5;
	float rings = fract(length(across + vec2(-4.0, 9.0)) * 0.6 + wobble);
	float fw = fwidth(rings);
	float band = smoothstep(0.0, 0.25 + fw, rings) * (1.0 - smoothstep(0.55, 0.9, rings));
	float fibre = noise2(vec2(local_at.x * 40.0, local_at.z * 900.0 + local_at.y * 900.0));
	vec3 color = mix(dark_color.rgb, light_color.rgb, band * 0.4 + fibre * 0.3 + 0.2);
	ALBEDO = color;
	ROUGHNESS = 0.45 + 0.25 * fibre;
	SPECULAR = 0.4;
}
