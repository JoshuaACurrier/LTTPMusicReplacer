using System.Text.Json.Serialization;

namespace LTTPEnhancementTools.Services;

public class AppSettings
{
    [JsonPropertyName("libraryFolder")]
    public string LibraryFolder { get; set; } = string.Empty;

    [JsonPropertyName("outputFolder")]
    public string OutputFolder { get; set; } = string.Empty;
}
