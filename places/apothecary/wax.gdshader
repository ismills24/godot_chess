shader_type spatial;
// Beeswax lit from inside by its own flame: the top of the candle glows warm through the wax
// and the glow fades down the stick. A faint mottling keeps it from reading as plastic.

uniform vec3 wax_color : source_color = vec3(0.86, 0.78, 0.62);
uniform vec3 glow_color : source_color = vec3(1.0, 0.62, 0.3);
// World height of the candle's top, and how far down the glow reaches.
uniform float top_y = 0.9;
uniform float reach = 0.1;
uniform float glow = 1.4;

varying vec3 world_at;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float below = clamp((top_y - world_at.y) / reach, 0.0, 1.0);
	float through = pow(1.0 - below, 1.6);
	vec3 cell = floor(world_at * 900.0);
	float mottle = 0.94 + 0.08 * hash(cell);
	ALBEDO = wax_color * mottle;
	ROUGHNESS = 0.55;
	SPECULAR = 0.35;
	EMISSION = (glow_color * through * glow + wax_color * mix(vec3(1.0), glow_color, 0.5) * 0.55) * mottle;
}
