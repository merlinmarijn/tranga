using System.Text.Json;

namespace API.MangaConnectors;

/// <summary>Loads opt-in declarative connectors without changing the handwritten connector registry.</summary>
public static class MangaConnectorCatalog
{
    public static MangaConnector[] LoadConfigured()
    {
        string installedDirectory = Path.Join(AppContext.BaseDirectory, "Connectors");
        string userDirectory = Path.Join(TrangaSettings.WorkingDirectory, "Connectors");
        Directory.CreateDirectory(userDirectory);

        return new[] { installedDirectory, userDirectory }
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.json"))
            .OrderBy(path => path.StartsWith(userDirectory, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .Select(File.ReadAllText)
            .Select(json => JsonSerializer.Deserialize<HtmlConnectorDefinition>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }))
            .Where(definition => definition is not null)
            .Select(definition => new ConfiguredHtmlMangaConnector(definition!))
            .GroupBy(connector => connector.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .Cast<MangaConnector>()
            .ToArray();
    }

    private sealed class ConfiguredHtmlMangaConnector(HtmlConnectorDefinition definition) : HtmlMangaConnector(definition)
    {
    }
}
