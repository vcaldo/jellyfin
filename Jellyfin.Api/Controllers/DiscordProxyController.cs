using System;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Api.Controllers;

/// <summary>
/// Proxies requests from the web UI to the Discord bot over the Docker network.
/// The bot URL is read from the DISCORD_BOT_URL environment variable (default: http://discord-bot:3000).
/// </summary>
[Route("Discord")]
[Authorize]
public class DiscordProxyController(
    IHttpClientFactory httpClientFactory,
    ILogger<DiscordProxyController> logger) : BaseJellyfinApiController
{
    private readonly string _botBaseUrl =
        Environment.GetEnvironmentVariable("DISCORD_BOT_URL")?.TrimEnd('/')
        ?? "http://discord-bot:3000";

    /// <summary>
    /// Proxy a play request to the Discord bot.
    /// </summary>
    /// <response code="200">Request proxied successfully.</response>
    /// <response code="502">Discord bot is unreachable.</response>
    /// <returns>The bot's JSON response.</returns>
    [HttpPost("Play")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<ActionResult> Play() => ProxyPost("/api/play");

    /// <summary>
    /// Proxy a pause request to the Discord bot.
    /// </summary>
    /// <response code="200">Request proxied successfully.</response>
    /// <response code="502">Discord bot is unreachable.</response>
    /// <returns>The bot's JSON response.</returns>
    [HttpPost("Pause")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<ActionResult> Pause() => ProxyPost("/api/pause");

    /// <summary>
    /// Proxy a resume request to the Discord bot.
    /// </summary>
    /// <response code="200">Request proxied successfully.</response>
    /// <response code="502">Discord bot is unreachable.</response>
    /// <returns>The bot's JSON response.</returns>
    [HttpPost("Resume")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<ActionResult> Resume() => ProxyPost("/api/resume");

    /// <summary>
    /// Proxy a stop request to the Discord bot.
    /// </summary>
    /// <response code="200">Request proxied successfully.</response>
    /// <response code="502">Discord bot is unreachable.</response>
    /// <returns>The bot's JSON response.</returns>
    [HttpPost("Stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public Task<ActionResult> Stop() => ProxyPost("/api/stop");

    private async Task<ActionResult> ProxyPost(string path)
    {
        var targetUrl = _botBaseUrl + path;

        logger.LogDebug("Proxying POST to Discord bot: {Url}", targetUrl);

        try
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync().ConfigureAwait(false);

            var client = httpClientFactory.CreateClient(NamedClient.Default);
            using var content = new StringContent(body, Encoding.UTF8, MediaTypeNames.Application.Json);
            using var response = await client.PostAsync(new Uri(targetUrl), content).ConfigureAwait(false);

            var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return new ContentResult
            {
                Content = responseBody,
                ContentType = MediaTypeNames.Application.Json,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Failed to reach Discord bot at {Url}", targetUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Discord bot unreachable", detail = ex.Message });
        }
    }
}
