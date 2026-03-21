namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Singleton que mantiene el índice de rotación entre proveedores.
/// Cada llamada a <see cref="GetNext"/> avanza el puntero de forma atómica
/// y thread-safe, distribuyendo las peticiones en round-robin.
/// </summary>
public sealed class ChatbotProviderRotator
{
    // Empieza en -1 para que el primer Increment devuelva 0
    private int _index = -1;

    /// <summary>
    /// Devuelve el índice de inicio para la próxima petición y avanza el puntero.
    /// El módulo garantiza que el índice siempre esté en [0, count).
    /// </summary>
    public int GetNext(int count)
    {
        if (count <= 0) return 0;

        // Interlocked.Increment es atómico — seguro para acceso concurrente
        var next = Interlocked.Increment(ref _index);

        // Módulo unsigned para evitar negativos tras overflow de int
        return (int)((uint)next % (uint)count);
    }
}
