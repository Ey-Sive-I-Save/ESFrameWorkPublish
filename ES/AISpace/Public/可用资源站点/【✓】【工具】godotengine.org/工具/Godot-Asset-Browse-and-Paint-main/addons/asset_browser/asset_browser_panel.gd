@tool
extends Control

const ThumbnailGenerator = preload("res://addons/asset_browser/thumbnail_generator.gd")
const BrowserStorage = preload("res://addons/asset_browser/browser_storage.gd")
const BrowserLibraryData = preload("res://addons/asset_browser/browser_library_data.gd")
const AssetGridView = preload("res://addons/asset_browser/asset_grid_view.gd")

var editor_plugin: EditorPlugin

const CACHE_DIR          := "user://asset_browser_cache/"
const SETTINGS_PATH      := "user://asset_browser_settings.cfg"
const PRESET_DIR         := "user://asset_browser_presets/"
const RECENT_LIMIT       := 20
const TILE_SIZE_MIN      := 64.0
const TILE_SIZE_MAX      := 256.0
const TILE_SIZE_DEFAULT  := 150.0

var _asset_folder: String        = "res://assets" # Legacy/current source compatibility
var _asset_folders: Array[String] = ["res://assets"]
var _selected_subfolder: String  = ""   # "" = All added folders; otherwise source path
var _cards: Array                = []
var _selected_cards: Array       = []   # multi-select
var _thumb_gen: ThumbnailGenerator
var _tile_size: float            = TILE_SIZE_DEFAULT
var _is_grid_view: bool          = true

# UI refs
var _grid: Container      # HFlowContainer (grid) or VBoxContainer (list)
var _search: LineEdit
var _folder_label: Label
var _subfolder_list: ItemList
var _remove_folder_btn: Button
var _scroll: ScrollContainer
var _empty_state: Label
var _size_slider: HSlider
var _view_btn: Button
var _paint_btn: Button
var _asset_count_label: Label
var _selection_status_label: Label
var _paint_settings: Control
var _quick_brush_bar: Control
var _quick_context_controls: Dictionary = {}
var _quick_parent_button: Button
var _quick_storage_control: OptionButton
var _quick_tool_control: OptionButton
var _quick_ready_label: Label
var _current_painter_parent_name: String = ""
var _quick_guidance_signature: String = ""
var _paint_status: Label
var _paint_option_controls: Dictionary = {}
var _context_sections: Dictionary = {}
var _syncing_paint_option := false
var _painter_values: Dictionary = {
	"erase_mode": false,
	"reapply_mode": false,
	"reapply_rotation": true,
	"reapply_scale": true,
	"reapply_alignment": false,
	"reapply_offset": false,
	"reapply_replace_variant": false,
	"placement_mode": 0,
	"brush_radius": 2.0,
	"count_per_click": 1,
	"drag_density": 2.0,
	"minimum_spacing": 0.75,
	"align_mode": 0,
	"random_rotation_enabled": true,
	"random_rotation_x_enabled": false,
	"random_rotation_y_enabled": true,
	"random_rotation_z_enabled": false,
	"random_rotation_x": 0.0,
	"random_rotation_y": 180.0,
	"random_rotation_z": 0.0,
	"random_scale_enabled": true,
	"random_scale_min": 0.85,
	"random_scale_max": 1.15,
	"surface_offset": 0.0,
	"minimum_slope_degrees": 0.0,
	"maximum_slope_degrees": 90.0,
	"minimum_height": -100000.0,
	"maximum_height": 100000.0,
	"collision_layer_mask": 4294967295,
	"surface_mode": 1,
	"grid_plane": 0,
	"grid_offset": 0.0,
	"grid_snap": 1.0,
	"grid_snap_mode": 0,
	"grid_custom_snap_x": 1.0,
	"grid_custom_snap_y": 1.0,
	"grid_custom_snap_z": 1.0,
	"grid_rotation_snap_enabled": false,
	"grid_rotation_snap_degrees": 90.0,
	"grid_override_occupied": false,
	"paint_only_selected": false,
	"ignore_painted_assets": true,
	"random_seed": 0,
	"distribution_mode": 0,
	"brush_falloff": 0.0,
	"cluster_count": 3,
	"cluster_strength": 0.65,
	"use_spatial_hash": true,
	"spatial_hash_cell_size": 4.0,
	"multimesh_chunking_enabled": true,
	"multimesh_chunk_world_size": 64.0,
	"multimesh_chunk_instance_limit": 2000,
	"multimesh_visibility_begin": 0.0,
	"multimesh_visibility_end": 0.0,
	"area_tool_mode": 0,
	"area_placement_count": 100,
	"area_max_placements": 5000,
	"path_spacing_mode": 0,
	"path_asset_order": 0,
	"path_spacing_min": 2.0,
	"path_spacing_max": 4.0,
	"path_profile": 0,
	"path_width": 4.0,
	"path_row_count": 3,
	"path_noise": 0.0,
	"path_point_spacing": 0.5,
	"path_smoothing": 0.35,
	"path_align_direction": true,
	"path_create_node": true,
	"path_auto_scatter": true,
	"path_live_update": true,
	"path_update_delay": 0.25,
	"path_max_placements": 500
}
var _preset_picker: OptionButton
var _preset_name: LineEdit
var _preset_file_dialog: EditorFileDialog
var _pending_selected_paths: Array[String] = []

# Phase 4 library organization
var _favorites: Array[String] = []
var _recent_assets: Array[String] = []
var _collections: Dictionary = {}
var _asset_tags: Dictionary = {}
var _asset_placement_bounds: Dictionary = {}
var _library_filter := "all" # all, favorites, recent, collection
var _active_collection := ""
var _library_list: ItemList
var _collection_list: ItemList
var _thumbnail_mode: OptionButton
var _asset_context_menu: PopupMenu
var _asset_context_actions: Dictionary = {}
var _context_asset_paths: Array[String] = []

# Phase 5 weighted variants and ecosystem groups
var _variant_settings: Dictionary = {} # scene_path -> {enabled, weight, category}
var _variant_list: VBoxContainer
var _variant_summary: Label
var _rebuilding_variants := false


func _ready() -> void:
	custom_minimum_size.y = 200
	_load_browser_settings()
	_thumb_gen = ThumbnailGenerator.new()
	add_child(_thumb_gen)
	_ensure_cache_dir()
	_build_ui()
	_apply_loaded_browser_ui()
	_scan_folder()


# ---------------------------------------------------------------------------
# UI construction
# ---------------------------------------------------------------------------

func _build_ui() -> void:
	var root_vbox := VBoxContainer.new()
	root_vbox.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(root_vbox)

	# Compact browser toolbar. Painter controls live in the right inspector pane.
	var topbar := HBoxContainer.new()
	root_vbox.add_child(topbar)

	var title := Label.new()
	title.text = "Asset Browser"
	title.add_theme_font_size_override("font_size", 15)
	title.add_theme_color_override("font_color", _editor_accent_color())
	title.tooltip_text = "Browse, organize, drag, and paint reusable scene assets"
	topbar.add_child(title)

	var separator := VSeparator.new()
	topbar.add_child(separator)

	_folder_label = Label.new()
	_folder_label.text = _asset_folder
	_folder_label.clip_text = true
	_folder_label.custom_minimum_size.x = 110.0
	_folder_label.tooltip_text = "Current folder being scanned for .tscn assets"
	topbar.add_child(_folder_label)

	var refresh_btn := Button.new()
	refresh_btn.text = "↻ Rescan"
	refresh_btn.tooltip_text = "Rescan the asset folder"
	refresh_btn.pressed.connect(_full_refresh)
	topbar.add_child(refresh_btn)

	_paint_btn = Button.new()
	_paint_btn.text = "🖌 Paint Assets"
	_paint_btn.tooltip_text = "Show the Asset Painter tools and paint selected scenes in the 3D viewport"
	_paint_btn.toggle_mode = true
	_paint_btn.toggled.connect(_on_paint_toggled)
	topbar.add_child(_paint_btn)

	_view_btn = Button.new()
	_view_btn.text = "List"
	_view_btn.tooltip_text = "Toggle grid and list views"
	_view_btn.pressed.connect(_toggle_view)
	topbar.add_child(_view_btn)

	_thumbnail_mode = OptionButton.new()
	_thumbnail_mode.add_item("Small")
	_thumbnail_mode.add_item("Medium")
	_thumbnail_mode.add_item("Large")
	_thumbnail_mode.add_item("Custom")
	_thumbnail_mode.tooltip_text = "Choose a thumbnail size preset or use the slider for a custom size"
	_thumbnail_mode.item_selected.connect(_on_thumbnail_mode_selected)
	topbar.add_child(_thumbnail_mode)

	var slider_label := Label.new()
	slider_label.text = "Size"
	slider_label.tooltip_text = "Thumbnail size"
	topbar.add_child(slider_label)

	_size_slider = HSlider.new()
	_size_slider.min_value = TILE_SIZE_MIN
	_size_slider.max_value = TILE_SIZE_MAX
	_size_slider.value = _tile_size
	_size_slider.custom_minimum_size.x = 80
	_size_slider.tooltip_text = "Change the thumbnail size in grid view"
	_size_slider.value_changed.connect(_on_tile_size_changed)
	topbar.add_child(_size_slider)

	_search = LineEdit.new()
	_search.placeholder_text = "Search assets, tags, or paths…"
	_search.custom_minimum_size.x = 220
	_search.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_search.clear_button_enabled = true
	_search.tooltip_text = "Search asset names, full paths, and tags. Press Ctrl+F to focus search."
	_search.text_changed.connect(_filter_cards)
	topbar.add_child(_search)

	# Three-pane workspace: folders | assets | context-sensitive tools.
	var workspace := HSplitContainer.new()
	workspace.size_flags_vertical = Control.SIZE_EXPAND_FILL
	workspace.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	root_vbox.add_child(workspace)

	var library_split := HSplitContainer.new()
	library_split.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	library_split.size_flags_vertical = Control.SIZE_EXPAND_FILL
	library_split.split_offset = 140
	workspace.add_child(library_split)

	# Keep the complete library sidebar inside its own vertical scroll area.
	# Paint mode reduces the workspace height, and without this container the
	# collection controls can spill over Godot's bottom dock tabs.
	var sidebar_scroll := ScrollContainer.new()
	sidebar_scroll.custom_minimum_size.x = 150.0
	sidebar_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	sidebar_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	sidebar_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	library_split.add_child(sidebar_scroll)

	var sidebar_vbox := VBoxContainer.new()
	sidebar_vbox.custom_minimum_size.x = 140.0
	sidebar_vbox.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	sidebar_scroll.add_child(sidebar_vbox)

	sidebar_vbox.add_child(_make_sidebar_section_header("Library"))

	_library_list = ItemList.new()
	_library_list.custom_minimum_size.y = 92.0
	_library_list.tooltip_text = "Show all assets, favorites, or recently used assets"
	_library_list.add_item("All Assets")
	_library_list.set_item_metadata(0, "all")
	_library_list.add_item("Favorites")
	_library_list.set_item_metadata(1, "favorites")
	_library_list.add_item("Recent")
	_library_list.set_item_metadata(2, "recent")
	_library_list.item_selected.connect(_on_library_filter_selected)
	_library_list.select(0)
	sidebar_vbox.add_child(_library_list)

	sidebar_vbox.add_child(_make_sidebar_section_header("Folders"))

	_subfolder_list = ItemList.new()
	_subfolder_list.custom_minimum_size.y = 120.0
	# The whole sidebar scrolls, so this list should not try to consume all
	# remaining height and push the collection buttons outside the dock.
	_subfolder_list.size_flags_vertical = Control.SIZE_SHRINK_BEGIN
	_subfolder_list.tooltip_text = "Filter assets by their top-level subfolder"
	_subfolder_list.item_selected.connect(_on_subfolder_selected)
	sidebar_vbox.add_child(_subfolder_list)

	var folder_buttons := HBoxContainer.new()
	folder_buttons.add_theme_constant_override("separation", 4)
	folder_buttons.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	sidebar_vbox.add_child(folder_buttons)

	var folder_btn := Button.new()
	folder_btn.text = "+"
	folder_btn.tooltip_text = "Add folder"
	folder_btn.custom_minimum_size.x = 32.0
	folder_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	folder_btn.pressed.connect(_pick_folder)
	folder_buttons.add_child(folder_btn)

	_remove_folder_btn = Button.new()
	_remove_folder_btn.text = "−"
	_remove_folder_btn.tooltip_text = "Remove the selected folder from this library"
	_remove_folder_btn.custom_minimum_size.x = 32.0
	_remove_folder_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_remove_folder_btn.disabled = true
	_remove_folder_btn.pressed.connect(_remove_selected_folder)
	folder_buttons.add_child(_remove_folder_btn)

	sidebar_vbox.add_child(_make_sidebar_section_header("Collections"))

	_collection_list = ItemList.new()
	_collection_list.custom_minimum_size.y = 80.0
	_collection_list.tooltip_text = "Show assets in a custom collection"
	_collection_list.item_selected.connect(_on_collection_selected)
	sidebar_vbox.add_child(_collection_list)

	var collection_buttons := HBoxContainer.new()
	collection_buttons.add_theme_constant_override("separation", 4)
	collection_buttons.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	sidebar_vbox.add_child(collection_buttons)
	_add_compact_action_button(collection_buttons, "+", "Create collection", _create_collection)
	_add_compact_action_button(collection_buttons, "✎", "Rename selected collection", _rename_collection)
	_add_compact_action_button(collection_buttons, "×", "Delete selected collection", _delete_collection)
	_add_compact_action_button(collection_buttons, "⊕", "Add selected assets to this collection", _add_selected_to_collection)
	_add_compact_action_button(collection_buttons, "⊖", "Remove selected assets from this collection", _remove_selected_from_collection)

	_refresh_collection_list()

	_scroll = ScrollContainer.new()
	_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	library_split.add_child(_scroll)
	_build_grid_container()

	var status_bar := HBoxContainer.new()
	status_bar.add_theme_constant_override("separation", 10)
	root_vbox.add_child(status_bar)
	var status_spacer := Control.new()
	status_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	status_bar.add_child(status_spacer)
	_asset_count_label = Label.new()
	_asset_count_label.text = "0 assets"
	_asset_count_label.modulate = Color(1, 1, 1, 0.58)
	status_bar.add_child(_asset_count_label)

	_build_paint_settings(root_vbox, workspace)


