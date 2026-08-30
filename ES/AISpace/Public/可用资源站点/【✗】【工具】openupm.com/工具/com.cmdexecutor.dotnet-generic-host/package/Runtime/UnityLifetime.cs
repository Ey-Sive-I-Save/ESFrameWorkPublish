using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnityEngine;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.Extensions.Hosting.Unity
{
    public class UnityLifetime : IHostLifetime, IDisposable
    {
        private CancellationTokenRegistration _applicationStartedRegistration;
        private CancellationTokenRegistration _applicationStoppingRegistration;

        private readonly ManualResetEvent _shutdownBlock = new ManualResetEvent(false);

        public UnityLifetime(IOptions<ConsoleLifetimeOptions> options, IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, IOptions<HostOptions> hostOptions)
            : this(options, environment, applicationLifetime, hostOptions, NullLoggerFactory.Instance)
        {
        }

        public UnityLifetime(IOptions<ConsoleLifetimeOptions> options, IHostEnvironment environment, IHostApplicationLifetime applicationLifetime, IOptions<HostOptions> hostOptions, ILoggerFactory loggerFactory)
        {
            Options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            ApplicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));
            HostOptions = hostOptions?.Value ?? throw new ArgumentNullException(nameof(hostOptions));
            Logger = loggerFactory.CreateLogger("Microsoft.Hosting.Lifetime");
        }

        private ConsoleLifetimeOptions Options { get; }

        private IHostEnvironment Environment { get; }

        private IHostApplicationLifetime ApplicationLifetime { get; }

        private HostOptions HostOptions { get; }

        private ILogger Logger { get; }

        public Task WaitForStartAsync(CancellationToken cancellationToken)
        {
            if (!Options.SuppressStatusMessages)
            {
                _applicationStartedRegistration = ApplicationLifetime.ApplicationStarted.Register(state => { ((UnityLifetime)state).OnApplicationStarted(); },
                    this);
                _applicationStoppingRegistration = ApplicationLifetime.ApplicationStopping.Register(state => { ((UnityLifetime)state).OnApplicationStopping(); },
                    this);
            }

            RegisterShutdownHandlers();

            return Task.CompletedTask;
        }

        private void RegisterShutdownHandlers()
        {
            Application.quitting += OnProcessExit;
        }

        private void OnApplicationStarted()
        {
            Logger.LogInformation("Unity host started");
            Logger.LogInformation("Hosting environment: {envName}", Environment.EnvironmentName);
            Logger.LogInformation("Content root path: {contentRoot}", Environment.ContentRootPath);
        }

        private void OnProcessExit()
        {
            ApplicationLifetime.StopApplication();
            if (!_shutdownBlock.WaitOne(HostOptions.ShutdownTimeout))
            {
                Logger.LogInformation("Waiting for the host to be disposed. Ensure all 'IHost' instances are wrapped in 'using' blocks");
            }

            // wait one more time after the above log message, but only for ShutdownTimeout, so it doesn't hang forever
            _shutdownBlock.WaitOne(HostOptions.ShutdownTimeout);

            // On Linux if the shutdown is triggered by SIGTERM then that's signaled with the 143 exit code.
            // Suppress that since we shut down gracefully. https://github.com/dotnet/aspnetcore/issues/6526
            System.Environment.ExitCode = 0;
        }

        private void OnApplicationStopping()
        {
            Logger.LogInformation("Unity host is shutting down...");
        }
        
        public Task StopAsync(CancellationToken cancellationToken)
        {
            // There's nothing to do here
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            UnregisterShutdownHandlers();

            _applicationStartedRegistration.Dispose();
            _applicationStoppingRegistration.Dispose();
        }

        private void UnregisterShutdownHandlers()
        {
            _shutdownBlock.Set();
            Application.quitting -= OnProcessExit;
        }
    }
}