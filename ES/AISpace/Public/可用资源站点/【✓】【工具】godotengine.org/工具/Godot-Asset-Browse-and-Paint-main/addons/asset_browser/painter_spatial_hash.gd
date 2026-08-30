@tool
extends RefCounted

## Stateless spatial-hash helpers used by the asset painter.
## The painter remains the owner of the hash dictionary and dirty state.

static func key_for_position(position: Vector3, cell_size: float) -> Vector3i:
	var size := maxf(0.25, cell_size)
	return Vector3i(
		floori(position.x / size),
		floori(position.y / size),
		floori(position.z / size)
	)

static func add_position(spacing_hash: Dictionary, position: Vector3, cell_size: float) -> void:
	var key := key_for_position(position, cell_size)
	if not spacing_hash.has(key):
		spacing_hash[key] = []
	(spacing_hash[key] as Array).append(position)

static func rebuild(spacing_hash: Dictionary, placement_parent: Node3D, cell_size: float) -> void:
	spacing_hash.clear()
	if placement_parent == null or not is_instance_valid(placement_parent):
		return
	for child in placement_parent.get_children():
		if child is MultiMeshInstance3D and child.has_meta("asset_painter_multimesh"):
			var multimesh_node := child as MultiMeshInstance3D
			if multimesh_node.multimesh == null:
				continue
			for index in multimesh_node.multimesh.instance_count:
				var position := (
					multimesh_node.global_transform
					* multimesh_node.multimesh.get_instance_transform(index)
				).origin
				add_position(spacing_hash, position, cell_size)
		elif child is Node3D and child.has_meta("asset_painter_placed"):
			add_position(spacing_hash, (child as Node3D).global_position, cell_size)

static func is_far_enough_hashed(
	spacing_hash: Dictionary,
	position: Vector3,
	spacing: float,
	cell_size: float
) -> bool:
	if spacing <= 0.0:
		return true
	var minimum_squared := spacing * spacing
	var center := key_for_position(position, cell_size)
	var reach := ceili(spacing / maxf(0.25, cell_size))
	for x in range(center.x - reach, center.x + reach + 1):
		for y in range(center.y - reach, center.y + reach + 1):
			for z in range(center.z - reach, center.z + reach + 1):
				var key := Vector3i(x, y, z)
				if not spacing_hash.has(key):
					continue
				for existing: Vector3 in spacing_hash[key]:
					if existing.distance_squared_to(position) < minimum_squared:
						return false
	return true

static func is_far_enough_linear(
	position: Vector3,
	spacing: float,
	placement_parent: Node3D,
	painted_nodes: Array[Node3D]
) -> bool:
	if spacing <= 0.0 or placement_parent == null:
		return true
	var minimum_squared := spacing * spacing
	for painted in painted_nodes:
		if (
			painted != null
			and is_instance_valid(painted)
			and painted.get_parent() == placement_parent
			and painted.global_position.distance_squared_to(position) < minimum_squared
		):
			return false
	for child in placement_parent.get_children():
		if not child is MultiMeshInstance3D or not child.has_meta("asset_painter_multimesh"):
			continue
		var multimesh_node := child as MultiMeshInstance3D
		if multimesh_node.multimesh == null:
			continue
		for index in multimesh_node.multimesh.instance_count:
			var global_position := (
				multimesh_node.global_transform
				* multimesh_node.multimesh.get_instance_transform(index)
			).origin
			if global_position.distance_squared_to(position) < minimum_squared:
				return false
	return true
