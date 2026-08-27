using System.Text.Json;
using System.IO;

namespace Come.Services;

public sealed class BuildStorageService : IBuildStorageService
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Come", "recent-build.json");

    public void Save(IEnumerable<string> partIds)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(partIds.ToArray()));
    }

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(_filePath)) return [];
        try { return JsonSerializer.Deserialize<string[]>(File.ReadAllText(_filePath)) ?? []; }
        catch (JsonException) { return []; }
    }
}
