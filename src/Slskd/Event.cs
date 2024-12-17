using System.Text.Json.Serialization;

namespace TuneTransporter.Slskd;

public class Event
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
    
    [JsonPropertyName("version")]
    public required int Version { get; set; }
    
    [JsonPropertyName("localDirectoryName")]
    public required string LocalDirectoryName { get; set; }
    
    [JsonPropertyName("remoteDirectoryName")]
    public required string RemoteDirectoryName { get; set; }
    
    [JsonPropertyName("username")]
    public required string Username { get; set; }
    
    [JsonPropertyName("id")]
    public required Guid Id { get; set; }
    
    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; set; }
    
    public string Name => Path.GetFileName(LocalDirectoryName);
}