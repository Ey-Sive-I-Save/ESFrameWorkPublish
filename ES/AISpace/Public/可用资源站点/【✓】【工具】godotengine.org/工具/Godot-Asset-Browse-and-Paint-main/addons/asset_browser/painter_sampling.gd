@tool
extends RefCounted

## Stateless random sampling helpers used by AssetPainter.
## The caller owns and passes the RandomNumberGenerator so deterministic seeds
## and stroke sequencing remain controlled by the painter.

static func choose_weighted_entry(entries: Array[Dictionary], rng: RandomNumberGenerator) -> Dictionary:
	var total_weight := 0.0
	for entry in entries:
		total_weight += maxf(0.0, float(entry.get("weight", 0.0)))
	if total_weight <= 0.0:
		return {}
	var choice := rng.randf_range(0.0, total_weight)
	var running := 0.0
	for entry in entries:
		running += maxf(0.0, float(entry.get("weight", 0.0)))
		if choice <= running:
			return entry
	return entries.back() as Dictionary

static func choose_weighted_path(entries: Array[Dictionary], rng: RandomNumberGenerator) -> String:
	return str(choose_weighted_entry(entries, rng).get("path", ""))

static func distribution_offset(
		distribution_mode: int,
		sample_index: int,
		sample_total: int,
		_attempt: int,
		brush_radius: float,
		brush_falloff: float,
		cluster_count: int,
		cluster_strength: float,
		rng: RandomNumberGenerator
	) -> Vector2:
	# The attempt index is retained in the API for future deterministic sequences.
	match distribution_mode:
		2: # Clustered
			var clusters: int = maxi(1, mini(cluster_count, sample_total))
			var cluster_index: int = sample_index % clusters
			var base_angle: float = TAU * float(cluster_index) / float(clusters) + rng.randf_range(-0.35, 0.35)
			var base_radius: float = brush_radius * rng.randf_range(0.15, 0.72)
			var center := Vector2(cos(base_angle), sin(base_angle)) * base_radius
			var spread: float = brush_radius * lerpf(0.42, 0.06, cluster_strength)
			var jitter_angle: float = rng.randf_range(0.0, TAU)
			var jitter_radius: float = sqrt(rng.randf()) * spread
			return (center + Vector2(cos(jitter_angle), sin(jitter_angle)) * jitter_radius).limit_length(brush_radius)
		3: # Center bias
			var angle: float = rng.randf_range(0.0, TAU)
			var power: float = 1.0 + maxf(0.25, brush_falloff) * 3.0
			var radius: float = pow(rng.randf(), power) * brush_radius
			return Vector2(cos(angle), sin(angle)) * radius
		4: # Edge bias
			var angle: float = rng.randf_range(0.0, TAU)
			var power: float = 1.0 + maxf(0.25, brush_falloff) * 3.0
			var radius: float = (1.0 - pow(rng.randf(), power)) * brush_radius
			return Vector2(cos(angle), sin(angle)) * radius
		_:
			var angle: float = rng.randf_range(0.0, TAU)
			var radius: float = sqrt(rng.randf()) * brush_radius
			return Vector2(cos(angle), sin(angle)) * radius

static func random_rotation_angle(
		maximum_degrees: float,
		placing_on_grid: bool,
		snap_enabled: bool,
		snap_degrees: float,
		rng: RandomNumberGenerator
	) -> float:
	var angle := rng.randf_range(-maximum_degrees, maximum_degrees)
	if placing_on_grid and snap_enabled and snap_degrees > 0.0:
		angle = snappedf(angle, snap_degrees)
	return angle
