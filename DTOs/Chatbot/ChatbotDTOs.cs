using System.ComponentModel.DataAnnotations;

namespace ProjectLedger.API.DTOs.Chatbot;

/// <summary>Request para enviar un mensaje al chatbot.</summary>
public class ChatbotMessageRequest
{
    /// <summary>Texto del mensaje del usuario.</summary>
    [Required]
    [MinLength(1)]
    [MaxLength(4000)]
    public string Message { get; set; } = string.Empty;
}

/// <summary>Respuesta del chatbot con metadata del proveedor utilizado.</summary>
public class ChatbotMessageResponse
{
    /// <summary>Texto de respuesta generado por el modelo de IA.</summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>Nombre del proveedor que procesó la petición (OpenRouter, Groq, Cerebras, BytePlus).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Identificador del modelo utilizado dentro del proveedor.</summary>
    public string Model { get; set; } = string.Empty;
}