func _build_paint_settings(root_vbox: VBoxContainer, workspace: HSplitContainer) -> void:
	_build_quick_brush_bar(root_vbox)

	_paint_settings = PanelContainer.new()
	_paint_settings.visible = false
	_paint_settings.custom_minimum_size.x = 280.0
	_paint_settings.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_paint_settings.tooltip_text = "Context-sensitive Asset Painter tools"
	workspace.add_child(_paint_settings)

	var tools_scroll := ScrollContainer.new()
	tools_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	tools_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_paint_settings.add_child(tools_scroll)

	var tools := VBoxContainer.new()
	tools.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tools_scroll.add_child(tools)


	var options := _create_collapsible_section(tools, "Options", false)
	_register_context_section("options", options)
	var selected_only := CheckButton.new()
	selected_only.text = "Selected Surface Only"
	selected_only.tooltip_text = "Only paint on the currently selected surface node"
	selected_only.toggled.connect(func(v: bool): _set_painter_option("paint_only_selected", v))
	_paint_option_controls["paint_only_selected"] = selected_only
	options.add_child(selected_only)

	var ignore_painted := CheckButton.new()
	ignore_painted.text = "Ignore Painted Assets"
	ignore_painted.tooltip_text = "Prevent newly painted assets from becoming paintable surfaces"
	ignore_painted.button_pressed = true
	ignore_painted.toggled.connect(func(v: bool): _set_painter_option("ignore_painted_assets", v))
	_paint_option_controls["ignore_painted_assets"] = ignore_painted
	options.add_child(ignore_painted)

	var distribution := _create_collapsible_section(tools, "Scatter Distribution", false)
	_register_context_section("distribution", distribution)
	var distribution_grid := GridContainer.new()
	distribution_grid.columns = 2
	distribution_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	distribution.add_child(distribution_grid)
	var distribution_mode_control := OptionButton.new()
	distribution_mode_control.add_item("Uniform Random")
	distribution_mode_control.add_item("Blue Noise")
	distribution_mode_control.add_item("Clustered")
	var saved_distribution_mode := int(_painter_values.get("distribution_mode", 0))
	if saved_distribution_mode < 0 or saved_distribution_mode > 2:
		saved_distribution_mode = 0
		_painter_values["distribution_mode"] = 0
	distribution_mode_control.select(saved_distribution_mode)
	distribution_mode_control.tooltip_text = "Choose how placement points are distributed inside the brush"
	distribution_mode_control.item_selected.connect(func(i: int): _set_painter_option("distribution_mode", i))
	_paint_option_controls["distribution_mode"] = distribution_mode_control
	_add_labeled_control(distribution_grid, "Pattern", distribution_mode_control, distribution_mode_control.tooltip_text)
	_add_paint_spin(distribution_grid, "Falloff", 0.0, 1.0, 0.0, 0.05, "brush_falloff")
	_add_paint_spin(distribution_grid, "Clusters", 1.0, 16.0, 3.0, 1.0, "cluster_count")
	_add_paint_spin(distribution_grid, "Cluster Tightness", 0.0, 1.0, 0.65, 0.05, "cluster_strength")
	var distribution_help := Label.new()
	distribution_help.text = "Blue Noise reduces clumps. Falloff thins the outer brush. Cluster settings apply to Clustered mode."
	distribution_help.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	distribution_help.add_theme_font_size_override("font_size", 11)
	distribution.add_child(distribution_help)

	var variants := _create_collapsible_section(tools, "Weighted Variants & Ecosystem", false)
	_register_context_section("variants", variants)
	var seed_grid := GridContainer.new()
	seed_grid.columns = 2
	seed_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	variants.add_child(seed_grid)
	_add_paint_spin(seed_grid, "Random Seed", 0.0, 2147483647.0, 0.0, 1.0, "random_seed")
	var seed_help := Label.new()
	seed_help.text = "0 = new random sequence. Any other value makes scatter repeatable."
	seed_help.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	seed_help.add_theme_font_size_override("font_size", 11)
	seed_help.modulate = Color(1, 1, 1, 0.65)
	variants.add_child(seed_help)
	_variant_summary = Label.new()
	_variant_summary.text = "Select assets to edit their probability and ecosystem group."
	_variant_summary.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	variants.add_child(_variant_summary)
	_variant_list = VBoxContainer.new()
	_variant_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	variants.add_child(_variant_list)
	var normalize_btn := Button.new()
	normalize_btn.text = "Equalize Enabled Weights"
	normalize_btn.tooltip_text = "Set every enabled selected asset to the same probability weight"
	normalize_btn.pressed.connect(_equalize_selected_variant_weights)
	variants.add_child(normalize_btn)
	_rebuild_variant_editor()

	var reapply := _create_collapsible_section(tools, "Advanced Editing: Reapply", false)
	_register_context_section("reapply", reapply)
	var reapply_help := Label.new()
	reapply_help.text = "Choose Reapply as the tool, then brush over existing painted assets. Only enabled properties are changed."
	reapply_help.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	reapply.add_child(reapply_help)
	_add_paint_check(reapply, "Rotation", true, "reapply_rotation", "Rerandomize enabled X/Y/Z rotation axes")
	_add_paint_check(reapply, "Scale", true, "reapply_scale", "Rerandomize scale using Scale Min and Scale Max")
	_add_paint_check(reapply, "Alignment", false, "reapply_alignment", "Realign existing assets to the surface under the brush")
	_add_paint_check(reapply, "Surface Offset", false, "reapply_offset", "Reapply the current Surface Offset")
	_add_paint_check(reapply, "Replace Variant", false, "reapply_replace_variant", "Replace affected assets using the enabled weighted variants")

	var area_tools := _create_collapsible_section(tools, "Area Painting Tools", false)
	_register_context_section("area", area_tools)
	var area_help := Label.new()
	area_help.text = "Rectangle and Lasso use the Tool menu. Fill operations use the current weighted variants, filters, spacing, transform, and Scene/MultiMesh storage mode."
	area_help.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	area_tools.add_child(area_help)
	var area_grid := GridContainer.new()
	area_grid.columns = 2
	area_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	area_tools.add_child(area_grid)
	_add_paint_spin(area_grid, "Safety Limit", 1.0, 100000.0, 5000.0, 1.0, "area_max_placements")
	var area_buttons := GridContainer.new()
	area_buttons.columns = 1
	area_tools.add_child(area_buttons)
	_add_action_button(area_buttons, "Fill Selected Mesh", "Scatter across the triangles of the selected MeshInstance3D", _fill_selected_mesh)
	_add_action_button(area_buttons, "Scatter Inside Selected Area3D", "Scatter within the first enabled CollisionShape3D under the selected Area3D", _fill_selected_area)
	_add_action_button(area_buttons, "Clear Inside Selected Area3D", "Remove painted assets whose origins are inside the selected Area3D", _clear_selected_area)

	var path_tools := _create_collapsible_section(tools, "Surface Path Brush", false)
	_register_context_section("path", path_tools)
	var path_help := Label.new()
	path_help.text = "Choose Surface Path Brush in the Tool menu, then drag directly across scene geometry. A Path3D is created and assets are scattered along it."
	path_help.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	path_tools.add_child(path_help)
	var path_grid := GridContainer.new()
	path_grid.columns = 2
	path_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	path_tools.add_child(path_grid)
	var path_spacing_mode_control := OptionButton.new()
	path_spacing_mode_control.add_item("Fixed")
	path_spacing_mode_control.add_item("Random Range")
	path_spacing_mode_control.tooltip_text = "Use fixed or randomized distance between placements along the path"
	path_spacing_mode_control.item_selected.connect(func(i: int): _set_painter_option("path_spacing_mode", i))
	_paint_option_controls["path_spacing_mode"] = path_spacing_mode_control
	_add_labeled_control(path_grid, "Spacing Mode", path_spacing_mode_control, path_spacing_mode_control.tooltip_text)
	var path_asset_order_control := OptionButton.new()
	path_asset_order_control.add_item("Weighted Random")
	path_asset_order_control.add_item("Alternate Assets")
	path_asset_order_control.tooltip_text = "Choose variants by weight or cycle through them"
	path_asset_order_control.item_selected.connect(func(i: int): _set_painter_option("path_asset_order", i))
	_paint_option_controls["path_asset_order"] = path_asset_order_control
	_add_labeled_control(path_grid, "Asset Order", path_asset_order_control, path_asset_order_control.tooltip_text)
	_add_paint_spin(path_grid, "Spacing Max", 0.01, 1000.0, 4.0, 0.1, "path_spacing_max")
	_add_paint_spin(path_grid, "Draw Point Spacing", 0.05, 20.0, 0.5, 0.05, "path_point_spacing")
	_add_paint_spin(path_grid, "Curve Smoothing", 0.0, 1.0, 0.35, 0.05, "path_smoothing")
	_add_paint_spin(path_grid, "Side Noise", 0.0, 100.0, 0.0, 0.05, "path_noise")
	_add_paint_check(path_tools, "Align to Path Direction", true, "path_align_direction", "Face each asset along the path tangent")
	_add_paint_check(path_tools, "Keep Created Path3D", true, "path_create_node", "Keep the generated SurfacePath node so it can be edited and regenerated")
	_add_paint_check(path_tools, "Scatter When Drawn", true, "path_auto_scatter", "Immediately scatter assets when the mouse button is released")
	_add_paint_check(path_tools, "Live Update Edited Curve", true, "path_live_update", "Automatically regenerate generated assets after editing the SurfacePath curve")
	_add_paint_spin(path_grid, "Update Delay", 0.05, 2.0, 0.25, 0.05, "path_update_delay")
	_add_paint_spin(path_grid, "Asset Safety Limit", 1.0, 10000.0, 500.0, 1.0, "path_max_placements")
	_add_action_button(path_tools, "Regenerate Selected Path", "Refresh the generated assets using the selected SurfacePath's stored settings", _scatter_selected_path)
	_add_action_button(path_tools, "Clear Generated Assets", "Remove generated assets while keeping the selected SurfacePath", _clear_selected_path_generated)
	_add_action_button(path_tools, "Bake Selected Path", "Detach generated assets from the path so they become normal scene content", _bake_selected_surface_path)

	var large_world := _create_collapsible_section(tools, "Advanced Performance", false)
	_register_context_section("large_world", large_world)
	var large_world_help := Label.new()
	large_world_help.text = "Optimize spacing checks and split painted MultiMeshes into manageable spatial chunks for large environments."
	large_world_help.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	large_world.add_child(large_world_help)
	_add_paint_check(large_world, "Use Spatial Hash", true, "use_spatial_hash", "Use nearby spatial cells instead of scanning every painted instance during spacing checks")
	var large_grid := GridContainer.new()
	large_grid.columns = 2
	large_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	large_world.add_child(large_grid)
	_add_paint_spin(large_grid, "Hash Cell Size", 0.25, 1000.0, 4.0, 0.25, "spatial_hash_cell_size")
	_add_paint_check(large_world, "Chunk MultiMeshes", true, "multimesh_chunking_enabled", "Create separate MultiMesh containers by world region for better culling and editing")
	_add_paint_spin(large_grid, "Chunk World Size", 1.0, 10000.0, 64.0, 1.0, "multimesh_chunk_world_size")
	_add_paint_spin(large_grid, "Instances per Chunk", 16.0, 100000.0, 2000.0, 16.0, "multimesh_chunk_instance_limit")
	_add_paint_spin(large_grid, "Visibility Begin", 0.0, 100000.0, 0.0, 1.0, "multimesh_visibility_begin")
	_add_paint_spin(large_grid, "Visibility End", 0.0, 100000.0, 0.0, 1.0, "multimesh_visibility_end")
	var chunk_note := Label.new()
	chunk_note.text = "Visibility End = 0 keeps Godot's normal unlimited visibility. New settings apply to newly created MultiMesh chunks."
	chunk_note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	large_world.add_child(chunk_note)

	var analysis := _create_collapsible_section(tools, "Statistics & Analysis", false)
	_register_context_section("analysis", analysis)
	var analysis_help := Label.new()
	analysis_help.text = "Live scene statistics and compatibility details for the currently selected asset. This section is available only while Paint Assets is active."
	analysis_help.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	analysis.add_child(analysis_help)

	var analysis_actions := GridContainer.new()
	analysis_actions.columns = 1
	analysis_actions.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	analysis.add_child(analysis_actions)

	var analyze_asset_button := _make_build_button("Refresh & Analyze", "Refresh scene statistics and inspect the first selected .tscn asset")
	analysis_actions.add_child(analyze_asset_button)

	var analysis_report := RichTextLabel.new()
	analysis_report.name = "StatisticsAnalysisReport"
	analysis_report.text = "Statistics will appear here while Paint Assets is active."
	analysis_report.fit_content = true
	analysis_report.scroll_active = false
	analysis_report.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	analysis_report.selection_enabled = true
	analysis_report.add_theme_font_size_override("font_size", 11)
	analysis.add_child(analysis_report)

	analyze_asset_button.pressed.connect(func(): _refresh_statistics_analysis(analysis_report, true))

	var presets := _create_collapsible_section(tools, "Brush Presets", false)
	_register_context_section("presets", presets)
	_preset_picker = OptionButton.new()
	_preset_picker.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_preset_picker.tooltip_text = "Choose a saved brush preset"
	_preset_picker.item_selected.connect(_on_preset_selected)
	presets.add_child(_preset_picker)

	_preset_name = LineEdit.new()
	_preset_name.placeholder_text = "Preset name"
	_preset_name.tooltip_text = "Name used when saving or renaming a preset"
	presets.add_child(_preset_name)

	var preset_buttons := GridContainer.new()
	preset_buttons.columns = 2
	presets.add_child(preset_buttons)
	_add_action_button(preset_buttons, "Save", "Save current settings and selected assets", _save_current_preset)
	_add_action_button(preset_buttons, "Load", "Load the selected preset", _load_selected_preset)
	_add_action_button(preset_buttons, "Rename", "Rename the selected preset", _rename_selected_preset)
	_add_action_button(preset_buttons, "Delete", "Delete the selected preset", _delete_selected_preset)
	_add_action_button(preset_buttons, "Export", "Export the selected preset to a .cfg file", _export_selected_preset)
	_add_action_button(preset_buttons, "Import", "Import a shared .cfg brush preset", _import_preset)
	_refresh_preset_list()

	var organization := _create_collapsible_section(tools, "Selected Asset Organization", false)
	_register_context_section("organization", organization)
	var favorite_btn := Button.new()
	favorite_btn.text = "Toggle Favorite"
	favorite_btn.tooltip_text = "Add or remove the currently selected assets from Favorites"
	favorite_btn.pressed.connect(_toggle_selected_favorites)
	organization.add_child(favorite_btn)
	var tags_row := HBoxContainer.new()
	organization.add_child(tags_row)
	var tags_edit := LineEdit.new()
	tags_edit.name = "SelectedTagsEdit"
	tags_edit.placeholder_text = "nature, forest, prop"
	tags_edit.tooltip_text = "Comma-separated tags used by search"
	tags_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	tags_row.add_child(tags_edit)
	var apply_tags := Button.new()
	apply_tags.text = "Apply Tags"
	apply_tags.tooltip_text = "Apply these tags to all selected assets"
	apply_tags.pressed.connect(func(): _apply_tags_to_selected(tags_edit.text))
	tags_row.add_child(apply_tags)

	_update_contextual_painter_sections()



