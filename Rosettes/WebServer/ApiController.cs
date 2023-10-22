using Microsoft.AspNetCore.Mvc;
using Rosettes.Core;
using Rosettes.Modules.Engine.Guild;

namespace Rosettes.WebServer;

[ApiController]
[Route("rosettes-api")]
public class ApiController : ControllerBase
{
    private readonly ILogger<ApiController> _logger;

    public ApiController(ILogger<ApiController> logger)
    {
        _logger = logger;
    }

    [HttpGet("CheckAlive")]
    public string CheckAlive()
    {
        return "Rosettes lives!";
    }

    [HttpGet("ServerFetch")]
    public string ServerFetch(string secretKey = "?")
    {
        if (secretKey != Settings.SecretKey)
        {
            return "No secret key.";
        }

        string ret = "";

        ulong userSum = 0;

        foreach (var guild in GuildEngine.GuildCache) {
            ret += $"{guild.NameCache} | {guild.Members} members | {guild.OwnerId} \n\n";
            userSum += guild.Members;
        }

        ret += $"Servers: {GuildEngine.GuildCache.Count} | Total users served: {userSum}";

        return ret;
    }
}