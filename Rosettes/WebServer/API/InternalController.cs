using Microsoft.AspNetCore.Mvc;
using Rosettes.Database;
using Rosettes.Modules.Engine.Guild;
using Rosettes.Managers;
using Discord;
using Discord.WebSocket;
using System.Collections.Generic;
using System.Security.Cryptography;

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
            return BadRequest(ApiResponse.Error("invalid_key"));
        }

        ulong? userId;
        try
        {
            userId = await UserRepository.GetUserByRosettesKey(request.Key.Trim());
        }
        catch
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Error("login_db_unavailable"));
        }

        if (userId is null || userId == 0)
        {
            return NotFound(ApiResponse.Error("key_not_found"));
        }

        return Ok(ApiResponse.Success("login_valid", new { user_id = userId.Value }));
    }

    private static string GenerateApplicationToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public sealed class AppCreateRequest
    {
        public string Name { get; init; } = string.Empty;
        public ulong OwnerId { get; init; }
    }

    public sealed class AppOwnerRequest
    {
        public ulong OwnerId { get; init; }
    }


    [HttpPost("apps")]
    public async Task<IActionResult> CreateApplication([FromBody] AppCreateRequest request)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        string name = request.Name.Trim();
        if (request.OwnerId == 0 || name.Length < 3 || name.Length > 50)
        {
            return BadRequest(ApiResponse.Error("invalid_application"));
        }

        string token = GenerateApplicationToken();
        int? appId = await AuthRepository.CreateApplication(name, request.OwnerId, token);
        if (appId is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Error("application_create_failed"));
        }

        return Ok(ApiResponse.Success("application_created", new { app_id = appId.Value, token }));
    }


    [HttpDelete("apps/{appId}")]
    public async Task<IActionResult> DeleteApplication(int appId, [FromBody] AppOwnerRequest request)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        if (appId <= 0 || request.OwnerId == 0)
        {
            return BadRequest(ApiResponse.Error("invalid_application"));
        }

        bool ok = await AuthRepository.DeleteApplication(appId, request.OwnerId);
        if (!ok)
        {
            return NotFound(ApiResponse.Error("application_not_found"));
        }

        return Ok(ApiResponse.Success("application_deleted"));
    }

    [HttpDelete("apps/{appId}/users/{userId}")]
    public async Task<IActionResult> RevokeApplicationUser(int appId, ulong userId, [FromBody] AppOwnerRequest request)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        if (appId <= 0 || request.OwnerId == 0 || userId == 0)
        {
            return BadRequest(ApiResponse.Error("invalid_application_user"));
        }

        bool ok = await AuthRepository.RevokeApplicationUser(appId, request.OwnerId, userId);
        if (!ok)
        {
            return NotFound(ApiResponse.Error("application_user_not_found"));
        }

        return Ok(ApiResponse.Success("application_user_revoked"));
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
            return NotFound(ApiResponse.Error("guild_reload_failed"));
        }

        return Ok(ApiResponse.Success("guild_reloaded"));
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
            return NotFound(ApiResponse.Error("autoroles_reload_failed"));
        }

        return Ok(ApiResponse.Success("autoroles_reloaded"));
    }

    [HttpGet("guild/{guildId}/channels")]
    public IActionResult GetGuildChannels(ulong guildId)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        var client = ServiceManager.GetService<DiscordSocketClient>();
        var socketGuild = client.GetGuild(guildId);
        if (socketGuild is null)
        {
            return NotFound(ApiResponse.Error("guild_not_found"));
        }

        var channels = new List<object>();

        foreach (var c in socketGuild.TextChannels)
        {
            channels.Add(new { id = c.Id, name = c.Name, type = "text" });
        }
        foreach (var c in socketGuild.VoiceChannels)
        {
            channels.Add(new { id = c.Id, name = c.Name, type = "voice" });
        }
        foreach (var c in socketGuild.StageChannels)
        {
            channels.Add(new { id = c.Id, name = c.Name, type = "stage" });
        }
        foreach (var c in socketGuild.ForumChannels)
        {
            channels.Add(new { id = c.Id, name = c.Name, type = "forum" });
        }

        return Ok(ApiResponse.Success("guild_channels", new { channels }));
    }

    [HttpGet("guild/{guildId}/roles/live")]
    public IActionResult GetGuildRolesLive(ulong guildId)
    {
        if (!InternalApi.IsAuthorized(Request))
        {
            return InternalApi.UnauthorizedResult();
        }

        var client = ServiceManager.GetService<DiscordSocketClient>();
        var socketGuild = client.GetGuild(guildId);
        if (socketGuild is null)
        {
            return NotFound(ApiResponse.Error("guild_not_found"));
        }

        var roles = socketGuild.Roles
            .OrderByDescending(r => r.Position)
            .Select(r => new
            {
                id = r.Id,
                name = r.Name,
                color = r.Colors.PrimaryColor.ToString(),
                position = r.Position,
                managed = r.IsManaged,
                isEveryone = r.IsEveryone
            })
            .ToList();

        return Ok(ApiResponse.Success("guild_roles_live", new { roles }));
    }

    public sealed class GuildSettingsRequest
    {
        public bool MessageParsing { get; init; }
        public bool RandomCommands { get; init; }
        public bool DumbCommands { get; init; }
        public bool Farm { get; init; }
        public bool VoiceAnnounce { get; init; }
        public ulong DefaultRole { get; init; }
        public ulong LogChannel { get; init; }
        public ulong FarmChannel { get; init; }
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
            return BadRequest(ApiResponse.Error("invalid_settings"));
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
            return NotFound(ApiResponse.Error("guild_settings_failed"));
        }

        bool runtimeOk = await GuildEngine.UpdateRuntimeFieldsFromPanel(
            guildId,
            request.DefaultRole,
            request.LogChannel,
            request.FarmChannel);

        if (!runtimeOk)
        {
            return NotFound(ApiResponse.Error("guild_runtime_fields_failed"));
        }

        return Ok(ApiResponse.Success("guild_settings_updated"));
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
            return BadRequest(ApiResponse.Error("invalid_autorole"));
        }

        if (request.Entries.Count > 20)
        {
            return BadRequest(ApiResponse.Error("too_many_entries"));
        }

        var entries = request.Entries
            .Where(e => !string.IsNullOrEmpty(e.Emote) && e.RoleId != 0)
            .Select(e => (e.Emote, e.RoleId))
            .ToList();

        if (entries.Count == 0)
        {
            return BadRequest(ApiResponse.Error("no_entries"));
        }

        uint? groupId = await AutoRolesEngine.CreateGroupTransactional(guildId, request.Name.Trim(), entries);
        if (groupId is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Error("autorole_create_failed"));
        }

        return Ok(ApiResponse.Success("autorole_created", new { group_id = groupId.Value }));
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
            return NotFound(ApiResponse.Error("autorole_delete_failed"));
        }

        return Ok(ApiResponse.Success("autorole_deleted"));
    }
}
