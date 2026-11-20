shader_type spatial;
uniform float pitch = 0.10;
uniform float height = 0.30;
varying vec3 local_position;
varying float side;

void vertex() {
    local_position = VERTEX;
    side = 1.0 - abs(NORMAL.y);
    float angle = atan(VERTEX.z, VERTEX.x);
    float phase = (VERTEX.y + height * 0.5) / pitch - angle / 6.2831853;
    float groove = pow(0.5 + 0.5 * cos(phase * 6.2831853), 12.0);
    // The thread is cut INTO a solid dowel, never wrapped around its outside.
    VERTEX.xz *= 1.0 - side * groove * 0.035;
}

void fragment() {
    float angle = atan(local_position.z, local_position.x);
    float phase = (local_position.y + height * 0.5) / pitch - angle / 6.2831853;
    float groove = pow(0.5 + 0.5 * cos(phase * 6.2831853), 12.0) * side;
    float grain = sin(angle * 21.0 + sin(local_position.y * 17.0)) * 0.025;
    ALBEDO = vec3(0.58, 0.37, 0.19) * (1.0 - groove * 0.27 + grain);
    ROUGHNESS = 0.85;
}
