shader_type spatial;
render_mode unshaded, blend_mul, depth_draw_always, shadows_disabled, cull_back;
// Drawn in the sharp view (common/sharp_layer.gd) over anything in the room that can come
// between the camera and the board, such as an opponent's reaching arm: it multiplies the
// view to nothing where it lies in front, so the pixel stage's own arm shows through there.

void fragment() {
    ALBEDO = vec3(0.0);
    ALPHA = 0.0;
}
