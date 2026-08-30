using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration
{
    public class ScriptableObjectConfigurationProvider : ConfigurationProvider
    {
        private readonly ScriptableObject _so;
        private readonly Dictionary<string, FieldInfo> _fields;

        public ScriptableObjectConfigurationProvider(Type type, ScriptableObject so)
        {
            _so = so;
            _fields = type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.DeclaredOnly |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .ToDictionary(p => p.Name, p => p);
        }

        public override void Load()
        {
            foreach (var field in _fields)
            {
                var attr = field.Value.GetCustomAttribute<ConfigurationKeyAttribute>();
                if (attr == null)
                    continue;

                if (Data.ContainsKey(attr.Name))
                {
                    Debug.LogWarning($"Configuration already contains key {attr.Name}, the duplicate key will be ignored", _so);
                    continue;
                }
                
                Data.Add(new KeyValuePair<string, string>(attr.Name, field.Value.GetValue(_so).ToString()));
            }
        }
    }
}