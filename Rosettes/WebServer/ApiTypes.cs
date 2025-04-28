namespace Rosettes.WebServer;

public static class GenericResponse
{
    public static Dictionary<string, dynamic> Error(string message)
    {
        return new Dictionary<string, dynamic>
        {
            { "success", false },
            { "message", message }
        };
    }

    public static Dictionary<string, dynamic> Success(string message)
    {
        return new Dictionary<string, dynamic>
        {
            {"success", false},
            {"message", message}
        };
    }
}