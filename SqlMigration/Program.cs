using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Shared.DataSeeding;
using Shared.Persistence;
using SqlMigrationRunner;

public class Program
{
    public static void Main(string[] args)
    {
        var host = CreateWebHostBuilder(args)
            .Build();

        EnsureSeedData(host.Services);

        Console.WriteLine("Exiting application...");
        Environment.Exit(0);
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
    WebHost.CreateDefaultBuilder(args)
           .UseStartup<Startup>();

    public static void EnsureSeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        using var context = scope.ServiceProvider.GetRequiredService<LoginDbContext>();

        if (context.Database.IsRelational())
        {
            context.Database.EnsureCreated();
        }

        context.Database.Migrate();

        if (!context.Users.Any())
        {
            context.Users.AddRange(TestUsers.Users);
            context.SaveChanges();
            Console.WriteLine("Seed data added to the database.");
        }
        else
        {
            Console.WriteLine("Database already contains seed data.");
        }
    }
}
