namespace Flekt.Computer.Agent.Services;

/// <summary>
/// Result from OmniParser containing detected UI elements and annotated image.
/// </summary>
public class OmniParserResult
{
    /// <summary>
    /// Base64-encoded annotated image with bounding boxes.
    /// </summary>
    public required string AnnotatedImageBase64 { get; init; }

    /// <summary>
    /// Detected UI elements with their bounding boxes and content.
    /// </summary>
    public required List<OmniParserElement> Elements { get; init; }

    /// <summary>
    /// Width of the original image.
    /// </summary>
    public int ImageWidth { get; init; }

    /// <summary>
    /// Height of the original image.
    /// </summary>
    public int ImageHeight { get; init; }
}

/// <summary>
/// A detected UI element from OmniParser.
/// </summary>
public class OmniParserElement
{
    /// <summary>
    /// Element ID (index).
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Element type: "text" or "icon".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Description or text content of the element.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether the element is interactable (clickable).
    /// </summary>
    public bool Interactivity { get; init; }

    /// <summary>
    /// Bounding box coordinates scaled to screen dimensions [x1, y1, x2, y2].
    /// </summary>
    public required int[] BoundingBox { get; init; }

    /// <summary>
    /// Center point of the element (for clicking).
    /// </summary>
    public required int[] Center { get; init; }
}

/// <summary>
/// Interface for OmniParser UI element detection service.
/// </summary>
public interface IOmniParser
{
    /// <summary>
    /// Parse a screenshot to detect UI elements.
    /// </summary>
    /// <param name="screenshotBase64">Base64-encoded screenshot image.</param>
    /// <param name="imageWidth">Width of the image in pixels.</param>
    /// <param name="imageHeight">Height of the image in pixels.</param>
    /// <param name="boxThreshold">Detection confidence threshold (0.0-1.0). Higher = fewer but more confident detections.</param>
    /// <param name="iouThreshold">IOU threshold for non-maximum suppression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>OmniParser result with detected elements and annotated image.</returns>
    Task<OmniParserResult> ParseAsync(
        string screenshotBase64,
        int imageWidth,
        int imageHeight,
        double boxThreshold = 0.3,
        double iouThreshold = 0.1,
        CancellationToken cancellationToken = default);
}
