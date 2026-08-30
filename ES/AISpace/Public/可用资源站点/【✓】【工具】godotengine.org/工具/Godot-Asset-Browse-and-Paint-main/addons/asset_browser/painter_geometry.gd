@tool
extends RefCounted

## Stateless geometry helpers used by painting, area fill, and path placement.

static func mesh_triangles(mesh_node: MeshInstance3D) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	if mesh_node == null or mesh_node.mesh == null:
		return result
	var mesh := mesh_node.mesh
	for surface_index in mesh.get_surface_count():
		var arrays := mesh.surface_get_arrays(surface_index)
		if arrays.is_empty():
			continue
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		var triangle_count := indices.size() / 3 if not indices.is_empty() else vertices.size() / 3
		for triangle_index in triangle_count:
			var ia := indices[triangle_index * 3] if not indices.is_empty() else triangle_index * 3
			var ib := indices[triangle_index * 3 + 1] if not indices.is_empty() else triangle_index * 3 + 1
			var ic := indices[triangle_index * 3 + 2] if not indices.is_empty() else triangle_index * 3 + 2
			var a := vertices[ia]
			var b := vertices[ib]
			var c := vertices[ic]
			var cross := (b - a).cross(c - a)
			var area := cross.length() * 0.5
			if area > 0.000001:
				result.append({"a": a, "b": b, "c": c, "normal": cross.normalized(), "area": area})
	return result

static func first_collision_shape(area: Area3D) -> CollisionShape3D:
	if area == null:
		return null
	for node in area.find_children("*", "CollisionShape3D", true, false):
		if node is CollisionShape3D and not (node as CollisionShape3D).disabled:
			return node as CollisionShape3D
	return null

static func shape_local_aabb(shape: Shape3D) -> AABB:
	if shape is BoxShape3D:
		var size := (shape as BoxShape3D).size
		return AABB(-size * 0.5, size)
	if shape is SphereShape3D:
		var radius := (shape as SphereShape3D).radius
		return AABB(Vector3.ONE * -radius, Vector3.ONE * radius * 2.0)
	if shape is CylinderShape3D:
		var cylinder := shape as CylinderShape3D
		return AABB(Vector3(-cylinder.radius, -cylinder.height * 0.5, -cylinder.radius), Vector3(cylinder.radius * 2.0, cylinder.height, cylinder.radius * 2.0))
	if shape is CapsuleShape3D:
		var capsule := shape as CapsuleShape3D
		return AABB(Vector3(-capsule.radius, -capsule.height * 0.5, -capsule.radius), Vector3(capsule.radius * 2.0, capsule.height, capsule.radius * 2.0))
	return AABB(Vector3(-0.5, -0.5, -0.5), Vector3.ONE)

static func point_inside_shape(point: Vector3, shape: Shape3D) -> bool:
	if shape == null:
		return false
	if shape is BoxShape3D:
		var half := (shape as BoxShape3D).size * 0.5
		return absf(point.x) <= half.x and absf(point.y) <= half.y and absf(point.z) <= half.z
	if shape is SphereShape3D:
		return point.length_squared() <= pow((shape as SphereShape3D).radius, 2.0)
	if shape is CylinderShape3D:
		var cylinder := shape as CylinderShape3D
		return absf(point.y) <= cylinder.height * 0.5 and Vector2(point.x, point.z).length_squared() <= cylinder.radius * cylinder.radius
	if shape is CapsuleShape3D:
		var capsule := shape as CapsuleShape3D
		var half_line := maxf(0.0, capsule.height * 0.5 - capsule.radius)
		var clamped_y := clampf(point.y, -half_line, half_line)
		return (point - Vector3(0.0, clamped_y, 0.0)).length_squared() <= capsule.radius * capsule.radius
	return shape_local_aabb(shape).has_point(point)

static func world_point_inside_shape(world_point: Vector3, shape_node: CollisionShape3D) -> bool:
	if shape_node == null or shape_node.shape == null:
		return false
	return point_inside_shape(shape_node.global_transform.affine_inverse() * world_point, shape_node.shape)

static func basis_from_normal(normal: Vector3) -> Basis:
	var up := normal.normalized()
	if up.length_squared() < 0.0001:
		up = Vector3.UP
	var reference_axis := Vector3.FORWARD if absf(up.dot(Vector3.FORWARD)) < 0.99 else Vector3.RIGHT
	var right := reference_axis.cross(up).normalized()
	var forward := up.cross(right).normalized()
	return Basis(right, up, -forward)

static func surface_right(normal: Vector3) -> Vector3:
	var up := normal.normalized()
	if up.length_squared() < 0.0001:
		up = Vector3.UP
	var reference_axis := Vector3.FORWARD if absf(up.dot(Vector3.FORWARD)) < 0.99 else Vector3.RIGHT
	return reference_axis.cross(up).normalized()

static func basis_from_direction(direction: Vector3, requested_up: Vector3) -> Basis:
	var forward := direction.normalized()
	var up := requested_up.normalized()
	if up.length_squared() < 0.0001:
		up = Vector3.UP
	if forward.length_squared() < 0.0001:
		return basis_from_normal(up)
	if absf(forward.dot(up)) > 0.98:
		up = Vector3.UP if absf(forward.dot(Vector3.UP)) < 0.98 else Vector3.RIGHT
	var z_axis := -forward
	var x_axis := up.cross(z_axis).normalized()
	var y_axis := z_axis.cross(x_axis).normalized()
	return Basis(x_axis, y_axis, z_axis).orthonormalized()

static func alignment_up(align_mode: int, blend_amount: float, normal: Vector3) -> Vector3:
	match align_mode:
		1, 3:
			return Vector3.UP
		2:
			return Vector3.UP.slerp(normal.normalized(), clampf(blend_amount, 0.0, 1.0)).normalized()
		_:
			return normal.normalized()
