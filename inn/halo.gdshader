shader_type spatial;
// A soft glow around a small flame (lantern glass, candle): a camera-facing quad with a
// radial falloff, added on top of the scene. It flickers with the stepped `elapsed`, each
// halo out of step with the others by its position.
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

uniform vec4 glow_color : source_color = vec4(1.0, 0.62, 0.3, 1.0);
uniform float strength = 0.9;
uniform float elapsed = 0.0;

void vertex() {
	// Billboard: keep the quad's position and scale, face the camera.
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * length(MODEL_MATRIX[0].xyz),
		INV_VIEW_MATRIX[1] * length(MODEL_MATRIX[1].xyz), INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	vec3 origin = (INV_VIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	float phase = dot(NODE_POSITION_WORLD, vec3(3.1, 1.7, 2.3));
	float flicker = 0.85 + 0.1 * sin(elapsed * 9.0 + phase) + 0.05 * sin(elapsed * 23.0 + phase * 2.0);
	float r = length(UV - 0.5) * 2.0;
	float glow = pow(max(1.0 - r, 0.0), 2.2);
	ALBEDO = glow_color.rgb * glow * strength * flicker;
}