func _build_quick_brush_bar(root_vbox: VBoxContainer) -> void:
	_quick_brush_bar = PanelContainer.new()
	_quick_brush_bar.visible = false
	var quick_style := StyleBoxFlat.new()
	var accent := _editor_accent_color()
	quick_style.bg_color = Color(accent.r, accent.g, accent.b, 0.055)
	quick_style.border_color = Color(accent.r, accent.g, accent.b, 0.24)
	quick_style.set_border_width_all(1)
	quick_style.corner_radius_top_left = 6
	quick_style.corner_radius_top_right = 6
	quick_style.corner_radius_bottom_left = 6
	quick_style.corner_radius_bottom_right = 6
	quick_style.content_margin_left = 8.0
	quick_style.content_margin_right = 8.0
	quick_style.content_margin_top = 6.0
	quick_style.content_margin_bottom = 5.0
	_quick_brush_bar.add_theme_stylebox_override("panel", quick_style)
	root_vbox.add_child(_quick_brush_bar)
	root_vbox.move_child(_quick_brush_bar, 1)

	var quick_vbox := VBoxContainer.new()
	_quick_brush_bar.add_child(quick_vbox)

	var quick_scroll := ScrollContainer.new()
	quick_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_AUTO
	quick_scroll.vertical_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	quick_scroll.custom_minimum_size.y = 60.0
	quick_vbox.add_child(quick_scroll)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	quick_scroll.add_child(row)

	var parent_btn := Button.new()
	_quick_parent_button = parent_btn
	parent_btn.text = "Select Parent"
	parent_btn.tooltip_text = "First, select a Node3D in the scene tree and click here. Painted assets will be placed beneath it."
	parent_btn.custom_minimum_size.x = 105.0
	parent_btn.pressed.connect(_use_selected_painter_parent)
	row.add_child(parent_btn)

	var divider := VSeparator.new()
	divider.custom_minimum_size.x = 10.0
	divider.tooltip_text = "Parent setup is separate from the quick painting controls"
	row.add_child(divider)


	var storage_group := VBoxContainer.new()
	row.add_child(storage_group)
	_quick_context_controls["storage"] = storage_group
	var storage_label := Label.new()
	storage_label.text = "Storage"
	storage_label.add_theme_font_size_override("font_size", 12)
	storage_group.add_child(storage_label)
	var placement_mode := OptionButton.new()
	_quick_storage_control = placement_mode
	placement_mode.add_item("Scene Instances")
	placement_mode.add_item("MultiMesh")
	placement_mode.tooltip_text = "Scene Instances preserve full .tscn behavior. MultiMesh is best for large numbers of simple visual assets."
	placement_mode.item_selected.connect(func(i: int):
		_set_painter_option("placement_mode", i)
		_update_contextual_painter_sections()
		_refresh_quick_guidance()
	)
	_paint_option_controls["placement_mode"] = placement_mode
	placement_mode.custom_minimum_size.x = 150.0
	storage_group.add_child(placement_mode)

	var tool_group := VBoxContainer.new()
	row.add_child(tool_group)
	var tool_label := Label.new()
	tool_label.text = "Tool"
	tool_label.add_theme_font_size_override("font_size", 12)
	tool_group.add_child(tool_label)
	var tool := OptionButton.new()
	_quick_tool_control = tool
	tool.add_item("🖌 Paint")
	tool.add_item("⌫ Erase")
	tool.add_item("⟳ Reapply")
	tool.add_item("▭ Rectangle Scatter")
	tool.add_item("✎ Lasso Scatter")
	tool.add_item("〰 Surface Path Brush")
	tool.tooltip_text = "Choose the active viewport tool"
	tool.item_selected.connect(func(i: int):
		_set_painter_option("erase_mode", i == 1)
		_set_painter_option("reapply_mode", i == 2)
		_set_painter_option("area_tool_mode", 1 if i == 3 else (2 if i == 4 else (3 if i == 5 else 0)))
		_update_contextual_painter_sections()
		_refresh_quick_guidance()
	)
	_paint_option_controls["erase_mode"] = tool
	_paint_option_controls["reapply_mode"] = tool
	_paint_option_controls["area_tool_mode"] = tool
	tool.custom_minimum_size.x = 150.0
	tool_group.add_child(tool)

	var surface_source_group := VBoxContainer.new()
	row.add_child(surface_source_group)
	_quick_context_controls["surface_source"] = surface_source_group
	var surface_source_label := Label.new()
	surface_source_label.text = "Paint On"
	surface_source_label.add_theme_font_size_override("font_size", 12)
	surface_source_group.add_child(surface_source_label)
	var surface_mode_control := OptionButton.new()
	surface_mode_control.add_item("Geometry")
	surface_mode_control.add_item("Geometry + Grid")
	surface_mode_control.add_item("Grid Only")
	surface_mode_control.select(int(_painter_values.get("surface_mode", 1)))
	surface_mode_control.custom_minimum_size.x = 142.0
	surface_mode_control.tooltip_text = "Choose whether the brush paints on scene geometry, falls back to a construction grid, or uses only the grid"
	surface_mode_control.item_selected.connect(func(i: int):
		_set_painter_option("surface_mode", i)
		_update_contextual_painter_sections()
	)
	_paint_option_controls["surface_mode"] = surface_mode_control
	surface_source_group.add_child(surface_mode_control)

	var grid_options_group := HBoxContainer.new()
	row.add_child(grid_options_group)
	_quick_context_controls["grid_options"] = grid_options_group
	var grid_plane_control := OptionButton.new()
	grid_plane_control.add_item("XZ")
	grid_plane_control.add_item("XY")
	grid_plane_control.add_item("YZ")
	grid_plane_control.select(int(_painter_values.get("grid_plane", 0)))
	grid_plane_control.tooltip_text = "Construction plane used for grid painting"
	grid_plane_control.item_selected.connect(func(i: int): _set_painter_option("grid_plane", i))
	_paint_option_controls["grid_plane"] = grid_plane_control
	_add_quick_labeled_control(grid_options_group, "Plane", grid_plane_control, 68.0)
	grid_options_group.add_child(_make_quick_spin_group("Offset", -100000.0, 100000.0, 0.0, 0.1, "grid_offset", 82.0))
	var snap_mode_control := OptionButton.new()
	snap_mode_control.add_item("Grid")
	snap_mode_control.add_item("Asset Bounds")
	snap_mode_control.add_item("Custom")
	snap_mode_control.select(int(_painter_values.get("grid_snap_mode", 0)))
	snap_mode_control.tooltip_text = "Grid uses one world-aligned interval. Asset Bounds locks the world lattice to the first enabled asset's saved placement bounds. Custom uses per-axis intervals."
	snap_mode_control.item_selected.connect(func(i: int):
		_set_painter_option("grid_snap_mode", i)
		_update_contextual_painter_sections()
	)
	_paint_option_controls["grid_snap_mode"] = snap_mode_control
	_add_quick_labeled_control(grid_options_group, "Snap Mode", snap_mode_control, 112.0)
	var asset_bounds_group := _make_asset_placement_bounds_group()
	grid_options_group.add_child(asset_bounds_group)
	_quick_context_controls["grid_asset_bounds"] = asset_bounds_group
	var fixed_snap_group := _make_quick_spin_group("Grid Size", 0.001, 1000.0, 1.0, 0.01, "grid_snap", 76.0)
	grid_options_group.add_child(fixed_snap_group)
	_quick_context_controls["grid_fixed_snap"] = fixed_snap_group
	var custom_snap_group := HBoxContainer.new()
	custom_snap_group.add_child(_make_quick_spin_group("X", 0.001, 1000.0, 1.0, 0.01, "grid_custom_snap_x", 62.0))
	custom_snap_group.add_child(_make_quick_spin_group("Y", 0.001, 1000.0, 1.0, 0.01, "grid_custom_snap_y", 62.0))
	custom_snap_group.add_child(_make_quick_spin_group("Z", 0.001, 1000.0, 1.0, 0.01, "grid_custom_snap_z", 62.0))
	grid_options_group.add_child(custom_snap_group)
	_quick_context_controls["grid_custom_snap"] = custom_snap_group
	var rotation_snap_group := HBoxContainer.new()
	var rotation_snap_check := CheckBox.new()
	rotation_snap_check.text = "Rotation Snap"
	rotation_snap_check.button_pressed = bool(_painter_values.get("grid_rotation_snap_enabled", false))
	rotation_snap_check.tooltip_text = "Quantize randomized rotation while painting on the grid"
	rotation_snap_check.toggled.connect(func(v: bool): _set_painter_option("grid_rotation_snap_enabled", v))
	_paint_option_controls["grid_rotation_snap_enabled"] = rotation_snap_check
	rotation_snap_group.add_child(rotation_snap_check)
	rotation_snap_group.add_child(_make_quick_spin_group("Angle", 1.0, 180.0, 90.0, 1.0, "grid_rotation_snap_degrees", 72.0))
	grid_options_group.add_child(rotation_snap_group)
	var override_occupied_check := CheckBox.new()
	override_occupied_check.text = "Replace Occupied"
	override_occupied_check.button_pressed = bool(_painter_values.get("grid_override_occupied", false))
	override_occupied_check.tooltip_text = "Replace painted assets that occupy the grid space where a new asset is placed"
	override_occupied_check.toggled.connect(func(v: bool): _set_painter_option("grid_override_occupied", v))
	_paint_option_controls["grid_override_occupied"] = override_occupied_check
	grid_options_group.add_child(override_occupied_check)

	var radius_group := _make_quick_spin_group("Radius", 0.1, 100.0, 2.0, 0.1, "brush_radius", 82.0)
	row.add_child(radius_group)
	_quick_context_controls["radius"] = radius_group

	var paint_group := HBoxContainer.new()
	row.add_child(paint_group)
	_quick_context_controls["paint"] = paint_group
	paint_group.add_child(_make_quick_spin_group("Count", 1.0, 100.0, 1.0, 1.0, "count_per_click", 72.0))
	paint_group.add_child(_make_quick_spin_group("Density", 0.1, 50.0, 2.0, 0.1, "drag_density", 78.0))

	var area_group := HBoxContainer.new()
	row.add_child(area_group)
	_quick_context_controls["area"] = area_group
	area_group.add_child(_make_quick_spin_group("Area Count", 1.0, 100000.0, 100.0, 1.0, "area_placement_count", 92.0))

	var spacing_group := _make_quick_spin_group("Spacing", 0.0, 100.0, 0.75, 0.05, "minimum_spacing", 82.0)
	row.add_child(spacing_group)
	_quick_context_controls["spacing"] = spacing_group

	var quick_transform_group := HBoxContainer.new()
	row.add_child(quick_transform_group)
	_quick_context_controls["quick_transform"] = quick_transform_group
	quick_transform_group.add_child(_make_quick_rotation_group())
	quick_transform_group.add_child(_make_quick_scale_group())

	var align := OptionButton.new()
	align.add_item("Surface")
	align.add_item("Upright")
	align.add_item("Blend")
	align.add_item("Custom Up")
	align.tooltip_text = "Choose how painted assets orient to the hit surface"
	align.item_selected.connect(func(i: int): _set_painter_option("align_mode", i))
	_paint_option_controls["align_mode"] = align
	_add_quick_labeled_control(quick_transform_group, "Alignment", align, 105.0)
	quick_transform_group.add_child(_make_quick_spin_group("Surface Offset", -100.0, 100.0, 0.0, 0.01, "surface_offset", 92.0))

	var path_group := HBoxContainer.new()
	row.add_child(path_group)
	_quick_context_controls["path"] = path_group
	var path_profile := OptionButton.new()
	path_profile.add_item("Single Line")
	path_profile.add_item("Double Line")
	path_profile.add_item("Corridor")
	path_profile.add_item("Rows")
	path_profile.tooltip_text = "Choose how assets are arranged around the drawn surface path"
	path_profile.item_selected.connect(func(i: int): _set_painter_option("path_profile", i))
	_paint_option_controls["path_profile"] = path_profile
	_add_quick_labeled_control(path_group, "Profile", path_profile, 112.0)
	path_group.add_child(_make_quick_spin_group("Spacing", 0.01, 1000.0, 2.0, 0.1, "path_spacing_min", 82.0))
	path_group.add_child(_make_quick_spin_group("Width", 0.0, 1000.0, 4.0, 0.1, "path_width", 78.0))
	path_group.add_child(_make_quick_spin_group("Rows", 1.0, 32.0, 3.0, 1.0, "path_row_count", 68.0))

	var filters_group := _make_quick_surface_filters_group()
	row.add_child(filters_group)
	_quick_context_controls["filters"] = filters_group

	var footer := HBoxContainer.new()
	footer.add_theme_constant_override("separation", 8)
	footer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	quick_vbox.add_child(footer)

	_quick_ready_label = Label.new()
	_quick_ready_label.text = "⚠ Select a parent and assets"
	_quick_ready_label.add_theme_font_size_override("font_size", 12)
	_quick_ready_label.size_flags_horizontal = Control.SIZE_SHRINK_BEGIN
	footer.add_child(_quick_ready_label)

	var footer_spacer := Control.new()
	footer_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	footer.add_child(footer_spacer)

	var shortcuts := Label.new()
	shortcuts.text = "⌨  [ ] Radius   Shift+[ ] Density   Ctrl+[ ] Count   Alt+[ ] Spacing   Shift+Paint Erase   Ctrl+Paint Precise   Esc Exit"
	shortcuts.tooltip_text = shortcuts.text
	shortcuts.add_theme_font_size_override("font_size", 14)
	shortcuts.modulate = Color(1.0, 1.0, 1.0, 0.72)
	footer.add_child(shortcuts)
	_refresh_quick_guidance.call_deferred()


func _add_quick_labeled_control(parent: Container, label_text: String, control: Control, minimum_width: float) -> void:
	var group := VBoxContainer.new()
	var label := Label.new()
	label.text = label_text
	label.add_theme_font_size_override("font_size", 12)
	group.add_child(label)
	control.custom_minimum_size.x = minimum_width
	group.add_child(control)
	parent.add_child(group)


func _make_quick_spin_group(label_text: String, min_value: float, max_value: float, value: float, step: float, option_name: String, minimum_width: float) -> VBoxContainer:
	var group := VBoxContainer.new()
	var label := Label.new()
	label.text = label_text
	label.add_theme_font_size_override("font_size", 12)
	group.add_child(label)
	var spin := SpinBox.new()
	spin.min_value = min_value
	spin.max_value = max_value
	spin.value = value
	spin.step = step
	spin.custom_minimum_size.x = minimum_width
	var quick_descriptions := {
		"brush_radius": "Brush radius in world units. Hotkeys: [ and ]",
		"count_per_click": "Assets attempted per click. Hotkeys: Ctrl + [ and Ctrl + ]",
		"drag_density": "Placement frequency while dragging. Hotkeys: Shift + [ and Shift + ]",
		"minimum_spacing": "Minimum distance between placements. Hotkeys: Alt + [ and Alt + ]",
		"surface_offset": "Moves placements along the surface normal",
		"area_placement_count": "Requested number of assets for rectangle and lasso operations",
		"path_spacing_min": "Distance between placements along the path",
		"path_width": "Width of corridor, double-line, or row path profiles",
		"path_row_count": "Number of parallel rows generated by the Rows profile",
		"grid_offset": "Position of the construction plane along its perpendicular axis",
		"grid_snap": "Grid snapping interval. Set to 0 for continuous placement",
	}
	spin.tooltip_text = str(quick_descriptions.get(option_name, label_text))
	if option_name in ["brush_radius", "minimum_spacing", "surface_offset", "path_spacing_min", "path_width", "grid_offset", "grid_snap"]:
		spin.suffix = " m"
	elif option_name == "drag_density":
		spin.suffix = " /m"
	spin.value_changed.connect(func(v: float):
		if not _syncing_paint_option:
			_set_painter_option(option_name, v)
	)
	_paint_option_controls[option_name] = spin
	group.add_child(spin)
	return group


func _make_quick_rotation_group() -> VBoxContainer:
	var group := VBoxContainer.new()
	var label := Label.new()
	label.text = "Rotation"
	label.add_theme_font_size_override("font_size", 12)
	group.add_child(label)

	var row := HBoxContainer.new()
	group.add_child(row)
	var enabled := CheckButton.new()
	enabled.text = ""
	enabled.button_pressed = bool(_painter_values.get("random_rotation_enabled", true))
	enabled.tooltip_text = "Enable randomized rotation for placed assets"
	enabled.toggled.connect(func(value: bool): _set_painter_option("random_rotation_enabled", value))
	_paint_option_controls["random_rotation_enabled"] = enabled
	row.add_child(enabled)

	var options_button := Button.new()
	options_button.text = "Axis ▾"
	options_button.custom_minimum_size.x = 82.0
	options_button.tooltip_text = "Choose randomized rotation axes and angle ranges"
	# Opening Axis automatically enables rotation, reducing an unnecessary click.
	row.add_child(options_button)

	var popup := PopupPanel.new()
	_quick_brush_bar.add_child(popup)
	var margin := MarginContainer.new()
	margin.custom_minimum_size = Vector2(285.0, 150.0)
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_bottom", 8)
	popup.add_child(margin)
	var grid := GridContainer.new()
	grid.columns = 2
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	margin.add_child(grid)
	_add_rotation_axis_controls(grid, "X", false, 0.0, "random_rotation_x_enabled", "random_rotation_x")
	_add_rotation_axis_controls(grid, "Y", true, 180.0, "random_rotation_y_enabled", "random_rotation_y")
	_add_rotation_axis_controls(grid, "Z", false, 0.0, "random_rotation_z_enabled", "random_rotation_z")
	options_button.pressed.connect(func():
		if not enabled.button_pressed:
			enabled.button_pressed = true
			_set_painter_option("random_rotation_enabled", true)
		var screen_pos := options_button.get_screen_position() + Vector2(0.0, options_button.size.y + 2.0)
		popup.popup(Rect2i(Vector2i(screen_pos), Vector2i(285, 150)))
	)
	return group


