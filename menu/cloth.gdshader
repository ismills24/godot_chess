shader_type spatial;
render_mode cull_disabled;

uniform float elapsed = 0.0;
uniform vec4 cloth_color : source_color = vec4(0.25, 0.20, 0.15, 1.0);

void vertex() {
	float hang = UV.y * UV.y;
	VERTEX.z += hang * (0.10 * sin(UV.x * 8.0 + elapsed * 1.17)
		+ 0.063 * sin(UV.y * 8.0 - elapsed * 1.8));
}

void fragment() {
	float weave = sin(UV.x * 150.0) * sin(UV.y * 142.0) * 0.062;
	float folds = sin(UV.x * 25.0 + UV.y * 3.1) * 0.12;
	ALBEDO = cloth_color.rgb * (0.86 + weave + folds);
	ROUGHNESS = 0.96;
	if (UV.y > 0.93 + 0.067 * sin(UV.x * 57.0) + 0.011 * sin(UV.x * 121.0)) {
		discard;
	}
}
