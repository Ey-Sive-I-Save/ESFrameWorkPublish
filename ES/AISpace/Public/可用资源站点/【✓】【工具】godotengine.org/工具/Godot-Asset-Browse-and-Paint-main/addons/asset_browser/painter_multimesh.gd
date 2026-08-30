@tool
extends RefCounted

static func get_asset_data(path: String, asset_cache: Dictionary, scene_cache: Dictionary) -> Dictionary:
	if asset_cache.has(path):
		return asset_cache[path] as Dictionary
	var packed: PackedScene = scene_cache.get(path) as PackedScene
	if packed == null:
		packed = load(path) as PackedScene
		if packed != null:
			scene_cache[path] = packed
	if packed == null:
		asset_cache[path] = {}
		return {}
	var root: Node = packed.instantiate()
	if not root is Node3D:
		root.free()
		asset_cache[path] = {}
		return {}
	var mesh_node: MeshInstance3D = null
	if root is MeshInstance3D and (root as MeshInstance3D).mesh != null:
		mesh_node = root as MeshInstance3D
	else:
		var candidates: Array[Node] = root.find_children("*", "MeshInstance3D", true, false)
		for candidate in candidates:
			var candidate_mesh := candidate as MeshInstance3D
			if candidate_mesh != null and candidate_mesh.mesh != null:
				mesh_node = candidate_mesh
				break
	if mesh_node == null:
		root.free()
		asset_cache[path] = {}
		return {}
	var relative: Transform3D = Transform3D.IDENTITY if mesh_node == root else mesh_node.transform
	var current: Node = mesh_node.get_parent()
	while current != null and current != root:
		if current is Node3D:
			relative = (current as Node3D).transform * relative
		current = current.get_parent()
	var data := {
		"mesh": mesh_node.mesh,
		"material_override": mesh_node.material_override,
		"mesh_relative": relative,
		"cast_shadow": mesh_node.cast_shadow
	}
	root.free()
	asset_cache[path] = data
	return data

static func get_or_create_node(
	parent: Node3D,
	path: String,
	asset_data: Dictionary,
	world_position: Vector3,
	chunking_enabled: bool,
	chunk_world_size: float,
	chunk_instance_limit: int,
	visibility_begin: float,
	visibility_end: float,
	placing_on_grid: bool,
	grid_layer: String
) -> MultiMeshInstance3D:
	if parent == null or not is_instance_valid(parent):
		return null
	var chunk_key := Vector2i.ZERO
	if chunking_enabled:
		chunk_key = Vector2i(floori(world_position.x / chunk_world_size), floori(world_position.z / chunk_world_size))
	for child in parent.get_children():
		if child is MultiMeshInstance3D and child.get_meta("asset_source_path", "") == path:
			var existing := child as MultiMeshInstance3D
			if placing_on_grid and str(existing.get_meta("asset_painter_grid_layer", "")) != grid_layer:
				continue
			if not placing_on_grid and existing.has_meta("asset_painter_grid_layer"):
				continue
			var same_chunk: bool = not chunking_enabled or Vector2i(existing.get_meta("asset_painter_chunk", Vector2i.ZERO)) == chunk_key
			var below_limit: bool = existing.multimesh == null or existing.multimesh.instance_count < chunk_instance_limit
			if same_chunk and below_limit:
				return existing
	var node := MultiMeshInstance3D.new()
	var asset_name: String = path.get_file().get_basename().validate_node_name()
	if asset_name.is_empty():
		asset_name = "PaintedAsset"
	node.name = "MM_" + asset_name
	if chunking_enabled:
		node.name += "_%d_%d" % [chunk_key.x, chunk_key.y]
		node.set_meta("asset_painter_chunk", chunk_key)
	node.set_meta("asset_painter_multimesh", true)
	node.set_meta("asset_painter_placed", true)
	node.set_meta("asset_source_path", path)
	if placing_on_grid:
		node.set_meta("asset_painter_grid_layer", grid_layer)
		node.set_meta("asset_painter_grid_cells", [])
	node.multimesh = MultiMesh.new()
	node.multimesh.transform_format = MultiMesh.TRANSFORM_3D
	node.multimesh.mesh = asset_data["mesh"] as Mesh
	node.multimesh.instance_count = 0
	node.material_override = asset_data["material_override"] as Material
	node.cast_shadow = int(asset_data["cast_shadow"])
	node.visibility_range_begin = visibility_begin
	node.visibility_range_end = visibility_end
	parent.add_child(node, true)
	node.owner = EditorInterface.get_edited_scene_root()
	return node

