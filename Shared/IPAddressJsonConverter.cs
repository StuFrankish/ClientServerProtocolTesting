using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

public class IPAddressJsonConverter : JsonConverter<IPAddress>
{
    public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        return IPAddress.TryParse(str, out var addr) ? addr : IPAddress.Loopback;
    }

    public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}