shader_type spatial;

#include "res://menu/alley_noise.gdshaderinc"

uniform sampler2D masonry : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D relief : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rough_map : filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec4 color_tint : source_color = vec4(0.75, 0.62, 0.53, 1.0);
uniform float age = 0.0;
uniform vec2 tile_scale = vec2(1.77, 2.28);
varying vec3 world_at;

void vertex() {
	world_at = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 surface = vec2(world_at.z, world_at.y);
	vec2 tiles = UV * tile_scale + vec2(age * 0.19, age * 0.27);
	vec3 brick = texture(masonry, tiles, 0.55).rgb;
	vec3 tangent_normal = texture(relief, tiles).rgb;
	float cavities = texture(rough_map, tiles).r;
	float streak = alley_noise(surface * vec2(1.3, 0.23) + age);
	float staining = alley_noise(surface * 0.47 + age * 2.1);
	float damp = (1.0 - smoothstep(0.02, 2.30, world_at.y)) * (0.54 + streak * 0.46);
	float soot = smoothstep(0.42, 0.89, streak * 0.64 + staining * 0.36);
	vec3 colors = brick * color_tint.rgb * mix(0.97, 0.54, soot * 0.81 + damp * 0.30);
	ALBEDO = colors;
	ROUGHNESS = mix(0.83 + cavities * 0.08, 0.64, damp);
	SPECULAR = 0.18;
	NORMAL_MAP = tangent_normal;
	NORMAL_MAP_DEPTH = 0.29 * (1.0 - smoothstep(4.4, 15.0, distance(CAMERA_POSITION_WORLD, world_at)));
}
