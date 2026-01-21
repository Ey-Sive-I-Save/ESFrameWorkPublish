import json

try:
    with open('Assets/Plugins/ES/0_Stand/Stand_Tools/ESVMCP/RunningData/Input/indoor_room_scene.json', 'r', encoding='utf-8') as f:
        data = json.load(f)
    print('✅ JSON语法正确')
    print(f'📋 总命令数量: {len(data["commands"])}')
    print(f'🏷️ 场景ID: {data["commandId"]}')
    print(f'📝 描述: {data["description"]}')

    # 统计不同类型的命令
    command_types = {}
    for cmd in data['commands']:
        cmd_type = cmd.get('type', 'Unknown')
        command_types[cmd_type] = command_types.get(cmd_type, 0) + 1

    print('\n📊 命令类型统计:')
    for cmd_type, count in sorted(command_types.items()):
        print(f'  {cmd_type}: {count}')

    # 检查场景包含的元素
    print('\n🏠 场景元素检查:')
    elements = {
        '基础结构': ['Floor', 'Wall', 'Ceiling'],
        '家具': ['Table', 'Chair', 'Bookshelf'],
        '材质': ['Material'],
        '光照': ['Light'],
        '物理': ['Collider', 'Rigidbody'],
        '层级': ['Parent', 'Transform']
    }

    for category, keywords in elements.items():
        count = sum(1 for cmd in data['commands']
                   if any(keyword.lower() in cmd.get('id', '').lower() or
                         keyword.lower() in cmd.get('name', '').lower()
                         for keyword in keywords))
        print(f'  {category}: {count}个相关命令')

except Exception as e:
    print(f'❌ JSON语法错误: {e}')