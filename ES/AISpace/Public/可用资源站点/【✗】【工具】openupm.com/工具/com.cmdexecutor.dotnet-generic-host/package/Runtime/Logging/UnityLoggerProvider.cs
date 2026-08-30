using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.Hosting.Unity.Logging
{
    public class UnityLoggerProvider : ILoggerProvider
    {
        private readonly IDisposable _onChangeToken;
        private UnityLoggerConfiguration _currentConfig;
        private readonly ConcurrentDictionary<string, UnityLogger> _loggers = new ConcurrentDictionary<string, UnityLogger>();

        public UnityLoggerProvider(IOptionsMonitor<UnityLoggerConfiguration> config)
        {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
        }

        public ILogger CreateLogger(string categoryName) => _loggers.GetOrAdd(categoryName, name => new UnityLogger(name, _currentConfig));

        public void Dispose()
        {
            _loggers.Clear();
            _onChangeToken.Dispose();
        }
    }
}