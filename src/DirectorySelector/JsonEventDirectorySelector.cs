namespace TuneTransporter.DirectorySelector;

using System.Text.Json;
using Slskd;

public class JsonEventDirectorySelector : IDirectorySelector
{
    private readonly string _json;
    private readonly string _downloadsPath;

    public JsonEventDirectorySelector(string json, string downloadsPath)
    {
        _json = json;
        _downloadsPath = downloadsPath;
    }
    
    public string GetTargetDirectory()
    {
        var slskdEvent = JsonSerializer.Deserialize<Event>(_json);
        
        if (slskdEvent == null)
        {
            throw new Exception("Could not parse event");
        }

        return Path.Combine(_downloadsPath, slskdEvent.Name);
    }
}