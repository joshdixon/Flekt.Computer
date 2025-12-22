using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Agent.Services;

/// <summary>
/// OmniParser client that calls the Flekt Computer API.
/// </summary>
public class CloudOmniParser : IOmniParser
{
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;
    private readonly ILogger<CloudOmniParser>? _logger;

    /// <summary>
    /// Create a CloudOmniParser client.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Flekt Computer API (e.g., "https://api.computer.flekt.co")</param>
    /// <param name="logger">Optional logger</param>
    public CloudOmniParser(
        string baseUrl,
        ILogger<CloudOmniParser>? logger = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    public async Task<OmniParserResult> ParseAsync(
        string screenshotBase64,
        int imageWidth,
        int imageHeight,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("CloudOmniParser: Sending {Width}x{Height} image", imageWidth, imageHeight);

        var request = new OmniParserRequest
        {
            Image = screenshotBase64,
            Width = imageWidth,
            Height = imageHeight,
            BoxThreshold = 0.3,
            IouThreshold = 0.1,
            UseOcr = true
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_baseUrl}/api/omniparser/parse",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("CloudOmniParser: API error {StatusCode}: {Error}",
                response.StatusCode, errorBody);
            throw new HttpRequestException($"OmniParser API error: {response.StatusCode} - {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OmniParserResponse>(
            JsonOptions, cancellationToken);

        if (result == null)
        {
            throw new InvalidOperationException("OmniParser returned null response");
        }

        _logger?.LogInformation("CloudOmniParser: Detected {Count} elements in {Latency}ms",
            result.Elements.Count, result.LatencyMs);

        var elements = result.Elements.Select(e => new OmniParserElement
        {
            Id = e.Id,
            Type = e.Type,
            Content = e.Content ?? "",
            Interactivity = e.Interactivity,
            BoundingBox = e.BboxPixels,
            Center = e.Center
        }).ToList();

        return new OmniParserResult
        {
            AnnotatedImageBase64 = result.AnnotatedImage,
            Elements = elements,
            ImageWidth = result.Width,
            ImageHeight = result.Height
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
}

internal class OmniParserRequest
{
    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("box_threshold")]
    public double BoxThreshold { get; set; } = 0.3;

    [JsonPropertyName("iou_threshold")]
    public double IouThreshold { get; set; } = 0.1;

    [JsonPropertyName("use_ocr")]
    public bool UseOcr { get; set; } = true;
}

internal class OmniParserResponse
{
    [JsonPropertyName("elements")]
    public List<OmniParserElementDto> Elements { get; set; } = new();

    [JsonPropertyName("annotated_image")]
    public string AnnotatedImage { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("num_icons")]
    public int NumIcons { get; set; }

    [JsonPropertyName("num_text")]
    public int NumText { get; set; }

    [JsonPropertyName("latency_ms")]
    public double LatencyMs { get; set; }

    [JsonPropertyName("device")]
    public string Device { get; set; } = "";
}

internal class OmniParserElementDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("bbox_pixels")]
    public int[] BboxPixels { get; set; } = Array.Empty<int>();

    [JsonPropertyName("center")]
    public int[] Center { get; set; } = Array.Empty<int>();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("interactivity")]
    public bool Interactivity { get; set; }
}
