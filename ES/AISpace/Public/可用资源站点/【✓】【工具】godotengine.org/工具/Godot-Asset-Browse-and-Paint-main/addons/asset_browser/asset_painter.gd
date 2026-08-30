@tool
extends Control

const PainterGridMath = preload("res://addons/asset_browser/painter_grid_math.gd")
const AssetAnalysis = preload("res://addons/asset_browser/asset_analysis.gd")
const PainterGeometry = preload("res://addons/asset_browser/painter_geometry.gd")
const PainterSampling = preload("res://addons/asset_browser/painter_sampling.gd")
const PainterSpatialHash = preload("res://addons/asset_browser/painter_spatial_hash.gd")
const PainterPreview = preload("res://addons/asset_browser/painter_preview.gd")
const PainterMultiMesh = preload("res://addons/asset_browser/painter_multimesh.gd")

signal active_changed(active: bool)

var plugin: EditorPlugin
var panel: Control
var active := false
var erase_mode := false
var reapply_mode := false
var reapply_rotation := true
var reapply_scale := true
var reapply_alignment := false
var reapply_offset := false
var reapply_replace_variant := false
var brush_radius := 2.0
var count_per_click := 1
var drag_density := 2.0
var minimum_spacing := 0.75
var align_mode := 0
var blend_amount := 1.0
var random_rotation_enabled := true
var random_rotation_x_enabled := false
var random_rotation_y_enabled := true
var random_rotation_z_enabled := false
var random_rotation_x := 0.0
var random_rotation_y := 180.0
var random_rotation_z := 0.0
var random_scale_enabled := true
var random_scale_min := 0.85
var random_scale_max := 1.15
var placement_parent: Node3D
var paint_only_selected := false
var ignore_painted_assets := true
var placement_mode := 0 # 0 = Scene Instances, 1 = MultiMesh
var minimum_slope_degrees := 0.0
var maximum_slope_degrees := 90.0
var minimum_height := -100000.0
var maximum_height := 100000.0
var surface_offset := 0.0
var collision_layer_mask: int = 0xFFFFFFFF
# Placement surface: 0 = Geometry, 1 = Geometry + Grid fallback, 2 = Grid only.
var surface_mode: int = 1
var grid_plane: int = 0 # 0 = XZ, 1 = XY, 2 = YZ
var grid_offset: float = 0.0
var grid_snap: float = 1.0
# Grid snapping: 0 = fixed grid, 1 = selected asset bounds, 2 = custom per-axis.
var grid_snap_mode: int = 0
var grid_custom_snap: Vector3 = Vector3.ONE
var grid_rotation_snap_enabled: bool = false
var grid_rotation_snap_degrees: float = 90.0
var grid_override_occupied: bool = false
var _placing_on_grid: bool = false
var _active_grid_cell: Vector3i = Vector3i.ZERO
var _active_grid_step: Vector3 = Vector3.ONE
var _active_grid_layer: String = ""
var _asset_bounds_cache: Dictionary = {}
var asset_placement_bounds: Dictionary = {}
var random_seed: int = 0 # 0 = nondeterministic
var distribution_mode: int = 0 # 0 uniform, 1 blue noise, 2 clustered, 3 center bias, 4 edge bias
var brush_falloff: float = 0.0
var cluster_count: int = 3
var cluster_strength: float = 0.65
var _stroke_sequence: int = 0
# Phase 13 large-world optimization.
var use_spatial_hash: bool = true
var spatial_hash_cell_size: float = 4.0
var multimesh_chunking_enabled: bool = true
var multimesh_chunk_world_size: float = 64.0
var multimesh_chunk_instance_limit: int = 2000
var multimesh_visibility_begin: float = 0.0
var multimesh_visibility_end: float = 0.0
var _spacing_hash: Dictionary = {}
var _spacing_hash_dirty: bool = true
# Cached visible-mesh raycast data. The fallback raycaster used to rediscover every
# MeshInstance3D and rebuild every surface array for every brush sample.
var _visible_mesh_nodes_cache: Array[MeshInstance3D] = []
var _mesh_surface_cache: Dictionary = {}
var _visible_mesh_cache_dirty: bool = true
var _visible_mesh_cache_elapsed: float = 0.0
const VISIBLE_MESH_CACHE_REFRESH_SECONDS := 1.0

var _status_text := "Choose a Node3D parent"
var _last_hit: Dictionary = {}
var _stroke_nodes: Array[Node] = []
var _drag_distance := 0.0
var _last_mouse := Vector2.ZERO
var _mouse_position := Vector2.ZERO
var _viewport_camera: Camera3D
var _rng := RandomNumberGenerator.new()
var _scene_cache: Dictionary = {}
var _painted_nodes: Array[Node3D] = []
var _last_raycast_mouse := Vector2(-100000.0, -100000.0)
var _raycast_dirty := true
var _multimesh_asset_cache: Dictionary = {}
var _multimesh_before_snapshot: Array = []
var _multimesh_stroke_active := false
var _scene_before_snapshot: Array = []
var _scene_reapply_active := false
var _scene_grid_override_active := false
var _preview_points: Array[Vector3] = []
var _surface_valid := false
var _surface_feedback := "Move the brush over a surface"
var _stroke_sample_positions: Array[Vector3] = []
# Phase 8 area tools.
var area_tool_mode: int = 0 # 0 brush, 1 rectangle, 2 lasso, 3 surface path
var area_placement_count: int = 100
var area_max_placements: int = 5000
var _area_dragging := false
var _area_start := Vector2.ZERO
var _area_current := Vector2.ZERO
var _lasso_points: PackedVector2Array = PackedVector2Array()
# Phase 9 Surface Path Brush.
var path_spacing_mode: int = 0 # 0 fixed, 1 random
var path_asset_order: int = 0 # 0 weighted random, 1 alternate
var path_spacing_min: float = 2.0
var path_spacing_max: float = 4.0
var path_profile: int = 0 # 0 single, 1 double, 2 corridor, 3 rows
var path_width: float = 4.0
var path_row_count: int = 3
var path_noise: float = 0.0
var path_point_spacing: float = 0.5
var path_smoothing: float = 0.35
var path_align_direction: bool = true
var path_create_node: bool = true
var path_auto_scatter: bool = true
var path_live_update: bool = true
var path_update_delay: float = 0.25
var path_max_placements: int = 500
var _path_entries_override: Array[Dictionary] = []
var _path_regen_tokens: Dictionary = {}
var _path_regenerating: bool = false
var _path_drawing: bool = false
var _path_screen_points: PackedVector2Array = PackedVector2Array()
var _path_world_points: Array[Vector3] = []
var _path_world_normals: Array[Vector3] = []

func setup(p: EditorPlugin, browser_panel: Control, _toolbar_host: Node) -> void:
	plugin = p
	panel = browser_panel
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rng.randomize()
	set_process(true)
	_connect_existing_surface_paths.call_deferred()

func set_active(value: bool) -> void:
	active = value
	visible = value
	_raycast_dirty = true
	_visible_mesh_cache_dirty = true
	_visible_mesh_cache_elapsed = VISIBLE_MESH_CACHE_REFRESH_SECONDS
	if value:
		_refresh_painted_nodes()
	_spacing_hash_dirty = true
	_update_status()
	queue_redraw()
	active_changed.emit(value)

func toggle_active() -> void:
	set_active(not active)

func set_option(option_name: String, value: Variant) -> void:
	match option_name:
		"erase_mode":
			erase_mode = bool(value)
			if erase_mode:
				reapply_mode = false
		"reapply_mode":
			reapply_mode = bool(value)
			if reapply_mode:
				erase_mode = false
		"reapply_rotation": reapply_rotation = bool(value)
		"reapply_scale": reapply_scale = bool(value)
		"reapply_alignment": reapply_alignment = bool(value)
		"reapply_offset": reapply_offset = bool(value)
		"reapply_replace_variant": reapply_replace_variant = bool(value)
		"brush_radius": brush_radius = maxf(0.1, float(value))
		"count_per_click": count_per_click = maxi(1, int(value))
		"drag_density": drag_density = maxf(0.1, float(value))
		"minimum_spacing": minimum_spacing = maxf(0.0, float(value))
		"align_mode": align_mode = int(value)
		"random_rotation_enabled": random_rotation_enabled = bool(value)
		"random_rotation_x_enabled": random_rotation_x_enabled = bool(value)
		"random_rotation_y_enabled": random_rotation_y_enabled = bool(value)
		"random_rotation_z_enabled": random_rotation_z_enabled = bool(value)
		"random_rotation_x": random_rotation_x = clampf(float(value), 0.0, 180.0)
		"random_rotation_y": random_rotation_y = clampf(float(value), 0.0, 180.0)
		"random_rotation_z": random_rotation_z = clampf(float(value), 0.0, 180.0)
		"random_scale_enabled": random_scale_enabled = bool(value)
		"random_scale_min": random_scale_min = maxf(0.01, float(value))
		"random_scale_max": random_scale_max = maxf(0.01, float(value))
		"paint_only_selected": paint_only_selected = bool(value)
		"ignore_painted_assets": ignore_painted_assets = bool(value)
		"placement_mode": placement_mode = int(value)
		"minimum_slope_degrees": minimum_slope_degrees = clampf(float(value), 0.0, 90.0)
		"maximum_slope_degrees": maximum_slope_degrees = clampf(float(value), 0.0, 90.0)
		"minimum_height": minimum_height = float(value)
		"maximum_height": maximum_height = float(value)
		"surface_offset": surface_offset = float(value)
		"distribution_mode": distribution_mode = clampi(int(value), 0, 4)
		"brush_falloff": brush_falloff = clampf(float(value), 0.0, 1.0)
		"cluster_count": cluster_count = clampi(int(value), 1, 16)
		"cluster_strength": cluster_strength = clampf(float(value), 0.0, 1.0)
		"use_spatial_hash":
			use_spatial_hash = bool(value)
			_spacing_hash_dirty = true
		"spatial_hash_cell_size":
			spatial_hash_cell_size = maxf(0.25, float(value))
			_spacing_hash_dirty = true
		"multimesh_chunking_enabled": multimesh_chunking_enabled = bool(value)
		"multimesh_chunk_world_size": multimesh_chunk_world_size = maxf(1.0, float(value))
		"multimesh_chunk_instance_limit": multimesh_chunk_instance_limit = clampi(int(value), 16, 100000)
		"multimesh_visibility_begin": multimesh_visibility_begin = maxf(0.0, float(value))
		"multimesh_visibility_end": multimesh_visibility_end = maxf(0.0, float(value))
		"area_tool_mode": area_tool_mode = clampi(int(value), 0, 3)
		"area_placement_count": area_placement_count = clampi(int(value), 1, 100000)
		"area_max_placements": area_max_placements = clampi(int(value), 1, 100000)
		"path_spacing_mode": path_spacing_mode = clampi(int(value), 0, 1)
		"path_asset_order": path_asset_order = clampi(int(value), 0, 1)
		"path_spacing_min": path_spacing_min = maxf(0.01, float(value))
		"path_spacing_max": path_spacing_max = maxf(0.01, float(value))
		"path_profile": path_profile = clampi(int(value), 0, 3)
		"path_width": path_width = maxf(0.0, float(value))
		"path_row_count": path_row_count = clampi(int(value), 1, 32)
		"path_noise": path_noise = maxf(0.0, float(value))
		"path_point_spacing": path_point_spacing = maxf(0.05, float(value))
		"path_smoothing": path_smoothing = clampf(float(value), 0.0, 1.0)
		"path_align_direction": path_align_direction = bool(value)
		"path_create_node": path_create_node = bool(value)
		"path_auto_scatter": path_auto_scatter = bool(value)
		"path_live_update": path_live_update = bool(value)
		"path_update_delay": path_update_delay = clampf(float(value), 0.05, 2.0)
		"path_max_placements": path_max_placements = clampi(int(value), 1, 10000)
		"random_seed":
			random_seed = maxi(0, int(value))
			if random_seed == 0:
				_rng.randomize()
			else:
				_rng.seed = random_seed
			_stroke_sequence = 0
		"surface_mode": surface_mode = clampi(int(value), 0, 2)
		"grid_plane": grid_plane = clampi(int(value), 0, 2)
		"grid_offset": grid_offset = float(value)
		"grid_snap": grid_snap = maxf(0.0, float(value))
		"grid_snap_mode": grid_snap_mode = clampi(int(value), 0, 2)
		"grid_custom_snap_x": grid_custom_snap.x = maxf(0.0, float(value))
		"grid_custom_snap_y": grid_custom_snap.y = maxf(0.0, float(value))
		"grid_custom_snap_z": grid_custom_snap.z = maxf(0.0, float(value))
		"grid_rotation_snap_enabled": grid_rotation_snap_enabled = bool(value)
		"grid_rotation_snap_degrees": grid_rotation_snap_degrees = clampf(float(value), 1.0, 180.0)
		"grid_override_occupied": grid_override_occupied = bool(value)
		"asset_placement_bounds":
			asset_placement_bounds = (value as Dictionary).duplicate(true) if value is Dictionary else {}
			_asset_bounds_cache.clear()
		"collision_layer_mask":
			if value == null:
				collision_layer_mask = 0xFFFFFFFF
			else:
				collision_layer_mask = clampi(int(value), 1, 0xFFFFFFFF)
	_raycast_dirty = true
	_update_preview_state()
	queue_redraw()

func use_selected_parent() -> void:
	_use_selected_parent()

func get_status_text() -> String:
	return _status_text

func _use_selected_parent() -> void:
	var nodes: Array[Node] = EditorInterface.get_selection().get_selected_nodes()
	placement_parent = nodes[0] as Node3D if nodes.size() > 0 and nodes[0] is Node3D else null
	_refresh_painted_nodes()
	_update_status()

