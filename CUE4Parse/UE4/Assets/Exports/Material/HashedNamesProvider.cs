using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using CUE4Parse.Utils;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.Material;

public sealed class HashedNamesProvider
{

    public static readonly Lazy<HashedNamesProvider> LazyInstance = new(() => new HashedNamesProvider());
    public static HashedNamesProvider Instance => LazyInstance.Value;

    private readonly ConcurrentDictionary<ulong, string> _hashedNames = new();

    private HashedNamesProvider()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CUE4Parse.Resources.ShaderHashedNames.json");
            if (stream == null)
            {
                Log.Error("Couldn't find ShaderHashedNames.json in Embedded Resources");
                return;
            }

            // Read token by token rather than JsonConvert.DeserializeObject<ConcurrentDictionary<ulong, string>>: Newtonsoft's
            // dictionary contracts make generic instantiations Native AOT does not have.
            using var reader = new JsonTextReader(new StreamReader(stream));
            while (reader.Read())
            {
                if (reader.TokenType != JsonToken.PropertyName || !ulong.TryParse((string) reader.Value!, out var hash)) continue;
                if (reader.Read() && reader.Value is string name) _hashedNames[hash] = name;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to load ShaderHashedNames.json from Embedded Resources");
        }
    }

    public static bool TryGetEntry(ulong hash, out string? entry)
    {
        return Instance._hashedNames.TryGetValue(hash, out entry);
    }

    public static bool TryAdd(string line)
    {
        var ind = line.LastIndexOf('_');
        var span = line.AsSpan()[(ind + 1)..];
        ulong hash = 0;
        if (ind > 0 && span.Length > 0 && int.TryParse(span, out var index) && ((index == 0 && span.Length == 1) || (index > 0 && !span[0].Equals("0"))))
        {
            var newline = line[..ind].ToUpperInvariant();
            hash = CityHash.CityHash64WithSeed(Encoding.UTF8.GetBytes(newline), (ulong) (index + 1));
        }
        else
        {
            hash = CityHash.CityHash64WithSeed(Encoding.UTF8.GetBytes(line.ToUpperInvariant()), 0);
        }

        return Instance._hashedNames.TryAdd(hash, line);
    }
}
