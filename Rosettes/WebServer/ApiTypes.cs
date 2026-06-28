namespace Rosettes.WebServer;

public static class GenericResponse
{
    public static Dictionary<string, object?> Error(string message, object? data = null)
    {
        return new Dictionary<string, object?>
        {
            { "success", false },
            { "message", message },
            { "data", data }
        };
    }

    public static Dictionary<string, object?> Success(string message, object? data = null)
    {
        return new Dictionary<string, object?>
        {
            { "success", true },
            { "message", message },
            { "data", data }
        };
    }
}