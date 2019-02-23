using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Watcher
{
    public class Program
    {
        static readonly string ProjectName = "SampleApplication";
        static readonly string ProjectPath = Path.GetFullPath(@"..\SampleApplication\");
        static readonly string DllPath = Path.Combine(ProjectPath, @"bin\Debug\netcoreapp3.0\SampleApplication.dll");
        static readonly string DotNetPath = @"C:\Program Files\dotnet\dotnet.exe";

        public static void Main(string[] args)
        {
            new HostBuilder()
                    .UseContentRoot(ProjectPath)
                    .ConfigureLogging(logging =>
                    {
                        logging.AddConsole()
                               .AddFilter("Watcher", LogLevel.Debug)
                               .SetMinimumLevel(LogLevel.Warning);
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddHostedService<WatcherService>();
                        services.AddSingleton<HostingServer>();

                        services.Configure<ProjectOptions>(o =>
                        {
                            o.ProjectName = ProjectName;
                            o.ProjectPath = ProjectPath;
                            o.DllPath = DllPath;
                            o.DotNetPath = DotNetPath;
                            o.Args = args;
                        });
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.Configure(app =>
                        {
                            app.UseDeveloperExceptionPage();

                            var server = app.ApplicationServices.GetRequiredService<HostingServer>();

                            app.Run(async context =>
                            {
                                var application = await server.WaitForApplicationAsync(default);

                                await application(context);
                            });
                        });
                    })
                    .Build()
                    .Run();
        }
    }
}
