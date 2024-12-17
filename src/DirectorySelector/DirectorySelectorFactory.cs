namespace TuneTransporter.DirectorySelector;

using System.Text.Json;

public class DirectorySelectorFactory
{
    private const string EventFlag = "--event";
    private readonly string[] _args;
    private readonly string _downloadsPath;

    public DirectorySelectorFactory(string[] args, string downloadsPath)
    {
        _args = args;
        _downloadsPath = downloadsPath;
    }
    
    public IDirectorySelector Create()
    {
        if (_args.Length > 0)
        {
            if (_args[0] == EventFlag)
            {
                return new JsonEventDirectorySelector(_args[1], _downloadsPath);
            }
        }
        
        return new InteractiveDirectorySelector(_downloadsPath);
    }
}