func _update_status() -> void:
	var parent_name := "None"
	if placement_parent != null and is_instance_valid(placement_parent):
		parent_name = str(placement_parent.name)
	var selected_count: int = 0
	if panel != null and panel.has_method("get_selected_scene_paths"):
		selected_count = (panel.call("get_selected_scene_paths") as Array).size()
	var mode_text := "MultiMesh" if placement_mode == 1 else "Scene"
	var tool_text := "Surface Path" if area_tool_mode == 3 else ("Rectangle" if area_tool_mode == 1 else ("Lasso" if area_tool_mode == 2 else ("Reapply" if reapply_mode else ("Erase" if erase_mode else "Paint"))))
	_status_text = "%s | %s | Assets: %d | Parent: %s | Radius: %.2f | Count: %d | Density: %.2f | Spacing: %.2f | %s" % [tool_text, mode_text, selected_count, parent_name, brush_radius, count_per_click, drag_density, minimum_spacing, _surface_feedback]
	if placement_parent == null or not is_instance_valid(placement_parent):
		_status_text += " | Select a Node3D and click Select Parent"
	if panel != null and panel.has_method("set_painter_status"):
		panel.set_painter_status(_status_text)

func _process(delta: float) -> void:
	if not active:
		return
	_visible_mesh_cache_elapsed += delta
	_update_status()
	if _raycast_dirty and _viewport_camera != null and is_instance_valid(_viewport_camera):
		_last_hit = _raycast_scene(_mouse_position, _viewport_camera)
		_last_raycast_mouse = _mouse_position
		_raycast_dirty = false
		_update_preview_state()
		queue_redraw()

func _draw() -> void:
	_draw_area_selection()
	_draw_surface_path_preview()
	if area_tool_mode == 3 or not active or _last_hit.is_empty() or _viewport_camera == null:
		return
	PainterPreview.draw_brush(self, _viewport_camera, _last_hit, _preview_points, brush_radius, _surface_valid, erase_mode, reapply_mode)

func _draw_area_selection() -> void:
	PainterPreview.draw_area_selection(self, active, _area_dragging, area_tool_mode, _area_start, _area_current, _lasso_points)

func _update_preview_state() -> void:
	_preview_points.clear()
	if _last_hit.is_empty():
		_surface_valid = false
		_surface_feedback = "No surface under brush"
		_update_status()
		return
	_surface_feedback = _get_filter_rejection_reason(_last_hit)
	_surface_valid = _surface_feedback.is_empty()
	if _surface_valid:
		_surface_feedback = "Reapply ready" if reapply_mode else ("Erase ready" if erase_mode else "Ready")
	if erase_mode or reapply_mode:
		_update_status()
		return
	var center_position: Vector3 = _last_hit["position"] as Vector3
	var normal: Vector3 = (_last_hit["normal"] as Vector3).normalized()
	_preview_points = PainterPreview.build_preview_points(center_position, normal, brush_radius, count_per_click, distribution_mode, cluster_count, cluster_strength, brush_falloff, surface_offset, random_seed)
	_update_status()

func _get_filter_rejection_reason(hit: Dictionary) -> String:
	if hit.is_empty():
		return "No surface under brush"
	var position: Vector3 = hit["position"] as Vector3
	var normal: Vector3 = (hit["normal"] as Vector3).normalized()
	var slope_degrees: float = rad_to_deg(acos(clampf(normal.dot(Vector3.UP), -1.0, 1.0)))
	var low_slope: float = minf(minimum_slope_degrees, maximum_slope_degrees)
	var high_slope: float = maxf(minimum_slope_degrees, maximum_slope_degrees)
	if slope_degrees < low_slope:
		return "Rejected: slope %.1f° is below %.1f°" % [slope_degrees, low_slope]
	if slope_degrees > high_slope:
		return "Rejected: slope %.1f° exceeds %.1f°" % [slope_degrees, high_slope]
	var low_height: float = minf(minimum_height, maximum_height)
	var high_height: float = maxf(minimum_height, maximum_height)
	if position.y < low_height:
		return "Rejected: height %.2f is below %.2f" % [position.y, low_height]
	if position.y > high_height:
		return "Rejected: height %.2f exceeds %.2f" % [position.y, high_height]
	return ""

func handle_viewport_input(camera: Camera3D, event: InputEvent) -> bool:
	if not active:
		return false
	_viewport_camera = camera

	if event is InputEventMouse:
		_mouse_position = event.position
		if _mouse_position.distance_squared_to(_last_raycast_mouse) > 0.25:
			_raycast_dirty = true

	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ESCAPE:
			if _path_drawing:
				_path_drawing = false
				_path_screen_points.clear()
				_path_world_points.clear()
				_path_world_normals.clear()
				queue_redraw()
			elif _area_dragging:
				_area_dragging = false
				_lasso_points.clear()
				queue_redraw()
			else:
				set_active(false)
			return true
		if event.keycode == KEY_BRACKETLEFT or event.keycode == KEY_BRACKETRIGHT:
			var direction: float = -1.0 if event.keycode == KEY_BRACKETLEFT else 1.0
			if event.alt_pressed:
				minimum_spacing = clampf(minimum_spacing + direction * 0.05, 0.0, 100.0)
				_sync_panel_option("minimum_spacing", minimum_spacing)
			elif event.ctrl_pressed:
				count_per_click = clampi(count_per_click + int(direction), 1, 100)
				_sync_panel_option("count_per_click", count_per_click)
			elif event.shift_pressed:
				drag_density = clampf(drag_density + direction * 0.25, 0.1, 50.0)
				_sync_panel_option("drag_density", drag_density)
			else:
				brush_radius = clampf(brush_radius + direction * 0.25, 0.1, 100.0)
				_sync_panel_option("brush_radius", brush_radius)
			queue_redraw()
			return true

	if area_tool_mode == 3:
		if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_begin_surface_path(event.position, camera)
			else:
				_finish_surface_path()
			queue_redraw()
			return true
		if event is InputEventMouseMotion and _path_drawing:
			_append_surface_path_point(event.position, camera)
			queue_redraw()
			return true
		return false

	if area_tool_mode > 0:
		if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_area_dragging = true
				_area_start = event.position
				_area_current = event.position
				_lasso_points.clear()
				if area_tool_mode == 2:
					_lasso_points.append(event.position)
			else:
				_area_current = event.position
				_execute_screen_area(camera)
				_area_dragging = false
				_lasso_points.clear()
			queue_redraw()
			return true
		if event is InputEventMouseMotion and _area_dragging:
			_area_current = event.position
			if area_tool_mode == 2 and (_lasso_points.is_empty() or _lasso_points[_lasso_points.size() - 1].distance_to(event.position) >= 4.0):
				_lasso_points.append(event.position)
			queue_redraw()
			return true
		return false

	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT:
			if event.pressed:
				_prepare_rng_for_operation()
				_stroke_nodes.clear()
				_stroke_sample_positions.clear()
				_drag_distance = 0.0
				_last_mouse = event.position
				if placement_mode == 1:
					_multimesh_before_snapshot = _capture_multimesh_snapshot()
					_multimesh_stroke_active = true
				elif reapply_mode:
					_scene_before_snapshot = _capture_scene_snapshot()
					_scene_reapply_active = true
				elif grid_override_occupied and surface_mode != 0:
					_scene_before_snapshot = _capture_scene_snapshot()
					_scene_grid_override_active = true
				_paint_at(event.position, camera, event.shift_pressed, event.ctrl_pressed)
			else:
				if placement_mode == 1:
					_commit_multimesh_stroke("Reapply MultiMesh Assets" if reapply_mode else ("Erase MultiMesh Assets" if erase_mode else "Paint MultiMesh Assets"))
				elif reapply_mode:
					_commit_scene_reapply()
				elif _scene_grid_override_active:
					_commit_scene_grid_override()
				else:
					_commit_stroke()
			return true

	if event is InputEventMouseMotion and Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		_drag_distance += event.position.distance_to(_last_mouse)
		_last_mouse = event.position
		var threshold: float = maxf(4.0, 60.0 / maxf(0.1, drag_density))
		if _drag_distance >= threshold:
			_drag_distance = 0.0
			_paint_at(event.position, camera, event.shift_pressed, event.ctrl_pressed)
		return true

	return false

func _prepare_rng_for_operation() -> void:
	if random_seed > 0:
		_rng.seed = random_seed + _stroke_sequence
		_stroke_sequence += 1

func _begin_bulk_operation() -> void:
	_prepare_rng_for_operation()
	_stroke_nodes.clear()
	_stroke_sample_positions.clear()
	if placement_mode == 1:
		_multimesh_before_snapshot = _capture_multimesh_snapshot()
		_multimesh_stroke_active = true
	elif grid_override_occupied and surface_mode != 0:
		_scene_before_snapshot = _capture_scene_snapshot()
		_scene_grid_override_active = true

func _finish_bulk_operation(action_name: String) -> void:
	if placement_mode == 1:
		_commit_multimesh_stroke(action_name)
	elif _scene_grid_override_active:
		_commit_scene_grid_override()
	else:
		_commit_stroke()
	_refresh_painted_nodes()

func _area_target_count() -> int:
	return mini(maxi(1, area_placement_count), maxi(1, area_max_placements))

func _lasso_bounds() -> Rect2:
	if _lasso_points.is_empty():
		return Rect2()
	var minimum := _lasso_points[0]
	var maximum := _lasso_points[0]
	for point in _lasso_points:
		minimum.x = minf(minimum.x, point.x)
		minimum.y = minf(minimum.y, point.y)
		maximum.x = maxf(maximum.x, point.x)
		maximum.y = maxf(maximum.y, point.y)
	return Rect2(minimum, maximum - minimum)

func _sample_lasso_screen_point() -> Variant:
	if _lasso_points.size() < 3:
		return null
	var triangle_indices: PackedInt32Array = Geometry2D.triangulate_polygon(_lasso_points)
	if triangle_indices.size() < 3:
		return null
	var triangle_areas: PackedFloat32Array = PackedFloat32Array()
	var total_area := 0.0
	for index in range(0, triangle_indices.size(), 3):
		var a: Vector2 = _lasso_points[triangle_indices[index]]
		var b: Vector2 = _lasso_points[triangle_indices[index + 1]]
		var c: Vector2 = _lasso_points[triangle_indices[index + 2]]
		var area := absf((b - a).cross(c - a)) * 0.5
		triangle_areas.append(area)
		total_area += area
	if total_area <= 0.0001:
		return null
	var pick := _rng.randf_range(0.0, total_area)
	var running := 0.0
	var selected_triangle := 0
	for triangle_index in triangle_areas.size():
		running += triangle_areas[triangle_index]
		if pick <= running:
			selected_triangle = triangle_index
			break
	var base_index := selected_triangle * 3
	var point_a: Vector2 = _lasso_points[triangle_indices[base_index]]
	var point_b: Vector2 = _lasso_points[triangle_indices[base_index + 1]]
	var point_c: Vector2 = _lasso_points[triangle_indices[base_index + 2]]
	var root_random := sqrt(_rng.randf())
	var second_random := _rng.randf()
	return point_a * (1.0 - root_random) + point_b * (root_random * (1.0 - second_random)) + point_c * (root_random * second_random)

func _execute_screen_area(camera: Camera3D) -> void:
	if placement_parent == null or not is_instance_valid(placement_parent):
		_surface_feedback = "Select a parent before using area tools"
		return
	if area_tool_mode == 2 and _lasso_points.size() < 3:
		return
	var bounds := _lasso_bounds() if area_tool_mode == 2 else Rect2(_area_start, _area_current - _area_start).abs()
	if bounds.size.x < 3.0 or bounds.size.y < 3.0:
		return
	if surface_mode == 2:
		_execute_grid_area_fill(camera, bounds)
		return
	_begin_bulk_operation()
	var placed := 0
	var target := _area_target_count()
	var attempts := target * 30
	for _attempt in attempts:
		if placed >= target:
			break
		var screen := Vector2.ZERO
		if area_tool_mode == 2:
			var sampled_point: Variant = _sample_lasso_screen_point()
			if sampled_point == null:
				break
			screen = sampled_point as Vector2
		else:
			screen = Vector2(_rng.randf_range(bounds.position.x, bounds.end.x), _rng.randf_range(bounds.position.y, bounds.end.y))
		var hit := _raycast_scene(screen, camera)
		if _place_single_from_hit(hit):
			placed += 1
	_finish_bulk_operation("Scatter %d Assets in %s" % [placed, "Rectangle" if area_tool_mode == 1 else "Lasso"])
	_surface_feedback = "Placed %d of %d requested assets" % [placed, target]
	if placed < target:
		_surface_feedback += " (spacing or surface filters prevented the rest)"
	_update_status()

func _execute_grid_area_fill(camera: Camera3D, screen_bounds: Rect2) -> void:
	var entries := _current_weighted_entries()
	if entries.is_empty():
		_surface_feedback = "Select one or more assets before filling grid cells"
		_update_status()
		return
	var step := _grid_cell_size()
	var plane_step := _grid_plane_step(step)
	if plane_step.x <= 0.00001 or plane_step.y <= 0.00001:
		_surface_feedback = "Grid snap size must be greater than zero"
		_update_status()
		return
	var world_points: Array[Vector3] = []
	var screen_points: PackedVector2Array = _lasso_points if area_tool_mode == 2 else PackedVector2Array([
		screen_bounds.position,
		Vector2(screen_bounds.end.x, screen_bounds.position.y),
		screen_bounds.end,
		Vector2(screen_bounds.position.x, screen_bounds.end.y)
	])
	for screen_point in screen_points:
		var origin := camera.project_ray_origin(screen_point)
		var direction := camera.project_ray_normal(screen_point).normalized()
		var hit := _raycast_grid_plane(origin, direction)
		if not hit.is_empty():
			world_points.append(hit["position"] as Vector3)
	if world_points.is_empty():
		return

	var first_cell := _world_to_grid_cell(world_points[0], step)
	var min_a := _grid_cell_axis_a(first_cell)
	var max_a := min_a
	var min_b := _grid_cell_axis_b(first_cell)
	var max_b := min_b
	for point in world_points:
		var cell := _world_to_grid_cell(point, step)
		var a := _grid_cell_axis_a(cell)
		var b := _grid_cell_axis_b(cell)
		min_a = mini(min_a, a)
		max_a = maxi(max_a, a)
		min_b = mini(min_b, b)
		max_b = maxi(max_b, b)

	_begin_bulk_operation()
	var placed := 0
	var visited := 0
	var safety_limit := maxi(1, area_max_placements)
	for a in range(min_a, max_a + 1):
		for b in range(min_b, max_b + 1):
			if visited >= safety_limit:
				break
			visited += 1
			var cell := _grid_cell_from_plane_axes(a, b)
			var world_position := _grid_cell_center(cell, step)
			var screen_position := camera.unproject_position(world_position)
			var inside := screen_bounds.has_point(screen_position)
			if area_tool_mode == 2:
				inside = Geometry2D.is_point_in_polygon(screen_position, _lasso_points)
			if inside:
				var entry := _choose_weighted_entry(entries)
				if not entry.is_empty() and _place_grid_fill_entry(entry, cell, step):
					placed += 1
		if visited >= safety_limit:
			break
	_finish_bulk_operation("Fill %d Grid Cells in %s" % [placed, "Rectangle" if area_tool_mode == 1 else "Lasso"])
	_surface_feedback = "Filled %d grid cells" % placed
	if visited >= safety_limit:
		_surface_feedback += " (stopped at safety limit)"
	_update_status()

