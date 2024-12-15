namespace TuneTransporter.SlskdEvent;

using System.Text.Json;

public class SlskdEventParser(JsonSerializerOptions? options = null)
{
    private JsonSerializerOptions? _options = options;
    
    public SlskdEvent ParseEvent(string json)
    {
        var slskdEvent = JsonSerializer.Deserialize<SlskdEvent>(json, _options);

        if (slskdEvent == null)
        {
            throw new Exception("Could not parse event");
        }
        
        return slskdEvent;
    }
}