using Microsoft.AspNetCore.Mvc;
using Rosettes.Core;

namespace Rosettes.WebServer;

public static class InternalApi
{
    public const string HeaderName = "X-Rosettes-Panel-Secret";

    public static bool IsAuthorized(HttpRequest request)
    {
        if (string.IsNullOrWhiteSpace(Settings.PanelApiSecret))
        {
            return false;
        }

        if (!request.Headers.TryGetValue(HeaderName, out var providedSecret))
        {
            return false;
        }

        return string.Equals(providedSecret.ToString(), Settings.PanelApiSecret, StringComparison.Ordinal);
    }

    public static UnauthorizedObjectResult UnauthorizedResult()
    {
        return new UnauthorizedObjectResult(GenericResponse.Error("unauthorized"));
    }
}
