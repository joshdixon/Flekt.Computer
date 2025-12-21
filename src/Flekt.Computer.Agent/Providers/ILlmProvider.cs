using Flekt.Computer.Agent.Models;

namespace Flekt.Computer.Agent.Providers;

public interface ILlmProvider
{
    /// <summary>
    /// Stream chat completion with tool calls support.
    /// </summary>
    IAsyncEnumerable<AgentResult> StreamChatAsync(
        IEnumerable<LlmMessage> messages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supported capabilities of this provider.
    /// </summary>
    LlmCapabilities Capabilities { get; }
}

public class LlmCapabilities
{
    public bool SupportsVision { get; init; }
    public bool SupportsTools { get; init; }
    public bool SupportsStreaming { get; init; }
    public int MaxImageSize { get; init; }
    public int ContextWindow { get; init; }
}

