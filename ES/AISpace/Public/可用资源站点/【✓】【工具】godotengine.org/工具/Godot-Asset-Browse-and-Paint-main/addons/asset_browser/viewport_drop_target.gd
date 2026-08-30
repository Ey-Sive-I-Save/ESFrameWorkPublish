@tool
extends Control

var _plugin: EditorPlugin
var _is_3d: bool


func setup(plugin: EditorPlugin, is_3d: bool) -> void:
	_plugin = plugin
	_is_3d = is_3d
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_PASS


func _can_drop_data(_pos: Vector2, data: Variant) -> bool:
	return typeof(data) == TYPE_DICTIONARY and data.get("type") == "files" \
		and data.get("files", []).size() > 0 \
		and (data["files"][0] as String).ends_with(".tscn")


func _drop_data(pos: Vector2, data: Variant) -> void:
	var files: Array = data["files"]

	var selected_nodes := EditorInterface.get_selection().get_selected_nodes()
	var parent: Node = selected_nodes[0] if selected_nodes.size() > 0 \
		else EditorInterface.get_edited_scene_root()

	if parent == null:
		push_warning("Asset Browser: no open scene to drop into.")
		return

	# Get hit result once — all instances share the same drop point/normal
	var hit := _raycast_scene(pos)
	var drop_pos: Vector3  = hit["position"]
	var drop_normal: Vector3 = hit["normal"]

	# For multiple scenes, arrange them in a row along the surface's X axis
	# We compute a spacing vector that lies on the surface plane
	var right := _surface_right(drop_normal)

	# Estimate total width so we can centre the row on the drop point
	var spacing: float = 1.5  # metres between instances; adjusted below
	var total_width: float = spacing * (files.size() - 1)
	var start_pos: Vector3 = drop_pos - right * (total_width * 0.5)

	var ur := _plugin.get_undo_redo()
	var action_name := "Drop Scene" if files.size() == 1 else "Batch Drop %d Scenes" % files.size()
	ur.create_action(action_name)

	for i in files.size():
		var scene_path: String = files[i]
		if not ResourceLoader.exists(scene_path):
			push_error("Asset Browser: scene not found — " + scene_path)
			continue

		var packed: PackedScene = load(scene_path)
		if packed == null:
			continue

		var instance: Node = packed.instantiate()
		var place_pos: Vector3 = start_pos + right * (spacing * i)

		if _is_3d and instance is Node3D:
			instance.global_position = place_pos
			# Align instance Y-up to surface normal
			if drop_normal != Vector3.UP and drop_normal != Vector3.ZERO:
				instance.global_transform.basis = _basis_from_normal(drop_normal)

		elif not _is_3d and instance is Node2D:
			var xform := EditorInterface.get_editor_viewport_2d().get_screen_transform()
			# Offset 2D instances horizontally
			var offset := Vector2(i * 64.0, 0.0)
			instance.position = xform.affine_inverse() * (pos + offset)

		ur.add_do_method(parent, "add_child", instance)
		ur.add_do_method(instance, "set_owner", EditorInterface.get_edited_scene_root())
		ur.add_undo_method(parent, "remove_child", instance)
		ur.add_do_reference(instance)

	ur.commit_action()


# ---------------------------------------------------------------------------
# Raycasting
# ---------------------------------------------------------------------------

func _raycast_scene(mouse_pos: Vector2) -> Dictionary:
	var sub_viewport := _get_sub_viewport()

	if sub_viewport:
		var cam := sub_viewport.get_camera_3d()
		if cam:
			var ray_origin := cam.project_ray_origin(mouse_pos)
			var ray_dir    := cam.project_ray_normal(mouse_pos)
			var ray_end    := ray_origin + ray_dir * 4096.0

			var space := sub_viewport.world_3d.direct_space_state
			var query  := PhysicsRayQueryParameters3D.create(ray_origin, ray_end)
			query.collide_with_areas = false
			query.collide_with_bodies = true

			var result := space.intersect_ray(query)
			if not result.is_empty():
				return {
					"position": result["position"] + result["normal"] * 0.001,
					"normal":   result["normal"]
				}

	# Fallback: Y=0 ground plane
	return {
		"position": _ground_plane_pos(mouse_pos),
		"normal":   Vector3.UP
	}


func _get_sub_viewport() -> SubViewport:
	var vp := EditorInterface.get_editor_viewport_3d(0)
	for child in vp.get_children():
		if child is SubViewport:
			return child
	return null


func _ground_plane_pos(mouse_pos: Vector2) -> Vector3:
	var sub_viewport := _get_sub_viewport()
	if sub_viewport == null:
		return Vector3.ZERO
	var cam := sub_viewport.get_camera_3d()
	if cam == null:
		return Vector3.ZERO
	var origin := cam.project_ray_origin(mouse_pos)
	var dir    := cam.project_ray_normal(mouse_pos)
	if abs(dir.y) > 0.001:
		var t := -origin.y / dir.y
		if t > 0.0:
			return origin + dir * t
	return origin + dir * 10.0


# ---------------------------------------------------------------------------
# Surface normal helpers
# ---------------------------------------------------------------------------

## Build a Basis where Y points along the surface normal
func _basis_from_normal(normal: Vector3) -> Basis:
	var up := normal.normalized()
	# Pick a reference vector that isn't parallel to up
	var ref := Vector3.FORWARD if abs(up.dot(Vector3.FORWARD)) < 0.99 else Vector3.RIGHT
	var right := ref.cross(up).normalized()
	var forward := up.cross(right).normalized()
	return Basis(right, up, -forward)


## A vector pointing "right" along the surface (used to space batch drops)
func _surface_right(normal: Vector3) -> Vector3:
	var up := normal.normalized()
	var ref := Vector3.FORWARD if abs(up.dot(Vector3.FORWARD)) < 0.99 else Vector3.RIGHT
	return ref.cross(up).normalized()
