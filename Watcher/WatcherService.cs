using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Watcher
{
    internal class WatcherService : BackgroundService
    {
        delegate IHostBuilder CreateHostBuilderDelegate(string[] args);

        private readonly ProjectOptions _options;
        private readonly HostingServer _server;
        private readonly ILogger<WatcherService> _logger;

        public WatcherService(IOptions<ProjectOptions> options, HostingServer server, ILogger<WatcherService> logger)
        {
            _options = options.Value;
            _server = server;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Don't block the thread startup thread
            await Task.Yield();

            // Make a file watcher for the project
            var watcher = new FileSystemWatcher(_options.ProjectPath, "*.cs")
            {
                EnableRaisingEvents = true
            };

            var noopLifetime = new NoopHostLifetime();

            // Run until the host has been shutdown
            while (!cancellationToken.IsCancellationRequested)
            {
                // Load the application in a new load context
                var loadContext = new AppLoadContext();

                _logger.LogDebug("Loading {projectName} into load context", _options.ProjectName);

                // We're loading from Stream because I can't figure out how to make loading from file work and reliably
                // unlock the file after unload
                var assemblyStream = new MemoryStream(File.ReadAllBytes(_options.DllPath));
                var assemblySymbols = new MemoryStream(File.ReadAllBytes(Path.ChangeExtension(_options.DllPath, ".pdb")));

                var projectAssembly = loadContext.LoadFromStream(assemblyStream, assemblySymbols);

                var type = projectAssembly.GetType($"{_options.ProjectName}.Program");
                var createHostBuilderMethodInfo = type.GetMethod("CreateHostBuilder", BindingFlags.Static | BindingFlags.Public);

                var createHostBuilder = (CreateHostBuilderDelegate)Delegate.CreateDelegate(typeof(CreateHostBuilderDelegate), createHostBuilderMethodInfo);

                // Create a new HostBuilder based on the application
                var applicationHostBuilder = createHostBuilder(_options.Args);

                // Override the IServer so that we get Start and Stop application notificaitons
                applicationHostBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<IServer>(_server);

                    // We delegate shutdown to the host, we'll call StopAsync on the application ourselves
                    services.AddSingleton<IHostLifetime>(noopLifetime);
                });

                // Build the host for the child application
                var applicationHost = applicationHostBuilder.Build();

                _logger.LogDebug("Starting application");

                // Start the application host
                await applicationHost.StartAsync(cancellationToken);

                // Wait for a file change in the target application
                await WaitForFileChangedAsync(watcher, cancellationToken);

                _logger.LogDebug("Stopping application");

                // Shut down the application host
                await applicationHost.StopAsync();

                _logger.LogDebug("Application stopped");

                // Unload the custom load context
                loadContext.Unload();

                _logger.LogDebug("Application context unloaded");

                // For some odd reason this ends the process
                // GC.Collect();

                // Don't rebuild if we're shuttind down gracefully
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _logger.LogDebug("Rebuilding application");

                // Rebuild the project (without restoring)
                var exitCode = await RunProcessAsync(new ProcessStartInfo
                {
                    FileName = _options.DotNetPath,
                    Arguments = "build --no-restore",
                    WorkingDirectory = _options.ProjectPath,
                    CreateNoWindow = false,
                });

                _logger.LogDebug("Exit code was {processExitCode}", exitCode);
            }
        }

        private static async Task WaitForFileChangedAsync(FileSystemWatcher watcher, CancellationToken cancellationToken)
        {
            // Wait for a file to change
            var fileChangedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnFileChanged(object sender, FileSystemEventArgs e)
            {
                fileChangedTcs.TrySetResult(null);
            }

            var registration = cancellationToken.Register(state => ((TaskCompletionSource<object>)state).TrySetResult(null), fileChangedTcs);

            using (registration)
            {
                watcher.Changed += OnFileChanged;

                await fileChangedTcs.Task;

                watcher.Changed -= OnFileChanged;
            }
        }

        private static Task<int> RunProcessAsync(ProcessStartInfo processStartInfo)
        {
            var process = Process.Start(processStartInfo);
            process.EnableRaisingEvents = true;

            if (process.HasExited)
            {
                return Task.FromResult(process.ExitCode);
            }

            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            process.Exited += (sender, e) =>
            {
                tcs.TrySetResult(process.ExitCode);
            };

            return tcs.Task;
        }
    }
}