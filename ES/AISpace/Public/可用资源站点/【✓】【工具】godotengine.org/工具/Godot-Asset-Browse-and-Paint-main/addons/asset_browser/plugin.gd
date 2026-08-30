@tool
extends EditorPlugin

var _panel: Control
var _drop_targets: Array[Control] = []
var _asset_painter: Control

func _enter_tree() -> void:
	set_input_event_forwarding_always_enabled()
	_panel = preload("res://addons/asset_browser/asset_browser_panel.gd").new()
	_panel.editor_plugin = self
	add_control_to_bottom_panel(_panel, "Assets")
	_attach_viewport_tools.call_deferred()

func _exit_tree() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.queue_free()
	for target in _drop_targets:
		if is_instance_valid(target):
			target.queue_free()
	_drop_targets.clear()
	if _panel:
		remove_control_from_bottom_panel(_panel)
		_panel.queue_free()

func _attach_viewport_tools() -> void:
	var vp3d := EditorInterface.get_editor_viewport_3d(0)
	if vp3d:
		var DropTarget = preload("res://addons/asset_browser/viewport_drop_target.gd")
		var target := DropTarget.new()
		target.setup(self, true)
		vp3d.add_child(target)
		_drop_targets.append(target)

		_asset_painter = preload("res://addons/asset_browser/asset_painter.gd").new()
		_asset_painter.setup(self, _panel, vp3d)
		if _panel.has_method("get_saved_painter_settings"):
			var saved_settings: Dictionary = _panel.get_saved_painter_settings()
			for option_name in saved_settings.keys():
				_asset_painter.set_option(str(option_name), saved_settings[option_name])
		_asset_painter.active_changed.connect(_on_painter_active_changed)
		vp3d.add_child(_asset_painter)

	var vp2d := EditorInterface.get_editor_viewport_2d()
	if vp2d:
		var DropTarget2D = preload("res://addons/asset_browser/viewport_drop_target.gd")
		var target2d := DropTarget2D.new()
		target2d.setup(self, false)
		vp2d.add_child(target2d)
		_drop_targets.append(target2d)

func set_asset_painter_active(active: bool) -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.set_active(active)


func set_asset_painter_option(option_name: String, value: Variant) -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.set_option(option_name, value)

func use_selected_asset_painter_parent() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.use_selected_parent()

func fill_selected_asset_painter_mesh() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.fill_selected_mesh()

func fill_selected_asset_painter_area() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.fill_selected_area()

func clear_selected_asset_painter_area() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.clear_selected_area()

func scatter_selected_asset_painter_path() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.scatter_selected_path()

func clear_selected_asset_painter_path() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.clear_selected_path_generated()

func bake_selected_asset_painter_path() -> void:
	if is_instance_valid(_asset_painter):
		_asset_painter.bake_selected_surface_path()

func get_asset_painter_statistics() -> String:
	if is_instance_valid(_asset_painter) and _asset_painter.has_method("get_environment_statistics"):
		return str(_asset_painter.get_environment_statistics())
	return "Statistics are unavailable."

func analyze_asset_painter_asset(scene_path: String) -> String:
	if is_instance_valid(_asset_painter) and _asset_painter.has_method("analyze_asset"):
		return str(_asset_painter.analyze_asset(scene_path))
	return "Asset analysis is unavailable."

func _on_painter_active_changed(active: bool) -> void:
	if is_instance_valid(_panel) and _panel.has_method("set_paint_button_pressed"):
		_panel.set_paint_button_pressed(active)

func _forward_3d_gui_input(viewport_camera: Camera3D, event: InputEvent) -> int:
	if is_instance_valid(_asset_painter) and _asset_painter.active:
		if _asset_painter.handle_viewport_input(viewport_camera, event):
			return EditorPlugin.AFTER_GUI_INPUT_STOP
	return EditorPlugin.AFTER_GUI_INPUT_PASS

func _handles(_object: Object) -> bool:
	return true
