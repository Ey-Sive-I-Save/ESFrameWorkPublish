@tool
extends Node
class_name ThumbnailGenerator

var _viewport: SubViewport
var _camera: Camera3D

var _queue: Array = []
var _busy: bool = false


func _ready() -> void:
	_viewport = SubViewport.new()
	_viewport.size = Vector2i(256, 256)
	_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED
	_viewport.own_world_3d = true
	add_child(_viewport)

	var world_env := WorldEnvironment.new()
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky.sky_material = sky_mat
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 1.0
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	world_env.environment = env
	_viewport.add_child(world_env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-45.0, 45.0, 0.0)
	sun.light_energy = 1.0
	sun.shadow_enabled = false
	_viewport.add_child(sun)

	_camera = Camera3D.new()
	_camera.current = true
	_viewport.add_child(_camera)


func generate_for_scene(scene_path: String, receiver: Object, callback: String) -> void:
	_queue.append({"path": scene_path, "receiver": receiver, "callback": callback})
	if not _busy:
		_process_next()


func _process_next() -> void:
	if _queue.is_empty():
		_busy = false
		return
	_busy = true
	var job: Dictionary = _queue.pop_front()
	_render(job["path"], job["receiver"], job["callback"])


func _render(scene_path: String, receiver: Object, callback: String) -> void:
	if not ResourceLoader.exists(scene_path):
		_finish(receiver, callback, scene_path, null)
		return

	var packed: PackedScene = load(scene_path)
	if packed == null:
		_finish(receiver, callback, scene_path, null)
		return

	var instance: Node = packed.instantiate()
	_viewport.add_child(instance)

	await RenderingServer.frame_post_draw

	var aabb: AABB = _get_global_aabb(instance)
	if aabb.size == Vector3.ZERO:
		instance.queue_free()
		_finish(receiver, callback, scene_path, null)
		return

	aabb = aabb.abs()
	var center: Vector3 = aabb.position + aabb.size * 0.5
	var radius: float = aabb.size.length() * 0.5

	# Place camera at 3/4 angle
	var cam_dir := Vector3(1.0, 1.0, 1.0).normalized()
	var cam_dist: float = radius * 2.5
	_camera.global_transform.origin = center + cam_dir * cam_dist
	_camera.look_at(center, Vector3.UP)

	# Fit FOV tightly to the bounding sphere so the object fills the frame.
	# Add a small padding factor (0.9) so edges aren't clipped.
	# fov = 2 * atan(radius / cam_dist) converted to degrees
	var fov_rad: float = 2.0 * atan(radius / cam_dist)
	_camera.fov = rad_to_deg(fov_rad) / 0.9

	_viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	await RenderingServer.frame_post_draw

	var img: Image = _viewport.get_texture().get_image()
	img.resize(256, 256)
	var tex: ImageTexture = ImageTexture.create_from_image(img)

	instance.queue_free()
	_viewport.render_target_update_mode = SubViewport.UPDATE_DISABLED

	_finish(receiver, callback, scene_path, tex)


func _finish(receiver: Object, callback: String, scene_path: String, tex: Texture2D) -> void:
	if is_instance_valid(receiver):
		receiver.call_deferred(callback, scene_path, tex)
	_process_next()


func _get_global_aabb(node: Node) -> AABB:
	var aabb: AABB = AABB()
	var first: bool = true

	if node is MeshInstance3D:
		var mesh_node := node as MeshInstance3D
		if mesh_node.mesh:
			var local: AABB = mesh_node.mesh.get_aabb()
			var corners := [
				local.position,
				local.position + Vector3(local.size.x, 0, 0),
				local.position + Vector3(0, local.size.y, 0),
				local.position + Vector3(0, 0, local.size.z),
				local.position + Vector3(local.size.x, local.size.y, 0),
				local.position + Vector3(local.size.x, 0, local.size.z),
				local.position + Vector3(0, local.size.y, local.size.z),
				local.position + local.size
			]
			for c in corners:
				var world: Vector3 = mesh_node.global_transform * c
				if first:
					aabb.position = world
					aabb.size = Vector3.ZERO
					first = false
				else:
					aabb = aabb.expand(world)

	for child in node.get_children():
		var child_aabb: AABB = _get_global_aabb(child)
		if child_aabb.size != Vector3.ZERO:
			if first:
				aabb = child_aabb
				first = false
			else:
				aabb = aabb.merge(child_aabb)

	return aabb