func _make_quick_scale_group() -> VBoxContainer:
	var group := VBoxContainer.new()
	var label := Label.new()
	label.text = "Scale"
	label.add_theme_font_size_override("font_size", 12)
	group.add_child(label)

	var row := HBoxContainer.new()
	group.add_child(row)
	var enabled := CheckButton.new()
	enabled.text = ""
	enabled.button_pressed = bool(_painter_values.get("random_scale_enabled", true))
	enabled.tooltip_text = "Enable randomized scale for placed assets"
	enabled.toggled.connect(func(value: bool): _set_painter_option("random_scale_enabled", value))
	_paint_option_controls["random_scale_enabled"] = enabled
	row.add_child(enabled)

	var options_button := Button.new()
	options_button.text = "Range ▾"
	options_button.custom_minimum_size.x = 88.0
	options_button.tooltip_text = "Set the minimum and maximum randomized scale"
	# Opening Range automatically enables scale, reducing an unnecessary click.
	row.add_child(options_button)

	var popup := PopupPanel.new()
	_quick_brush_bar.add_child(popup)
	var margin := MarginContainer.new()
	margin.custom_minimum_size = Vector2(245.0, 110.0)
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_bottom", 8)
	popup.add_child(margin)
	var grid := GridContainer.new()
	grid.columns = 2
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	margin.add_child(grid)
	_add_paint_spin(grid, "Minimum", 0.01, 10.0, 0.85, 0.01, "random_scale_min")
	_add_paint_spin(grid, "Maximum", 0.01, 10.0, 1.15, 0.01, "random_scale_max")
	options_button.pressed.connect(func():
		if not enabled.button_pressed:
			enabled.button_pressed = true
			_set_painter_option("random_scale_enabled", true)
		var screen_pos := options_button.get_screen_position() + Vector2(0.0, options_button.size.y + 2.0)
		popup.popup(Rect2i(Vector2i(screen_pos), Vector2i(245, 110)))
	)
	return group


func _make_quick_surface_filters_group() -> VBoxContainer:
	var group := VBoxContainer.new()
	var label := Label.new()
	label.text = "Surface"
	label.add_theme_font_size_override("font_size", 12)
	group.add_child(label)

	var button := Button.new()
	button.text = "Filters ▾"
	button.custom_minimum_size.x = 96.0
	button.tooltip_text = "Set slope, height, and collision-layer filters for placement surfaces"
	group.add_child(button)

	var popup := PopupPanel.new()
	_quick_brush_bar.add_child(popup)
	var margin := MarginContainer.new()
	margin.custom_minimum_size = Vector2(310.0, 220.0)
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_bottom", 8)
	popup.add_child(margin)
	var grid := GridContainer.new()
	grid.columns = 2
	grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	margin.add_child(grid)
	_add_paint_spin(grid, "Min Slope", 0.0, 90.0, 0.0, 1.0, "minimum_slope_degrees")
	_add_paint_spin(grid, "Max Slope", 0.0, 90.0, 90.0, 1.0, "maximum_slope_degrees")
	_add_paint_spin(grid, "Min Height", -100000.0, 100000.0, -100000.0, 0.5, "minimum_height")
	_add_paint_spin(grid, "Max Height", -100000.0, 100000.0, 100000.0, 0.5, "maximum_height")
	_add_paint_spin(grid, "Layer Mask", 1.0, 4294967295.0, 4294967295.0, 1.0, "collision_layer_mask")
	button.pressed.connect(func():
		var screen_pos := button.get_screen_position() + Vector2(0.0, button.size.y + 2.0)
		popup.popup(Rect2i(Vector2i(screen_pos), Vector2i(310, 220)))
	)
	return group



func _make_asset_placement_bounds_group() -> VBoxContainer:
	var group := VBoxContainer.new()
	var label := Label.new()
	label.text = "Placement Bounds"
	label.add_theme_font_size_override("font_size", 12)
	group.add_child(label)
	var button := Button.new()
	button.text = "Bounds ▾"
	button.custom_minimum_size.x = 92.0
	button.tooltip_text = "Override the modular placement size for the selected asset. This can differ from its visual mesh bounds."
	group.add_child(button)

	var popup := PopupPanel.new()
	_quick_brush_bar.add_child(popup)
	var margin := MarginContainer.new()
	margin.custom_minimum_size = Vector2(320.0, 205.0)
	margin.add_theme_constant_override("margin_left", 10)
	margin.add_theme_constant_override("margin_top", 8)
	margin.add_theme_constant_override("margin_right", 10)
	margin.add_theme_constant_override("margin_bottom", 8)
	popup.add_child(margin)
	var content := VBoxContainer.new()
	margin.add_child(content)
	var asset_label := Label.new()
	asset_label.text = "Select one asset"
	asset_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	content.add_child(asset_label)
	var note := Label.new()
	note.text = "Placement bounds control edge-to-edge spacing and are saved per asset."
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	note.modulate = Color(1, 1, 1, 0.7)
	content.add_child(note)
	var grid := GridContainer.new()
	grid.columns = 2
	content.add_child(grid)
	var spins: Dictionary = {}
	for axis_name in ["X", "Y", "Z"]:
		var axis_label := Label.new()
		axis_label.text = axis_name
		grid.add_child(axis_label)
		var spin := SpinBox.new()
		spin.min_value = 0.001
		spin.max_value = 1000.0
		spin.step = 0.001
		spin.custom_minimum_size.x = 170.0
		spin.suffix = " m"
		grid.add_child(spin)
		spins[axis_name] = spin
	var buttons := HBoxContainer.new()
	content.add_child(buttons)
	var measure_button := Button.new()
	measure_button.text = "Measure Asset"
	measure_button.tooltip_text = "Measure the selected asset mesh and use that result as its editable placement bounds"
	buttons.add_child(measure_button)
	var clear_button := Button.new()
	clear_button.text = "Clear Override"
	clear_button.tooltip_text = "Remove the saved manual placement bounds for this asset"
	buttons.add_child(clear_button)

	var updating := {"value": false}
	var active_path := {"value": ""}
	var refresh_popup := func() -> void:
		updating["value"] = true
		var paths: Array[String] = get_selected_scene_paths()
		var path: String = ""
		if not paths.is_empty():
			path = paths[0]
		active_path["value"] = path
		if path.is_empty():
			asset_label.text = "Select one asset"
			for spin_value in spins.values():
				(spin_value as SpinBox).editable = false
				(spin_value as SpinBox).set_value_no_signal(1.0)
			measure_button.disabled = true
			clear_button.disabled = true
		else:
			asset_label.text = path.get_file().get_basename()
			var measured := _measure_asset_placement_bounds(path)
			var saved: Vector3 = measured
			if _asset_placement_bounds.has(path):
				var stored = _asset_placement_bounds[path]
				if stored is Vector3:
					saved = stored
				elif stored is Array and (stored as Array).size() >= 3:
					saved = Vector3(float(stored[0]), float(stored[1]), float(stored[2]))
			(spins["X"] as SpinBox).set_value_no_signal(maxf(0.001, saved.x))
			(spins["Y"] as SpinBox).set_value_no_signal(maxf(0.001, saved.y))
			(spins["Z"] as SpinBox).set_value_no_signal(maxf(0.001, saved.z))
			for spin_value in spins.values():
				(spin_value as SpinBox).editable = true
			measure_button.disabled = false
			clear_button.disabled = not _asset_placement_bounds.has(path)
		updating["value"] = false

	var save_values := func() -> void:
		if bool(updating["value"]):
			return
		var path := str(active_path["value"])
		if path.is_empty():
			return
		var value := Vector3(
			maxf(0.001, (spins["X"] as SpinBox).value),
			maxf(0.001, (spins["Y"] as SpinBox).value),
			maxf(0.001, (spins["Z"] as SpinBox).value)
		)
		_asset_placement_bounds[path] = value
		_save_browser_settings()
		_sync_asset_placement_bounds_to_painter()
		clear_button.disabled = false
	for spin_value in spins.values():
		(spin_value as SpinBox).value_changed.connect(func(_v: float): save_values.call())
	measure_button.pressed.connect(func():
		var path := str(active_path["value"])
		if path.is_empty():
			return
		_asset_placement_bounds[path] = _measure_asset_placement_bounds(path)
		_save_browser_settings()
		_sync_asset_placement_bounds_to_painter()
		refresh_popup.call()
	)
	clear_button.pressed.connect(func():
		var path := str(active_path["value"])
		if path.is_empty():
			return
		_asset_placement_bounds.erase(path)
		_save_browser_settings()
		_sync_asset_placement_bounds_to_painter()
		refresh_popup.call()
	)
	button.pressed.connect(func():
		refresh_popup.call()
		var screen_pos := button.get_screen_position() + Vector2(0.0, button.size.y + 2.0)
		popup.popup(Rect2i(Vector2i(screen_pos), Vector2i(320, 205)))
	)
	return group


func _measure_asset_placement_bounds(path: String) -> Vector3:
	var packed := load(path) as PackedScene
	if packed == null:
		return Vector3.ONE
	var root := packed.instantiate()
	if not root is Node3D:
		root.free()
		return Vector3.ONE
	var combined := AABB()
	var has_bounds := false
	var mesh_nodes: Array[Node] = []
	if root is MeshInstance3D:
		mesh_nodes.append(root)
	mesh_nodes.append_array(root.find_children("*", "MeshInstance3D", true, false))
	for node in mesh_nodes:
		var mesh_node := node as MeshInstance3D
		if mesh_node == null or mesh_node.mesh == null:
			continue
		var relative := _panel_node_transform_relative_to_root(mesh_node, root as Node3D)
		var transformed := relative * mesh_node.mesh.get_aabb()
		if not has_bounds:
			combined = transformed
			has_bounds = true
		else:
			combined = combined.merge(transformed)
	root.free()
	if not has_bounds:
		return Vector3.ONE
	var size := combined.size.abs()
	return Vector3(maxf(0.001, size.x), maxf(0.001, size.y), maxf(0.001, size.z))


func _panel_node_transform_relative_to_root(node: Node3D, root: Node3D) -> Transform3D:
	if node == root:
		return Transform3D.IDENTITY
	var result := node.transform
	var current := node.get_parent()
	while current != null and current != root:
		if current is Node3D:
			result = (current as Node3D).transform * result
		current = current.get_parent()
	return result


func _sync_asset_placement_bounds_to_painter() -> void:
	if editor_plugin and editor_plugin.has_method("set_asset_painter_option"):
		editor_plugin.set_asset_painter_option("asset_placement_bounds", _asset_placement_bounds.duplicate(true))

func _set_quick_group_visible(group_key: String, is_visible: bool) -> void:
	var group: Control = _quick_context_controls.get(group_key) as Control
	if group != null and is_instance_valid(group):
		group.visible = is_visible


func _create_collapsible_section(parent: VBoxContainer, title_text: String, expanded: bool) -> VBoxContainer:
	var section := VBoxContainer.new()
	section.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(section)
	var header := Button.new()
	header.alignment = HORIZONTAL_ALIGNMENT_LEFT
	header.flat = false
	header.custom_minimum_size.y = 30.0
	var display_title := _section_icon(title_text) + "  " + title_text
	header.text = ("▼ " if expanded else "▶ ") + display_title
	header.tooltip_text = "Show or hide %s controls" % title_text
	var accent := _editor_accent_color()
	header.add_theme_color_override("font_color", accent.lightened(0.18))
	header.add_theme_color_override("font_hover_color", accent.lightened(0.32))
	var normal_style := StyleBoxFlat.new()
	normal_style.bg_color = Color(accent.r, accent.g, accent.b, 0.11)
	normal_style.corner_radius_top_left = 4
	normal_style.corner_radius_top_right = 4
	normal_style.corner_radius_bottom_left = 4
	normal_style.corner_radius_bottom_right = 4
	normal_style.content_margin_left = 8.0
	normal_style.content_margin_right = 8.0
	normal_style.content_margin_top = 4.0
	normal_style.content_margin_bottom = 4.0
	var hover_style := normal_style.duplicate() as StyleBoxFlat
	hover_style.bg_color = Color(accent.r, accent.g, accent.b, 0.2)
	header.add_theme_stylebox_override("normal", normal_style)
	header.add_theme_stylebox_override("hover", hover_style)
	header.add_theme_stylebox_override("pressed", hover_style)
	section.add_child(header)
	var content := VBoxContainer.new()
	content.visible = expanded
	content.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	section.add_child(content)
	content.set_meta("collapsible_section_root", section)
	header.pressed.connect(func():
		content.visible = not content.visible
		header.text = ("▼ " if content.visible else "▶ ") + display_title
	)
	return content


func _section_icon(title_text: String) -> String:
	var icons := {
		"Transform": "↻",
		"Surface Filters": "⛰",
		"Options": "⚙",
		"Scatter Distribution": "✣",
		"Weighted Variants & Ecosystem": "♣",
		"Advanced Editing: Reapply": "⟳",
		"Area Painting Tools": "▱",
		"Surface Path Brush": "〰",
		"Advanced Performance": "⚡",
		"Statistics & Analysis": "▥",
		"Brush Presets": "★",
		"Asset Organization": "▤"
	}
	return str(icons.get(title_text, "◆"))


func _editor_accent_color() -> Color:
	var settings := EditorInterface.get_editor_settings()
	if settings != null:
		var value: Variant = settings.get_setting("interface/theme/accent_color")
		if value is Color:
			return value
	return Color(0.35, 0.65, 1.0)


func _make_sidebar_section_header(title_text: String) -> PanelContainer:
	var header := PanelContainer.new()
	header.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.mouse_filter = Control.MOUSE_FILTER_IGNORE

	var accent := _editor_accent_color()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(accent.r, accent.g, accent.b, 0.12)
	style.border_color = Color(accent.r, accent.g, accent.b, 0.28)
	style.border_width_bottom = 1
	style.corner_radius_top_left = 3
	style.corner_radius_top_right = 3
	style.corner_radius_bottom_left = 3
	style.corner_radius_bottom_right = 3
	style.content_margin_left = 7.0
	style.content_margin_right = 7.0
	style.content_margin_top = 4.0
	style.content_margin_bottom = 4.0
	header.add_theme_stylebox_override("panel", style)

	var label := Label.new()
	label.text = title_text
	label.add_theme_font_size_override("font_size", 14)
	label.add_theme_color_override("font_color", get_theme_color("font_color", "Label"))
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	header.add_child(label)
	return header


func _register_context_section(section_key: String, content: Control) -> void:
	var section_root: Control = content.get_meta("collapsible_section_root", content) as Control
	_context_sections[section_key] = section_root


func _set_context_section_visible(section_key: String, is_visible: bool) -> void:
	var section_root: Control = _context_sections.get(section_key) as Control
	if section_root != null and is_instance_valid(section_root):
		section_root.visible = is_visible


func _current_painter_tool_index() -> int:
	var area_mode := int(_painter_values.get("area_tool_mode", 0))
	if area_mode == 1:
		return 3
	if area_mode == 2:
		return 4
	if area_mode == 3:
		return 5
	if bool(_painter_values.get("reapply_mode", false)):
		return 2
	if bool(_painter_values.get("erase_mode", false)):
		return 1
	return 0