func _grid_reference_asset_path() -> String:
	var entries := _current_weighted_entries()
	if entries.is_empty():
		return ""
	return str(entries[0].get("path", ""))

func _grid_cell_size() -> Vector3:
	if grid_snap_mode == 1:
		var reference_path := _grid_reference_asset_path()
		if not reference_path.is_empty():
			var measured := _get_asset_local_aabb(reference_path).size.abs()
			if asset_placement_bounds.has(reference_path):
				var stored: Variant = asset_placement_bounds[reference_path]
				if stored is Vector3:
					measured = (stored as Vector3).abs()
				elif stored is Array and (stored as Array).size() >= 3:
					measured = Vector3(float(stored[0]), float(stored[1]), float(stored[2])).abs()
			return Vector3(maxf(measured.x, 0.001), maxf(measured.y, 0.001), maxf(measured.z, 0.001))
	elif grid_snap_mode == 2:
		var custom := grid_custom_snap.abs()
		return Vector3(maxf(custom.x, 0.001), maxf(custom.y, 0.001), maxf(custom.z, 0.001))
	var fixed := maxf(grid_snap, 0.001)
	return Vector3(fixed, fixed, fixed)

func _grid_plane_step(step: Vector3) -> Vector2:
	return PainterGridMath.plane_step(grid_plane, step)

func _world_to_grid_cell(position: Vector3, step: Vector3) -> Vector3i:
	return PainterGridMath.world_to_cell(position, step)

func _grid_cell_center(cell: Vector3i, step: Vector3) -> Vector3:
	return PainterGridMath.cell_center(grid_plane, grid_offset, cell, step)

func _grid_cell_axis_a(cell: Vector3i) -> int:
	return PainterGridMath.cell_axis_a(grid_plane, cell)

func _grid_cell_axis_b(cell: Vector3i) -> int:
	return PainterGridMath.cell_axis_b(grid_plane, cell)

func _grid_cell_from_plane_axes(a: int, b: int) -> Vector3i:
	return PainterGridMath.cell_from_plane_axes(grid_plane, a, b)

func _grid_plane_coordinates(position: Vector3) -> Vector2:
	return PainterGridMath.plane_coordinates(grid_plane, position)

func _grid_world_position(coordinate: Vector2) -> Vector3:
	return PainterGridMath.world_position(grid_plane, grid_offset, coordinate)

func _place_grid_fill_entry(entry: Dictionary, cell: Vector3i, step: Vector3) -> bool:
	var path := str(entry.get("path", ""))
	if path.is_empty():
		return false
	var normal := _grid_plane_normal()
	var layer := _grid_layer_key(step)
	if _grid_cell_is_occupied(cell, layer):
		if not grid_override_occupied:
			return false
		_remove_grid_cell_assets(cell, layer)
	var cell_center := _grid_cell_center(cell, step)
	_stroke_sample_positions.append(cell_center)
	_begin_grid_placement(cell, step)
	if placement_mode == 1:
		_place_multimesh(path, cell_center, normal)
	else:
		_place_scene(path, cell_center, normal)
	_end_grid_placement()
	return true

func _grid_plane_normal() -> Vector3:
	return PainterGridMath.plane_normal(grid_plane)

func _grid_layer_key(step: Vector3) -> String:
	return PainterGridMath.layer_key(grid_plane, grid_offset, step)

func _begin_grid_placement(cell: Vector3i, step: Vector3) -> void:
	_placing_on_grid = true
	_active_grid_cell = cell
	_active_grid_step = step
	_active_grid_layer = _grid_layer_key(step)

func _end_grid_placement() -> void:
	_placing_on_grid = false
	_active_grid_layer = ""

func _grid_visual_anchor(path: String) -> Vector3:
	var bounds := _get_asset_local_aabb(path)
	match grid_plane:
		0:
			return Vector3(bounds.position.x + bounds.size.x * 0.5, bounds.position.y, bounds.position.z + bounds.size.z * 0.5)
		1:
			return Vector3(bounds.position.x + bounds.size.x * 0.5, bounds.position.y + bounds.size.y * 0.5, bounds.position.z)
		2:
			return Vector3(bounds.position.x, bounds.position.y + bounds.size.y * 0.5, bounds.position.z + bounds.size.z * 0.5)
	return bounds.get_center()

func _grid_corrected_root_position(path: String, cell_center: Vector3, basis: Basis) -> Vector3:
	return cell_center - basis * _grid_visual_anchor(path) + _grid_plane_normal() * surface_offset

func _grid_cell_is_occupied(cell: Vector3i, layer: String) -> bool:
	if placement_parent == null:
		return false
	for child in placement_parent.get_children():
		if child is MultiMeshInstance3D and child.has_meta("asset_painter_multimesh"):
			if str(child.get_meta("asset_painter_grid_layer", "")) != layer:
				continue
			var cells: Array = child.get_meta("asset_painter_grid_cells", []) as Array
			for value in cells:
				if value is Vector3i and (value as Vector3i) == cell:
					return true
		elif child is Node3D and child.has_meta("asset_painter_placed"):
			if str(child.get_meta("asset_painter_grid_layer", "")) == layer and child.get_meta("asset_painter_grid_cell", Vector3i(2147483647, 2147483647, 2147483647)) == cell:
				return true
	return false

func _remove_grid_cell_assets(cell: Vector3i, layer: String) -> void:
	if placement_parent == null:
		return
	for child in placement_parent.get_children():
		if child is MultiMeshInstance3D and child.has_meta("asset_painter_multimesh"):
			var mm := child as MultiMeshInstance3D
			if mm.multimesh == null or str(mm.get_meta("asset_painter_grid_layer", "")) != layer:
				continue
			var cells: Array = mm.get_meta("asset_painter_grid_cells", []) as Array
			var kept_transforms: Array[Transform3D] = []
			var kept_cells: Array = []
			for index in mm.multimesh.instance_count:
				var existing_cell: Variant = cells[index] if index < cells.size() else null
				if existing_cell is Vector3i and (existing_cell as Vector3i) == cell:
					continue
				kept_transforms.append(mm.multimesh.get_instance_transform(index))
				kept_cells.append(existing_cell)
			_set_multimesh_transforms(mm, kept_transforms)
			mm.set_meta("asset_painter_grid_cells", kept_cells)
		elif child is Node3D and child.has_meta("asset_painter_placed"):
			if str(child.get_meta("asset_painter_grid_layer", "")) == layer and child.get_meta("asset_painter_grid_cell", Vector3i(2147483647, 2147483647, 2147483647)) == cell:
				placement_parent.remove_child(child)
				child.free()
	_spacing_hash_dirty = true
	_refresh_painted_nodes()

func fill_selected_mesh() -> void:
	if placement_parent == null or not is_instance_valid(placement_parent):
		_surface_feedback = "Select a parent before filling"
		_update_status()
		return
	var selected := EditorInterface.get_selection().get_selected_nodes()
	var mesh_node: MeshInstance3D = null
	for node in selected:
		if node is MeshInstance3D and (node as MeshInstance3D).mesh != null:
			mesh_node = node as MeshInstance3D
			break
	if mesh_node == null:
		_surface_feedback = "Select a MeshInstance3D to fill"
		_update_status()
		return
	var triangles := _mesh_triangles(mesh_node)
	if triangles.is_empty():
		_surface_feedback = "The selected mesh has no readable triangles"
		_update_status()
		return
	var total_area := 0.0
	for triangle_value in triangles:
		total_area += float((triangle_value as Dictionary).get("area", 0.0))
	if total_area <= 0.0:
		return
	_begin_bulk_operation()
	var target := _area_target_count()
	var placed := 0
	for _attempt in target * 12:
		if placed >= target:
			break
		var pick := _rng.randf_range(0.0, total_area)
		var triangle: Dictionary = triangles[triangles.size() - 1] as Dictionary
		var running := 0.0
		for triangle_value in triangles:
			var candidate := triangle_value as Dictionary
			running += float(candidate.get("area", 0.0))
			if pick <= running:
				triangle = candidate
				break
		var r1 := sqrt(_rng.randf())
		var r2 := _rng.randf()
		var a: Vector3 = triangle["a"] as Vector3
		var b: Vector3 = triangle["b"] as Vector3
		var c: Vector3 = triangle["c"] as Vector3
		var local_point := a * (1.0 - r1) + b * (r1 * (1.0 - r2)) + c * (r1 * r2)
		var local_normal: Vector3 = triangle["normal"] as Vector3
		var hit := {"position": mesh_node.global_transform * local_point, "normal": (mesh_node.global_basis * local_normal).normalized(), "collider": mesh_node}
		if _place_single_from_hit(hit):
			placed += 1
	_finish_bulk_operation("Fill Selected Mesh with %d Assets" % placed)
	_surface_feedback = "Filled %s with %d assets" % [mesh_node.name, placed]
	_update_status()

func fill_selected_area() -> void:
	var area := _selected_area3d()
	if area == null:
		_surface_feedback = "Select an Area3D with a CollisionShape3D"
		_update_status()
		return
	if placement_parent == null or not is_instance_valid(placement_parent):
		_surface_feedback = "Select a parent before filling"
		_update_status()
		return
	var shape_node := _first_collision_shape(area)
	if shape_node == null or shape_node.shape == null:
		_surface_feedback = "Selected Area3D has no supported CollisionShape3D"
		return
	var local_bounds := _shape_local_aabb(shape_node.shape)
	_begin_bulk_operation()
	var target := _area_target_count()
	var placed := 0
	for _attempt in target * 20:
		if placed >= target:
			break
		var local := Vector3(_rng.randf_range(local_bounds.position.x, local_bounds.end.x), _rng.randf_range(local_bounds.position.y, local_bounds.end.y), _rng.randf_range(local_bounds.position.z, local_bounds.end.z))
		if not _point_inside_shape(local, shape_node.shape):
			continue
		var world := shape_node.global_transform * local
		var hit := {"position": world, "normal": Vector3.UP, "collider": area}
		if _place_single_from_hit(hit):
			placed += 1
	_finish_bulk_operation("Scatter %d Assets in Area3D" % placed)
	_surface_feedback = "Scattered %d assets inside %s" % [placed, area.name]
	_update_status()

func clear_selected_area() -> void:
	var area := _selected_area3d()
	if area == null or placement_parent == null:
		_surface_feedback = "Select an Area3D and a placement parent"
		_update_status()
		return
	var shape_node := _first_collision_shape(area)
	if shape_node == null or shape_node.shape == null:
		return
	if placement_mode == 1:
		_multimesh_before_snapshot = _capture_multimesh_snapshot()
		_multimesh_stroke_active = true
		for child in placement_parent.get_children():
			if child is MultiMeshInstance3D and child.has_meta("asset_painter_multimesh"):
				var mm := child as MultiMeshInstance3D
				var kept: Array[Transform3D] = []
				if mm.multimesh != null:
					for index in mm.multimesh.instance_count:
						var transform := mm.multimesh.get_instance_transform(index)
						var world := (mm.global_transform * transform).origin
						if not _world_point_inside_shape(world, shape_node):
							kept.append(transform)
				_set_multimesh_transforms(mm, kept)
		_commit_multimesh_stroke("Clear Painted Assets in Area3D")
	else:
		var before := _capture_scene_snapshot()
		for child in placement_parent.get_children():
			if child is Node3D and child.has_meta("asset_painter_placed") and not child is MultiMeshInstance3D:
				if _world_point_inside_shape((child as Node3D).global_position, shape_node):
					placement_parent.remove_child(child)
					child.free()
		var after := _capture_scene_snapshot()
		_apply_scene_snapshot(placement_parent, before)
		var ur := plugin.get_undo_redo()
		ur.create_action("Clear Painted Assets in Area3D")
		ur.add_do_method(self, "_apply_scene_snapshot", placement_parent, after)
		ur.add_undo_method(self, "_apply_scene_snapshot", placement_parent, before)
		ur.commit_action()
	_surface_feedback = "Cleared painted assets inside %s" % area.name
	_update_status()

func _place_single_from_hit(hit: Dictionary) -> bool:
	if hit.is_empty() or not _passes_placement_filters(hit):
		return false
	var entries := _current_weighted_entries()
	if entries.is_empty():
		return false
	var entry := _choose_weighted_entry(entries)
	var path := str(entry.get("path", ""))
	if path.is_empty():
		return false
	hit = _apply_grid_snap_for_asset(hit, path)
	var normal: Vector3 = (hit["normal"] as Vector3).normalized()
	var position: Vector3 = hit["position"] as Vector3
	if bool(hit.get("grid_surface", false)):
		var grid_cell: Vector3i = hit.get("grid_cell", Vector3i.ZERO) as Vector3i
		var grid_step: Vector3 = hit.get("grid_step", _grid_cell_size()) as Vector3
		var grid_layer := _grid_layer_key(grid_step)
		if _grid_cell_is_occupied(grid_cell, grid_layer):
			if not grid_override_occupied:
				return false
			_remove_grid_cell_assets(grid_cell, grid_layer)
		_begin_grid_placement(grid_cell, grid_step)
	else:
		position += normal * surface_offset
	var required_spacing := maxf(minimum_spacing, float(entry.get("minimum_spacing", 0.0)))
	if not bool(hit.get("grid_surface", false)) and not _is_far_enough(position, required_spacing):
		return false
	_stroke_sample_positions.append(position)
	if placement_mode == 1:
		_place_multimesh(path, position, normal)
	else:
		_place_scene(path, position, normal)
	if bool(hit.get("grid_surface", false)):
		_end_grid_placement()
	return true

