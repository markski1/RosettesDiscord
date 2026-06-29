using Microsoft.AspNetCore.Mvc;
using Rosettes.Database;
using Rosettes.Modules.Engine.Guild;
using System.Collections.Generic;

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

    public sealed class GuildSettingsRequest
    {
        public bool MessageParsing { get; init; }
        public bool RandomCommands { get; init; }
        public bool DumbCommands { get; init; }
        public bool Farm { get; init; }
        public bool VoiceAnnounce { get; init; }
    }

    [HttpPost("guild/{guildId}/settings")]
    public async Task<IActionResult> UpdateGuildSettings(ulong guildId, [FromBody] GuildSettingsRequest request)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        if (request is null)
        {
            return BadRequest(GenericResponse.Error("invalid_settings"));
        }

        bool ok = await GuildEngine.UpdateSettingsFromPanel(
            guildId,
            request.MessageParsing,
            request.RandomCommands,
            request.DumbCommands,
            request.Farm,
            request.VoiceAnnounce);

        if (!ok)
        {
            return NotFound(GenericResponse.Error("guild_settings_failed"));
        }

        return Ok(GenericResponse.Success("guild_settings_updated"));
    }

    public sealed class AutoroleEntryRequest
    {
        public string Emote { get; init; } = string.Empty;
        public ulong RoleId { get; init; }
    }

    public sealed class AutoroleCreateRequest
    {
        public string Name { get; init; } = string.Empty;
        public List<AutoroleEntryRequest> Entries { get; init; } = new();
    }

    [HttpPost("guild/{guildId}/autoroles")]
    public async Task<IActionResult> CreateAutoroleGroup(ulong guildId, [FromBody] AutoroleCreateRequest request)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(GenericResponse.Error("invalid_autorole"));
        }

        if (request.Entries.Count > 20)
        {
            return BadRequest(GenericResponse.Error("too_many_entries"));
        }

        var entries = request.Entries
            .Where(e => !string.IsNullOrEmpty(e.Emote) && e.RoleId != 0)
            .Select(e => (e.Emote, e.RoleId))
            .ToList();

        if (entries.Count == 0)
        {
            return BadRequest(GenericResponse.Error("no_entries"));
        }

        uint? groupId = await AutoRolesEngine.CreateGroupTransactional(guildId, request.Name.Trim(), entries);
        if (groupId is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, GenericResponse.Error("autorole_create_failed"));
        }

        return Ok(GenericResponse.Success("autorole_created", new { group_id = groupId.Value }));
    }

    [HttpDelete("guild/{guildId}/autoroles/{groupId}")]
    public async Task<IActionResult> DeleteAutoroleGroup(ulong guildId, uint groupId)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        bool ok = await AutoRolesEngine.DeleteGroupTransactional(guildId, groupId);
        if (!ok)
        {
            return NotFound(GenericResponse.Error("autorole_delete_failed"));
        }

        return Ok(GenericResponse.Success("autorole_deleted"));
    }
}
