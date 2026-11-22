float cell_hash(vec2 cell) {
	vec3 p = fract(cell.xyx * vec3(0.1031, 0.11369, 0.13787));
	p += dot(p, p.yzx + 19.19);
	return fract((p.x + p.y) * p.z);
}

float alley_noise(vec2 point) {
	vec2 cell = floor(point);
	vec2 f = fract(point);
	f = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(cell_hash(cell), cell_hash(cell + vec2(1, 0)), f.x),
		mix(cell_hash(cell + vec2(0, 1)), cell_hash(cell + vec2(1)), f.x), f.y);
}
