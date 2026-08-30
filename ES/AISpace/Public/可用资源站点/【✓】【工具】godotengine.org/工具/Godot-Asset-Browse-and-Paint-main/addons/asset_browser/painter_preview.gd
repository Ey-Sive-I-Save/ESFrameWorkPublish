@tool
extends RefCounted

static func draw_brush(
	canvas: Control,
	camera: Camera3D,
	hit: Dictionary,
	preview_points: Array[Vector3],
	brush_radius: float,
	surface_valid: bool,
	erase_mode: bool,
	reapply_mode: bool
) -> void:
	if hit.is_empty() or camera == null:
		return
	var hit_position: Vector3 = hit["position"] as Vector3
	var hit_normal: Vector3 = (hit["normal"] as Vector3).normalized()
	if camera.is_position_behind(hit_position):
		return
	var center: Vector2 = camera.unproject_position(hit_position)
	var right := _surface_right(hit_normal)
	var edge_world: Vector3 = hit_position + right * brush_radius
	var edge: Vector2 = camera.unproject_position(edge_world)
	var radius_px: float = maxf(4.0, center.distance_to(edge))
	var brush_color := Color(0.25, 0.75, 1.0, 0.95)
	if bool(hit.get("grid_surface", false)):
		brush_color = Color(0.15, 0.88, 0.95, 0.95)
	if reapply_mode:
		brush_color = Color(0.75, 0.35, 1.0, 0.95)
	elif erase_mode:
		brush_color = Color(1.0, 0.25, 0.25, 0.95)
	elif not surface_valid:
		brush_color = Color(1.0, 0.55, 0.15, 0.95)
	canvas.draw_circle(center, radius_px, Color(brush_color.r, brush_color.g, brush_color.b, 0.075))
	canvas.draw_arc(center, radius_px + 1.5, 0.0, TAU, 72, Color(0.0, 0.0, 0.0, 0.55), 4.5, true)
	canvas.draw_arc(center, radius_px, 0.0, TAU, 72, brush_color, 2.5, true)
	canvas.draw_arc(center, radius_px * 0.5, 0.0, TAU, 48, Color(brush_color.r, brush_color.g, brush_color.b, 0.52), 1.25, true)
	canvas.draw_circle(center, 4.5, Color(0.0, 0.0, 0.0, 0.65))
	canvas.draw_circle(center, 3.0, brush_color)
	canvas.draw_line(center + Vector2(-8.0, 0.0), center + Vector2(-4.5, 0.0), brush_color, 1.5, true)
	canvas.draw_line(center + Vector2(4.5, 0.0), center + Vector2(8.0, 0.0), brush_color, 1.5, true)
	canvas.draw_line(center + Vector2(0.0, -8.0), center + Vector2(0.0, -4.5), brush_color, 1.5, true)
	canvas.draw_line(center + Vector2(0.0, 4.5), center + Vector2(0.0, 8.0), brush_color, 1.5, true)
	var normal_end_world: Vector3 = hit_position + hit_normal * maxf(0.5, brush_radius * 0.35)
	if not camera.is_position_behind(normal_end_world):
		var normal_end: Vector2 = camera.unproject_position(normal_end_world)
		canvas.draw_line(center, normal_end, brush_color, 2.0, true)
		var direction: Vector2 = (normal_end - center).normalized()
		var side := Vector2(-direction.y, direction.x)
		canvas.draw_colored_polygon(PackedVector2Array([normal_end, normal_end - direction * 9.0 + side * 4.0, normal_end - direction * 9.0 - side * 4.0]), brush_color)
	for point in preview_points:
		if camera.is_position_behind(point):
			continue
		var screen_point: Vector2 = camera.unproject_position(point)
		var marker_color := Color(brush_color.r, brush_color.g, brush_color.b, 0.78)
		canvas.draw_circle(screen_point, 5.0, Color(marker_color.r, marker_color.g, marker_color.b, 0.20))
		canvas.draw_arc(screen_point, 5.0, 0.0, TAU, 16, marker_color, 1.5, true)
		canvas.draw_line(screen_point + Vector2(-3.0, 0.0), screen_point + Vector2(3.0, 0.0), marker_color, 1.0, true)
		canvas.draw_line(screen_point + Vector2(0.0, -3.0), screen_point + Vector2(0.0, 3.0), marker_color, 1.0, true)