func _update_contextual_painter_sections() -> void:
	if _context_sections.is_empty():
		return
	var tool_index := _current_painter_tool_index()
	var is_paint := tool_index == 0
	var is_erase := tool_index == 1
	var is_reapply := tool_index == 2
	var is_area := tool_index == 3 or tool_index == 4
	var is_path := tool_index == 5
	var uses_placement_settings := is_paint or is_reapply or is_area or is_path

	_set_quick_group_visible("storage", not is_erase)
	_set_quick_group_visible("surface_source", true)
	_set_quick_group_visible("grid_options", int(_painter_values.get("surface_mode", 1)) != 0)
	var current_grid_snap_mode := int(_painter_values.get("grid_snap_mode", 0))
	_set_quick_group_visible("grid_fixed_snap", current_grid_snap_mode == 0)
	_set_quick_group_visible("grid_asset_bounds", current_grid_snap_mode == 1)
	_set_quick_group_visible("grid_custom_snap", current_grid_snap_mode == 2)
	_set_quick_group_visible("radius", is_paint or is_erase or is_reapply)
	_set_quick_group_visible("paint", is_paint)
	_set_quick_group_visible("area", is_area)
	_set_quick_group_visible("spacing", is_paint or is_area)
	_set_quick_group_visible("quick_transform", uses_placement_settings)
	_set_quick_group_visible("path", is_path)
	var uses_geometry_surface := int(_painter_values.get("surface_mode", 1)) != 2
	_set_quick_group_visible("filters", (is_paint or is_area or is_path) and uses_geometry_surface)

	_set_context_section_visible("options", true)
	_set_context_section_visible("distribution", is_paint or is_area)
	_set_context_section_visible("variants", not is_erase)
	_set_context_section_visible("reapply", is_reapply)
	_set_context_section_visible("area", is_area)
	_set_context_section_visible("path", is_path)
	_set_context_section_visible("large_world", int(_painter_values.get("placement_mode", 0)) == 1 and not is_erase)
	_set_context_section_visible("analysis", true)
	_set_context_section_visible("presets", true)
	_set_context_section_visible("organization", true)



func _add_labeled_control(parent: GridContainer, label_text: String, control: Control, tooltip: String) -> void:
	var label := Label.new()
	label.text = label_text
	label.tooltip_text = tooltip
	parent.add_child(label)
	control.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	parent.add_child(control)


func _add_action_button(parent: Container, text: String, tooltip: String, callable: Callable) -> void:
	var button := Button.new()
	button.text = text
	button.tooltip_text = tooltip
	button.pressed.connect(callable)
	parent.add_child(button)


func _add_compact_action_button(parent: Container, symbol: String, tooltip: String, callable: Callable) -> void:
	var button := Button.new()
	button.text = symbol
	button.tooltip_text = tooltip
	button.custom_minimum_size.x = 28.0
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	button.pressed.connect(callable)
	parent.add_child(button)


func _make_build_button(text: String, tooltip: String) -> Button:
	var button := Button.new()
	button.text = text
	button.tooltip_text = tooltip
	button.custom_minimum_size = Vector2(0, 34)
	button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	return button


func _fill_selected_mesh() -> void:
	if editor_plugin != null and editor_plugin.has_method("fill_selected_asset_painter_mesh"):
		editor_plugin.fill_selected_asset_painter_mesh()

func _fill_selected_area() -> void:
	if editor_plugin != null and editor_plugin.has_method("fill_selected_asset_painter_area"):
		editor_plugin.fill_selected_asset_painter_area()

func _clear_selected_area() -> void:
	if editor_plugin != null and editor_plugin.has_method("clear_selected_asset_painter_area"):
		editor_plugin.clear_selected_asset_painter_area()

func _scatter_selected_path() -> void:
	if editor_plugin != null and editor_plugin.has_method("scatter_selected_asset_painter_path"):
		editor_plugin.scatter_selected_asset_painter_path()

func _clear_selected_path_generated() -> void:
	if editor_plugin != null and editor_plugin.has_method("clear_selected_asset_painter_path"):
		editor_plugin.clear_selected_asset_painter_path()

func _bake_selected_surface_path() -> void:
	if editor_plugin != null and editor_plugin.has_method("bake_selected_asset_painter_path"):
		editor_plugin.bake_selected_asset_painter_path()

func _add_paint_spin(parent: Container, label_text: String, min_value: float, max_value: float, value: float, step: float, option_name: String) -> void:
	var descriptions := {
		"brush_radius": "World-space radius of the paint brush. Hotkeys: [ and ]",
		"count_per_click": "Number of assets attempted per click. Hotkeys: Ctrl + [ and Ctrl + ]",
		"drag_density": "How often assets are placed while dragging. Hotkeys: Shift + [ and Shift + ]",
		"minimum_spacing": "Minimum world-space distance between painted assets. Hotkeys: Alt + [ and Alt + ]",
		"surface_offset": "Move placed assets along the surface normal",
		"minimum_slope_degrees": "Minimum allowed surface slope, where 0 is flat and 90 is vertical",
		"maximum_slope_degrees": "Maximum allowed surface slope, where 0 is flat and 90 is vertical",
		"minimum_height": "Minimum allowed world-space Y height",
		"maximum_height": "Maximum allowed world-space Y height",
		"collision_layer_mask": "Collision and visibility layer mask used to choose paintable surfaces",
		"random_scale_min": "Smallest random scale used for each placed asset",
		"random_scale_max": "Largest random scale used for each placed asset",
		"random_seed": "0 creates a new random sequence. A positive seed makes weighted scatter repeatable.",
		"brush_falloff": "Reduces placement probability near the outside edge of the brush.",
		"cluster_count": "Number of local groups created in Clustered distribution mode.",
		"cluster_strength": "How tightly assets gather around each cluster center.",
		"spatial_hash_cell_size": "World-space size of cells used for fast spacing lookups.",
		"multimesh_chunk_world_size": "World-space width and depth of each MultiMesh region.",
		"multimesh_chunk_instance_limit": "Maximum instances stored in one MultiMesh chunk before another chunk is created.",
		"multimesh_visibility_begin": "Distance where newly created MultiMesh chunks begin becoming visible.",
		"multimesh_visibility_end": "Distance where newly created MultiMesh chunks stop being visible. Zero means unlimited.",
		"path_spacing_min": "Fixed spacing, or the minimum spacing in Random Range mode.",
		"path_spacing_max": "Maximum spacing in Random Range mode.",
	}
	var description: String = str(descriptions.get(option_name, label_text))
	var spin := SpinBox.new()
	spin.tooltip_text = description
	spin.min_value = min_value
	spin.max_value = max_value
	spin.step = step
	spin.value = value
	if option_name in ["minimum_slope_degrees", "maximum_slope_degrees"]:
		spin.suffix = " deg"
	elif option_name in ["minimum_height", "maximum_height", "surface_offset", "minimum_spacing", "brush_radius", "path_spacing_min", "path_spacing_max", "path_width"]:
		spin.suffix = " m"
	elif option_name in ["random_scale_min", "random_scale_max"]:
		spin.suffix = "×"
	spin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	spin.value_changed.connect(func(v: float):
		if not _syncing_paint_option:
			_set_painter_option(option_name, v)
	)
	_paint_option_controls[option_name] = spin
	if parent is GridContainer:
		_add_labeled_control(parent as GridContainer, label_text, spin, description)
	else:
		parent.add_child(spin)


func _add_paint_check(parent: Container, label: String, enabled: bool, option_name: String, tooltip: String) -> void:
	var check := CheckButton.new()
	check.text = label
	check.button_pressed = enabled
	check.tooltip_text = tooltip
	check.toggled.connect(func(value: bool): _set_painter_option(option_name, value))
	_paint_option_controls[option_name] = check
	parent.add_child(check)


func _add_rotation_axis_controls(parent: Container, axis_name: String, enabled: bool, degrees: float, enabled_option: String, degrees_option: String) -> void:
	var row := HBoxContainer.new()
	var toggle := CheckButton.new()
	toggle.text = axis_name
	toggle.tooltip_text = "Enable random rotation around the local %s axis" % axis_name
	toggle.button_pressed = enabled
	toggle.toggled.connect(func(value: bool): _set_painter_option(enabled_option, value))
	_paint_option_controls[enabled_option] = toggle
	row.add_child(toggle)
	var spin := SpinBox.new()
	spin.min_value = 0.0
	spin.max_value = 180.0
	spin.step = 1.0
	spin.value = degrees
	spin.suffix = " deg"
	spin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	spin.tooltip_text = "Maximum random rotation in either direction"
	spin.value_changed.connect(func(value: float):
		if not _syncing_paint_option:
			_set_painter_option(degrees_option, value)
	)
	_paint_option_controls[degrees_option] = spin
	row.add_child(spin)
	if parent is GridContainer:
		_add_labeled_control(parent as GridContainer, "Rotation " + axis_name, row, "Randomize local %s-axis rotation" % axis_name)
	else:
		parent.add_child(row)


func _set_painter_option(option_name: String, value: Variant) -> void:
	_painter_values[option_name] = value
	_save_browser_settings()
	if editor_plugin and editor_plugin.has_method("set_asset_painter_option"):
		editor_plugin.set_asset_painter_option(option_name, value)
	if option_name == "erase_mode" or option_name == "reapply_mode" or option_name == "area_tool_mode" or option_name == "placement_mode" or option_name == "surface_mode" or option_name == "grid_snap_mode":
		_update_contextual_painter_sections()

func sync_painter_option(option_name: String, value: Variant) -> void:
	_painter_values[option_name] = value
	_save_browser_settings()
	var control: Control = _paint_option_controls.get(option_name) as Control
	if control == null:
		return
	_syncing_paint_option = true
	if control is SpinBox:
		(control as SpinBox).value = float(value)
	elif control is OptionButton:
		if option_name == "erase_mode" or option_name == "reapply_mode" or option_name == "area_tool_mode":
			var erase_value := bool(_painter_values.get("erase_mode", false))
			var reapply_value := bool(_painter_values.get("reapply_mode", false))
			var area_value := int(_painter_values.get("area_tool_mode", 0))
			(control as OptionButton).select(3 if area_value == 1 else (4 if area_value == 2 else (5 if area_value == 3 else (2 if reapply_value else (1 if erase_value else 0)))))
		else:
			(control as OptionButton).select(int(value))
	elif control is BaseButton:
		(control as BaseButton).set_pressed_no_signal(bool(value))
	_syncing_paint_option = false
	if option_name == "erase_mode" or option_name == "reapply_mode" or option_name == "area_tool_mode" or option_name == "placement_mode" or option_name == "surface_mode" or option_name == "grid_snap_mode":
		_update_contextual_painter_sections()

func _use_selected_painter_parent() -> void:
	if editor_plugin and editor_plugin.has_method("use_selected_asset_painter_parent"):
		editor_plugin.use_selected_asset_painter_parent()

func set_painter_status(text: String) -> void:
	if _paint_status:
		_paint_status.text = text
	var marker := "Parent: "
	var marker_index := text.find(marker)
	if marker_index >= 0:
		var start := marker_index + marker.length()
		var end := text.find(" |", start)
		_current_painter_parent_name = text.substr(start, end - start if end >= 0 else text.length() - start)
		if _current_painter_parent_name == "None":
			_current_painter_parent_name = ""
	_refresh_quick_guidance()

func _make_toolbar_style(color: Color, alpha: float = 0.9) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(color.r, color.g, color.b, alpha)
	style.border_color = color.lightened(0.18)
	style.set_border_width_all(1)
	style.corner_radius_top_left = 5
	style.corner_radius_top_right = 5
	style.corner_radius_bottom_left = 5
	style.corner_radius_bottom_right = 5
	style.content_margin_left = 8.0
	style.content_margin_right = 8.0
	style.content_margin_top = 4.0
	style.content_margin_bottom = 4.0
	return style

func _apply_colored_control(control: Control, color: Color, strong: bool = false) -> void:
	if control == null or not is_instance_valid(control):
		return
	var normal := _make_toolbar_style(color, 0.82 if strong else 0.28)
	var hover := _make_toolbar_style(color.lightened(0.12), 0.94 if strong else 0.42)
	control.add_theme_stylebox_override("normal", normal)
	control.add_theme_stylebox_override("hover", hover)
	control.add_theme_stylebox_override("pressed", hover)
	control.add_theme_color_override("font_color", Color(1, 1, 1))
	control.add_theme_color_override("font_hover_color", Color(1, 1, 1))

func _refresh_quick_guidance() -> void:
	if _quick_parent_button == null or not is_instance_valid(_quick_parent_button):
		return
	var has_parent := not _current_painter_parent_name.is_empty()
	var selected_count := get_selected_scene_paths().size()
	var storage_index := _quick_storage_control.selected if _quick_storage_control != null and is_instance_valid(_quick_storage_control) else 0
	var tool_index_value := _quick_tool_control.selected if _quick_tool_control != null and is_instance_valid(_quick_tool_control) else 0
	var signature := "%s|%d|%d|%d" % [_current_painter_parent_name, selected_count, storage_index, tool_index_value]
	if signature == _quick_guidance_signature:
		return
	_quick_guidance_signature = signature
	if has_parent:
		_quick_parent_button.text = "✓ Parent: %s" % _current_painter_parent_name
		_quick_parent_button.tooltip_text = "Painted assets will be placed beneath %s. Select another Node3D and click to change it." % _current_painter_parent_name
		_apply_colored_control(_quick_parent_button, Color(0.20, 0.68, 0.34), true)
	else:
		_quick_parent_button.text = "Select Parent"
		_quick_parent_button.tooltip_text = "Required: select a Node3D in the scene tree, then click here."
		_apply_colored_control(_quick_parent_button, Color(0.95, 0.55, 0.12), true)


	if _quick_storage_control != null and is_instance_valid(_quick_storage_control):
		var storage_color := Color(0.48, 0.34, 0.86) if _quick_storage_control.selected == 1 else Color(0.20, 0.50, 0.86)
		_apply_colored_control(_quick_storage_control, storage_color)

	if _quick_tool_control != null and is_instance_valid(_quick_tool_control):
		var tool_colors := [Color(0.18, 0.55, 0.95), Color(0.86, 0.24, 0.22), Color(0.60, 0.32, 0.88), Color(0.20, 0.70, 0.38), Color(0.95, 0.55, 0.15), Color(0.10, 0.72, 0.78)]
		var tool_index := clampi(_quick_tool_control.selected, 0, tool_colors.size() - 1)
		_apply_colored_control(_quick_tool_control, tool_colors[tool_index])

	if _quick_ready_label != null and is_instance_valid(_quick_ready_label):
		if has_parent and selected_count > 0:
			_quick_ready_label.text = "● Ready to Paint"
			_quick_ready_label.modulate = Color(0.42, 1.0, 0.56)
		elif not has_parent and selected_count == 0:
			_quick_ready_label.text = "⚠ Select a parent and assets"
			_quick_ready_label.modulate = Color(1.0, 0.68, 0.25)
		elif not has_parent:
			_quick_ready_label.text = "⚠ Select a parent"
			_quick_ready_label.modulate = Color(1.0, 0.68, 0.25)
		else:
			_quick_ready_label.text = "⚠ Select assets"
			_quick_ready_label.modulate = Color(1.0, 0.68, 0.25)

func _build_grid_container() -> void:
	if _grid and is_instance_valid(_grid):
		_grid.queue_free()

	var built: Dictionary = AssetGridView.build_container(_is_grid_view, _scroll)
	_grid = built.get("grid") as Container
	_empty_state = built.get("empty_state") as Label


# ---------------------------------------------------------------------------
# Scanning
# ---------------------------------------------------------------------------

