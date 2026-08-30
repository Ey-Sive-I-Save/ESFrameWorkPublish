@tool
extends RefCounted

## Stateless asset-grid/card UI helpers. The browser panel retains ownership of
## library state and callbacks while this component owns card construction and
## visual presentation.

static func build_container(is_grid_view: bool, scroll: ScrollContainer) -> Dictionary:
	var grid: Container
	if is_grid_view:
		grid = HFlowContainer.new()
	else:
		grid = VBoxContainer.new()
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(grid)

	var empty_state := Label.new()
	empty_state.text = "Your asset library is empty\n\nAdd a folder containing reusable .tscn scenes to get started."
	empty_state.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	empty_state.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	empty_state.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	empty_state.modulate = Color(1.0, 1.0, 1.0, 0.52)
	empty_state.add_theme_font_size_override("font_size", 16)
	empty_state.custom_minimum_size = Vector2(420.0, 160.0)
	empty_state.visible = false
	grid.add_child(empty_state)
	return {"grid": grid, "empty_state": empty_state}

static func card_style(selected: bool, hovered: bool, accent: Color, base: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	if selected:
		style.bg_color = Color(accent.r, accent.g, accent.b, 0.22)
		style.border_color = Color(accent.r, accent.g, accent.b, 0.95)
		style.set_border_width_all(2)
	elif hovered:
		style.bg_color = Color(1.0, 1.0, 1.0, 0.055)
		style.border_color = Color(1.0, 1.0, 1.0, 0.18)
		style.set_border_width_all(1)
	else:
		style.bg_color = Color(base.r, base.g, base.b, 0.34)
		style.border_color = Color(1.0, 1.0, 1.0, 0.08)
		style.set_border_width_all(1)
	style.corner_radius_top_left = 6
	style.corner_radius_top_right = 6
	style.corner_radius_bottom_left = 6
	style.corner_radius_bottom_right = 6
	style.content_margin_left = 6.0
	style.content_margin_right = 6.0
	style.content_margin_top = 6.0
	style.content_margin_bottom = 5.0
	return style

static func update_card_visual(card: Control, hovered: bool, accent: Color, base: Color) -> void:
	if card == null or not is_instance_valid(card):
		return
	card.add_theme_stylebox_override("panel", card_style(bool(card.get_meta("selected", false)), hovered, accent, base))

static func set_quick_action_visible(card: Control, visible: bool) -> void:
	var action: Variant = card.get_meta("quick_action", null)
	if action is Control and is_instance_valid(action):
		(action as Control).modulate.a = 1.0 if visible else 0.32

static func card_thumbnail(card: Control) -> TextureRect:
	var node: Variant = card.get_meta("thumbnail_node", null)
	if node is TextureRect and is_instance_valid(node):
		return node as TextureRect
	return null

static func set_card_thumbnail(card: Control, texture: Texture2D) -> void:
	var thumbnail := card_thumbnail(card)
	if thumbnail != null:
		thumbnail.texture = texture

static func make_card(
		scene_path: String,
		subfolder: String,
		source_folder: String,
		is_grid_view: bool,
		tile_size: float,
		is_favorite: bool,
		tooltip: String,
		accent: Color,
		base: Color,
		favorite_callback: Callable,
		mouse_entered_callback: Callable,
		mouse_exited_callback: Callable,
		gui_input_callback: Callable
	) -> Control:
	if is_grid_view:
		return _make_grid_card(scene_path, subfolder, tile_size, is_favorite, tooltip, accent, base, favorite_callback, mouse_entered_callback, mouse_exited_callback, gui_input_callback)
	return _make_list_card(scene_path, subfolder, source_folder, is_favorite, tooltip, accent, base, favorite_callback, mouse_entered_callback, mouse_exited_callback, gui_input_callback)

static func _apply_card_meta(card: Control, scene_path: String, subfolder: String, tooltip: String) -> void:
	card.set_meta("scene_path", scene_path)
	card.set_meta("scene_name", scene_path.get_file().get_basename())
	card.set_meta("subfolder", subfolder)
	card.set_meta("selected", false)
	card.tooltip_text = tooltip + "\n\nDouble-click: Open scene\nRight-click: Asset actions\nCtrl/Shift-click: Multi-select"
	card.mouse_filter = Control.MOUSE_FILTER_STOP
	card.focus_mode = Control.FOCUS_ALL

static func _make_favorite_button(scene_path: String, is_favorite: bool, callback: Callable) -> Button:
	var favorite := Button.new()
	favorite.text = "★" if is_favorite else "☆"
	favorite.flat = true
	favorite.tooltip_text = "Add or remove this asset from Favorites"
	favorite.pressed.connect(callback.bind(scene_path, favorite))
	favorite.modulate.a = 1.0 if is_favorite else 0.32
	return favorite

static func _connect_card(card: Control, entered: Callable, exited: Callable, gui_input: Callable) -> void:
	card.mouse_entered.connect(entered.bind(card))
	card.mouse_exited.connect(exited.bind(card))
	card.gui_input.connect(gui_input.bind(card))

static func _make_grid_card(scene_path: String, subfolder: String, tile_size: float, is_favorite: bool, tooltip: String, accent: Color, base: Color, favorite_callback: Callable, entered: Callable, exited: Callable, gui_input: Callable) -> Control:
	var card := PanelContainer.new()
	card.custom_minimum_size = Vector2(tile_size + 12.0, tile_size + 38.0)
	_apply_card_meta(card, scene_path, subfolder, tooltip)
	update_card_visual(card, false, accent, base)

	var content := VBoxContainer.new()
	content.add_theme_constant_override("separation", 4)
	card.add_child(content)

	var thumb := TextureRect.new()
	thumb.custom_minimum_size = Vector2(tile_size, tile_size)
	thumb.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	thumb.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	thumb.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	thumb.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.add_child(thumb)
	card.set_meta("thumbnail_node", thumb)

	var name_row := HBoxContainer.new()
	name_row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.add_child(name_row)

	var label := Label.new()
	label.text = scene_path.get_file().get_basename()
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.clip_text = true
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	name_row.add_child(label)
	card.set_meta("name_label", label)

	var favorite := _make_favorite_button(scene_path, is_favorite, favorite_callback)
	name_row.add_child(favorite)
	card.set_meta("quick_action", favorite)
	_connect_card(card, entered, exited, gui_input)
	return card

static func _make_list_card(scene_path: String, subfolder: String, source_folder: String, is_favorite: bool, tooltip: String, accent: Color, base: Color, favorite_callback: Callable, entered: Callable, exited: Callable, gui_input: Callable) -> Control:
	var card := PanelContainer.new()
	card.custom_minimum_size = Vector2(0.0, 50.0)
	_apply_card_meta(card, scene_path, subfolder, tooltip)
	update_card_visual(card, false, accent, base)

	var content := HBoxContainer.new()
	content.add_theme_constant_override("separation", 8)
	card.add_child(content)

	var thumb := TextureRect.new()
	thumb.custom_minimum_size = Vector2(38.0, 38.0)
	thumb.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	thumb.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	thumb.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.add_child(thumb)
	card.set_meta("thumbnail_node", thumb)

	var label := Label.new()
	label.text = scene_path.get_file().get_basename()
	label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.add_child(label)
	card.set_meta("name_label", label)

	var favorite := _make_favorite_button(scene_path, is_favorite, favorite_callback)
	content.add_child(favorite)
	card.set_meta("quick_action", favorite)

	var path_label := Label.new()
	path_label.text = scene_path.get_base_dir().trim_prefix(source_folder)
	path_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	path_label.modulate = Color(1, 1, 1, 0.45)
	path_label.clip_text = true
	path_label.custom_minimum_size.x = 120.0
	path_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	content.add_child(path_label)

	_connect_card(card, entered, exited, gui_input)
	return card
