shader_type spatial;
// The board for the plastic club set: a tournament roll-up board, buff and green vinyl with
// a faint pebbled texture and a dull sheen, on a buff border with a green rule round it.
#include "res://assets/boards/styles/common.gdshaderinc"

void fragment() {
    vec2 q = board_point();
    float pebble = piece_noise(vec3(q * 70.0, 1.0));
    vec3 buff = vec3(0.90, 0.88, 0.77);
    vec3 green = vec3(0.24, 0.47, 0.29);
    vec3 albedo;
    if (frame) {
        albedo = mix(buff * 0.94, green, frame_line(0.16, 0.05));
        albedo = mix(albedo, vec3(0.08, 0.16, 0.10), frame_side());
    } else {
        albedo = mix(green, buff, board_light(q));
    }
    ALBEDO = albedo * (0.97 + pebble * 0.05);
    ROUGHNESS = 0.5 + pebble * 0.1;
    SPECULAR = 0.4;
}