func _scan_folder() -> void:
	_selected_cards.clear()
	_build_grid_container()
	_cards.clear()

	var all_scenes: Array[String] = []
	var seen: Dictionary = {}
	for source_folder in _asset_folders:
		var source_scenes: Array[String] = []
		_collect_scenes(source_folder, source_scenes)
		for scene_path in source_scenes:
			if not seen.has(scene_path):
				seen[scene_path] = true
				all_scenes.append(scene_path)
	all_scenes.sort()
	_rebuild_subfolder_list(all_scenes)

	for path in all_scenes:
		_add_card(path)

	_update_folder_summary_label()
	_apply_visibility()
	_restore_pending_asset_selection()


func _collect_scenes(folder: String, result: Array[String]) -> void:
	var dir := DirAccess.open(folder)
	if dir == null:
		return
	dir.list_dir_begin()
	var f := dir.get_next()
	while f != "":
		var full := folder.path_join(f)
		if dir.current_is_dir():
			if not f.begins_with("."):
				_collect_scenes(full, result)
		elif f.ends_with(".tscn"):
			result.append(full)
		f = dir.get_next()
	dir.list_dir_end()


func _rebuild_subfolder_list(_scenes: Array[String]) -> void:
	_subfolder_list.clear()
	_subfolder_list.add_item("📂  All Folders")
	_subfolder_list.set_item_metadata(0, "")
	_subfolder_list.set_item_tooltip(0, "Show assets from every added folder")

	for source_folder in _asset_folders:
		var display_name := source_folder.trim_suffix("/").get_file()
		if display_name == "":
			display_name = source_folder
		var idx := _subfolder_list.item_count
		_subfolder_list.add_item("📁  " + display_name)
		_subfolder_list.set_item_metadata(idx, source_folder)
		_subfolder_list.set_item_tooltip(idx, source_folder + " (includes subfolders)")

	var select_index := 0
	if _selected_subfolder != "":
		for i in range(1, _subfolder_list.item_count):
			if str(_subfolder_list.get_item_metadata(i)) == _selected_subfolder:
				select_index = i
				break
	_subfolder_list.select(select_index)
	if select_index == 0:
		_selected_subfolder = ""


func _on_subfolder_selected(index: int) -> void:
	_selected_subfolder = str(_subfolder_list.get_item_metadata(index))
	_asset_folder = _selected_subfolder if _selected_subfolder != "" else (_asset_folders[0] if not _asset_folders.is_empty() else "res://assets")
	_update_folder_summary_label()
	_update_folder_actions()
	_save_browser_settings()
	_apply_visibility()


func _source_folder_for_scene(scene_path: String) -> String:
	var best := ""
	for source_folder in _asset_folders:
		var normalized := source_folder.trim_suffix("/")
		if scene_path == normalized or scene_path.begins_with(normalized + "/"):
			if normalized.length() > best.length():
				best = normalized
	return best


func _update_folder_summary_label() -> void:
	if not _folder_label:
		return
	if _selected_subfolder != "":
		_folder_label.text = _selected_subfolder
	elif _asset_folders.size() == 1:
		_folder_label.text = _asset_folders[0]
	else:
		_folder_label.text = "%d folders" % _asset_folders.size()


func _update_folder_actions() -> void:
	if _remove_folder_btn == null:
		return
	_remove_folder_btn.disabled = _selected_subfolder.is_empty()
	_remove_folder_btn.tooltip_text = "Select a source folder in the sidebar before removing it" if _selected_subfolder.is_empty() else "Remove %s from this library (files are not deleted)" % _selected_subfolder


# ---------------------------------------------------------------------------
# Asset card visual polish
# ---------------------------------------------------------------------------

func _card_style(selected: bool, hovered: bool = false) -> StyleBoxFlat:
	return AssetGridView.card_style(selected, hovered, get_theme_color("accent_color", "Editor"), get_theme_color("base_color", "Editor"))

func _update_card_visual(card: Control, hovered: bool = false) -> void:
	AssetGridView.update_card_visual(card, hovered, get_theme_color("accent_color", "Editor"), get_theme_color("base_color", "Editor"))

func _on_card_mouse_entered(card: Control) -> void:
	_update_card_visual(card, true)
	_set_card_quick_actions_visible(card, true)

func _on_card_mouse_exited(card: Control) -> void:
	_update_card_visual(card, false)
	_set_card_quick_actions_visible(card, bool(card.get_meta("selected", false)))

func _set_card_quick_actions_visible(card: Control, visible: bool) -> void:
	AssetGridView.set_quick_action_visible(card, visible)

func _card_thumbnail(card: Control) -> TextureRect:
	return AssetGridView.card_thumbnail(card)

# ---------------------------------------------------------------------------
# Cards
# ---------------------------------------------------------------------------

func _add_card(scene_path: String) -> void:
	var card := _make_card(scene_path)
	_grid.add_child(card)
	_cards.append(card)

	var cached := _load_cached_thumbnail(scene_path)
	if cached:
		_set_card_thumbnail(card, cached)
	else:
		_thumb_gen.generate_for_scene(scene_path, self, "_on_thumb_done")

func _on_thumb_done(scene_path: String, texture: Texture2D) -> void:
	if texture == null:
		return
	_save_cached_thumbnail(scene_path, texture)
	for card in _cards:
		if not is_instance_valid(card):
			continue
		if card.get_meta("scene_path", "") == scene_path:
			_set_card_thumbnail(card, texture)
			return

func _set_card_thumbnail(card: Control, tex: Texture2D) -> void:
	AssetGridView.set_card_thumbnail(card, tex)

func _make_card(scene_path: String) -> Control:
	var source_folder := _source_folder_for_scene(scene_path)
	return AssetGridView.make_card(
		scene_path,
		source_folder,
		source_folder,
		_is_grid_view,
		_tile_size,
		_favorites.has(scene_path),
		_asset_tooltip(scene_path),
		get_theme_color("accent_color", "Editor"),
		get_theme_color("base_color", "Editor"),
		_toggle_favorite,
		_on_card_mouse_entered,
		_on_card_mouse_exited,
		_on_card_gui_input
	)

func _make_grid_card(scene_path: String, subfolder: String) -> Control:
	return AssetGridView.make_card(scene_path, subfolder, _source_folder_for_scene(scene_path), true, _tile_size, _favorites.has(scene_path), _asset_tooltip(scene_path), get_theme_color("accent_color", "Editor"), get_theme_color("base_color", "Editor"), _toggle_favorite, _on_card_mouse_entered, _on_card_mouse_exited, _on_card_gui_input)

func _make_list_card(scene_path: String, subfolder: String) -> Control:
	return AssetGridView.make_card(scene_path, subfolder, _source_folder_for_scene(scene_path), false, _tile_size, _favorites.has(scene_path), _asset_tooltip(scene_path), get_theme_color("accent_color", "Editor"), get_theme_color("base_color", "Editor"), _toggle_favorite, _on_card_mouse_entered, _on_card_mouse_exited, _on_card_gui_input)

func _apply_card_meta(card: Control, scene_path: String, subfolder: String) -> void:
	# Compatibility wrapper retained for callers outside the extracted grid component.
	card.set_meta("scene_path", scene_path)
	card.set_meta("scene_name", scene_path.get_file().get_basename())
	card.set_meta("subfolder", subfolder)
	card.set_meta("selected", false)
	card.tooltip_text = _asset_tooltip(scene_path) + "\n\nDouble-click: Open scene\nRight-click: Asset actions\nCtrl/Shift-click: Multi-select"
	card.mouse_filter = Control.MOUSE_FILTER_STOP
	card.focus_mode = Control.FOCUS_ALL


# ---------------------------------------------------------------------------
# Selection
# ---------------------------------------------------------------------------

func _set_card_selected(card: Control, selected: bool) -> void:
	card.set_meta("selected", selected)
	if selected:
		if not _selected_cards.has(card):
			_selected_cards.append(card)
	else:
		_selected_cards.erase(card)
	_update_card_visual(card)
	_set_card_quick_actions_visible(card, selected or _favorites.has(str(card.get_meta("scene_path", ""))))
	var name_label: Variant = card.get_meta("name_label", null)
	if name_label is Label and is_instance_valid(name_label):
		(name_label as Label).add_theme_color_override("font_color", _editor_accent_color() if selected else get_theme_color("font_color", "Label"))
	_update_selection_status()
	if not _rebuilding_variants:
		_rebuild_variant_editor.call_deferred()
	_refresh_quick_guidance.call_deferred()


func _update_selection_status() -> void:
	if _selection_status_label == null or not is_instance_valid(_selection_status_label):
		return
	var count := _selected_cards.size()
	_selection_status_label.text = "No assets selected" if count == 0 else ("1 asset selected" if count == 1 else "%d assets selected" % count)
	_selection_status_label.modulate = Color(1, 1, 1, 0.58) if count == 0 else Color(_editor_accent_color().r, _editor_accent_color().g, _editor_accent_color().b, 1.0)

func _clear_selection() -> void:
	for card in _selected_cards.duplicate():
		_set_card_selected(card, false)
	_selected_cards.clear()



# ---------------------------------------------------------------------------
# Weighted variants / ecosystem groups
# ---------------------------------------------------------------------------

func _default_variant_setting() -> Dictionary:
	return BrowserLibraryData.default_variant_setting()

func _get_variant_setting(path: String) -> Dictionary:
	return BrowserLibraryData.variant_setting(_variant_settings, path)

func _rebuild_variant_editor() -> void:
	if _variant_list == null or not is_instance_valid(_variant_list):
		return
	_rebuilding_variants = true
	for child in _variant_list.get_children():
		child.queue_free()
	var paths := get_selected_scene_paths()
	if paths.is_empty():
		if _variant_summary:
			_variant_summary.text = "Select assets to edit their probability and ecosystem group."
		_rebuilding_variants = false
		return
	var enabled_count := 0
	var total_weight := 0.0
	for path in paths:
		var setting := _get_variant_setting(path)
		if bool(setting["enabled"]):
			enabled_count += 1
			total_weight += float(setting["weight"])
		var card := VBoxContainer.new()
		card.tooltip_text = path
		_variant_list.add_child(card)
		var top := HBoxContainer.new()
		card.add_child(top)
		var enabled := CheckButton.new()
		enabled.button_pressed = bool(setting["enabled"])
		enabled.tooltip_text = "Include this asset when painting"
		top.add_child(enabled)
		var name_label := Label.new()
		name_label.text = path.get_file().get_basename()
		name_label.tooltip_text = path
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		name_label.clip_text = true
		top.add_child(name_label)
		var weight := SpinBox.new()
		weight.min_value = 0.0
		weight.max_value = 10000.0
		weight.step = 0.1
		weight.value = float(setting["weight"])
		weight.custom_minimum_size.x = 78.0
		weight.tooltip_text = "Relative probability. A weight of 2 is twice as likely as a weight of 1."
		top.add_child(weight)
		var category := LineEdit.new()
		category.text = str(setting["category"])
		category.placeholder_text = "Ecosystem group"
		category.tooltip_text = "Organize variants into groups such as Trees, Ground Cover, Rocks, or Details"
		category.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		card.add_child(category)
		var spacing_row := HBoxContainer.new()
		card.add_child(spacing_row)
		var spacing_label := Label.new()
		spacing_label.text = "Min Spacing"
		spacing_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		spacing_row.add_child(spacing_label)
		var asset_spacing := SpinBox.new()
		asset_spacing.min_value = 0.0
		asset_spacing.max_value = 100.0
		asset_spacing.step = 0.05
		asset_spacing.value = float(setting.get("minimum_spacing", 0.0))
		asset_spacing.tooltip_text = "Minimum spacing for this asset. 0 uses the global brush spacing."
		asset_spacing.custom_minimum_size.x = 90.0
		spacing_row.add_child(asset_spacing)
		enabled.toggled.connect(_on_variant_enabled_changed.bind(path))
		weight.value_changed.connect(_on_variant_weight_changed.bind(path))
		asset_spacing.value_changed.connect(_on_variant_spacing_changed.bind(path))
		category.text_submitted.connect(_on_variant_category_submitted.bind(path))
		category.focus_exited.connect(_on_variant_category_focus_exited.bind(category, path))
	if _variant_summary:
		_variant_summary.text = "%d enabled of %d selected · Total weight %.1f" % [enabled_count, paths.size(), total_weight]
	_rebuilding_variants = false

func _on_variant_enabled_changed(value: bool, path: String) -> void:
	_set_variant_value(path, "enabled", value)

func _on_variant_weight_changed(value: float, path: String) -> void:
	_set_variant_value(path, "weight", value)

func _on_variant_spacing_changed(value: float, path: String) -> void:
	_set_variant_value(path, "minimum_spacing", value)

func _on_variant_category_submitted(value: String, path: String) -> void:
	_set_variant_value(path, "category", value)

func _on_variant_category_focus_exited(control: LineEdit, path: String) -> void:
	if is_instance_valid(control):
		_set_variant_value(path, "category", control.text)

func _set_variant_value(path: String, key: String, value: Variant) -> void:
	var setting := _get_variant_setting(path)
	match key:
		"enabled": setting[key] = bool(value)
		"weight": setting[key] = maxf(0.0, float(value))
		"minimum_spacing": setting[key] = maxf(0.0, float(value))
		"category":
			var text := str(value).strip_edges()
			setting[key] = "General" if text.is_empty() else text
	_variant_settings[path] = setting
	_save_browser_settings()
	_rebuild_variant_editor.call_deferred()

func _equalize_selected_variant_weights() -> void:
	for path in get_selected_scene_paths():
		var setting := _get_variant_setting(path)
		if bool(setting.get("enabled", true)):
			setting["weight"] = 1.0
			_variant_settings[path] = setting
	_save_browser_settings()
	_rebuild_variant_editor()

func get_weighted_scene_entries() -> Array[Dictionary]:
	return BrowserLibraryData.weighted_entries(get_selected_scene_paths(), _variant_settings)

func _apply_visibility() -> void:
	var query := _search.text.to_lower().strip_edges() if _search else ""
	for card in _cards:
		if not is_instance_valid(card):
			continue
		var scene_path: String = str(card.get_meta("scene_path", ""))
		var scene_name: String = str(card.get_meta("scene_name", "")).to_lower()
		var tags_text: String = _tags_for_path(scene_path).to_lower()
		var searchable: String = scene_name + " " + scene_path.to_lower() + " " + tags_text
		var query_match: bool = query.is_empty() or searchable.contains(query)
		var card_folder: String = str(card.get_meta("subfolder", ""))
		var folder_match: bool = _selected_subfolder == "" or card_folder == _selected_subfolder
		var library_match := true
		match _library_filter:
			"favorites": library_match = _favorites.has(scene_path)
			"recent": library_match = _recent_assets.has(scene_path)
			"collection": library_match = _collection_paths(_active_collection).has(scene_path)
		card.visible = query_match and folder_match and library_match
	_update_empty_state()


func _update_empty_state() -> void:
	if _empty_state == null or not is_instance_valid(_empty_state):
		return
	var visible_count := 0
	for card in _cards:
		if is_instance_valid(card) and card.visible:
			visible_count += 1
	_empty_state.visible = visible_count == 0
	if _asset_count_label != null and is_instance_valid(_asset_count_label):
		var total_count := _cards.size()
		_asset_count_label.text = "%d shown · %d total" % [visible_count, total_count] if visible_count != total_count else ("1 asset" if total_count == 1 else "%d assets" % total_count)
	if visible_count > 0:
		return
	var query := _search.text.strip_edges() if _search else ""
	if not query.is_empty():
		_empty_state.text = "No assets match ‘%s’
Clear the search or try another folder." % query
	elif _library_filter == "favorites":
		_empty_state.text = "No favorite assets yet
Right-click an asset to add it to Favorites."
	elif _library_filter == "recent":
		_empty_state.text = "No recent assets yet
