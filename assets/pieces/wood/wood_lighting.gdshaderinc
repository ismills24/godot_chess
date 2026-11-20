vec3 wood_color(vec3 position_local, vec3 normal_world, vec3 position_world, vec3 camera_world, bool dark) {
    vec3 n = normalize(normal_world);
    vec3 l = normalize(vec3(-0.65, 0.85, 0.5));
    float phase = position_local.x * 19.0 + position_local.z * 11.0 + sin(position_local.y * 3.0 + position_local.z * 5.0);
    float grain = sin(phase) * 0.006;
    vec3 base = dark ? vec3(0.39, 0.205, 0.105) : vec3(0.91, 0.73, 0.46);
    float illumination = 0.38 + 0.62 * max(dot(n, l), 0.0);
    vec3 view_dir = normalize(camera_world - position_world);
    float sheen = pow(max(dot(n, normalize(l + view_dir)), 0.0), 18.0) * 0.045;
    return base * (illumination + grain) + vec3(1.0, 0.85, 0.65) * sheen;
}
