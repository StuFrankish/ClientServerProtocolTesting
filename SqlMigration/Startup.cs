using Microsoft.EntityFrameworkCore;
using Shared.Persistence;

namespace SqlMigrationRunner;

public class Startup(IConfiguration config)
{
    public IConfiguration Configuration { get; } = config;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<LoginDbContext>(options =>
            options.UseSqlServer(
                Configuration.GetConnectionString("LoginServer"),
                sqlOptions => sqlOptions.MigrationsAssembly("SqlMigrationRunner")
            ));
    }

    public void Configure(IApplicationBuilder app)
    {
    }
}