Select or paint an asset and it will appear here."
	elif _library_filter == "collection":
		_empty_state.text = "This collection is empty
Right-click selected assets to add them here."
	else:
		_empty_state.text = "Your asset library is empty

Click + Add Folder and choose a folder containing reusable .tscn scenes."

func _filter_cards(_query: String) -> void:
	_save_browser_settings()
	_apply_visibility()


# ---------------------------------------------------------------------------
# View toggle and tile size
# ---------------------------------------------------------------------------

func _toggle_view() -> void:
	_is_grid_view = not _is_grid_view
	_view_btn.text = "⊞" if _is_grid_view else "☰"
	_size_slider.visible = _is_grid_view
	# Rebuild all cards in the new style
	_save_browser_settings()
	_scan_folder()


func _on_tile_size_changed(value: float) -> void:
	_tile_size = value
	_sync_thumbnail_mode()
	_save_browser_settings()
	for card in _cards:
		if not is_instance_valid(card):
			continue
		if _is_grid_view:
			card.custom_minimum_size = Vector2(_tile_size + 12.0, _tile_size + 38.0)
			var thumb := _card_thumbnail(card)
			if thumb:
				thumb.custom_minimum_size = Vector2(_tile_size, _tile_size)


# ---------------------------------------------------------------------------
# Input — drag, double-click, multi-select
# ---------------------------------------------------------------------------

func _unhandled_key_input(event: InputEvent) -> void:
	if not (event is InputEventKey) or not event.pressed or event.echo:
		return
	var key := event as InputEventKey
	if key.ctrl_pressed and key.keycode == KEY_F:
		_search.grab_focus()
		_search.select_all()
		get_viewport().set_input_as_handled()
	elif key.keycode == KEY_F5:
		_full_refresh()
		get_viewport().set_input_as_handled()
	elif key.keycode == KEY_ESCAPE and _search != null and not _search.text.is_empty():
		_search.clear()
		get_viewport().set_input_as_handled()

func _on_card_gui_input(event: InputEvent, card: Control) -> void:
	if event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ENTER or event.keycode == KEY_KP_ENTER:
			var scene_path: String = str(card.get_meta("scene_path", ""))
			if not scene_path.is_empty():
				EditorInterface.open_scene_from_path(scene_path)
			accept_event()
			return
		elif event.keycode == KEY_SPACE:
			_clear_selection()
			_set_card_selected(card, true)
			accept_event()
			return

	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT and event.pressed:
		if not bool(card.get_meta("selected", false)):
			_clear_selection()
			_set_card_selected(card, true)
		_show_asset_context_menu(card)
		accept_event()
		return

	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			# Double-click: open scene
			if event.double_click:
				var scene_path: String = card.get_meta("scene_path", "")
				if not scene_path.is_empty():
					EditorInterface.open_scene_from_path(scene_path)
				return

			# Shift/Ctrl-click: toggle this card in/out of selection
			if event.shift_pressed or event.ctrl_pressed:
				_set_card_selected(card, not card.get_meta("selected", false))
			else:
				# Plain click: clear selection and select only this card
				_clear_selection()
				_set_card_selected(card, true)

			_mark_recent(str(card.get_meta("scene_path", "")))
			_save_browser_settings()
			card.set_meta("drag_start_pos", get_global_mouse_position())

		else:
			card.set_meta("dragging", false)

	if event is InputEventMouseMotion and Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT):
		if not card.has_meta("drag_start_pos"):
			return
		var dist: float = get_global_mouse_position().distance_to(card.get_meta("drag_start_pos"))
		if dist > 5.0 and not card.get_meta("dragging", false):
			card.set_meta("dragging", true)
			_begin_drag(card)


func _begin_drag(card: Control) -> void:
	# Drag all selected cards; if this card isn't selected, drag just it
	var drag_cards: Array = _selected_cards.duplicate() \
		if _selected_cards.has(card) else [card]

	var paths: Array[String] = []
	for c in drag_cards:
		var p: String = c.get_meta("scene_path", "")
		if not p.is_empty():
			paths.append(p)

	if paths.is_empty():
		return

	# Build drag preview
	var preview := VBoxContainer.new()

	# Show first thumbnail
	var first_card: Control = drag_cards[0]
	var thumb_src := _card_thumbnail(first_card)
	if thumb_src and thumb_src.texture:
		var pv_tex := TextureRect.new()
		pv_tex.texture = thumb_src.texture
		pv_tex.custom_minimum_size = Vector2(80, 80)
		pv_tex.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		pv_tex.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		preview.add_child(pv_tex)

	var pv_lbl := Label.new()
	pv_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	if paths.size() == 1:
		pv_lbl.text = paths[0].get_file().get_basename()
	else:
		pv_lbl.text = "%d scenes" % paths.size()
	preview.add_child(pv_lbl)

	card.force_drag({"type": "files", "files": paths}, preview)



# ---------------------------------------------------------------------------
# Asset card context menu
# ---------------------------------------------------------------------------

func _ensure_asset_context_menu() -> void:
	if _asset_context_menu != null and is_instance_valid(_asset_context_menu):
		return
	_asset_context_menu = PopupMenu.new()
	_asset_context_menu.name = "AssetContextMenu"
	_asset_context_menu.id_pressed.connect(_on_asset_context_action)
	add_child(_asset_context_menu)

func _show_asset_context_menu(card: Control) -> void:
	_ensure_asset_context_menu()
	_context_asset_paths = get_selected_scene_paths()
	if _context_asset_paths.is_empty():
		var fallback_path := str(card.get_meta("scene_path", ""))
		if not fallback_path.is_empty():
			_context_asset_paths.append(fallback_path)

	_asset_context_menu.clear()
	_asset_context_actions.clear()
	var all_favorites := not _context_asset_paths.is_empty()
	for path in _context_asset_paths:
		if not _favorites.has(path):
			all_favorites = false
			break

	_asset_context_menu.add_item("Remove from Favorites" if all_favorites else "Add to Favorites", 1)
	_asset_context_menu.add_item("Edit Tags…", 2)
	_asset_context_menu.add_item("Copy Asset Path%s" % ("s" if _context_asset_paths.size() > 1 else ""), 3)
	_asset_context_menu.add_separator()

	var collection_names: Array[String] = []
	for key in _collections.keys():
		collection_names.append(str(key))
	collection_names.sort()
	var action_id := 1000
	if collection_names.is_empty():
		_asset_context_menu.add_item("Add to Collection → New Collection…", 4)
	else:
		for collection_name in collection_names:
			_asset_context_menu.add_item("Add to Collection → %s" % collection_name, action_id)
			_asset_context_actions[action_id] = {"action": "add_collection", "collection": collection_name}
			action_id += 1
		_asset_context_menu.add_item("Add to Collection → New Collection…", 4)

	if not _active_collection.is_empty() and _collections.has(_active_collection):
		_asset_context_menu.add_item("Remove from ‘%s’" % _active_collection, 5)

	_asset_context_menu.position = Vector2i(get_viewport().get_mouse_position())
	_asset_context_menu.popup()

func _on_asset_context_action(id: int) -> void:
	match id:
		1:
			_toggle_context_favorites()
		2:
			_edit_context_tags()
		3:
			DisplayServer.clipboard_set("\n".join(_context_asset_paths))
		4:
			_create_collection_for_paths(_context_asset_paths)
		5:
			_remove_paths_from_collection(_active_collection, _context_asset_paths)
		_:
			var action: Dictionary = _asset_context_actions.get(id, {})
			if str(action.get("action", "")) == "add_collection":
				_add_paths_to_collection(str(action.get("collection", "")), _context_asset_paths)

func _toggle_context_favorites() -> void:
	if _context_asset_paths.is_empty():
		return
	var all_favorites := true
	for path in _context_asset_paths:
		if not _favorites.has(path):
			all_favorites = false
			break
	for path in _context_asset_paths:
		if all_favorites:
			_favorites.erase(path)
		elif not _favorites.has(path):
			_favorites.append(path)
	_save_browser_settings()
	_scan_folder()

func _edit_context_tags() -> void:
	if _context_asset_paths.is_empty():
		return
	var dialog := ConfirmationDialog.new()
	dialog.title = "Edit Asset Tags"
	var edit := LineEdit.new()
	edit.placeholder_text = "nature, modular, wall"
	if _context_asset_paths.size() == 1:
		edit.text = _tags_for_path(_context_asset_paths[0])
	dialog.add_child(edit)
	dialog.confirmed.connect(func():
		var tags := edit.text.strip_edges()
		for path in _context_asset_paths:
			_asset_tags[path] = tags
		_save_browser_settings()
		_apply_visibility()
		dialog.queue_free()
	)
	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered(Vector2i(420, 120))
	edit.grab_focus()

func _add_paths_to_collection(collection_name: String, paths_to_add: Array[String]) -> void:
	if collection_name.is_empty() or not _collections.has(collection_name):
		return
	_collections[collection_name] = BrowserLibraryData.add_unique_paths(_collection_paths(collection_name), paths_to_add)
	_save_browser_settings()
	_apply_visibility()

func _remove_paths_from_collection(collection_name: String, paths_to_remove: Array[String]) -> void:
	if collection_name.is_empty() or not _collections.has(collection_name):
		return
	_collections[collection_name] = BrowserLibraryData.remove_paths(_collection_paths(collection_name), paths_to_remove)
	_save_browser_settings()
	_apply_visibility()

func _create_collection_for_paths(paths_to_add: Array[String]) -> void:
	var dialog := ConfirmationDialog.new()
	dialog.title = "New Asset Collection"
	var edit := LineEdit.new()
	edit.placeholder_text = "Collection name"
	dialog.add_child(edit)
	dialog.confirmed.connect(func():
		var collection_name := edit.text.strip_edges()
		if not collection_name.is_empty():
			if not _collections.has(collection_name):
				_collections[collection_name] = []
			_add_paths_to_collection(collection_name, paths_to_add)
			_refresh_collection_list(collection_name)
		dialog.queue_free()
	)
	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered(Vector2i(360, 120))
	edit.grab_focus()

# ---------------------------------------------------------------------------
# Phase 4: library organization
# ---------------------------------------------------------------------------

func _on_library_filter_selected(index: int) -> void:
	_library_filter = str(_library_list.get_item_metadata(index))
	_active_collection = ""
	if _collection_list:
		_collection_list.deselect_all()
	_apply_visibility()

func _on_collection_selected(index: int) -> void:
	_active_collection = str(_collection_list.get_item_metadata(index))
	_library_filter = "collection"
	if _library_list:
		_library_list.deselect_all()
	_apply_visibility()

func _refresh_collection_list(select_name: String = "") -> void:
	if _collection_list == null:
		return
	_collection_list.clear()
	var names: Array[String] = []
	for key in _collections.keys():
		names.append(str(key))
	names.sort()
	for name in names:
		var index := _collection_list.item_count
		_collection_list.add_item(name)
		_collection_list.set_item_metadata(index, name)
		if name == select_name:
			_collection_list.select(index)

func _create_collection() -> void:
	var dialog := ConfirmationDialog.new()
	dialog.title = "New Asset Collection"
	var edit := LineEdit.new()
	edit.placeholder_text = "Collection name"
	dialog.add_child(edit)
	dialog.confirmed.connect(func():
		var collection_name := edit.text.strip_edges()
		if not collection_name.is_empty():
			if not _collections.has(collection_name):
				_collections[collection_name] = []
			_refresh_collection_list(collection_name)
			_active_collection = collection_name
			_library_filter = "collection"
			_save_browser_settings()
			_apply_visibility()
		dialog.queue_free()
	)
	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered(Vector2i(360, 120))
	edit.grab_focus()

func _rename_collection() -> void:
	if _active_collection.is_empty() or not _collections.has(_active_collection):
		return
	var dialog := ConfirmationDialog.new()
	dialog.title = "Rename Asset Collection"
	var edit := LineEdit.new()
	edit.text = _active_collection
	edit.select_all()
	dialog.add_child(edit)
	dialog.confirmed.connect(func():
		var new_name := edit.text.strip_edges()
		if not new_name.is_empty() and new_name != _active_collection:
			var paths: Variant = _collections[_active_collection]
			_collections.erase(_active_collection)
			_collections[new_name] = paths
			_active_collection = new_name
			_refresh_collection_list(new_name)
			_save_browser_settings()
		dialog.queue_free()
	)
	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered(Vector2i(360, 120))
	edit.grab_focus()

func _delete_collection() -> void:
	if _active_collection.is_empty() or not _collections.has(_active_collection):
		return
	_collections.erase(_active_collection)
	_active_collection = ""
	_library_filter = "all"
	_refresh_collection_list()
	if _library_list:
		_library_list.select(0)
	_save_browser_settings()
	_apply_visibility()

func _collection_paths(collection_name: String) -> Array[String]:
	return BrowserLibraryData.collection_paths(_collections, collection_name)

func _add_selected_to_collection() -> void:
	if _active_collection.is_empty() or not _collections.has(_active_collection):
		return
	_collections[_active_collection] = BrowserLibraryData.add_unique_paths(_collection_paths(_active_collection), get_selected_scene_paths())
	_save_browser_settings()
	_apply_visibility()

func _remove_selected_from_collection() -> void:
	if _active_collection.is_empty() or not _collections.has(_active_collection):
		return
	_collections[_active_collection] = BrowserLibraryData.remove_paths(_collection_paths(_active_collection), get_selected_scene_paths())
	_save_browser_settings()
	_apply_visibility()

func _toggle_favorite(scene_path: String, button: Button) -> void:
	if _favorites.has(scene_path):
		_favorites.erase(scene_path)
		button.text = "☆"
	else:
		_favorites.append(scene_path)
		button.text = "★"
	_save_browser_settings()
	_apply_visibility()

func _toggle_selected_favorites() -> void:
	var selected := get_selected_scene_paths()
	if selected.is_empty():
		return
	_favorites = BrowserLibraryData.toggle_paths(_favorites, selected)
	_save_browser_settings()
	_scan_folder()

func _mark_recent(scene_path: String) -> void:
	_recent_assets = BrowserLibraryData.mark_recent(_recent_assets, scene_path, RECENT_LIMIT)

func _apply_tags_to_selected(raw_tags: String) -> void:
	var normalized := BrowserLibraryData.normalize_tags(raw_tags)
	for path in get_selected_scene_paths():
		_asset_tags[path] = normalized.duplicate()
	_save_browser_settings()
	_scan_folder()

func _tags_for_path(scene_path: String) -> String:
	return BrowserLibraryData.tags_text(_asset_tags, scene_path)

func _asset_tooltip(scene_path: String) -> String:
	var lines: Array[String] = [scene_path]
	var tags := _tags_for_path(scene_path)
	if not tags.is_empty():
		lines.append("Tags: " + tags)
	if _favorites.has(scene_path):
		lines.append("Favorite")
	return "\n".join(PackedStringArray(lines))

func _on_thumbnail_mode_selected(index: int) -> void:
	match index:
		0: _size_slider.value = 90.0
		1: _size_slider.value = 150.0
		2: _size_slider.value = 220.0
		3: pass

func _sync_thumbnail_mode() -> void:
	if _thumbnail_mode == null:
		return
	if absf(_tile_size - 90.0) < 0.5:
		_thumbnail_mode.select(0)
	elif absf(_tile_size - 150.0) < 0.5:
		_thumbnail_mode.select(1)
	elif absf(_tile_size - 220.0) < 0.5:
		_thumbnail_mode.select(2)
	else:
		_thumbnail_mode.select(3)

# ---------------------------------------------------------------------------
# Thumbnail cache
# ---------------------------------------------------------------------------

func _ensure_cache_dir() -> void:
	BrowserStorage.ensure_directory(CACHE_DIR)


func _cache_key(scene_path: String) -> String:
	return BrowserStorage.cache_key(scene_path)


