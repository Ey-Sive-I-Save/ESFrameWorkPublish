using System;
using System.Reflection;
using UnityEngine;

namespace ES
{
    /// <summary>
    /// 内置场景中的热更新脚本入口。场景不能直接序列化 ES_Logic 类型，
    /// 因此由 AOT 壳在程序集加载完成后按类型名挂接真正的热更新 MonoBehaviour。
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class ESHotUpdateSceneEntry : MonoBehaviour
    {
        [SerializeField] private string componentTypeName = string.Empty;
        private bool attached;

        public void Configure(string value) => componentTypeName = value ?? string.Empty;

        private void Awake() => AttachHotUpdateComponent();

        private void AttachHotUpdateComponent()
        {
            if (attached || string.IsNullOrWhiteSpace(componentTypeName)) return;
            Type componentType = ResolveType(componentTypeName.Trim());
            if (componentType == null)
            {
                Debug.LogError("[ES][HotUpdateSceneEntry] 未找到热更新场景组件：" + componentTypeName, this);
                return;
            }
            if (!typeof(MonoBehaviour).IsAssignableFrom(componentType))
            {
                Debug.LogError("[ES][HotUpdateSceneEntry] 类型不是 MonoBehaviour：" + componentTypeName, this);
                return;
            }
            if (GetComponent(componentType) == null)
                gameObject.AddComponent(componentType);
            attached = true;
            Debug.Log("[ES][HotUpdateSceneEntry] 已挂接热更新场景组件：" + componentType.FullName, this);
        }

        private static Type ResolveType(string name)
        {
            Type resolved = Type.GetType(name, false);
            if (resolved != null) return resolved;
            string fullName = name;
            int comma = name.IndexOf(',');
            if (comma >= 0) fullName = name.Substring(0, comma).Trim();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolved = assembly.GetType(fullName, false);
                if (resolved != null) return resolved;
            }
            return null;
        }
    }
}
