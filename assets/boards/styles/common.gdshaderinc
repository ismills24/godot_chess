// Shared by the board style shaders (styles/*.gdshader), one to match each piece style.
// The same material goes on the playing surface (frame = false) and on the frame round it
// (frame = true). Both meshes are centred on the board in x/z, so the board's squares are read
// straight off the local position: `board` is in square units, 0..8 across, with (0, 0) at
// the corner of a8 (the dark side's left) and x along the files.
#include "res://assets/pieces/wood/wood_lighting.gdshaderinc"
uniform bool frame = false;
// The width of one square in the chess set's units (chess_set.gd SQUARE).
uniform float square_size = 0.055;
// The board's size in squares (files, ranks); chess_set.gd sets it for boards other than 8x8.
uniform vec2 board_size = vec2(8.0, 8.0);
varying vec3 local_position;
varying vec3 local_normal;

void vertex() {
    local_position = VERTEX;
    local_normal = NORMAL;
}

vec2 board_point() {
    return local_position.xz / square_size + board_size * 0.5;
}

float board_fbm(vec3 p) {
    return piece_noise(p) * 0.5 + piece_noise(p * 2.03 + 11.7) * 0.25
        + piece_noise(p * 4.01 + 3.1) * 0.125 + piece_noise(p * 8.07 + 7.9) * 0.0625;
}

// 1 on a light square, 0 on a dark one, blended across the edge over one screen pixel so the
// grid stays clean at any distance. The near-left corner (a1) is always dark, as in chess,
// whatever the board's size.
float board_light(vec2 q) {
    vec2 cell = floor(q);
    float light = mod(cell.x + cell.y + board_size.y - 1.0, 2.0);
    vec2 inside = 0.5 - abs(fract(q) - 0.5);
    vec2 pixel = max(fwidth(q), vec2(1e-5));
    float sharp = min(smoothstep(0.0, pixel.x, inside.x), smoothstep(0.0, pixel.y, inside.y));
    return mix(0.5, light, sharp);
}

// How far into its square a point is from the nearest edge (0 on the line, 0.5 at the centre).
float board_edge(vec2 q) {
    vec2 inside = 0.5 - abs(fract(q) - 0.5);
    return min(inside.x, inside.y);
}

// A steady random number for each square, to vary the pieces of veneer or stone.
float board_seed(vec2 q) {
    return piece_hash(vec3(floor(q), 3.7));
}

// A thin line `width` square units wide, inset `inset` from the playing field's edge, on the
// frame's top face (a stringing or painted line round the board). 0 elsewhere.
float frame_line(float inset, float width) {
    if (local_normal.y < 0.5) {
        return 0.0;
    }
    vec2 q = board_point() - board_size * 0.5;
    float out_of_field = max(abs(q.x) - board_size.x * 0.5, abs(q.y) - board_size.y * 0.5);
    float d = abs(out_of_field - inset) - width * 0.5;
    float pixel = max(fwidth(out_of_field), 1e-5);
    return 1.0 - smoothstep(0.0, pixel, d);
}

// 1 on the frame's sides, 0 on its top.
float frame_side() {
    return 1.0 - step(0.5, local_normal.y);
}
