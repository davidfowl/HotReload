using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Watcher
{
    /// <summary>
    /// This server is plugged into the loaded application to notify when the application is ready to handle requests
    /// and ready to stop handling requests
    /// </summary>
    internal class HostingServer : IServer
    {
        private TaskCompletionSource<RequestDelegate> _severReadyTcs = new TaskCompletionSource<RequestDelegate>(TaskCreationOptions.RunContinuationsAsynchronously);

        public IFeatureCollection Features { get; set; }

        public HostingServer(IServer server)
        {
            // Set the features from the host server
            Features = server.Features;
        }

        public Task<RequestDelegate> WaitForApplicationAsync(CancellationToken cancellationToken = default)
        {
            return _severReadyTcs.Task;
        }

        public void Dispose()
        {
        }

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            // REVIEW: Doing this right requires us to hook into Hosting and the HostingApplication
            RequestDelegate app = context =>
            {
                var ctx = application.CreateContext(context.Features);

                context.Response.OnCompleted(() =>
                {
                    application.DisposeContext(ctx, null);
                    return Task.CompletedTask;
                });

                return application.ProcessRequestAsync(ctx);
            };

            _severReadyTcs.TrySetResult(app);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _severReadyTcs = new TaskCompletionSource<RequestDelegate>(TaskCreationOptions.RunContinuationsAsynchronously);

            return Task.CompletedTask;
        }
    }
}
