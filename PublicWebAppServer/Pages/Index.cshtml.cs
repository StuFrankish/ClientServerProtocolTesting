using Microsoft.AspNetCore.Mvc.RazorPages;
using Shared;
using Shared.Caching;

namespace PublicWebAppServer.Pages;

public class IndexModel(ILogger<IndexModel> logger, IConfiguration configuration) : PageModel
{
    private readonly ILogger<IndexModel> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public IList<WorldInfo> Realms { get; set; } = new List<WorldInfo>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Realms = await RequestRealmListAsync(cancellationToken);
    }

    public async Task<WorldInfo[]> RequestRealmListAsync(CancellationToken ct)
    {
        var worlds = new List<WorldInfo>();

        try
        {
            var _cache = new RedisWorldInfoCache(connectionString: _configuration.GetConnectionString("LoginServerRedis")!);
            worlds = await _cache.GetListAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IndexModel] Error fetching realm list: {ex.Message}");
        }

        return worlds.ToArray();
    }
}
