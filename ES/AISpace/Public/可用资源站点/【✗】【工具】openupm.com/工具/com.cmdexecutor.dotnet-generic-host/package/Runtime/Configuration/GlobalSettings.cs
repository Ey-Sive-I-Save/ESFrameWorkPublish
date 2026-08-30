using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Microsoft.Extensions.Hosting.Unity.Configuration
{
    /// <summary>
    /// Configuration provider automatically creates Config/globalsettings.json under the Game's directory.
    /// Inherit from this class and extend it with properties to use them as configuration keys.
    /// </summary>
    [Serializable]
    public abstract class GlobalSettings : IConfigurationProvider
    {
        internal const string ConfigDirPath = "Config";
        internal const string ConfigFilePath = "globalsettings.json";
        internal const string LocalFilePath = ConfigDirPath + "/" + ConfigFilePath;
        private static readonly JsonSerializerOptions _serializerOptions = new() {Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, IncludeFields = true, WriteIndented = true};
        private readonly Type _type;
        private readonly Dictionary<string, PropertyInfo> _properties;
        private IConfigurationRoot _configurationRoot;
        private ConfigurationReloadToken _reloadToken = new ConfigurationReloadToken();

        /// <summary>
        /// Invoked when settings are saved to file.
        /// </summary>
        public event Action Saved = delegate {  };

        protected GlobalSettings()
        {
            _type = GetType();
            _properties = _type.GetProperties(
                    BindingFlags.Instance |
                    BindingFlags.SetProperty |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                .ToDictionary(p => p.Name, p => p);
        }

        /// <summary>
        /// For internal purpose.
        /// </summary>
        /// <param name="configuration"></param>
        internal void SetConfiguration(IConfiguration configuration)
        {
            // ReSharper disable once InvertIf
            if (configuration != null)
            {
                _configurationRoot = (IConfigurationRoot) configuration;
                configuration.Bind(this);
            }
        }

        public bool TryGet(string key, out string value)
        {
            if (_properties.TryGetValue(key, out var property))
            {
                var v = property.GetValue(this);
                if (v != null)
                {
                    value = v.ToString();
                    return true;
                }
            }

            value = default;
            return false;
        }

        public void Set(string key, string value)
        {
            if (!_properties.TryGetValue(key, out var property))
                return;

            var v = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(this, v);
        }

        public IChangeToken GetReloadToken() => _reloadToken;

        /// <summary>
        /// Read configuration from the config file.
        /// </summary>
        public void Load()
        {
            var json = BeforeLoad(File.ReadAllText(LocalFilePath));
            var deserialized = JsonSerializer.Deserialize(json, _type, _serializerOptions);

            foreach (var property in _properties.Values)
            {
                var value = property.GetValue(deserialized);
                property.SetValue(this, value);
            }

            OnReload();
        }

        public virtual IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath)
        {
            var prefix = parentPath == null ? string.Empty : parentPath + ConfigurationPath.KeyDelimiter;

            return _properties.Keys
                .Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(key => Segment(key, prefix.Length))
                .Concat(earlierKeys)
                .OrderBy(k => k, ConfigurationKeyComparer.Instance);
        }

        private static string Segment(string key, int prefixLength)
        {
            var indexOf = key.IndexOf(ConfigurationPath.KeyDelimiter, prefixLength, StringComparison.OrdinalIgnoreCase);
            return indexOf < 0 ? key.Substring(prefixLength) : key.Substring(prefixLength, indexOf - prefixLength);
        }

        /// <summary>
        /// Save configuration to the config file.
        /// </summary>
        public void Save(bool writeDefaults = false)
        {
            if (writeDefaults)
                WriteDefaults();
            
            var json = BeforeSave(System.Text.Json.JsonSerializer.Serialize(this, _type, _serializerOptions));

            Directory.CreateDirectory(ConfigDirPath);
            File.WriteAllText(LocalFilePath, json);

            Saved();
        }
        
        /// <summary>
        /// Assign default values.
        /// </summary>
        protected virtual void WriteDefaults(){}

        private void OnReload()
        {
            var previousToken = Interlocked.Exchange(ref _reloadToken, new ConfigurationReloadToken());
            previousToken.OnReload();
        }

        /// <summary>
        /// Override to perform extra actions onto the serialized data that just was loaded from the config file.
        /// Could be used to decrypt or unzip the data before deserializing.
        /// </summary>
        /// <param name="content">Raw data that was read from the config file.</param>
        /// <returns>Should return a valid json.</returns>
        public virtual string BeforeLoad(string content) => content;

        /// <summary>
        /// Override to perform extra actions onto the serialized json.
        /// Could be used to encrypt or zip the data before writing to the config file.
        /// </summary>
        /// <param name="json">Serialized settings.</param>
        /// <returns>A content to save into the config file.</returns>
        public virtual string BeforeSave(string json) => json;

        public override string ToString() => _type.Name;
    }
}