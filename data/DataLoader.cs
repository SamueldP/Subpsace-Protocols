using System.Text.Json;

namespace SubspaceProtocols.Data;

/// <summary>
/// Resolves /data/*.json relative to AppContext.BaseDirectory so paths work
/// identically in development and inside the published standalone .exe folder.
/// </summary>
public static class DataLoader
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static string GetDataDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "data");

    public static T? LoadJson<T>(string fileName) where T : class
    {
        string path = Path.Combine(GetDataDirectory(), fileName);
        if (!File.Exists(path))
            return null;

        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, DefaultOptions);
    }

    public static void SaveJson<T>(string fileName, T data)
    {
        string dir = GetDataDirectory();
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        string json = JsonSerializer.Serialize(data, DefaultOptions);
        File.WriteAllText(path, json);
    }
}