func _mesh_triangles(mesh_node: MeshInstance3D) -> Array[Dictionary]:
	return PainterGeometry.mesh_triangles(mesh_node)

func _selected_area3d() -> Area3D:
	for node in EditorInterface.get_selection().get_selected_nodes():
		if node is Area3D:
			return node as Area3D
	return null

func _first_collision_shape(area: Area3D) -> CollisionShape3D:
	return PainterGeometry.first_collision_shape(area)

func _shape_local_aabb(shape: Shape3D) -> AABB:
	return PainterGeometry.shape_local_aabb(shape)

func _point_inside_shape(point: Vector3, shape: Shape3D) -> bool:
	return PainterGeometry.point_inside_shape(point, shape)

func _world_point_inside_shape(world_point: Vector3, shape_node: CollisionShape3D) -> bool:
	return PainterGeometry.world_point_inside_shape(world_point, shape_node)

func _paint_at(mouse_pos: Vector2, camera: Camera3D, temporary_erase: bool, precise: bool) -> void:
	if placement_parent == null or not is_instance_valid(placement_parent):
		_update_status()
		return
	var hit: Dictionary = _raycast_scene(mouse_pos, camera)
	if hit.is_empty() or not _passes_placement_filters(hit):
		return
	if temporary_erase or erase_mode:
		if bool(hit.get("grid_surface", false)):
			var snapped_hit := _apply_grid_snap_for_asset(hit, _grid_reference_asset_path())
			var erase_cell: Vector3i = snapped_hit.get("grid_cell", Vector3i.ZERO) as Vector3i
			var erase_step: Vector3 = snapped_hit.get("grid_step", _grid_cell_size()) as Vector3
			var erase_layer := _grid_layer_key(erase_step)
			if placement_mode == 1:
				_remove_grid_cell_assets(erase_cell, erase_layer)
			else:
				_erase_scene_grid_cell_with_undo(erase_cell, erase_layer)
		elif placement_mode == 1:
			_erase_multimesh_near(hit["position"] as Vector3)
		else:
			_erase_near(hit["position"] as Vector3)
		return
	if reapply_mode:
		if placement_mode == 1:
			_reapply_multimesh_near(hit["position"] as Vector3, hit["normal"] as Vector3)
		else:
			_reapply_scenes_near(hit["position"] as Vector3, hit["normal"] as Vector3)
		return
	var entries: Array[Dictionary] = []
	if panel != null and panel.has_method("get_weighted_scene_entries"):
		var raw_entries: Array = panel.call("get_weighted_scene_entries") as Array
		for raw_entry in raw_entries:
			if raw_entry is Dictionary:
				entries.append(raw_entry as Dictionary)
	elif panel != null and panel.has_method("get_selected_scene_paths"):
		var raw_paths: Array = panel.call("get_selected_scene_paths") as Array
		for selected_path in raw_paths:
			entries.append({"path": str(selected_path), "weight": 1.0, "category": "General", "minimum_spacing": 0.0})
	if entries.is_empty():
		_status_text = "Select and enable one or more weighted .tscn variants"
		if panel != null and panel.has_method("set_painter_status"):
			panel.set_painter_status(_status_text)
		return
	var amount: int = 1 if precise else count_per_click
	var event_samples: Array[Vector3] = []
	for i in amount:
		var entry: Dictionary = _choose_weighted_entry(entries)
		if entry.is_empty():
			continue
		var sample: Dictionary = hit if precise else _sample_surface_distributed(hit, camera, i, amount, event_samples)
		if sample.is_empty() or not _passes_placement_filters(sample):
			continue
		var path: String = str(entry.get("path", ""))
		if path.is_empty():
			continue
		sample = _apply_grid_snap_for_asset(sample, path)
		var sample_normal: Vector3 = sample["normal"] as Vector3
		var placement_position: Vector3 = sample["position"] as Vector3
		if bool(sample.get("grid_surface", false)):
			var grid_cell: Vector3i = sample.get("grid_cell", Vector3i.ZERO) as Vector3i
			var grid_step: Vector3 = sample.get("grid_step", _grid_cell_size()) as Vector3
			var grid_layer := _grid_layer_key(grid_step)
			if _grid_cell_is_occupied(grid_cell, grid_layer):
				if not grid_override_occupied:
					continue
				_remove_grid_cell_assets(grid_cell, grid_layer)
			_begin_grid_placement(grid_cell, grid_step)
		else:
			placement_position += sample_normal * surface_offset
		var required_spacing: float = maxf(minimum_spacing, float(entry.get("minimum_spacing", 0.0)))
		if not bool(sample.get("grid_surface", false)) and not _is_far_enough(placement_position, required_spacing):
			continue
		event_samples.append(placement_position)
		_stroke_sample_positions.append(placement_position)
		if placement_mode == 1:
			_place_multimesh(path, placement_position, sample_normal)
		else:
			_place_scene(path, placement_position, sample_normal)
		if bool(sample.get("grid_surface", false)):
			_end_grid_placement()

func _choose_weighted_entry(entries: Array[Dictionary]) -> Dictionary:
	return PainterSampling.choose_weighted_entry(entries, _rng)

func _choose_weighted_path(entries: Array[Dictionary]) -> String:
	return PainterSampling.choose_weighted_path(entries, _rng)

func _sample_surface_distributed(center_hit: Dictionary, camera: Camera3D, sample_index: int, sample_total: int, accepted: Array[Vector3]) -> Dictionary:
	var center_normal: Vector3 = center_hit["normal"] as Vector3
	var right: Vector3 = _surface_right(center_normal)
	var forward: Vector3 = center_normal.cross(right).normalized()
	var center_position: Vector3 = center_hit["position"] as Vector3
	var best_hit: Dictionary = {}
	var best_score := -INF
	var attempts: int = 24 if distribution_mode == 1 else 12
	for attempt in attempts:
		var offset2: Vector2 = _distribution_offset(sample_index, sample_total, attempt)
		var normalized_radius: float = clampf(offset2.length() / maxf(brush_radius, 0.001), 0.0, 1.0)
		if brush_falloff > 0.0 and _rng.randf() < brush_falloff * normalized_radius * normalized_radius:
			continue
		var world_point: Vector3 = center_position + right * offset2.x + forward * offset2.y
		if camera.is_position_behind(world_point):
			continue
		var screen: Vector2 = camera.unproject_position(world_point)
		var sampled_hit: Dictionary = _raycast_scene(screen, camera)
		if sampled_hit.is_empty():
			continue
		if distribution_mode != 1:
			return sampled_hit
		var candidate: Vector3 = sampled_hit["position"] as Vector3
		var nearest := INF
		for existing in accepted:
			nearest = minf(nearest, candidate.distance_squared_to(existing))
		for existing in _stroke_sample_positions:
			nearest = minf(nearest, candidate.distance_squared_to(existing))
		if accepted.is_empty() and _stroke_sample_positions.is_empty():
			nearest = candidate.distance_squared_to(center_position)
		if nearest > best_score:
			best_score = nearest
			best_hit = sampled_hit
	return best_hit

func _distribution_offset(sample_index: int, sample_total: int, attempt: int) -> Vector2:
	return PainterSampling.distribution_offset(
		distribution_mode,
		sample_index,
		sample_total,
		attempt,
		brush_radius,
		brush_falloff,
		cluster_count,
		cluster_strength,
		_rng
	)

func _sample_surface(center_hit: Dictionary, camera: Camera3D) -> Dictionary:
	var accepted: Array[Vector3] = []
	return _sample_surface_distributed(center_hit, camera, 0, 1, accepted)

func _place_scene(path: String, position: Vector3, normal: Vector3) -> void:
	var packed: PackedScene = _scene_cache.get(path) as PackedScene
	if packed == null:
		packed = load(path) as PackedScene
		if packed != null:
			_scene_cache[path] = packed
	if packed == null:
		return
	var instance: Node = packed.instantiate()
	if not instance is Node3D:
		instance.queue_free()
		return
	var asset_name: String = path.get_file().get_basename().validate_node_name()
	if asset_name.is_empty():
		asset_name = "PaintedAsset"
	instance.name = asset_name
	placement_parent.add_child(instance, true)
	instance.owner = EditorInterface.get_edited_scene_root()
	var instance_3d: Node3D = instance as Node3D
	instance_3d.global_position = position
	var up: Vector3 = _alignment_up(normal)
	instance_3d.global_basis = _basis_from_normal(up)
	if random_rotation_enabled and random_rotation_x_enabled and random_rotation_x > 0.0:
		instance_3d.rotate_object_local(Vector3.RIGHT, deg_to_rad(_random_rotation_angle(random_rotation_x)))
	if random_rotation_enabled and random_rotation_y_enabled and random_rotation_y > 0.0:
		instance_3d.rotate_object_local(Vector3.UP, deg_to_rad(_random_rotation_angle(random_rotation_y)))
	if random_rotation_enabled and random_rotation_z_enabled and random_rotation_z > 0.0:
		instance_3d.rotate_object_local(Vector3.BACK, deg_to_rad(_random_rotation_angle(random_rotation_z)))
	var random_scale: float = 1.0
	if random_scale_enabled:
		random_scale = _rng.randf_range(minf(random_scale_min, random_scale_max), maxf(random_scale_min, random_scale_max))
	instance_3d.scale *= Vector3.ONE * random_scale
	if _placing_on_grid:
		instance_3d.global_position = _grid_corrected_root_position(path, _grid_cell_center(_active_grid_cell, _active_grid_step), instance_3d.global_basis)
		instance.set_meta("asset_painter_grid_cell", _active_grid_cell)
		instance.set_meta("asset_painter_grid_layer", _active_grid_layer)
	instance.set_meta("asset_painter_placed", true)
	instance.set_meta("asset_source_path", path)
	instance.set_meta("asset_painter_base_position", instance_3d.global_position)
	instance.set_meta("asset_painter_surface_normal", normal.normalized())
	_stroke_nodes.append(instance)
	_painted_nodes.append(instance_3d)
	_add_spacing_hash_position(instance_3d.global_position)

func _place_multimesh(path: String, position: Vector3, normal: Vector3) -> void:
	var asset_data: Dictionary = _get_multimesh_asset_data(path)
	if asset_data.is_empty():
		_status_text = "MultiMesh incompatible: %s has no usable MeshInstance3D" % path.get_file()
		if panel != null and panel.has_method("set_painter_status"):
			panel.set_painter_status(_status_text)
		return
	var mm_node: MultiMeshInstance3D = _get_or_create_multimesh_node(path, asset_data, position)
	if mm_node == null or mm_node.multimesh == null:
		return
	var root_global := Transform3D(_basis_from_normal(_alignment_up(normal)), position)
	if random_rotation_enabled and random_rotation_x_enabled and random_rotation_x > 0.0:
		root_global.basis = root_global.basis.rotated(root_global.basis.x.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_x)))
	if random_rotation_enabled and random_rotation_y_enabled and random_rotation_y > 0.0:
		root_global.basis = root_global.basis.rotated(root_global.basis.y.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_y)))
	if random_rotation_enabled and random_rotation_z_enabled and random_rotation_z > 0.0:
		root_global.basis = root_global.basis.rotated(root_global.basis.z.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_z)))
	var random_scale: float = 1.0
	if random_scale_enabled:
		random_scale = _rng.randf_range(minf(random_scale_min, random_scale_max), maxf(random_scale_min, random_scale_max))
	root_global.basis = root_global.basis.scaled(Vector3.ONE * random_scale)
	if _placing_on_grid:
		root_global.origin = _grid_corrected_root_position(path, _grid_cell_center(_active_grid_cell, _active_grid_step), root_global.basis)
	var mesh_relative: Transform3D = asset_data["mesh_relative"] as Transform3D
	var mesh_global: Transform3D = root_global * mesh_relative
	var local_transform: Transform3D = mm_node.global_transform.affine_inverse() * mesh_global
	var transforms: Array[Transform3D] = []
	for index in mm_node.multimesh.instance_count:
		transforms.append(mm_node.multimesh.get_instance_transform(index))
	transforms.append(local_transform)
	_set_multimesh_transforms(mm_node, transforms)
	if _placing_on_grid:
		var grid_cells: Array = mm_node.get_meta("asset_painter_grid_cells", []) as Array
		grid_cells.append(_active_grid_cell)
		mm_node.set_meta("asset_painter_grid_cells", grid_cells)
	_add_spacing_hash_position(root_global.origin)

func _get_multimesh_asset_data(path: String) -> Dictionary:
	return PainterMultiMesh.get_asset_data(path, _multimesh_asset_cache, _scene_cache)

func _get_or_create_multimesh_node(path: String, asset_data: Dictionary, world_position: Vector3 = Vector3.ZERO) -> MultiMeshInstance3D:
	return PainterMultiMesh.get_or_create_node(
		placement_parent,
		path,
		asset_data,
		world_position,
		multimesh_chunking_enabled,
		multimesh_chunk_world_size,
		multimesh_chunk_instance_limit,
		multimesh_visibility_begin,
		multimesh_visibility_end,
		_placing_on_grid,
		_active_grid_layer
	)

func _erase_multimesh_near(position: Vector3) -> void:
	if PainterMultiMesh.erase_near(placement_parent, position, brush_radius):
		_spacing_hash_dirty = true

func _set_multimesh_transforms(node: MultiMeshInstance3D, transforms: Array[Transform3D]) -> void:
	_spacing_hash_dirty = true
	PainterMultiMesh.set_transforms(node, transforms)

func _capture_multimesh_snapshot() -> Array:
	if placement_parent == null or not is_instance_valid(placement_parent):
		return []
	return PainterMultiMesh.capture_snapshot(placement_parent)

func _apply_multimesh_snapshot(parent, snapshot: Array) -> void:
	if parent == null or not is_instance_valid(parent):
		return
	PainterMultiMesh.apply_snapshot(parent, snapshot)
	_spacing_hash_dirty = true

