using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectLedger.API.DTOs.Chatbot;
using ProjectLedger.API.Services.Chatbot.Interfaces;

namespace ProjectLedger.API.Controllers;

[ApiController]
[Route("api/chatbot")]
[Authorize]
[Tags("Chatbot")]
[Produces("application/json")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    /// <summary>
    /// Envía un mensaje al chatbot de IA con contexto financiero real inyectado.
    /// Incluye el resumen mensual actual y los pagos vencidos en el system prompt.
    /// Soporta historial de conversación para respuestas de seguimiento coherentes.
    /// Cada petición usa el siguiente proveedor en la rotación (OpenRouter → Groq → Cerebras → BytePlus).
    /// Si el proveedor asignado no está disponible, se pasa automáticamente al siguiente.
    /// </summary>
    [HttpPost("message")]
    [ProducesResponseType(typeof(ChatbotMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendMessage(
        [FromBody] ChatbotMessageRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId   = User.GetRequiredUserId();
            var response = await _chatbotService.SendMessageAsync(
                userId,
                request.Message,
                request.History,
                ct);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }
}
