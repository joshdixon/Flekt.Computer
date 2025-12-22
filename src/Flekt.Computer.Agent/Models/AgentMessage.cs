namespace Flekt.Computer.Agent.Models;

public enum AgentRole
{
    System,
    User,
    Assistant,
    Tool
}

public class AgentMessage
{
    public required AgentRole Role { get; init; }
    public string? Content { get; init; }
    public string? ToolCallId { get; init; }
    public List<ToolCall>? ToolCalls { get; init; }
    public List<ImageContent>? Images { get; init; }
}

public class ImageContent
{
    public required string Base64Data { get; init; }
    public string MimeType { get; init; } = "image/png";
    public int? Width { get; init; }
    public int? Height { get; init; }
}

public class ToolCall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Arguments { get; init; } // JSON string
}

public enum AgentResultType
{
    Reasoning,      // Thinking/planning from the model
    ToolCall,       // Model wants to execute a tool
    Screenshot,     // Screenshot taken after action
    Message,        // Final text response
    Error           // Error occurred
}

public class AgentResult
{
    public required AgentResultType Type { get; init; }
    public string? Content { get; init; }
    public ToolCall? ToolCall { get; init; }
    public string? Screenshot { get; init; } // Base64
    public string? AnnotatedScreenshot { get; init; } // Base64 - OmniParser annotated version
    public AgentMessage? Message { get; init; }
    public bool ContinueLoop { get; init; } = true;
    public AgentUsage? Usage { get; init; }
}

public class AgentUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens { get; init; }
    public decimal? EstimatedCost { get; init; }
}

public class ComputerAgentOptions
{
    public bool ScreenshotAfterAction { get; init; } = true;
    public TimeSpan ScreenshotDelay { get; init; } = TimeSpan.FromMilliseconds(500);
    public int MaxIterations { get; init; } = 100;
    public int OnlyNMostRecentScreenshots { get; init; } = 3;
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Enable OmniParser for UI element detection. When enabled, screenshots
    /// will be processed by OmniParser to detect UI elements with bounding boxes.
    /// This dramatically improves click accuracy for all models.
    /// </summary>
    public bool EnableOmniParser { get; init; } = false;

    /// <summary>
    /// Base URL for the Flekt Computer API (e.g., "https://api.computer.flekt.co").
    /// Required if EnableOmniParser is true.
    /// </summary>
    public string? OmniParserBaseUrl { get; init; }
}