func _commit_multimesh_stroke(action_name: String = "Paint MultiMesh Assets") -> void:
	if not _multimesh_stroke_active or placement_parent == null or not is_instance_valid(placement_parent):
		_multimesh_stroke_active = false
		_multimesh_before_snapshot.clear()
		return
	_multimesh_stroke_active = false
	var before: Array = _multimesh_before_snapshot.duplicate(true)
	var after: Array = _capture_multimesh_snapshot()
	if before == after:
		return
	_apply_multimesh_snapshot(placement_parent, before)
	var ur: EditorUndoRedoManager = plugin.get_undo_redo()
	ur.create_action(action_name)
	ur.add_do_method(self, "_apply_multimesh_snapshot", placement_parent, after)
	ur.add_undo_method(self, "_apply_multimesh_snapshot", placement_parent, before)
	ur.commit_action()

func _current_weighted_entries() -> Array[Dictionary]:
	if not _path_entries_override.is_empty():
		return _path_entries_override.duplicate(true)
	var entries: Array[Dictionary] = []
	if panel != null and panel.has_method("get_weighted_scene_entries"):
		for raw_entry in panel.call("get_weighted_scene_entries") as Array:
			if raw_entry is Dictionary:
				entries.append(raw_entry as Dictionary)
	return entries

func _randomized_basis(normal: Vector3) -> Basis:
	var basis := _basis_from_normal(_alignment_up(normal))
	if reapply_rotation:
		if random_rotation_enabled and random_rotation_x_enabled and random_rotation_x > 0.0:
			basis = basis.rotated(basis.x.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_x)))
		if random_rotation_enabled and random_rotation_y_enabled and random_rotation_y > 0.0:
			basis = basis.rotated(basis.y.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_y)))
		if random_rotation_enabled and random_rotation_z_enabled and random_rotation_z > 0.0:
			basis = basis.rotated(basis.z.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_z)))
	return basis

func _reapply_scenes_near(center: Vector3, brush_normal: Vector3) -> void:
	if placement_parent == null:
		return
	var entries := _current_weighted_entries()
	var targets: Array[Node3D] = []
	for child in placement_parent.get_children():
		if child is Node3D and child.has_meta("asset_painter_placed") and not child is MultiMeshInstance3D:
			var node := child as Node3D
			if node.global_position.distance_to(center) <= brush_radius:
				targets.append(node)
	for node in targets:
		var normal: Vector3 = node.get_meta("asset_painter_surface_normal", brush_normal) as Vector3
		if reapply_alignment:
			normal = brush_normal.normalized()
		var base_position: Vector3 = node.get_meta("asset_painter_base_position", node.global_position) as Vector3
		if reapply_offset:
			node.global_position = base_position + normal * surface_offset
		if reapply_rotation or reapply_alignment:
			var old_scale := node.global_basis.get_scale()
			node.global_basis = _randomized_basis(normal)
			node.global_basis = node.global_basis.scaled(old_scale)
		if reapply_scale:
			var new_scale := 1.0
			if random_scale_enabled:
				new_scale = _rng.randf_range(minf(random_scale_min, random_scale_max), maxf(random_scale_min, random_scale_max))
			var normalized := node.global_basis.orthonormalized()
			node.global_basis = normalized.scaled(Vector3.ONE * new_scale)
		if reapply_replace_variant and not entries.is_empty():
			var replacement := _choose_weighted_entry(entries)
			var new_path := str(replacement.get("path", ""))
			var old_path := str(node.get_meta("asset_source_path", ""))
			if not new_path.is_empty() and new_path != old_path:
				_replace_scene_node(node, new_path)

func _replace_scene_node(old_node: Node3D, path: String) -> void:
	var packed: PackedScene = _scene_cache.get(path) as PackedScene
	if packed == null:
		packed = load(path) as PackedScene
		if packed != null:
			_scene_cache[path] = packed
	if packed == null:
		return
	var replacement := packed.instantiate()
	if not replacement is Node3D:
		replacement.free()
		return
	var replacement_3d := replacement as Node3D
	replacement.name = path.get_file().get_basename().validate_node_name()
	placement_parent.add_child(replacement, true)
	replacement.owner = EditorInterface.get_edited_scene_root()
	replacement_3d.global_transform = old_node.global_transform
	replacement.set_meta("asset_painter_placed", true)
	replacement.set_meta("asset_source_path", path)
	replacement.set_meta("asset_painter_base_position", old_node.get_meta("asset_painter_base_position", old_node.global_position))
	replacement.set_meta("asset_painter_surface_normal", old_node.get_meta("asset_painter_surface_normal", Vector3.UP))
	placement_parent.remove_child(old_node)
	old_node.free()

func _reapply_multimesh_near(center: Vector3, brush_normal: Vector3) -> void:
	if placement_parent == null:
		return
	var entries := _current_weighted_entries()
	var replacements: Array[Dictionary] = []
	for child in placement_parent.get_children():
		if not child is MultiMeshInstance3D or not child.has_meta("asset_painter_multimesh"):
			continue
		var mm := child as MultiMeshInstance3D
		if mm.multimesh == null:
			continue
		var kept: Array[Transform3D] = []
		for index in mm.multimesh.instance_count:
			var local := mm.multimesh.get_instance_transform(index)
			var global_transform_value: Transform3D = mm.global_transform * local
			if global_transform_value.origin.distance_to(center) > brush_radius:
				kept.append(local)
				continue
			var old_scale: Vector3 = global_transform_value.basis.get_scale()
			if reapply_rotation or reapply_alignment:
				global_transform_value.basis = _randomized_basis(brush_normal)
				global_transform_value.basis = global_transform_value.basis.scaled(old_scale)
			if reapply_scale:
				var scale_value := 1.0
				if random_scale_enabled:
					scale_value = _rng.randf_range(minf(random_scale_min, random_scale_max), maxf(random_scale_min, random_scale_max))
				global_transform_value.basis = global_transform_value.basis.orthonormalized().scaled(Vector3.ONE * scale_value)
			if reapply_offset:
				global_transform_value.origin += brush_normal.normalized() * surface_offset
			if reapply_replace_variant and not entries.is_empty():
				var replacement := _choose_weighted_entry(entries)
				replacements.append({"path": str(replacement.get("path", "")), "transform": global_transform_value})
			else:
				kept.append(mm.global_transform.affine_inverse() * global_transform_value)
		_set_multimesh_transforms(mm, kept)
	for item in replacements:
		var path := str(item.get("path", ""))
		if path.is_empty():
			continue
		var asset_data := _get_multimesh_asset_data(path)
		if asset_data.is_empty():
			continue
		var root_global: Transform3D = item.get("transform") as Transform3D
		var target := _get_or_create_multimesh_node(path, asset_data, root_global.origin)
		if target == null or target.multimesh == null:
			continue
		var transforms: Array[Transform3D] = []
		for i in target.multimesh.instance_count:
			transforms.append(target.multimesh.get_instance_transform(i))
		var mesh_global := root_global * (asset_data["mesh_relative"] as Transform3D)
		transforms.append(target.global_transform.affine_inverse() * mesh_global)
		_set_multimesh_transforms(target, transforms)

func _capture_scene_snapshot() -> Array:
	var snapshot: Array = []
	if placement_parent == null:
		return snapshot
	for child in placement_parent.get_children():
		if child is Node3D and child.has_meta("asset_painter_placed") and not child is MultiMeshInstance3D:
			snapshot.append({
				"path": str(child.get_meta("asset_source_path", "")),
				"transform": (child as Node3D).transform,
				"base_position": child.get_meta("asset_painter_base_position", (child as Node3D).global_position),
				"normal": child.get_meta("asset_painter_surface_normal", Vector3.UP),
				"grid_layer": str(child.get_meta("asset_painter_grid_layer", "")),
				"grid_cell": child.get_meta("asset_painter_grid_cell", Vector3i.ZERO),
				"has_grid_cell": child.has_meta("asset_painter_grid_cell")
			})
	return snapshot

func _apply_scene_snapshot(parent: Node3D, snapshot: Array) -> void:
	if parent == null or not is_instance_valid(parent):
		return
	for child in parent.get_children():
		if child is Node3D and child.has_meta("asset_painter_placed") and not child is MultiMeshInstance3D:
			parent.remove_child(child)
			child.free()
	for item_value in snapshot:
		var item := item_value as Dictionary
		var path := str(item.get("path", ""))
		var packed := load(path) as PackedScene
		if packed == null:
			continue
		var node := packed.instantiate()
		if not node is Node3D:
			node.free()
			continue
		node.name = path.get_file().get_basename().validate_node_name()
		parent.add_child(node, true)
		node.owner = EditorInterface.get_edited_scene_root()
		(node as Node3D).transform = item.get("transform") as Transform3D
		node.set_meta("asset_painter_placed", true)
		node.set_meta("asset_source_path", path)
		node.set_meta("asset_painter_base_position", item.get("base_position", (node as Node3D).global_position))
		node.set_meta("asset_painter_surface_normal", item.get("normal", Vector3.UP))
		if bool(item.get("has_grid_cell", false)):
			node.set_meta("asset_painter_grid_layer", str(item.get("grid_layer", "")))
			node.set_meta("asset_painter_grid_cell", item.get("grid_cell", Vector3i.ZERO) as Vector3i)
	_refresh_painted_nodes()

func _commit_scene_reapply() -> void:
	if not _scene_reapply_active or placement_parent == null:
		return
	_scene_reapply_active = false
	var before := _scene_before_snapshot.duplicate(true)
	var after := _capture_scene_snapshot()
	if before == after:
		return
	_apply_scene_snapshot(placement_parent, before)
	var ur := plugin.get_undo_redo()
	ur.create_action("Reapply Painted Assets")
	ur.add_do_method(self, "_apply_scene_snapshot", placement_parent, after)
	ur.add_undo_method(self, "_apply_scene_snapshot", placement_parent, before)
	ur.commit_action()

func _commit_scene_grid_override() -> void:
	if not _scene_grid_override_active or placement_parent == null:
		return
	_scene_grid_override_active = false
	_stroke_nodes.clear()
	var before := _scene_before_snapshot.duplicate(true)
	var after := _capture_scene_snapshot()
	if before == after:
		return
	_apply_scene_snapshot(placement_parent, before)
	var ur := plugin.get_undo_redo()
	ur.create_action("Paint and Replace Grid Assets")
	ur.add_do_method(self, "_apply_scene_snapshot", placement_parent, after)
	ur.add_undo_method(self, "_apply_scene_snapshot", placement_parent, before)
	ur.commit_action()

func _erase_scene_grid_cell_with_undo(cell: Vector3i, layer: String) -> void:
	if placement_parent == null:
		return
	var before := _capture_scene_snapshot()
	_remove_grid_cell_assets(cell, layer)
	var after := _capture_scene_snapshot()
	if before == after:
		return
	_apply_scene_snapshot(placement_parent, before)
	var ur := plugin.get_undo_redo()
	ur.create_action("Erase Grid Cell")
	ur.add_do_method(self, "_apply_scene_snapshot", placement_parent, after)
	ur.add_undo_method(self, "_apply_scene_snapshot", placement_parent, before)
	ur.commit_action()

func _erase_near(position: Vector3) -> void:
	if placement_parent == null:
		return
	var removed: Array[Node] = []
	for child in placement_parent.get_children():
		if child is Node3D and child.has_meta("asset_painter_placed") and (child as Node3D).global_position.distance_to(position) <= brush_radius:
			removed.append(child)
	if removed.is_empty():
		return
	var ur: EditorUndoRedoManager = plugin.get_undo_redo()
	ur.create_action("Erase Painted Assets")
	for node in removed:
		ur.add_do_method(placement_parent, "remove_child", node)
		ur.add_undo_method(placement_parent, "add_child", node)
		ur.add_undo_method(node, "set_owner", EditorInterface.get_edited_scene_root())
		ur.add_undo_reference(node)
	ur.commit_action()

func _commit_stroke() -> void:
	if _stroke_nodes.is_empty() or placement_parent == null:
		return
	# The nodes are already in their final scene-tree state. Detaching them here and
	# re-adding them only to execute the UndoRedo "do" step leaves instanced
	# Node3D children outside the tree temporarily. Some scenes query their global
	# transforms during that transition, and saved owners are invalid while their
	# ancestor is detached. Register the existing state and commit without executing
	# the do methods instead.
	var nodes: Array[Node] = _stroke_nodes.duplicate()
	var edited_root: Node = EditorInterface.get_edited_scene_root()
	var ur: EditorUndoRedoManager = plugin.get_undo_redo()
	ur.create_action("Paint %d Assets" % nodes.size())
	for node in nodes:
		if not is_instance_valid(node) or node.get_parent() != placement_parent:
			continue
		ur.add_do_method(placement_parent, "add_child", node, true)
		if edited_root != null and (edited_root == placement_parent or edited_root.is_ancestor_of(placement_parent)):
			ur.add_do_method(node, "set_owner", edited_root)
		ur.add_undo_method(placement_parent, "remove_child", node)
		ur.add_do_reference(node)
	ur.commit_action(false)
	_stroke_nodes.clear()

func _spacing_hash_key(position: Vector3) -> Vector3i:
	return PainterSpatialHash.key_for_position(position, spatial_hash_cell_size)

func _add_spacing_hash_position(position: Vector3) -> void:
	if not use_spatial_hash:
		return
	if _spacing_hash_dirty:
		_rebuild_spacing_hash()
	PainterSpatialHash.add_position(_spacing_hash, position, spatial_hash_cell_size)

func _rebuild_spacing_hash() -> void:
	PainterSpatialHash.rebuild(_spacing_hash, placement_parent, spatial_hash_cell_size)
	_spacing_hash_dirty = false

func _is_far_enough(position: Vector3, required_spacing: float = -1.0) -> bool:
	var spacing: float = minimum_spacing if required_spacing < 0.0 else required_spacing
	if spacing <= 0.0 or placement_parent == null:
		return true
	if use_spatial_hash:
		if _spacing_hash_dirty:
			_rebuild_spacing_hash()
		return PainterSpatialHash.is_far_enough_hashed(
			_spacing_hash,
			position,
			spacing,
			spatial_hash_cell_size
		)
	return PainterSpatialHash.is_far_enough_linear(
		position,
		spacing,
		placement_parent,
		_painted_nodes
	)

