@tool
extends RefCounted

## Stateless grid-coordinate conversions used by the asset painter.

static func plane_step(grid_plane: int, step: Vector3) -> Vector2:
	match grid_plane:
		0:
			return Vector2(step.x, step.z)
		1:
			return Vector2(step.x, step.y)
		2:
			return Vector2(step.z, step.y)
	return Vector2(step.x, step.z)

static func world_to_cell(position: Vector3, step: Vector3) -> Vector3i:
	return Vector3i(
		floori(position.x / step.x),
		floori(position.y / step.y),
		floori(position.z / step.z)
	)

static func cell_center(grid_plane: int, grid_offset: float, cell: Vector3i, step: Vector3) -> Vector3:
	var center := Vector3(
		(float(cell.x) + 0.5) * step.x,
		(float(cell.y) + 0.5) * step.y,
		(float(cell.z) + 0.5) * step.z
	)
	match grid_plane:
		0:
			center.y = grid_offset
		1:
			center.z = grid_offset
		2:
			center.x = grid_offset
	return center

static func cell_axis_a(grid_plane: int, cell: Vector3i) -> int:
	match grid_plane:
		0, 1:
			return cell.x
		2:
			return cell.z
	return cell.x

static func cell_axis_b(grid_plane: int, cell: Vector3i) -> int:
	match grid_plane:
		0:
			return cell.z
		1, 2:
			return cell.y
	return cell.z

static func cell_from_plane_axes(grid_plane: int, a: int, b: int) -> Vector3i:
	match grid_plane:
		0:
			return Vector3i(a, 0, b)
		1:
			return Vector3i(a, b, 0)
		2:
			return Vector3i(0, b, a)
	return Vector3i(a, 0, b)

static func plane_coordinates(grid_plane: int, position: Vector3) -> Vector2:
	match grid_plane:
		0:
			return Vector2(position.x, position.z)
		1:
			return Vector2(position.x, position.y)
		2:
			return Vector2(position.z, position.y)
	return Vector2(position.x, position.z)

static func world_position(grid_plane: int, grid_offset: float, coordinate: Vector2) -> Vector3:
	match grid_plane:
		0:
			return Vector3(coordinate.x, grid_offset, coordinate.y)
		1:
			return Vector3(coordinate.x, coordinate.y, grid_offset)
		2:
			return Vector3(grid_offset, coordinate.y, coordinate.x)
	return Vector3(coordinate.x, grid_offset, coordinate.y)

static func plane_normal(grid_plane: int) -> Vector3:
	match grid_plane:
		1:
			return Vector3.BACK
		2:
			return Vector3.RIGHT
	return Vector3.UP

static func layer_key(grid_plane: int, grid_offset: float, step: Vector3) -> String:
	return "%d|%.6f|%.6f|%.6f|%.6f" % [grid_plane, grid_offset, step.x, step.y, step.z]