static func draw_area_selection(canvas: Control, active: bool, dragging: bool, tool_mode: int, area_start: Vector2, area_current: Vector2, lasso_points: PackedVector2Array) -> void:
	if not active or not dragging:
		return
	var color := Color(0.35, 0.9, 0.55, 0.95)
	if tool_mode == 1:
		var rect := Rect2(area_start, area_current - area_start).abs()
		canvas.draw_rect(rect, Color(color.r, color.g, color.b, 0.09), true)
		canvas.draw_rect(rect, color, false, 2.0)
	elif tool_mode == 2 and lasso_points.size() > 1:
		canvas.draw_polyline(lasso_points, color, 2.0, true)
		if lasso_points.size() > 2:
			canvas.draw_line(lasso_points[lasso_points.size() - 1], lasso_points[0], Color(color.r, color.g, color.b, 0.5), 1.0, true)

static func draw_surface_path(canvas: Control, active: bool, tool_mode: int, camera: Camera3D, points: PackedVector2Array, drawing: bool, mouse_position: Vector2) -> void:
	if not active or tool_mode != 3 or camera == null:
		return
	if points.size() >= 2:
		for index in range(points.size() - 1):
			canvas.draw_line(points[index], points[index + 1], Color(0.2, 0.95, 0.65, 0.95), 3.0, true)
	for point in points:
		canvas.draw_circle(point, 3.5, Color(0.8, 1.0, 0.9, 0.95))
	if drawing and not points.is_empty():
		canvas.draw_line(points[points.size() - 1], mouse_position, Color(0.2, 0.95, 0.65, 0.45), 1.5, true)

static func build_preview_points(
	center_position: Vector3,
	normal: Vector3,
	brush_radius: float,
	count: int,
	distribution_mode: int,
	cluster_count: int,
	cluster_strength: float,
	brush_falloff: float,
	surface_offset: float,
	random_seed: int
) -> Array[Vector3]:
	var points: Array[Vector3] = []
	var preview_count: int = clampi(count, 1, 32)
	var right: Vector3 = _surface_right(normal)
	var forward: Vector3 = normal.cross(right).normalized()
	var preview_rng := RandomNumberGenerator.new()
	preview_rng.seed = 912367 + random_seed + preview_count * 31 + distribution_mode * 997
	for i in preview_count:
		var angle: float = preview_rng.randf_range(0.0, TAU)
		var radius_fraction: float = sqrt(preview_rng.randf())
		match distribution_mode:
			2:
				var clusters: int = maxi(1, mini(cluster_count, preview_count))
				var cluster_angle: float = TAU * float(i % clusters) / float(clusters)
				var cluster_center := Vector2(cos(cluster_angle), sin(cluster_angle)) * brush_radius * 0.45
				var jitter := Vector2(cos(angle), sin(angle)) * brush_radius * lerpf(0.32, 0.05, cluster_strength) * radius_fraction
				var offset: Vector2 = (cluster_center + jitter).limit_length(brush_radius * 0.9)
				points.append(center_position + right * offset.x + forward * offset.y + normal * surface_offset)
			3:
				radius_fraction = pow(preview_rng.randf(), 1.0 + maxf(0.25, brush_falloff) * 3.0)
				points.append(center_position + right * cos(angle) * radius_fraction * brush_radius * 0.9 + forward * sin(angle) * radius_fraction * brush_radius * 0.9 + normal * surface_offset)
			4:
				radius_fraction = 1.0 - pow(preview_rng.randf(), 1.0 + maxf(0.25, brush_falloff) * 3.0)
				points.append(center_position + right * cos(angle) * radius_fraction * brush_radius * 0.9 + forward * sin(angle) * radius_fraction * brush_radius * 0.9 + normal * surface_offset)
			_:
				points.append(center_position + right * cos(angle) * radius_fraction * brush_radius * 0.9 + forward * sin(angle) * radius_fraction * brush_radius * 0.9 + normal * surface_offset)
	return points

static func _surface_right(normal: Vector3) -> Vector3:
	var reference := Vector3.UP
	if absf(normal.dot(reference)) > 0.98:
		reference = Vector3.FORWARD
	return reference.cross(normal).normalized()
