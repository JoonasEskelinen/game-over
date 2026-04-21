shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_opaque;

// Pi-ystävällinen: ei tekstuureja, vain matemaattinen kohina + TIME
uniform float u_fade : hint_range(0.0, 1.0) = 1.0;
uniform vec3 u_color_core : source_color = vec3(0.25, 0.92, 1.0);
uniform vec3 u_color_hot : source_color = vec3(0.95, 1.0, 1.0);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float n2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	float a = hash(i);
	float b = hash(i + vec2(1.0, 0.0));
	float c = hash(i + vec2(0.0, 1.0));
	float d = hash(i + vec2(1.0, 1.0));
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float radial = length(uv);
	float ang = atan(uv.y, uv.x);

	float t = TIME * 22.0;
	// Ulkoreuna: iskun rengas (vahinkoalueen reunus)
	float rim = smoothstep(0.78, 0.98, radial) * (1.0 - smoothstep(0.98, 1.02, radial));
	// Sisäalue: halkeileva sähkö
	float inner = (1.0 - smoothstep(0.0, 0.12, radial)) * 0.35
		+ smoothstep(0.12, 0.88, radial) * (1.0 - smoothstep(0.88, 1.0, radial));

	float crawl = n2(vec2(ang * 5.0, radial * 14.0 - t * 0.4));
	float crawl2 = n2(vec2(ang * 11.0 + radial * 6.0, t * 0.35));
	float bolts = smoothstep(0.72, 0.98, crawl) + smoothstep(0.78, 0.95, crawl2) * 0.85;

	float arc_a = sin(ang * 9.0 + t + n2(vec2(ang * 3.0, t * 0.2)) * 4.0);
	float arc_b = sin(ang * 17.0 - t * 1.1 + radial * 25.0);
	float arcs = (smoothstep(0.82, 1.0, abs(arc_a)) + smoothstep(0.85, 1.0, abs(arc_b)) * 0.6) * inner;

	float flick = step(0.55, fract(sin(dot(uv + vec2(t, t * 0.7), vec2(12.9898, 78.233))) * 43758.0));
	flick = mix(0.65, 1.0, flick);

	float g = rim * 1.15 + bolts * inner * 0.9 + arcs * 1.25;
	g *= flick * u_fade;
	g = clamp(g, 0.0, 2.8);

	vec3 col = mix(u_color_core, u_color_hot, clamp(g * 0.55, 0.0, 1.0));
	ALBEDO = col * g;
	ALPHA = clamp(g * 0.75, 0.0, 1.0);
	EMISSION = col * g * 2.4;
}
