using LoginServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared;
using Shared.Persistence;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<LoginDbContext>(options =>
            options.UseSqlServer(
                context.Configuration.GetConnectionString("LoginServer"),
                sqlOptions => sqlOptions.MigrationsAssembly("SqlMigrationRunner")
            ));

        services.AddSingleton<LoginService>();
        services.AddSingleton<WorldRegistry>();
    })
    .Build();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var registry = host.Services.GetRequiredService<WorldRegistry>();
var loginService = host.Services.GetRequiredService<LoginService>();

// Listen for world heartbeats on UDP port 14004
registry.StartHeartbeatListener(14004, cts.Token);

// Start login service on TCP port 14002
Console.WriteLine("Login server starting...");
await loginService.StartAsync(cts.Token);

// Setup UDP listener for world server updates
var udpEndpoint = new IPEndPoint(IPAddress.Any, 7000);
var udpClient = new UdpClient(udpEndpoint);
Console.WriteLine($"[Login] Listening for world updates on UDP {udpEndpoint}");

// Start listening for UDP messages from world servers
_ = Task.Run(async () =>
{
    try
    {
        while (true)
        {
            var result = await udpClient.ReceiveAsync();
            Console.WriteLine($"[Login] Received UDP data from {result.RemoteEndPoint}, {result.Buffer.Length} bytes");
            
            try
            {
                var json = Encoding.UTF8.GetString(result.Buffer);
                Console.WriteLine($"[Login] Processing world update: {json}");
                var worldInfo = JsonSerializer.Deserialize<WorldInfo>(json);
                
                if (worldInfo != null)
                {
                    Console.WriteLine($"[Login] Registering world update: ID={worldInfo.Id}, Name={worldInfo.Name}, State={worldInfo.State}");
                    registry.Register(worldInfo);
                }
                else
                {
                    Console.WriteLine("[Login] Failed to deserialize world info (null result)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Login] Error processing world update: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Login] UDP listener error: {ex.Message}");
    }
});