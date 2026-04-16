using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ProjectLedger.API.DTOs.Chatbot;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Controllers;

/// <summary>
/// Streams AI-powered financial assistant responses using Server-Sent Events.
/// Requires JWT authentication and the <c>Plan:CanUseApi</c> policy.
/// Each request runs a 2-step LLM pipeline: intent classification followed by streaming response generation.
/// </summary>
[ApiController]
[Route("api/chatbot")]
[Authorize]
[Authorize(Policy = "Plan:CanUseApi")]
[Tags("Chatbot")]
[Produces("application/json")]
public class ChatbotController : ControllerBase
{
    private static readonly JsonSerializerOptions _sseJsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition      = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    /// <summary>
    /// Streams the chatbot response token by token using Server-Sent Events (text/event-stream).
    /// The intent parsing and backend routing happen upfront (non-streaming), then the final
    /// LLM response is streamed. Emits the following SSE event types in order:
    /// - type:"meta"  → sent once before the first token; contains pipeline metadata.
    /// - type:"chunk" → one partial text token per event.
    /// - type:"done"  → sent once after the last chunk to signal stream completion.
    /// On pipeline failure, a type:"error" event is emitted instead.
    /// </summary>
    [EnableRateLimiting("ChatbotRateLimit")]
    [HttpPost("message/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [Produces("text/event-stream")]
    public async Task StreamMessage(
        [FromBody] ChatbotMessageRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl    = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // Disable Nginx proxy buffering

        var userId = User.GetRequiredUserId();

        try
        {
            await foreach (var evt in _chatbotService.StreamMessageAsync(
                userId, request.Message, request.History, ct))
            {
                var json = JsonSerializer.Serialize(evt, _sseJsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (InvalidOperationException ex)
        {
            // Map known internal error keys to user-friendly messages.
            // Never forward raw exception messages to the client.
            var safeMessage = ex.Message == "ChatbotNoProvidersEnabled"
                ? "No AI providers are currently available. Please try again later."
                : "An error occurred while processing your request. Please try again.";

            var errorJson = JsonSerializer.Serialize(
                new { type = "error", content = safeMessage }, _sseJsonOptions);
            await Response.WriteAsync($"data: {errorJson}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