func _refresh_painted_nodes() -> void:
	_painted_nodes.clear()
	_spacing_hash_dirty = true
	if placement_parent == null or not is_instance_valid(placement_parent):
		return
	for child in placement_parent.get_children():
		if child is Node3D and child.has_meta("asset_painter_placed"):
			_painted_nodes.append(child as Node3D)

func _sync_panel_option(option_name: String, value: Variant) -> void:
	if panel != null and panel.has_method("sync_painter_option"):
		panel.sync_painter_option(option_name, value)

func _begin_surface_path(screen_position: Vector2, camera: Camera3D) -> void:
	_path_drawing = true
	_path_screen_points.clear()
	_path_world_points.clear()
	_path_world_normals.clear()
	_prepare_rng_for_operation()
	_append_surface_path_point(screen_position, camera, true)
	_surface_feedback = "Drawing surface path..."
	_update_status()

func _append_surface_path_point(screen_position: Vector2, camera: Camera3D, force: bool = false) -> void:
	var hit: Dictionary = _raycast_scene(screen_position, camera)
	if hit.is_empty() or not _passes_placement_filters(hit):
		return
	var point: Vector3 = hit["position"] as Vector3
	var normal: Vector3 = (hit["normal"] as Vector3).normalized()
	if not force and not _path_world_points.is_empty():
		if _path_world_points[_path_world_points.size() - 1].distance_to(point) < path_point_spacing:
			return
	_path_screen_points.append(screen_position)
	_path_world_points.append(point)
	_path_world_normals.append(normal)

func _finish_surface_path() -> void:
	if not _path_drawing:
		return
	_path_drawing = false
	if _path_world_points.size() < 2:
		_surface_feedback = "Draw a longer path across a valid surface"
		_update_status()
		return
	if placement_parent == null or not is_instance_valid(placement_parent):
		_surface_feedback = "Select a parent before drawing a surface path"
		_update_status()
		return
	var path_node: Path3D = _create_surface_path_node()
	if path_node == null:
		_surface_feedback = "Could not create the surface path"
		_update_status()
		return
	var placed := 0
	if path_auto_scatter:
		placed = _regenerate_surface_path(path_node)
	_surface_feedback = "Created %s" % path_node.name
	if path_auto_scatter:
		_surface_feedback += " and placed %d assets" % placed
	if not path_create_node:
		placement_parent.remove_child(path_node)
		path_node.queue_free()
		_surface_feedback = "Placed %d assets from temporary surface path" % placed
	_update_status()
	_path_screen_points.clear()
	_path_world_points.clear()
	_path_world_normals.clear()
	queue_redraw()

func _create_surface_path_node() -> Path3D:
	if placement_parent == null or not is_instance_valid(placement_parent):
		return null
	var path_node := Path3D.new()
	path_node.name = "SurfacePath"
	path_node.set_meta("asset_painter_surface_path", true)
	path_node.set_meta("asset_painter_path_profile", path_profile)
	path_node.set_meta("asset_painter_path_width", path_width)
	path_node.set_meta("asset_painter_path_rows", path_row_count)
	path_node.set_meta("asset_painter_path_noise", path_noise)
	_store_surface_path_settings(path_node)
	var curve := Curve3D.new()
	for index in _path_world_points.size():
		var local_point: Vector3 = placement_parent.global_transform.affine_inverse() * _path_world_points[index]
		curve.add_point(local_point)
	path_node.curve = curve
	placement_parent.add_child(path_node, true)
	path_node.owner = EditorInterface.get_edited_scene_root()
	_apply_curve_smoothing(curve)
	_connect_surface_path_controller(path_node)
	if not path_create_node:
		path_node.set_meta("asset_painter_temporary_path", true)
	return path_node

func _apply_curve_smoothing(curve: Curve3D) -> void:
	if curve == null or curve.point_count < 3 or path_smoothing <= 0.0:
		return
	for index in range(1, curve.point_count - 1):
		var previous: Vector3 = curve.get_point_position(index - 1)
		var current: Vector3 = curve.get_point_position(index)
		var following: Vector3 = curve.get_point_position(index + 1)
		var tangent: Vector3 = (following - previous) * (path_smoothing / 6.0)
		var max_handle: float = minf(current.distance_to(previous), current.distance_to(following)) * 0.45
		if tangent.length() > max_handle and max_handle > 0.0:
			tangent = tangent.normalized() * max_handle
		curve.set_point_in(index, -tangent)
		curve.set_point_out(index, tangent)

func _connect_existing_surface_paths() -> void:
	var root: Node = EditorInterface.get_edited_scene_root()
	if root == null:
		return
	_connect_surface_paths_recursive(root)

func _connect_surface_paths_recursive(node: Node) -> void:
	if node is Path3D and node.has_meta("asset_painter_surface_path"):
		_connect_surface_path_controller(node as Path3D)
	for child in node.get_children():
		_connect_surface_paths_recursive(child)

func _store_surface_path_settings(path_node: Path3D) -> void:
	if path_node == null:
		return
	path_node.set_meta("asset_painter_surface_path", true)
	path_node.set_meta("asset_painter_entries", _current_weighted_entries().duplicate(true))
	path_node.set_meta("asset_painter_placement_mode", placement_mode)
	path_node.set_meta("asset_painter_path_profile", path_profile)
	path_node.set_meta("asset_painter_path_width", path_width)
	path_node.set_meta("asset_painter_path_rows", path_row_count)
	path_node.set_meta("asset_painter_path_noise", path_noise)
	path_node.set_meta("asset_painter_path_spacing_mode", path_spacing_mode)
	path_node.set_meta("asset_painter_path_spacing_min", path_spacing_min)
	path_node.set_meta("asset_painter_path_spacing_max", path_spacing_max)
	path_node.set_meta("asset_painter_path_asset_order", path_asset_order)
	path_node.set_meta("asset_painter_path_align_direction", path_align_direction)
	path_node.set_meta("asset_painter_random_seed", random_seed)
	path_node.set_meta("asset_painter_surface_offset", surface_offset)
	path_node.set_meta("asset_painter_live_update", path_live_update)
	path_node.set_meta("asset_painter_update_delay", path_update_delay)
	path_node.set_meta("asset_painter_path_max_placements", path_max_placements)

func _surface_path_entries(path_node: Path3D) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	var raw: Variant = path_node.get_meta("asset_painter_entries", [])
	if raw is Array:
		for item in raw:
			if item is Dictionary:
				result.append((item as Dictionary).duplicate(true))
	return result

func _get_surface_path_generated_parent(path_node: Path3D) -> Node3D:
	var existing: Node = path_node.get_node_or_null("GeneratedAssets")
	if existing is Node3D:
		return existing as Node3D
	var generated := Node3D.new()
	generated.name = "GeneratedAssets"
	generated.set_meta("asset_painter_path_generated", true)
	path_node.add_child(generated, true)
	generated.owner = EditorInterface.get_edited_scene_root()
	return generated

func _clear_surface_path_generated(path_node: Path3D) -> void:
	var generated: Node = path_node.get_node_or_null("GeneratedAssets")
	if generated == null:
		return
	for child in generated.get_children():
		generated.remove_child(child)
		child.queue_free()

func _connect_surface_path_controller(path_node: Path3D) -> void:
	if path_node == null or path_node.curve == null:
		return
	var callback := Callable(self, "_on_surface_path_curve_changed").bind(path_node)
	if not path_node.curve.changed.is_connected(callback):
		path_node.curve.changed.connect(callback)

func _on_surface_path_curve_changed(path_node: Path3D) -> void:
	if _path_regenerating or path_node == null or not is_instance_valid(path_node):
		return
	if not bool(path_node.get_meta("asset_painter_live_update", true)):
		return
	var token: int = int(_path_regen_tokens.get(path_node.get_instance_id(), 0)) + 1
	_path_regen_tokens[path_node.get_instance_id()] = token
	var delay: float = float(path_node.get_meta("asset_painter_update_delay", 0.25))
	_debounced_surface_path_regenerate(path_node, token, delay)

func _debounced_surface_path_regenerate(path_node: Path3D, token: int, delay: float) -> void:
	await get_tree().create_timer(delay).timeout
	if path_node == null or not is_instance_valid(path_node):
		return
	if int(_path_regen_tokens.get(path_node.get_instance_id(), -1)) != token:
		return
	_regenerate_surface_path(path_node)

func _apply_surface_path_settings(path_node: Path3D) -> Dictionary:
	var previous := {
		"placement_mode": placement_mode,
		"path_profile": path_profile,
		"path_width": path_width,
		"path_row_count": path_row_count,
		"path_noise": path_noise,
		"path_spacing_mode": path_spacing_mode,
		"path_spacing_min": path_spacing_min,
		"path_spacing_max": path_spacing_max,
		"path_asset_order": path_asset_order,
		"path_align_direction": path_align_direction,
		"path_max_placements": path_max_placements,
		"random_seed": random_seed,
		"surface_offset": surface_offset,
	}
	placement_mode = int(path_node.get_meta("asset_painter_placement_mode", placement_mode))
	path_profile = int(path_node.get_meta("asset_painter_path_profile", path_profile))
	path_width = float(path_node.get_meta("asset_painter_path_width", path_width))
	path_row_count = int(path_node.get_meta("asset_painter_path_rows", path_row_count))
	path_noise = float(path_node.get_meta("asset_painter_path_noise", path_noise))
	path_spacing_mode = int(path_node.get_meta("asset_painter_path_spacing_mode", path_spacing_mode))
	path_spacing_min = float(path_node.get_meta("asset_painter_path_spacing_min", path_spacing_min))
	path_spacing_max = float(path_node.get_meta("asset_painter_path_spacing_max", path_spacing_max))
	path_asset_order = int(path_node.get_meta("asset_painter_path_asset_order", path_asset_order))
	path_align_direction = bool(path_node.get_meta("asset_painter_path_align_direction", path_align_direction))
	path_max_placements = clampi(int(path_node.get_meta("asset_painter_path_max_placements", path_max_placements)), 1, 10000)
	random_seed = int(path_node.get_meta("asset_painter_random_seed", random_seed))
	surface_offset = float(path_node.get_meta("asset_painter_surface_offset", surface_offset))
	return previous

func _restore_surface_path_settings(previous: Dictionary) -> void:
	placement_mode = int(previous.get("placement_mode", placement_mode))
	path_profile = int(previous.get("path_profile", path_profile))
	path_width = float(previous.get("path_width", path_width))
	path_row_count = int(previous.get("path_row_count", path_row_count))
	path_noise = float(previous.get("path_noise", path_noise))
	path_spacing_mode = int(previous.get("path_spacing_mode", path_spacing_mode))
	path_spacing_min = float(previous.get("path_spacing_min", path_spacing_min))
	path_spacing_max = float(previous.get("path_spacing_max", path_spacing_max))
	path_asset_order = int(previous.get("path_asset_order", path_asset_order))
	path_align_direction = bool(previous.get("path_align_direction", path_align_direction))
	path_max_placements = int(previous.get("path_max_placements", path_max_placements))
	random_seed = int(previous.get("random_seed", random_seed))
	surface_offset = float(previous.get("surface_offset", surface_offset))

func _regenerate_surface_path(path_node: Path3D) -> int:
	if path_node == null or not is_instance_valid(path_node) or path_node.curve == null:
		return 0
	_path_regenerating = true
	var previous: Dictionary = _apply_surface_path_settings(path_node)
	_clear_surface_path_generated(path_node)
	var placed: int = _scatter_path_node(path_node)
	_restore_surface_path_settings(previous)
	_path_regenerating = false
	return placed

func set_selected_path_live_update(enabled: bool) -> void:
	for node in EditorInterface.get_selection().get_selected_nodes():
		if node is Path3D and node.has_meta("asset_painter_surface_path"):
			node.set_meta("asset_painter_live_update", enabled)
			_connect_surface_path_controller(node as Path3D)

func clear_selected_path_generated() -> void:
	for node in EditorInterface.get_selection().get_selected_nodes():
		if node is Path3D and node.has_meta("asset_painter_surface_path"):
			_clear_surface_path_generated(node as Path3D)

func bake_selected_surface_path() -> void:
	for node in EditorInterface.get_selection().get_selected_nodes():
		if not (node is Path3D) or not node.has_meta("asset_painter_surface_path"):
			continue
		var path_node := node as Path3D
		var generated := path_node.get_node_or_null("GeneratedAssets") as Node3D
		if generated == null or path_node.get_parent() == null:
			continue
		var destination := path_node.get_parent()
		var children := generated.get_children()
		for child in children:
			var global_xform := Transform3D.IDENTITY
			if child is Node3D:
				global_xform = (child as Node3D).global_transform
			generated.remove_child(child)
			destination.add_child(child, true)
			child.owner = EditorInterface.get_edited_scene_root()
			if child is Node3D:
				(child as Node3D).global_transform = global_xform
		path_node.set_meta("asset_painter_live_update", false)
		_surface_feedback = "Baked generated assets from %s" % path_node.name
		_update_status()

func _draw_surface_path_preview() -> void:
	PainterPreview.draw_surface_path(self, active, area_tool_mode, _viewport_camera, _path_screen_points, _path_drawing, _mouse_position)

func scatter_selected_path() -> void:
	var selected_nodes: Array[Node] = EditorInterface.get_selection().get_selected_nodes()
	var path_node: Path3D = null
	for selected_node in selected_nodes:
		if selected_node is Path3D:
			path_node = selected_node as Path3D
			break
	if path_node == null or path_node.curve == null:
		_surface_feedback = "Select a SurfacePath or Path3D"
		_update_status()
		return
	_store_surface_path_settings(path_node)
	var placed: int = _regenerate_surface_path(path_node)
	_surface_feedback = "Regenerated %d assets along %s" % [placed, path_node.name]
	_update_status()

