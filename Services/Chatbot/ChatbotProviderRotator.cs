namespace ProjectLedger.API.Services.Chatbot;

/// <summary>
/// Singleton that maintains the rotation index between providers.
/// Each call to <see cref="GetNext"/> advances the pointer atomically
/// and in a thread-safe manner, distributing the requests in a round-robin fashion.
/// </summary>
public sealed class ChatbotProviderRotator
{
    // Starts at -1 so the first Increment returns 0
    private int _index = -1;

    /// <summary>
    /// Returns the starting index for the next request and advances the pointer.
    /// The modulo operator ensures the index is always in [0, count).
    /// </summary>
    public int GetNext(int count)
    {
        if (count <= 0) return 0;

        // Interlocked.Increment is atomic — safe for concurrent access
        var next = Interlocked.Increment(ref _index);

        // Unsigned modulo to avoid negative numbers after int overflow
        return (int)((uint)next % (uint)count);
    }
}
