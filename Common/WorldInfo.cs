// Add JsonSerializerOptions to ensure consistency

public static class WorldInfoSerializer
{
    public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}

// In World Server when sending update:
var json = JsonSerializer.Serialize(_worldInfo, WorldInfoSerializer.Options);

// In Login Server when receiving update:
var worldInfo = JsonSerializer.Deserialize<WorldInfo>(json, WorldInfoSerializer.Options);