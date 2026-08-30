@tool
extends RefCounted

## Stateless collection, favorite, recent, tag, and variant data operations.

static func string_array(value: Variant) -> Array[String]:
	var result: Array[String] = []
	if value is Array:
		for item in value:
			result.append(str(item))
	return result

static func collection_paths(collections: Dictionary, collection_name: String) -> Array[String]:
	return string_array(collections.get(collection_name, []))

static func add_unique_paths(existing: Array[String], paths_to_add: Array[String]) -> Array[String]:
	var result: Array[String] = existing.duplicate()
	for path in paths_to_add:
		if not result.has(path):
			result.append(path)
	return result

static func remove_paths(existing: Array[String], paths_to_remove: Array[String]) -> Array[String]:
	var result: Array[String] = existing.duplicate()
	for path in paths_to_remove:
		result.erase(path)
	return result

static func all_paths_present(existing: Array[String], paths: Array[String]) -> bool:
	if paths.is_empty():
		return false
	for path in paths:
		if not existing.has(path):
			return false
	return true

static func toggle_paths(existing: Array[String], paths: Array[String]) -> Array[String]:
	if all_paths_present(existing, paths):
		return remove_paths(existing, paths)
	return add_unique_paths(existing, paths)

static func mark_recent(existing: Array[String], scene_path: String, limit: int) -> Array[String]:
	var result: Array[String] = existing.duplicate()
	if scene_path.is_empty():
		return result
	result.erase(scene_path)
	result.push_front(scene_path)
	while result.size() > limit:
		result.pop_back()
	return result

static func normalize_tags(raw_tags: String) -> Array[String]:
	var normalized: Array[String] = []
	for part in raw_tags.split(","):
		var tag := part.strip_edges().to_lower()
		if not tag.is_empty() and not normalized.has(tag):
			normalized.append(tag)
	return normalized

static func tags_text(asset_tags: Dictionary, scene_path: String) -> String:
	return ", ".join(PackedStringArray(string_array(asset_tags.get(scene_path, []))))

static func default_variant_setting() -> Dictionary:
	return {"enabled": true, "weight": 1.0, "category": "General", "minimum_spacing": 0.0}

static func variant_setting(settings: Dictionary, path: String) -> Dictionary:
	var result := default_variant_setting()
	var saved: Variant = settings.get(path, {})
	if saved is Dictionary:
		var saved_dictionary: Dictionary = saved
		for key in saved_dictionary.keys():
			result[key] = saved_dictionary[key]
	return result

static func weighted_entries(selected_paths: Array[String], settings: Dictionary) -> Array[Dictionary]:
	var entries: Array[Dictionary] = []
	for path in selected_paths:
		var setting := variant_setting(settings, path)
		if not bool(setting.get("enabled", true)):
			continue
		var weight := maxf(0.0, float(setting.get("weight", 1.0)))
		if weight <= 0.0:
			continue
		entries.append({
			"path": path,
			"weight": weight,
			"category": str(setting.get("category", "General")),
			"minimum_spacing": float(setting.get("minimum_spacing", 0.0))
		})
	return entries
