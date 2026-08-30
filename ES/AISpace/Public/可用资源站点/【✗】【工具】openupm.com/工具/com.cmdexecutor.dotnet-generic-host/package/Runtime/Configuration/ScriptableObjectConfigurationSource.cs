using System;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Configuration
{
    public class ScriptableObjectConfigurationSource : IConfigurationSource
    {
        private readonly Type _type;
        private readonly string _resourcesPath;
        private ScriptableObject _scriptableObject;

        public ScriptableObjectConfigurationSource(Type type, string resourcesPath)
        {
            _type = type;
            _resourcesPath = resourcesPath;
        }

        public ScriptableObjectConfigurationSource(Type type, ScriptableObject scriptableObject)
        {
            _type = type;
            _scriptableObject = scriptableObject;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (!_scriptableObject)
                _scriptableObject = Resources.Load<ScriptableObject>(_resourcesPath);

            return new ScriptableObjectConfigurationProvider(_type, _scriptableObject);
        }
    }
}