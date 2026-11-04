
using System.Text.Json;

namespace SupportTicket.EntityGenerator.Configuration;

public static class GenerationSettingsLoader
{
    public static GenerationSettings Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Generator settings file not found.",
                filePath);
        }

        var json = File.ReadAllText(filePath);

        var settings = JsonSerializer.Deserialize<GenerationSettings>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        return settings
            ?? throw new InvalidOperationException(
                "Generator settings could not be loaded.");
    }
}