func _scatter_path_node(path_node: Path3D) -> int:
	if path_node == null or path_node.curve == null:
		return 0
	var original_parent: Node3D = placement_parent
	var generated_parent: Node3D = _get_surface_path_generated_parent(path_node)
	if generated_parent == null:
		return 0
	placement_parent = generated_parent
	var stored_entries: Array[Dictionary] = _surface_path_entries(path_node)
	_path_entries_override = stored_entries
	var entries: Array[Dictionary] = _current_weighted_entries()
	if entries.is_empty():
		_path_entries_override.clear()
		placement_parent = original_parent
		return 0
	var curve_length: float = path_node.curve.get_baked_length()
	if curve_length <= 0.001:
		_path_entries_override.clear()
		placement_parent = original_parent
		return 0
	_begin_bulk_operation()
	var placed := 0
	var asset_sequence_index := 0
	var distance := 0.0
	var safety := clampi(path_max_placements, 1, 10000)
	var iteration_limit := maxi(1000, safety * 20)
	var iterations := 0
	while distance <= curve_length and placed < safety and iterations < iteration_limit:
		iterations += 1
		var local_position: Vector3 = path_node.curve.sample_baked(distance, true)
		var world_position: Vector3 = path_node.global_transform * local_position
		var epsilon: float = minf(0.25, maxf(0.02, curve_length * 0.002))
		var previous_world: Vector3 = path_node.global_transform * path_node.curve.sample_baked(maxf(0.0, distance - epsilon), true)
		var next_world: Vector3 = path_node.global_transform * path_node.curve.sample_baked(minf(curve_length, distance + epsilon), true)
		var direction: Vector3 = (next_world - previous_world).normalized()
		if direction.length_squared() < 0.0001:
			direction = -path_node.global_basis.z.normalized()
		var center_hit: Dictionary = _raycast_world_segment(world_position + Vector3.UP * 2.0, world_position - Vector3.UP * 8.0)
		var normal := Vector3.UP
		if not center_hit.is_empty():
			world_position = center_hit["position"] as Vector3
			normal = (center_hit["normal"] as Vector3).normalized()
		var side: Vector3 = direction.cross(normal).normalized()
		if side.length_squared() < 0.0001:
			side = direction.cross(Vector3.UP).normalized()
		var offsets: Array[float] = _path_profile_offsets()
		for base_offset in offsets:
			if placed >= safety:
				break
			var side_offset: float = base_offset
			if path_profile == 2:
				side_offset = _rng.randf_range(-path_width * 0.5, path_width * 0.5)
			if path_noise > 0.0:
				side_offset += _rng.randf_range(-path_noise, path_noise)
			var candidate: Vector3 = world_position + side * side_offset
			var ground_hit: Dictionary = _raycast_world_segment(candidate + normal * 2.0 + Vector3.UP * 2.0, candidate - normal * 4.0 - Vector3.UP * 4.0)
			if not ground_hit.is_empty():
				if not _passes_placement_filters(ground_hit):
					continue
				candidate = ground_hit["position"] as Vector3
				normal = (ground_hit["normal"] as Vector3).normalized()
			else:
				# The curve was drawn directly on a valid surface. Some imported meshes
				# do not reliably answer a second world-space projection ray, so keep
				# the sampled curve position instead of dropping the placement.
				var fallback_hit := {"position": candidate, "normal": normal, "collider": path_node}
				if not _passes_placement_filters(fallback_hit):
					continue
			candidate += normal * surface_offset
			var entry: Dictionary
			if path_asset_order == 1:
				entry = entries[asset_sequence_index % entries.size()]
			else:
				entry = _choose_weighted_entry(entries)
			asset_sequence_index += 1
			if entry.is_empty():
				continue
			var required_spacing: float = maxf(minimum_spacing, float(entry.get("minimum_spacing", 0.0)))
			if not _is_far_enough(candidate, required_spacing):
				continue
			var source_path: String = str(entry.get("path", ""))
			if source_path.is_empty():
				continue
			var basis: Basis
			if path_align_direction:
				basis = _basis_from_direction(direction, _alignment_up(normal))
			else:
				basis = _basis_from_normal(_alignment_up(normal))
			if _place_path_asset(source_path, candidate, normal, basis):
				_stroke_sample_positions.append(candidate)
				placed += 1
		var step_distance: float
		if path_spacing_mode == 1:
			step_distance = _rng.randf_range(minf(path_spacing_min, path_spacing_max), maxf(path_spacing_min, path_spacing_max))
		else:
			step_distance = path_spacing_min
		# Never advance more densely than the global spacing setting. This avoids
		# accidental floods when path spacing is set near zero.
		var effective_step: float = maxf(0.1, maxf(step_distance, minimum_spacing))
		distance += effective_step
	_finish_bulk_operation("Scatter %d Assets Along Surface Path" % placed)
	if placed >= safety:
		_surface_feedback = "Surface path stopped at the %d asset safety limit" % safety
	elif iterations >= iteration_limit:
		_surface_feedback = "Surface path stopped because spacing was too dense"
	_path_entries_override.clear()
	placement_parent = original_parent
	return placed

func _path_profile_offsets() -> Array[float]:
	var offsets: Array[float] = []
	match path_profile:
		1:
			offsets.append(-path_width * 0.5)
			offsets.append(path_width * 0.5)
		2:
			for _index in maxi(1, path_row_count):
				offsets.append(0.0)
		3:
			var rows := maxi(1, path_row_count)
			if rows == 1:
				offsets.append(0.0)
			else:
				for index in rows:
					offsets.append(lerpf(-path_width * 0.5, path_width * 0.5, float(index) / float(rows - 1)))
		_:
			offsets.append(0.0)
	return offsets

func _raycast_world_segment(origin: Vector3, ray_end: Vector3) -> Dictionary:
	var root: Node = EditorInterface.get_edited_scene_root()
	var world: World3D = null
	if root is Node3D:
		world = (root as Node3D).get_world_3d()
	if world != null:
		var excluded: Array[RID] = []
		for _attempt in 64:
			var query: PhysicsRayQueryParameters3D = PhysicsRayQueryParameters3D.create(origin, ray_end)
			query.collide_with_areas = false
			query.collide_with_bodies = true
			if collision_layer_mask > 0:
				query.collision_mask = collision_layer_mask
			else:
				query.collision_mask = 0xFFFFFFFF
			query.exclude = excluded
			var result: Dictionary = world.direct_space_state.intersect_ray(query)
			if result.is_empty():
				break
			var collider: Object = result.get("collider") as Object
			var collider_node: Node = collider as Node
			if ignore_painted_assets and _is_painted_asset_or_descendant(collider_node):
				if collider is CollisionObject3D:
					excluded.append((collider as CollisionObject3D).get_rid())
					continue
				break
			if not _surface_is_allowed(collider_node):
				break
			var hit_position: Vector3 = result["position"] as Vector3
			var hit_normal: Vector3 = (result["normal"] as Vector3).normalized()
			return {"position": hit_position + hit_normal * 0.002, "normal": hit_normal, "collider": collider}
	return _raycast_visible_meshes(origin, ray_end, (ray_end - origin).normalized())

func _basis_from_direction(direction: Vector3, requested_up: Vector3) -> Basis:
	return PainterGeometry.basis_from_direction(direction, requested_up)

func _randomized_path_basis(base_basis: Basis) -> Basis:
	var result := base_basis
	if random_rotation_enabled and random_rotation_x_enabled and random_rotation_x > 0.0:
		result = result.rotated(result.x.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_x)))
	if random_rotation_enabled and random_rotation_y_enabled and random_rotation_y > 0.0:
		result = result.rotated(result.y.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_y)))
	if random_rotation_enabled and random_rotation_z_enabled and random_rotation_z > 0.0:
		result = result.rotated(result.z.normalized(), deg_to_rad(_random_rotation_angle(random_rotation_z)))
	var random_scale: float = 1.0
	if random_scale_enabled:
		random_scale = _rng.randf_range(minf(random_scale_min, random_scale_max), maxf(random_scale_min, random_scale_max))
	return result.scaled(Vector3.ONE * random_scale)

func _place_path_asset(path: String, position: Vector3, normal: Vector3, basis: Basis) -> bool:
	if placement_mode == 1:
		return _place_path_multimesh(path, position, basis)
	return _place_path_scene(path, position, normal, basis)

func _place_path_scene(path: String, position: Vector3, normal: Vector3, basis: Basis) -> bool:
	var packed: PackedScene = _scene_cache.get(path) as PackedScene
	if packed == null:
		packed = load(path) as PackedScene
		if packed != null:
			_scene_cache[path] = packed
	if packed == null:
		return false
	var instance: Node = packed.instantiate()
	if not instance is Node3D:
		instance.queue_free()
		return false
	var asset_name: String = path.get_file().get_basename().validate_node_name()
	if asset_name.is_empty():
		instance.name = "PaintedAsset"
	else:
		instance.name = asset_name
	placement_parent.add_child(instance, true)
	instance.owner = EditorInterface.get_edited_scene_root()
	var instance_3d := instance as Node3D
	instance_3d.global_transform = Transform3D(_randomized_path_basis(basis), position)
	instance.set_meta("asset_painter_placed", true)
	instance.set_meta("asset_source_path", path)
	instance.set_meta("asset_painter_base_position", instance_3d.global_position)
	instance.set_meta("asset_painter_surface_normal", normal.normalized())
	_stroke_nodes.append(instance)
	_painted_nodes.append(instance_3d)
	_add_spacing_hash_position(instance_3d.global_position)
	return true

func _place_path_multimesh(path: String, position: Vector3, basis: Basis) -> bool:
	var asset_data: Dictionary = _get_multimesh_asset_data(path)
	if asset_data.is_empty():
		return false
	var mm_node: MultiMeshInstance3D = _get_or_create_multimesh_node(path, asset_data, position)
	if mm_node == null or mm_node.multimesh == null:
		return false
	var root_global := Transform3D(_randomized_path_basis(basis), position)
	var mesh_relative: Transform3D = asset_data["mesh_relative"] as Transform3D
	var local_transform: Transform3D = mm_node.global_transform.affine_inverse() * (root_global * mesh_relative)
	var transforms: Array[Transform3D] = []
	for index in mm_node.multimesh.instance_count:
		transforms.append(mm_node.multimesh.get_instance_transform(index))
	transforms.append(local_transform)
	_set_multimesh_transforms(mm_node, transforms)
	_add_spacing_hash_position(position)
	return true

func _alignment_up(normal: Vector3) -> Vector3:
	return PainterGeometry.alignment_up(align_mode, blend_amount, normal)

func _raycast_scene(mouse_pos: Vector2, camera: Camera3D) -> Dictionary:
	if camera == null:
		return {}
	var origin: Vector3 = camera.project_ray_origin(mouse_pos)
	var ray_direction: Vector3 = camera.project_ray_normal(mouse_pos).normalized()
	var ray_end: Vector3 = origin + ray_direction * 4096.0

	if surface_mode == 2:
		return _raycast_grid_plane(origin, ray_direction)

	var physics_hit: Dictionary = _raycast_physics(origin, ray_end)
	if not physics_hit.is_empty():
		return physics_hit
	var mesh_hit: Dictionary = _raycast_visible_meshes(origin, ray_end, ray_direction)
	if not mesh_hit.is_empty():
		return mesh_hit
	if surface_mode == 1:
		return _raycast_grid_plane(origin, ray_direction)
	return {}

func _raycast_grid_plane(origin: Vector3, ray_direction: Vector3) -> Dictionary:
	var normal := Vector3.UP
	var plane_point := Vector3(0.0, grid_offset, 0.0)
	match grid_plane:
		1:
			normal = Vector3.BACK
			plane_point = Vector3(0.0, 0.0, grid_offset)
		2:
			normal = Vector3.RIGHT
			plane_point = Vector3(grid_offset, 0.0, 0.0)
	var denominator: float = ray_direction.dot(normal)
	if absf(denominator) < 0.00001:
		return {}
	var distance: float = (plane_point - origin).dot(normal) / denominator
	if distance < 0.0:
		return {}
	var hit_position: Vector3 = origin + ray_direction * distance
	return {"position": hit_position, "normal": normal, "collider": null, "grid_surface": true}

func _apply_grid_snap_for_asset(hit: Dictionary, _path: String) -> Dictionary:
	if hit.is_empty() or not bool(hit.get("grid_surface", false)):
		return hit
	var result := hit.duplicate()
	var step := _grid_cell_size()
	var position: Vector3 = result["position"] as Vector3
	var cell := _world_to_grid_cell(position, step)
	result["position"] = _grid_cell_center(cell, step)
	result["grid_cell"] = cell
	result["grid_step"] = step
	return result

func _get_asset_local_aabb(path: String) -> AABB:
	if _asset_bounds_cache.has(path):
		return _asset_bounds_cache[path] as AABB
	var packed: PackedScene = _scene_cache.get(path) as PackedScene
	if packed == null:
		packed = load(path) as PackedScene
		if packed != null:
			_scene_cache[path] = packed
	if packed == null:
		return AABB(Vector3.ZERO, Vector3.ONE)
	var root := packed.instantiate()
	if not root is Node3D:
		root.free()
		return AABB(Vector3.ZERO, Vector3.ONE)
	var combined := AABB()
	var has_bounds := false
	var mesh_nodes: Array[Node] = []
	if root is MeshInstance3D:
		mesh_nodes.append(root)
	mesh_nodes.append_array(root.find_children("*", "MeshInstance3D", true, false))
	for node in mesh_nodes:
		var mesh_node := node as MeshInstance3D
		if mesh_node == null or mesh_node.mesh == null:
			continue
		var relative := _node_transform_relative_to_root(mesh_node, root as Node3D)
		var transformed := relative * mesh_node.mesh.get_aabb()
		if not has_bounds:
			combined = transformed
			has_bounds = true
		else:
			combined = combined.merge(transformed)
	root.free()
	if not has_bounds:
		combined = AABB(Vector3.ZERO, Vector3.ONE)
	_asset_bounds_cache[path] = combined
	return combined

func _node_transform_relative_to_root(node: Node3D, root: Node3D) -> Transform3D:
	if node == root:
		return Transform3D.IDENTITY
	var result := node.transform
	var current := node.get_parent()
	while current != null and current != root:
		if current is Node3D:
			result = (current as Node3D).transform * result
		current = current.get_parent()
	return result

