shader_type sky;
// A daytime sky: a deep zenith fading to a pale horizon, the sun's disc and glow, and two cloud
// layers (puffy cumulus and stretched streaks of cirrus) drifting slowly. Clouds carry fine
// octaves for crisp, billowing edges at full resolution.

uniform vec3 zenith_color : source_color = vec3(0.12, 0.32, 0.72);
uniform vec3 horizon_color : source_color = vec3(0.62, 0.78, 0.92);
uniform vec3 ground_color : source_color = vec3(0.35, 0.4, 0.45);
uniform vec3 cloud_color : source_color = vec3(1.0, 0.98, 0.95);
uniform vec3 cloud_shade : source_color = vec3(0.6, 0.66, 0.78);
uniform vec3 sun_color : source_color = vec3(1.0, 0.92, 0.75);
uniform float cumulus = 0.45;
uniform float cirrus = 0.3;
uniform float drift = 0.004;
uniform float horizon_glow = 0.35;
uniform float brightness = 1.0;

float hash(vec2 p) {
	vec3 q = fract(p.xyx * vec3(0.1031, 0.11369, 0.13787));
	q += dot(q, q.yzx + 19.19);
	return fract((q.x + q.y) * q.z);
}

float noise(vec2 p) {
	vec2 c = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(c), hash(c + vec2(1, 0)), f.x), mix(hash(c + vec2(0, 1)), hash(c + vec2(1)), f.x), f.y);
}

float fbm(vec2 p) {
	float total = 0.0;
	float amp = 0.5;
	mat2 turn = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	for (int i = 0; i < 7; i++) {
		total += noise(p) * amp;
		p = turn * p * 2.03 + vec2(1.7, 9.2);
		amp *= 0.5;
	}
	return total;
}

void sky() {
	vec3 d = EYEDIR;
	float up = max(d.y, 0.0);
	vec3 color = mix(horizon_color, zenith_color, pow(up, 0.55));
	vec3 sun_dir = LIGHT0_DIRECTION;
	float to_sun = max(dot(d, sun_dir), 0.0);
	color += sun_color * horizon_glow * pow(to_sun, 6.0) * (1.0 - up * 0.6);
	if (d.y > 0.0) {
		vec2 plane = d.xz / (d.y + 0.08);
		float t = TIME * drift;
		// Cumulus: lumpy, lit on their sun side.
		vec2 c = plane * 0.55 + vec2(t, t * 0.4);
		float puff = fbm(c);
		float cover = smoothstep(1.0 - cumulus, 1.0 - cumulus + 0.16, puff);
		float lit = clamp((puff - fbm(c + sun_dir.xz * 0.12)) * 6.0 + 0.55, 0.0, 1.0);
		vec3 cloud = mix(cloud_shade, cloud_color, lit);
		// Cirrus: long thin streaks.
		vec2 s = plane * vec2(0.12, 0.9) + vec2(t * 0.6, 0.0);
		float streak = fbm(s + fbm(s * 0.5) * 1.5);
		float wisp = smoothstep(1.0 - cirrus, 1.0, streak) * 0.75;
		float fade = smoothstep(0.0, 0.18, d.y);
		color = mix(color, cloud_color * 0.97, wisp * fade);
		color = mix(color, cloud, cover * fade);
	} else {
		color = mix(horizon_color, ground_color, smoothstep(0.0, 0.15, -d.y));
	}
	float disc = smoothstep(0.9993, 0.9996, to_sun);
	color += sun_color * (disc * 6.0 + pow(to_sun, 220.0) * 1.2);
	COLOR = color * brightness;
}
