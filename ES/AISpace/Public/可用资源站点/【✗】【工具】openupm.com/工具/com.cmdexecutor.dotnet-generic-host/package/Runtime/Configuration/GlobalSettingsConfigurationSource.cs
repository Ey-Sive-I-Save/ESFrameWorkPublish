using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnityEngine;

namespace Microsoft.Extensions.Hosting.Unity.Configuration
{
    public class GlobalSettingsConfigurationSource<TSettings> : IConfigurationSource where TSettings : GlobalSettings, new()
    {
        internal TSettings Settings { get; set; } = new();

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            if (!File.Exists(GlobalSettings.LocalFilePath))
                Settings.Save(true);

            Settings.Load();

            return Settings;
        }
    }
}