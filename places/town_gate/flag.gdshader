shader_type spatial;
render_mode cull_disabled;
// A banner flying from a pole: the pole side (UV.x 0) is held, the fly end ripples in the
// wind. Field colour with a band of the trim colour, and a swallowtail cut in the fly.

uniform float elapsed = 0.0;
uniform vec4 field_color : source_color = vec4(0.55, 0.08, 0.07, 1.0);
uniform vec4 trim_color : source_color = vec4(0.85, 0.66, 0.2, 1.0);

void vertex() {
	float free = UV.x;
	VERTEX.z += free * (0.12 * sin(UV.x * 7.0 - elapsed * 4.1) + 0.05 * sin(UV.x * 13.0 - elapsed * 6.3 + UV.y * 3.0));
	VERTEX.y -= free * free * 0.12;
}

void fragment() {
	float notch = abs(UV.y - 0.5) * 2.0;
	if (UV.x > 0.82 + 0.18 * notch) {
		discard;
	}
	vec3 color = field_color.rgb;
	if (abs(UV.y - 0.5) < 0.09) {
		color = trim_color.rgb;
	}
	color *= 0.85 + 0.15 * sin(UV.x * 7.0 - elapsed * 4.1);
	ALBEDO = color;
	ROUGHNESS = 0.95;
}
