shader_type spatial;

uniform vec4 wood_color : source_color = vec4(0.22, 0.12, 0.067, 1.0);
varying vec3 grain_at;

void vertex() {
	grain_at = VERTEX;
}

void fragment() {
	float curve = sin(grain_at.x * 4.7 + grain_at.y * 2.6) * 2.7;
	float rings = sin(grain_at.y * 89.0 + curve + sin(grain_at.x * 17.0) * 0.36);
	float fibers = sin(grain_at.y * 243.0 + curve * 2.2) * 0.045;
	float worn = smoothstep(0.48, 0.8, sin(grain_at.x * 10.0 + grain_at.y * 18.0));
	ALBEDO = wood_color.rgb * (0.86 + rings * 0.12 + fibers + worn * 0.16);
	ROUGHNESS = 0.77;
}
