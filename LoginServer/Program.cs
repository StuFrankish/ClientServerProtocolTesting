using LoginServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Persistence;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration.GetConnectionString("LoginServer");

        services.AddDbContext<LoginDbContext>(options =>
            options.UseSqlServer(connectionString,
                sqlOptions => sqlOptions.MigrationsAssembly("SqlMigrationRunner")
            ));

        services.AddSingleton<WorldRegistry>();
        services.AddSingleton<LoginService>();

        // Register hosted services
        services.AddHostedService<LoginHostedService>();
        services.AddHostedService<WorldRegistryHostedService>();
    })
    .Build();

await host.RunAsync();