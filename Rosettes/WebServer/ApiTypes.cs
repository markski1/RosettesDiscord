using System.Text.Json.Serialization;

namespace Rosettes.WebServer;

public sealed record ApiResponse(
    [property: JsonPropertyName("success")] bool IsSuccess,
    string Code,
    string Message,
    object? Data = null)
{
    public static ApiResponse Error(string code, string? message = null, object? data = null)
    {
        return new ApiResponse(false, code, message ?? code, data);
    }

    public static ApiResponse Success(string code, object? data = null, string? message = null)
    {
        return new ApiResponse(true, code, message ?? code, data);
    }
}
