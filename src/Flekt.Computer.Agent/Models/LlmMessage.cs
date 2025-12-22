using System.Text.Json;

namespace Flekt.Computer.Agent.Models;

/// <summary>
/// Internal message format for LLM providers
/// </summary>
public class LlmMessage
{
    public required string Role { get; init; }
    public List<LlmContent> Content { get; init; } = new();
    public List<ToolCall>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }

    /// <summary>
    /// Reasoning/thinking content from models that support it.
    /// May be encrypted/redacted depending on the provider.
    /// </summary>
    public string? Reasoning { get; set; }

    /// <summary>
    /// Raw reasoning_details from OpenRouter/Gemini responses.
    /// Must be preserved and sent back unchanged for reasoning continuity.
    /// </summary>
    public JsonElement? ReasoningDetails { get; set; }
}

public class LlmContent
{
    public required string Type { get; init; } // "text" or "image_url"
    public string? Text { get; init; }
    public ImageUrl? ImageUrl { get; init; }
}

public class ImageUrl
{
    public required string Url { get; init; } // data:image/png;base64,...
}


