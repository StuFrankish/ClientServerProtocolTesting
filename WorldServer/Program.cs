using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;
using System.Net;
using System.Text.Json;

class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Load settings
                var configText = File.ReadAllText("worldsettings.json");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var settings = JsonSerializer.Deserialize<WorldSettings>(configText, options);
                if (settings == null)
                {
                    throw new Exception("Failed to load worldsettings.json");
                }

                services.AddSingleton(settings);
                services.AddSingleton(new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                services.AddSingleton(new WorldInfo
                {
                    Id = settings.Id,
                    Name = settings.Name,
                    IP = IPAddress.Parse(settings.Host),
                    Port = settings.Port,
                    State = settings.State
                });

                services.AddHostedService<HeartbeatService>();
                services.AddHostedService<WorldClientService>();
            })
            .Build();

        await host.RunAsync();
    }
}
