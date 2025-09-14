using ES;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ES
{



    // 1. 定义网格属性标记
/*    public class TrueGridAttribute : Attribute
    {
        public int Columns { get; set; } = 4;
        public float CellWidth { get; set; } = 100f;
        public float CellHeight { get; set; } = 120f;
    }

    // 2. 创建网格绘制器
    public class TrueGridDrawer<T> : OdinValueDrawer<List<T>> where T : new()
    {
        private const float Padding = 5f;

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var attr = Property.GetAttribute<TrueGridAttribute>();
            int columns = attr?.Columns ?? 4;
            float cellWidth = attr?.CellWidth ?? 100f;
            float cellHeight = attr?.CellHeight ?? 120f;

            // 绘制标签
            if (label != null)
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }

            var list = ValueEntry.SmartValue;
            int itemCount = list.Count;
            int rows = Mathf.CeilToInt(itemCount / (float)columns);

            // 绘制网格
            for (int row = 0; row < rows; row++)
            {
                EditorGUILayout.BeginHorizontal();
                {
                    for (int col = 0; col < columns; col++)
                    {
                        int index = row * columns + col;
                        if (index < itemCount)
                        {
                            DrawGridCell(list[index], index, cellWidth, cellHeight);
                        }
                        else
                        {
                            // 空白单元格
                            GUILayout.Space(cellWidth + Padding);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            // 添加按钮
            if (GUILayout.Button("+ 添加项目", GUILayout.Width(columns * (cellWidth + Padding))))
            {
                list.Add(new T());
            }
        }

        private void DrawGridCell(T item, int index, float width, float height)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width), GUILayout.Height(height));
            {
                // 获取当前属性的子属性
                var property = Property.Children[index];

                // 绘制每个子属性
                foreach (var child in property.Children)
                {
                    if (child.Name == "Icon" && child.ValueEntry.TypeOfValue == typeof(Texture2D))
                    {
                        // 特殊处理纹理绘制
                        var texture = (Texture2D)child.ValueEntry.WeakSmartValue;
                        var rect = GUILayoutUtility.GetRect(width - 20f, width - 20f);
                        if (texture != null)
                        {
                            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
                        }
                    }
                    else
                    {
                        // 正常绘制其他属性
                        child.Draw(child.Label);
                    }
                }

                // 删除按钮
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20f)))
                {
                    ValueEntry.SmartValue.RemoveAt(index);
                    GUIUtility.ExitGUI(); // 立即退出当前GUI绘制
                }
            }
            EditorGUILayout.EndVertical();
        }
    }

    // 3. 定义网格项数据结构
    [Serializable]
    public class GridItem
    {
        [HorizontalGroup("Group", width: 80)]
        [PreviewField(60)]
        [HideLabel]
        public Texture2D Icon;

        [VerticalGroup("Group/right")]
        [LabelWidth(50)]
        public string Name;

        [VerticalGroup("Group/right")]
        [LabelWidth(50)]
        [ProgressBar(0, 100)]
        public float Value;
    }

    // 4. 使用示例
    [Serializable]
    public class TrueGridExample
    {
        [Title("真正的网格布局")]
        [TrueGrid(Columns = 3, CellWidth = 120f, CellHeight = 150f)]
        [ShowInInspector]
        
        public List<GridItem> ItemGrid = new List<GridItem>();

        [Button("随机添加")]
        private void AddRandomItem()
        {
            ItemGrid.Add(new GridItem
            {
                Name = "Item " + UnityEngine.Random.Range(1, 100),
                Value = UnityEngine.Random.Range(0f, 100f)
            });
        }
    }*/
}