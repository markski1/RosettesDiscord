using Discord.WebSocket;
using Discord;
using Microsoft.AspNetCore.Mvc;
using Rosettes.Core;
using Rosettes.Managers;

namespace Rosettes.WebServer.API;

[ApiController]
[Route("rosapi")]
public class RootController : ControllerBase
{
    private const string SecretHeaderName = "X-Rosettes-Secret";
    private const int MaxMessageLength = 2000;

    [HttpGet("alive")]
    public string CheckAlive()
    {
        return "Rosettes lives!";
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage(ulong channelId, string message, int type = 0)
    {
        if (string.IsNullOrWhiteSpace(Settings.SecretKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse.Error("secret_not_configured"));
        }

        if (!Request.Headers.TryGetValue(SecretHeaderName, out var providedSecret) ||
            !string.Equals(providedSecret.ToString(), Settings.SecretKey, StringComparison.Ordinal))
        {
            return Unauthorized(ApiResponse.Error("unauthorized"));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(ApiResponse.Error("message_required"));
        }

        if (message.Length > MaxMessageLength)
        {
            return BadRequest(ApiResponse.Error("message_too_long"));
        }

        if (type is not 0 and not 1)
        {
            return BadRequest(ApiResponse.Error("invalid_message_type"));
        }

        try
        {
            var client = ServiceManager.GetService<DiscordSocketClient>();
            if (type == 0)
            {
                if (client.GetChannel(channelId) is not ITextChannel destinationChannel)
                {
                    return NotFound(ApiResponse.Error("channel_not_found"));
                }

                await destinationChannel.SendMessageAsync(message);
            }
            else
            {
                if (client.GetUser(channelId) is not { } user)
                {
                    return NotFound(ApiResponse.Error("user_not_found"));
                }

                await user.SendMessageAsync(message);
            }

            return Ok(ApiResponse.Success("message_sent"));
        }
        catch (Exception ex)
        {
            Global.GenerateErrorMessage("api-send-message", ex.Message);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Error("message_send_failed"));
        }
    }
}
