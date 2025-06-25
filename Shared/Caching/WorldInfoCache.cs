using StackExchange.Redis;
using System.Text.Json;

namespace Shared.Caching;

public class RedisWorldInfoCache
{
    private readonly IDatabase _db;
    private const string CacheKey = "worldinfo";

    public RedisWorldInfoCache(string connectionString = "localhost:6379")
    {
        var mux = ConnectionMultiplexer.Connect(connectionString);
        _db = mux.GetDatabase();
    }

    public async Task SaveListAsync(List<WorldInfo> worldInfos)
    {
        var json = JsonSerializer.Serialize(worldInfos);
        await _db.StringSetAsync(CacheKey, json).ConfigureAwait(false);
    }

    public async Task AddOrUpdateAsync(WorldInfo worldInfo)
    {
        var list = await GetListAsync().ConfigureAwait(false);
        var idx = list.FindIndex(w => w.Id == worldInfo.Id);

        if (idx >= 0)
            list[idx] = worldInfo;
        else
            list.Add(worldInfo);

        await SaveListAsync(list).ConfigureAwait(false);
    }

    public async Task<List<WorldInfo>> GetListAsync()
    {
        var entry = await _db.StringGetAsync(CacheKey).ConfigureAwait(false);
        if (entry.IsNullOrEmpty)
            return [];

        return JsonSerializer.Deserialize<List<WorldInfo>>(entry!) ?? [];
    }
}