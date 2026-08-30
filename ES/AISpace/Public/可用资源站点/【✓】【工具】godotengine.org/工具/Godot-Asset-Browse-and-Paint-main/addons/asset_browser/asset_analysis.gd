@tool
extends RefCounted

## Read-only scene and mesh analysis. Kept separate from painting input/state.

static func collect_nodes(root: Node) -> Array[Node]:
	var result: Array[Node] = []
	if root == null:
		return result
	var stack: Array[Node] = [root]
	while not stack.is_empty():
		var node: Node = stack.pop_back()
		result.append(node)
		for child in node.get_children():
			stack.append(child)
	return result

static func mesh_triangle_count(mesh: Mesh) -> int:
	if mesh == null:
		return 0
	var triangles := 0
	for surface in mesh.get_surface_count():
		var arrays := mesh.surface_get_arrays(surface)
		if arrays.is_empty():
			continue
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		if not indices.is_empty():
			triangles += indices.size() / 3
		else:
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			triangles += vertices.size() / 3
	return triangles

static func analyze_asset(scene_path: String) -> String:
	if scene_path.is_empty() or not ResourceLoader.exists(scene_path):
		return "Asset Analysis\nThe selected asset could not be loaded."
	var packed := load(scene_path) as PackedScene
	if packed == null:
		return "Asset Analysis\nThe selected resource is not a valid PackedScene."
	var instance := packed.instantiate()
	if instance == null:
		return "Asset Analysis\nThe selected scene could not be instantiated."
	var mesh_count := 0
	var material_count := 0
	var triangle_count := 0
	var collision_count := 0
	var script_count := 0
	var animation_count := 0
	var particle_count := 0
	var light_count := 0
	for node in collect_nodes(instance):
		if node.get_script() != null:
			script_count += 1
		if node is MeshInstance3D:
			var mesh_node := node as MeshInstance3D
			if mesh_node.mesh != null:
				mesh_count += 1
				triangle_count += mesh_triangle_count(mesh_node.mesh)
				material_count += mesh_node.mesh.get_surface_count()
			if mesh_node.material_override != null:
				material_count += 1
		elif node is CollisionShape3D or node is CollisionObject3D:
			collision_count += 1
		elif node is AnimationPlayer:
			animation_count += 1
		elif node is GPUParticles3D or node is CPUParticles3D:
			particle_count += 1
		elif node is Light3D:
			light_count += 1
	var compatible := mesh_count > 0
	var recommended := "MultiMesh" if compatible and script_count == 0 and animation_count == 0 and particle_count == 0 and light_count == 0 else "Scene Instances"
	instance.queue_free()
	return "Asset Analysis: %s\nMeshes: %d\nEstimated triangles: %d\nMaterials/surfaces: %d\nCollision nodes: %d\nScripts: %d\nAnimationPlayers: %d\nParticle nodes: %d\nLights: %d\nMultiMesh compatible: %s\nRecommended mode: %s" % [scene_path.get_file(), mesh_count, triangle_count, material_count, collision_count, script_count, animation_count, particle_count, light_count, "Yes" if compatible else "No", recommended]
