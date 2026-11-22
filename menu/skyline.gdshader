shader_type spatial;
render_mode unshaded, fog_disabled, shadows_disabled;

uniform vec4 haze_tint : source_color;
varying float rooftop_height;

void vertex() {
	rooftop_height = (MODEL_MATRIX * vec4(VERTEX, 1.0)).y;
}

void fragment() {
	ALBEDO = mix(vec3(0.115, 0.24, 0.267), haze_tint.rgb, smoothstep(4.0, 18.0, rooftop_height));
}