static func get_transforms(node: MultiMeshInstance3D) -> Array[Transform3D]:
	var transforms: Array[Transform3D] = []
	if node == null or node.multimesh == null:
		return transforms
	for index in node.multimesh.instance_count:
		transforms.append(node.multimesh.get_instance_transform(index))
	return transforms

static func set_transforms(node: MultiMeshInstance3D, transforms: Array[Transform3D]) -> void:
	if node == null or node.multimesh == null:
		return
	node.multimesh.instance_count = transforms.size()
	for index in transforms.size():
		node.multimesh.set_instance_transform(index, transforms[index])

static func erase_near(parent: Node3D, position: Vector3, radius: float) -> bool:
	if parent == null or not is_instance_valid(parent):
		return false
	var changed := false
	for child in parent.get_children():
		if not child is MultiMeshInstance3D or not child.has_meta("asset_painter_multimesh"):
			continue
		var node := child as MultiMeshInstance3D
		if node.multimesh == null:
			continue
		var kept: Array[Transform3D] = []
		for transform in get_transforms(node):
			if (node.global_transform * transform).origin.distance_to(position) > radius:
				kept.append(transform)
		if kept.size() != node.multimesh.instance_count:
			set_transforms(node, kept)
			changed = true
	return changed

static func capture_snapshot(parent) -> Array:
	var snapshot: Array = []
	if parent == null or not is_instance_valid(parent) or not parent is Node3D:
		return snapshot
	var parent_3d := parent as Node3D
	for child in parent_3d.get_children():
		if not child is MultiMeshInstance3D or not child.has_meta("asset_painter_multimesh"):
			continue
		var node := child as MultiMeshInstance3D
		snapshot.append({
			"path": str(node.get_meta("asset_source_path", "")),
			"name": str(node.name),
			"mesh": node.multimesh.mesh if node.multimesh != null else null,
			"material_override": node.material_override,
			"cast_shadow": node.cast_shadow,
			"chunk": node.get_meta("asset_painter_chunk", Vector2i.ZERO),
			"has_chunk": node.has_meta("asset_painter_chunk"),
			"visibility_begin": node.visibility_range_begin,
			"visibility_end": node.visibility_range_end,
			"grid_layer": str(node.get_meta("asset_painter_grid_layer", "")),
			"grid_cells": (node.get_meta("asset_painter_grid_cells", []) as Array).duplicate(),
			"transforms": get_transforms(node)
		})
	return snapshot

static func apply_snapshot(parent, snapshot: Array) -> void:
	if parent == null or not is_instance_valid(parent) or not parent is Node3D:
		return
	var parent_3d := parent as Node3D
	for child in parent_3d.get_children():
		if child is MultiMeshInstance3D and child.has_meta("asset_painter_multimesh"):
			parent_3d.remove_child(child)
			child.free()
	for entry_value in snapshot:
		var entry: Dictionary = entry_value as Dictionary
		var node := MultiMeshInstance3D.new()
		node.name = str(entry.get("name", "PaintedMultiMesh"))
		node.set_meta("asset_painter_multimesh", true)
		node.set_meta("asset_painter_placed", true)
		node.set_meta("asset_source_path", str(entry.get("path", "")))
		if bool(entry.get("has_chunk", false)):
			node.set_meta("asset_painter_chunk", entry.get("chunk", Vector2i.ZERO) as Vector2i)
		node.multimesh = MultiMesh.new()
		node.multimesh.transform_format = MultiMesh.TRANSFORM_3D
		node.multimesh.mesh = entry.get("mesh") as Mesh
		node.material_override = entry.get("material_override") as Material
		node.cast_shadow = int(entry.get("cast_shadow", GeometryInstance3D.SHADOW_CASTING_SETTING_ON))
		node.visibility_range_begin = float(entry.get("visibility_begin", 0.0))
		node.visibility_range_end = float(entry.get("visibility_end", 0.0))
		var grid_layer := str(entry.get("grid_layer", ""))
		if not grid_layer.is_empty():
			node.set_meta("asset_painter_grid_layer", grid_layer)
			node.set_meta("asset_painter_grid_cells", (entry.get("grid_cells", []) as Array).duplicate())
		parent_3d.add_child(node, true)
		node.owner = EditorInterface.get_edited_scene_root()
		var transforms: Array[Transform3D] = []
		for transform_value in entry.get("transforms", []):
			transforms.append(transform_value as Transform3D)
		set_transforms(node, transforms)
