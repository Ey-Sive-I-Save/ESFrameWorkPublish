@tool
extends RefCounted

## Pure persistence helpers shared by the browser panel.
## This component intentionally owns no UI state.

static func ensure_directory(path: String) -> void:
	var absolute_path := ProjectSettings.globalize_path(path)
	if not DirAccess.dir_exists_absolute(absolute_path):
		DirAccess.make_dir_recursive_absolute(absolute_path)

static func cache_key(scene_path: String) -> String:
	var unix_time := FileAccess.get_modified_time(scene_path)
	var safe_name := scene_path.trim_prefix("res://").replace("/", "_").replace(".", "_")
	return safe_name + "_" + str(unix_time) + ".png"

static func cache_path(cache_dir: String, scene_path: String) -> String:
	return cache_dir + cache_key(scene_path)

static func load_cached_thumbnail(cache_dir: String, scene_path: String) -> Texture2D:
	var path := cache_path(cache_dir, scene_path)
	if not FileAccess.file_exists(path):
		return null
	var image := Image.load_from_file(path)
	if image == null or image.is_empty():
		return null
	return ImageTexture.create_from_image(image)

static func save_cached_thumbnail(cache_dir: String, scene_path: String, texture: Texture2D) -> void:
	if texture == null:
		return
	ensure_directory(cache_dir)
	var image := texture.get_image()
	if image == null or image.is_empty():
		return
	image.save_png(cache_path(cache_dir, scene_path))

static func to_string_array(value: Variant) -> Array[String]:
	var result: Array[String] = []
	if value is Array:
		for item in value:
			result.append(str(item))
	return result

static func sanitize_preset_name(raw_name: String) -> String:
	var cleaned := raw_name.strip_edges()
	for character in ["/", "\\", ":", "*", "?", "\"", "<", ">", "|"]:
		cleaned = cleaned.replace(character, "_")
	return cleaned

static func preset_path(preset_dir: String, preset_name: String) -> String:
	return preset_dir + sanitize_preset_name(preset_name) + ".cfg"