func _cache_path(scene_path: String) -> String:
	return BrowserStorage.cache_path(CACHE_DIR, scene_path)


func _load_cached_thumbnail(scene_path: String) -> Texture2D:
	return BrowserStorage.load_cached_thumbnail(CACHE_DIR, scene_path)

func _save_cached_thumbnail(scene_path: String, texture: Texture2D) -> void:
	BrowserStorage.save_cached_thumbnail(CACHE_DIR, scene_path, texture)

func _clear_cache() -> void:
	var dir := DirAccess.open(ProjectSettings.globalize_path(CACHE_DIR))
	if dir == null:
		return
	dir.list_dir_begin()
	var f := dir.get_next()
	while f != "":
		if f.ends_with(".png"):
			dir.remove(f)
		f = dir.get_next()
	dir.list_dir_end()
	_full_refresh()


func _full_refresh() -> void:
	_scan_folder()


func _refresh_statistics_analysis(report: RichTextLabel, include_asset_analysis: bool) -> void:
	if editor_plugin == null:
		report.text = "Statistics are unavailable."
		return
	var parts: Array[String] = []
	if editor_plugin.has_method("get_asset_painter_statistics"):
		parts.append(str(editor_plugin.get_asset_painter_statistics()))
	if include_asset_analysis:
		var selected_paths := get_selected_scene_paths()
		if selected_paths.is_empty():
			parts.append("\nAsset Analysis\nSelect a .tscn asset in the browser first.")
		elif editor_plugin.has_method("analyze_asset_painter_asset"):
			parts.append("\n" + str(editor_plugin.analyze_asset_painter_asset(selected_paths[0])))
	report.text = "\n".join(parts)


# ---------------------------------------------------------------------------
# Folder picker
# ---------------------------------------------------------------------------

func _pick_folder() -> void:
	var dialog := EditorFileDialog.new()
	dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_DIR
	dialog.access = EditorFileDialog.ACCESS_RESOURCES
	dialog.dir_selected.connect(func(path: String) -> void:
		var normalized := path.trim_suffix("/")
		if not _asset_folders.has(normalized):
			_asset_folders.append(normalized)
		_asset_folder = normalized
		_selected_subfolder = normalized
		_save_browser_settings()
		_scan_folder()
		dialog.queue_free()
	)
	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered_ratio(0.6)


func _remove_selected_folder() -> void:
	if _selected_subfolder.is_empty():
		return
	var folder_to_remove := _selected_subfolder
	_asset_folders.erase(folder_to_remove)
	_selected_subfolder = ""
	if _asset_folders.is_empty():
		_asset_folders = ["res://assets"]
	_asset_folder = _asset_folders[0]
	_save_browser_settings()
	_scan_folder()
	set_painter_status("Removed source folder: %s" % folder_to_remove)


func get_selected_scene_paths() -> Array[String]:
	var paths: Array[String] = []
	for card in _selected_cards:
		if is_instance_valid(card):
			var path: String = card.get_meta("scene_path", "")
			if not path.is_empty():
				paths.append(path)
	return paths


func _update_paint_button_state(enabled: bool) -> void:
	if not _paint_btn:
		return
	if enabled:
		_paint_btn.text = "Exit Painter"
		_paint_btn.tooltip_text = "Exit Asset Painter mode (Esc)"
	else:
		_paint_btn.text = "🖌 Paint Assets"
		_paint_btn.tooltip_text = "Show the Asset Painter tools and paint selected scenes in the 3D viewport"


func _on_paint_toggled(enabled: bool) -> void:
	_update_paint_button_state(enabled)
	if _quick_brush_bar:
		_quick_brush_bar.visible = enabled
	if enabled:
		_refresh_quick_guidance.call_deferred()
	if _paint_settings:
		_paint_settings.visible = enabled
	if editor_plugin and editor_plugin.has_method("set_asset_painter_active"):
		editor_plugin.set_asset_painter_active(enabled)


func set_paint_button_pressed(enabled: bool) -> void:
	if _paint_btn:
		_paint_btn.set_pressed_no_signal(enabled)
	_update_paint_button_state(enabled)
	if _quick_brush_bar:
		_quick_brush_bar.visible = enabled
	if _paint_settings:
		_paint_settings.visible = enabled


# ---------------------------------------------------------------------------
# Persistent settings and brush presets
# ---------------------------------------------------------------------------

func _load_browser_settings() -> void:
	var config := ConfigFile.new()
	if config.load(SETTINGS_PATH) != OK:
		return
	var saved_folders = config.get_value("browser", "asset_folders", [])
	_asset_folders = _to_string_array(saved_folders)
	if _asset_folders.is_empty():
		var legacy_folder := str(config.get_value("browser", "asset_folder", _asset_folder))
		_asset_folders = [legacy_folder]
	var valid_folders: Array[String] = []
	for folder_path in _asset_folders:
		if DirAccess.dir_exists_absolute(ProjectSettings.globalize_path(folder_path)):
			valid_folders.append(folder_path.trim_suffix("/"))
	_asset_folders = valid_folders
	if _asset_folders.is_empty():
		_asset_folders = ["res://assets"]
	_selected_subfolder = str(config.get_value("browser", "selected_asset_folder", ""))
	if _selected_subfolder != "" and not _asset_folders.has(_selected_subfolder):
		_selected_subfolder = ""
	_asset_folder = _selected_subfolder if _selected_subfolder != "" else _asset_folders[0]
	_tile_size = float(config.get_value("browser", "tile_size", TILE_SIZE_DEFAULT))
	_is_grid_view = bool(config.get_value("browser", "grid_view", true))
	var saved_painter_values: Dictionary = config.get_value("painter", "values", {}) as Dictionary
	for option_name in saved_painter_values.keys():
		_painter_values[str(option_name)] = saved_painter_values[option_name]
	_pending_selected_paths = _to_string_array(config.get_value("browser", "selected_assets", []))
	_favorites = _to_string_array(config.get_value("library", "favorites", []))
	_recent_assets = _to_string_array(config.get_value("library", "recent", []))
	_collections = config.get_value("library", "collections", {}) as Dictionary
	_asset_tags = config.get_value("library", "tags", {}) as Dictionary
	_asset_placement_bounds = config.get_value("painter", "asset_placement_bounds", {}) as Dictionary
	_variant_settings = config.get_value("painter", "variant_settings", {}) as Dictionary

func _apply_loaded_browser_ui() -> void:
	if _folder_label:
		_update_folder_summary_label()
	if _size_slider:
		_size_slider.set_value_no_signal(_tile_size)
	if _view_btn:
		_view_btn.text = "⊞" if _is_grid_view else "☰"
	if _size_slider:
		_size_slider.visible = _is_grid_view
	_sync_thumbnail_mode()
	_update_folder_actions()
	var config := ConfigFile.new()
	if config.load(SETTINGS_PATH) == OK and _search:
		_search.text = str(config.get_value("browser", "search", ""))
	for option_name in _painter_values.keys():
		sync_painter_option(str(option_name), _painter_values[option_name])
	_sync_asset_placement_bounds_to_painter()

func _save_browser_settings() -> void:
	var config := ConfigFile.new()
	config.set_value("browser", "asset_folder", _asset_folder)
	config.set_value("browser", "asset_folders", _asset_folders)
	config.set_value("browser", "selected_asset_folder", _selected_subfolder)
	config.set_value("browser", "tile_size", _tile_size)
	config.set_value("browser", "grid_view", _is_grid_view)
	config.set_value("browser", "search", _search.text if _search else "")
	config.set_value("browser", "selected_assets", get_selected_scene_paths())
	config.set_value("library", "favorites", _favorites)
	config.set_value("library", "recent", _recent_assets)
	config.set_value("library", "collections", _collections)
	config.set_value("library", "tags", _asset_tags)
	config.set_value("painter", "values", _painter_values)
	config.set_value("painter", "variant_settings", _variant_settings)
	config.set_value("painter", "asset_placement_bounds", _asset_placement_bounds)
	config.save(SETTINGS_PATH)

func get_saved_painter_settings() -> Dictionary:
	var settings := _painter_values.duplicate(true)
	settings["asset_placement_bounds"] = _asset_placement_bounds.duplicate(true)
	return settings

func _restore_pending_asset_selection() -> void:
	if _pending_selected_paths.is_empty():
		return
	_clear_selection()
	for card in _cards:
		if not is_instance_valid(card):
			continue
		var scene_path: String = str(card.get_meta("scene_path", ""))
		if _pending_selected_paths.has(scene_path):
			_set_card_selected(card, true)
	_pending_selected_paths.clear()
	_rebuild_variant_editor.call_deferred()
	_save_browser_settings()

func _to_string_array(value: Variant) -> Array[String]:
	return BrowserStorage.to_string_array(value)

func _ensure_preset_dir() -> void:
	BrowserStorage.ensure_directory(PRESET_DIR)

func _sanitize_preset_name(raw_name: String) -> String:
	return BrowserStorage.sanitize_preset_name(raw_name)

func _preset_path(preset_name: String) -> String:
	return BrowserStorage.preset_path(PRESET_DIR, preset_name)

func _refresh_preset_list(select_name: String = "") -> void:
	if _preset_picker == null:
		return
	_ensure_preset_dir()
	_preset_picker.clear()
	var names: Array[String] = []
	var directory := DirAccess.open(PRESET_DIR)
	if directory:
		directory.list_dir_begin()
		var file_name := directory.get_next()
		while file_name != "":
			if not directory.current_is_dir() and file_name.ends_with(".cfg"):
				names.append(file_name.get_basename())
			file_name = directory.get_next()
		directory.list_dir_end()
	names.sort()
	for preset_name in names:
		_preset_picker.add_item(preset_name)
	if _preset_picker.item_count == 0:
		_preset_picker.add_item("No saved presets")
		_preset_picker.disabled = true
	else:
		_preset_picker.disabled = false
		var target_index := 0
		if not select_name.is_empty():
			for i in _preset_picker.item_count:
				if _preset_picker.get_item_text(i) == select_name:
					target_index = i
					break
		_preset_picker.select(target_index)
		_preset_name.text = _preset_picker.get_item_text(target_index)

func _current_preset_name() -> String:
	if _preset_picker == null or _preset_picker.disabled or _preset_picker.item_count == 0:
		return ""
	return _preset_picker.get_item_text(_preset_picker.selected)

func _on_preset_selected(index: int) -> void:
	if _preset_picker.disabled:
		return
	_preset_name.text = _preset_picker.get_item_text(index)

func _save_current_preset() -> void:
	var preset_name := _sanitize_preset_name(_preset_name.text)
	if preset_name.is_empty():
		preset_name = "Brush Preset"
	_ensure_preset_dir()
	var config := ConfigFile.new()
	config.set_value("preset", "name", preset_name)
	config.set_value("preset", "version", 2)
	config.set_value("preset", "assets", get_selected_scene_paths())
	config.set_value("painter", "values", _painter_values)
	config.set_value("painter", "variant_settings", _variant_settings)
	config.set_value("painter", "asset_placement_bounds", _asset_placement_bounds)
	var error := config.save(_preset_path(preset_name))
	if error == OK:
		_refresh_preset_list(preset_name)
		set_painter_status("Saved brush preset: %s" % preset_name)
	else:
		set_painter_status("Could not save brush preset: %s" % error_string(error))

func _load_selected_preset() -> void:
	var preset_name := _current_preset_name()
	if preset_name.is_empty():
		return
	_load_preset_path(_preset_path(preset_name))

func _load_preset_path(path: String) -> void:
	var config := ConfigFile.new()
	var error := config.load(path)
	if error != OK:
		set_painter_status("Could not load preset: %s" % error_string(error))
		return
	var values: Dictionary = config.get_value("painter", "values", {}) as Dictionary
	_painter_values = values.duplicate(true)
	for option_name in _painter_values.keys():
		var key := str(option_name)
		sync_painter_option(key, _painter_values[option_name])
		if editor_plugin and editor_plugin.has_method("set_asset_painter_option"):
			editor_plugin.set_asset_painter_option(key, _painter_values[option_name])
	_variant_settings = config.get_value("painter", "variant_settings", _variant_settings) as Dictionary
	_asset_placement_bounds = config.get_value("painter", "asset_placement_bounds", _asset_placement_bounds) as Dictionary
	_sync_asset_placement_bounds_to_painter()
	_pending_selected_paths = _to_string_array(config.get_value("preset", "assets", []))
	_restore_pending_asset_selection()
	_rebuild_variant_editor.call_deferred()
	_save_browser_settings()
	set_painter_status("Loaded brush preset: %s" % str(config.get_value("preset", "name", path.get_file().get_basename())))

func _rename_selected_preset() -> void:
	var old_name := _current_preset_name()
	var new_name := _sanitize_preset_name(_preset_name.text)
	if old_name.is_empty() or new_name.is_empty() or old_name == new_name:
		return
	var old_path := _preset_path(old_name)
	var new_path := _preset_path(new_name)
	var directory := DirAccess.open(PRESET_DIR)
	if directory == null:
		return
	if FileAccess.file_exists(new_path):
		directory.remove(new_name + ".cfg")
	var error := directory.rename(old_name + ".cfg", new_name + ".cfg")
	if error == OK:
		var config := ConfigFile.new()
		if config.load(new_path) == OK:
			config.set_value("preset", "name", new_name)
			config.save(new_path)
		_refresh_preset_list(new_name)
		set_painter_status("Renamed preset to: %s" % new_name)
	else:
		set_painter_status("Could not rename preset: %s" % error_string(error))

func _delete_selected_preset() -> void:
	var preset_name := _current_preset_name()
	if preset_name.is_empty():
		return
	var directory := DirAccess.open(PRESET_DIR)
	if directory == null:
		return
	var error := directory.remove(preset_name + ".cfg")
	if error == OK:
		_refresh_preset_list()
		set_painter_status("Deleted brush preset: %s" % preset_name)
	else:
		set_painter_status("Could not delete preset: %s" % error_string(error))

func _export_selected_preset() -> void:
	var preset_name := _current_preset_name()
	if preset_name.is_empty():
		return
	var dialog := EditorFileDialog.new()
	dialog.file_mode = EditorFileDialog.FILE_MODE_SAVE_FILE
	dialog.access = EditorFileDialog.ACCESS_FILESYSTEM
	dialog.add_filter("*.cfg", "Asset Painter Brush Preset")
	dialog.current_file = preset_name + ".cfg"
	dialog.file_selected.connect(func(destination: String) -> void:
		var bytes := FileAccess.get_file_as_bytes(_preset_path(preset_name))
		var output := FileAccess.open(destination, FileAccess.WRITE)
		if output:
			output.store_buffer(bytes)
			set_painter_status("Exported preset: %s" % destination)
		dialog.queue_free()
	)
	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered_ratio(0.65)

func _import_preset() -> void:
	var dialog := EditorFileDialog.new()
	dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_FILE
	dialog.access = EditorFileDialog.ACCESS_FILESYSTEM
	dialog.add_filter("*.cfg", "Asset Painter Brush Preset")
	dialog.file_selected.connect(func(source: String) -> void:
		var config := ConfigFile.new()
		var error := config.load(source)
		if error == OK:
			var preset_name := _sanitize_preset_name(str(config.get_value("preset", "name", source.get_file().get_basename())))
			if preset_name.is_empty():
				preset_name = "Imported Preset"
			_ensure_preset_dir()
			config.set_value("preset", "name", preset_name)
			config.save(_preset_path(preset_name))
			_refresh_preset_list(preset_name)
			_load_preset_path(_preset_path(preset_name))
		else:
			set_painter_status("Could not import preset: %s" % error_string(error))
		dialog.queue_free()
	)
	EditorInterface.get_base_control().add_child(dialog)
	dialog.popup_centered_ratio(0.65)