func _grid_position_is_occupied(new_path: String, position: Vector3, normal: Vector3) -> bool:
	if placement_parent == null:
		return false
	var new_root := Transform3D(_basis_from_normal(_alignment_up(normal)), position)
	var new_bounds: AABB = new_root * _get_asset_local_aabb(new_path)
	if placement_mode == 1:
		for child in placement_parent.get_children():
			if not child is MultiMeshInstance3D or not child.has_meta("asset_painter_multimesh"):
				continue
			var mm := child as MultiMeshInstance3D
			if mm.multimesh == null:
				continue
			var old_path := str(mm.get_meta("asset_source_path", ""))
			var asset_data := _get_multimesh_asset_data(old_path)
			if asset_data.is_empty():
				continue
			var inverse_mesh_relative: Transform3D = (asset_data["mesh_relative"] as Transform3D).affine_inverse()
			var old_local_bounds := _get_asset_local_aabb(old_path)
			for index in mm.multimesh.instance_count:
				var local_transform := mm.multimesh.get_instance_transform(index)
				var root_global: Transform3D = (mm.global_transform * local_transform) * inverse_mesh_relative
				if _grid_bounds_overlap(new_bounds, root_global * old_local_bounds):
					return true
	else:
		for child in placement_parent.get_children():
			if not child is Node3D or child is MultiMeshInstance3D or not child.has_meta("asset_painter_placed"):
				continue
			var old_path := str(child.get_meta("asset_source_path", ""))
			if old_path.is_empty():
				continue
			var old_bounds: AABB = (child as Node3D).global_transform * _get_asset_local_aabb(old_path)
			if _grid_bounds_overlap(new_bounds, old_bounds):
				return true
	return false

func _remove_occupied_grid_assets(new_path: String, position: Vector3, normal: Vector3) -> void:
	if placement_parent == null:
		return
	var new_root := Transform3D(_basis_from_normal(_alignment_up(normal)), position)
	var new_bounds: AABB = new_root * _get_asset_local_aabb(new_path)
	var changed := false
	if placement_mode == 1:
		for child in placement_parent.get_children():
			if not child is MultiMeshInstance3D or not child.has_meta("asset_painter_multimesh"):
				continue
			var mm := child as MultiMeshInstance3D
			if mm.multimesh == null:
				continue
			var old_path := str(mm.get_meta("asset_source_path", ""))
			var asset_data := _get_multimesh_asset_data(old_path)
			if asset_data.is_empty():
				continue
			var inverse_mesh_relative: Transform3D = (asset_data["mesh_relative"] as Transform3D).affine_inverse()
			var old_local_bounds := _get_asset_local_aabb(old_path)
			var kept: Array[Transform3D] = []
			for index in mm.multimesh.instance_count:
				var local_transform := mm.multimesh.get_instance_transform(index)
				var mesh_global: Transform3D = mm.global_transform * local_transform
				var root_global: Transform3D = mesh_global * inverse_mesh_relative
				var old_bounds: AABB = root_global * old_local_bounds
				if _grid_bounds_overlap(new_bounds, old_bounds):
					changed = true
				else:
					kept.append(local_transform)
			if kept.size() != mm.multimesh.instance_count:
				_set_multimesh_transforms(mm, kept)
	else:
		var remove_nodes: Array[Node] = []
		for child in placement_parent.get_children():
			if not child is Node3D or child is MultiMeshInstance3D or not child.has_meta("asset_painter_placed"):
				continue
			var old_path := str(child.get_meta("asset_source_path", ""))
			if old_path.is_empty():
				continue
			var old_bounds: AABB = (child as Node3D).global_transform * _get_asset_local_aabb(old_path)
			if _grid_bounds_overlap(new_bounds, old_bounds):
				remove_nodes.append(child)
		for node in remove_nodes:
			placement_parent.remove_child(node)
			node.free()
			changed = true
	if changed:
		_spacing_hash_dirty = true
		_refresh_painted_nodes()

func _grid_bounds_overlap(a: AABB, b: AABB) -> bool:
	# Shrink very slightly so assets that only touch at their edges are not replaced.
	var epsilon := 0.001
	var a_min := a.position + Vector3.ONE * epsilon
	var a_max := a.end - Vector3.ONE * epsilon
	var b_min := b.position + Vector3.ONE * epsilon
	var b_max := b.end - Vector3.ONE * epsilon
	return (
		a_min.x < b_max.x and a_max.x > b_min.x
		and a_min.y < b_max.y and a_max.y > b_min.y
		and a_min.z < b_max.z and a_max.z > b_min.z
	)

func _random_rotation_angle(maximum_degrees: float) -> float:
	return PainterSampling.random_rotation_angle(
		maximum_degrees,
		_placing_on_grid,
		grid_rotation_snap_enabled,
		grid_rotation_snap_degrees,
		_rng
	)

func _raycast_physics(origin: Vector3, ray_end: Vector3) -> Dictionary:
	if _viewport_camera == null or not is_instance_valid(_viewport_camera) or _viewport_camera.get_world_3d() == null:
		return {}
	var excluded: Array[RID] = []
	var space_state: PhysicsDirectSpaceState3D = _viewport_camera.get_world_3d().direct_space_state
	for attempt in 64:
		var query: PhysicsRayQueryParameters3D = PhysicsRayQueryParameters3D.create(origin, ray_end)
		query.collide_with_areas = false
		query.collide_with_bodies = true
		var safe_collision_mask: int = collision_layer_mask
		if safe_collision_mask <= 0:
			safe_collision_mask = 0xFFFFFFFF
		query.collision_mask = safe_collision_mask
		query.exclude = excluded
		var result: Dictionary = space_state.intersect_ray(query)
		if result.is_empty():
			return {}
		var collider: Object = result["collider"] as Object
		var collider_node: Node = collider as Node
		if ignore_painted_assets and _is_painted_asset_or_descendant(collider_node):
			if collider is CollisionObject3D:
				excluded.append((collider as CollisionObject3D).get_rid())
				continue
			return {}
		if not _surface_is_allowed(collider_node):
			return {}
		var hit_position: Vector3 = result["position"] as Vector3
		var hit_normal: Vector3 = (result["normal"] as Vector3).normalized()
		return {"position": hit_position + hit_normal * 0.002, "normal": hit_normal, "collider": collider}
	return {}

func _refresh_visible_mesh_cache() -> void:
	_visible_mesh_nodes_cache.clear()
	var root: Node = EditorInterface.get_edited_scene_root()
	if root == null:
		_visible_mesh_cache_dirty = false
		_visible_mesh_cache_elapsed = 0.0
		return
	if root is MeshInstance3D:
		_visible_mesh_nodes_cache.append(root as MeshInstance3D)
	var found: Array[Node] = root.find_children("*", "MeshInstance3D", true, false)
	for node in found:
		if node is MeshInstance3D:
			_visible_mesh_nodes_cache.append(node as MeshInstance3D)
	_visible_mesh_cache_dirty = false
	_visible_mesh_cache_elapsed = 0.0

func _cached_mesh_surfaces(mesh: Mesh) -> Array:
	var key: int = mesh.get_instance_id()
	if _mesh_surface_cache.has(key):
		return _mesh_surface_cache[key] as Array
	var cached_surfaces: Array = []
	for surface_index in mesh.get_surface_count():
		var arrays: Array = mesh.surface_get_arrays(surface_index)
		if arrays.is_empty():
			continue
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		if vertices.is_empty():
			continue
		cached_surfaces.append({"vertices": vertices, "indices": indices})
	_mesh_surface_cache[key] = cached_surfaces
	return cached_surfaces

func _raycast_visible_meshes(origin: Vector3, ray_end: Vector3, ray_direction: Vector3) -> Dictionary:
	if _visible_mesh_cache_dirty or _visible_mesh_cache_elapsed >= VISIBLE_MESH_CACHE_REFRESH_SECONDS:
		_refresh_visible_mesh_cache()

	var nearest_distance: float = INF
	var nearest_hit: Dictionary = {}
	for mesh_instance in _visible_mesh_nodes_cache:
		if mesh_instance == null or not is_instance_valid(mesh_instance):
			_visible_mesh_cache_dirty = true
			continue
		if not mesh_instance.is_visible_in_tree() or mesh_instance.mesh == null:
			continue
		if (mesh_instance.layers & collision_layer_mask) == 0:
			continue
		if ignore_painted_assets and _is_painted_asset_or_descendant(mesh_instance):
			continue
		if not _surface_is_allowed(mesh_instance):
			continue

		var inverse: Transform3D = mesh_instance.global_transform.affine_inverse()
		var local_origin: Vector3 = inverse * origin
		var local_end: Vector3 = inverse * ray_end
		var mesh: Mesh = mesh_instance.mesh
		# Cheap broad-phase test. This skips triangle work for every mesh the ray
		# cannot possibly touch, which matters greatly when painting across a scene.
		if not mesh.get_aabb().intersects_segment(local_origin, local_end):
			continue

		var cached_surfaces: Array = _cached_mesh_surfaces(mesh)
		for surface_data_variant in cached_surfaces:
			var surface_data: Dictionary = surface_data_variant as Dictionary
			var vertices: PackedVector3Array = surface_data["vertices"] as PackedVector3Array
			var indices: PackedInt32Array = surface_data["indices"] as PackedInt32Array
			var triangle_count: int = indices.size() / 3 if not indices.is_empty() else vertices.size() / 3
			for triangle_index in triangle_count:
				var base: int = triangle_index * 3
				var ia: int = indices[base] if not indices.is_empty() else base
				var ib: int = indices[base + 1] if not indices.is_empty() else base + 1
				var ic: int = indices[base + 2] if not indices.is_empty() else base + 2
				if ia >= vertices.size() or ib >= vertices.size() or ic >= vertices.size():
					continue
				var a: Vector3 = vertices[ia]
				var b: Vector3 = vertices[ib]
				var c: Vector3 = vertices[ic]
				var local_hit: Variant = Geometry3D.segment_intersects_triangle(local_origin, local_end, a, b, c)
				if local_hit == null:
					continue
				var world_hit: Vector3 = mesh_instance.global_transform * (local_hit as Vector3)
				var distance: float = origin.distance_to(world_hit)
				if distance >= nearest_distance:
					continue
				var local_normal: Vector3 = (b - a).cross(c - a).normalized()
				var normal_matrix: Basis = mesh_instance.global_transform.basis.inverse().transposed()
				var world_normal: Vector3 = (normal_matrix * local_normal).normalized()
				if world_normal.dot(ray_direction) > 0.0:
					world_normal = -world_normal
				nearest_distance = distance
				nearest_hit = {
					"position": world_hit + world_normal * 0.002,
					"normal": world_normal,
					"collider": mesh_instance
				}
	return nearest_hit


func _passes_placement_filters(hit: Dictionary) -> bool:
	return _get_filter_rejection_reason(hit).is_empty()

func _is_painted_asset_or_descendant(node: Node) -> bool:
	var current: Node = node
	while current != null:
		if current.has_meta("asset_painter_placed"):
			return true
		current = current.get_parent()
	return false

func _surface_is_allowed(surface_node: Node) -> bool:
	if not paint_only_selected:
		return true
	if surface_node == null:
		return false
	var selected: Array[Node] = EditorInterface.get_selection().get_selected_nodes()
	if selected.is_empty():
		return false
	var selected_node: Node = selected[0]
	return surface_node == selected_node or selected_node.is_ancestor_of(surface_node) or surface_node.is_ancestor_of(selected_node)

func _basis_from_normal(normal: Vector3) -> Basis:
	return PainterGeometry.basis_from_normal(normal)

func _surface_right(normal: Vector3) -> Vector3:
	return PainterGeometry.surface_right(normal)

func _analysis_collect_nodes(root: Node) -> Array[Node]:
	return AssetAnalysis.collect_nodes(root)

func get_environment_statistics() -> String:
	var root := EditorInterface.get_edited_scene_root()
	if root == null:
		return "Statistics & Analysis\nNo edited 3D scene is open."
	var scene_instances := 0
	var multimeshes := 0
	var multimesh_instances := 0
	var unique_assets: Dictionary = {}
	var estimated_triangles: int = 0
	for node in _analysis_collect_nodes(root):
		if node is MultiMeshInstance3D and node.has_meta("asset_painter_multimesh"):
			multimeshes += 1
			var mm_node := node as MultiMeshInstance3D
			if mm_node.multimesh != null:
				multimesh_instances += mm_node.multimesh.instance_count
				if mm_node.multimesh.mesh != null:
					estimated_triangles += _analysis_mesh_triangle_count(mm_node.multimesh.mesh) * mm_node.multimesh.instance_count
			var mm_source := str(node.get_meta("asset_source_path", ""))
			if not mm_source.is_empty():
				unique_assets[mm_source] = true
		elif node is Node3D and node.has_meta("asset_painter_placed"):
			scene_instances += 1
			var source := str(node.get_meta("asset_source_path", ""))
			if not source.is_empty():
				unique_assets[source] = true
				var data := _get_multimesh_asset_data(source)
				var mesh := data.get("mesh") as Mesh
				if mesh != null:
					estimated_triangles += _analysis_mesh_triangle_count(mesh)
	var estimated_draw_calls := scene_instances + multimeshes
	var rating := "A"
	if estimated_draw_calls > 2000:
		rating = "D"
	elif estimated_draw_calls > 1000:
		rating = "C"
	elif estimated_draw_calls > 300:
		rating = "B"
	return "Statistics & Analysis\nScene instances: %d\nMultiMeshes: %d\nMultiMesh instances: %d\nUnique assets: %d\nEstimated draw calls: %d\nEstimated triangles: %d\nOptimization rating: %s" % [scene_instances, multimeshes, multimesh_instances, unique_assets.size(), estimated_draw_calls, estimated_triangles, rating]

func analyze_asset(scene_path: String) -> String:
	return AssetAnalysis.analyze_asset(scene_path)

func _analysis_mesh_triangle_count(mesh: Mesh) -> int:
	return AssetAnalysis.mesh_triangle_count(mesh)

