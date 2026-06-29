using Microsoft.AspNetCore.Mvc;
using Rosettes.Database;
using Rosettes.Modules.Engine.Guild;

namespace Rosettes.WebServer.API;

[ApiController]
[Route("rosapi/internal")]
public class InternalController : ControllerBase
{
    public sealed class PanelLoginRequest
    {
        public string Key { get; init; } = string.Empty;
    }

    [HttpPost("panel/login")]
    public async Task<IActionResult> PanelLogin([FromBody] PanelLoginRequest request)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return BadRequest(GenericResponse.Error("invalid_key"));
        }

        ulong? userId;
        try
        {
            userId = await UserRepository.GetUserByRosettesKey(request.Key.Trim());
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, GenericResponse.Error("login_db_unavailable"));
        }

        if (userId is null || userId == 0)
        {
            return NotFound(GenericResponse.Error("key_not_found"));
        }

        return Ok(GenericResponse.Success("login_valid", new { user_id = userId.Value }));
    }

    [HttpPost("guild/{guildId}/reload")]
    public async Task<IActionResult> ReloadGuild(ulong guildId)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        bool success = await GuildEngine.ReloadRuntimeFields(guildId);
        if (!success)
        {
            return NotFound(GenericResponse.Error("guild_reload_failed"));
        }

        return Ok(GenericResponse.Success("guild_reloaded"));
    }

    [HttpPost("autoroles/{guildId}/reload")]
    public async Task<IActionResult> ReloadAutoroles(ulong guildId)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        bool success = await AutoRolesEngine.ReloadGuildFromDatabase(guildId);
        if (!success)
        {
            return NotFound(GenericResponse.Error("autoroles_reload_failed"));
        }

        return Ok(GenericResponse.Success("autoroles_reloaded"));
    }
}
