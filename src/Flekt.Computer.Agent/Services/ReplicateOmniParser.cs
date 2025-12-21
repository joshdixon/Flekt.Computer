using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Flekt.Computer.Agent.Services;

/// <summary>
/// OmniParser implementation using the Replicate API.
/// </summary>
public class ReplicateOmniParser : IOmniParser
{
    private readonly string _apiToken;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReplicateOmniParser>? _logger;
    private readonly ReplicateOmniParserOptions _options;

    private const string ReplicateApiUrl = "https://api.replicate.com/v1/predictions";
    private const string OmniParserVersion = "microsoft/omniparser-v2:49cf3d41b8d3aca1360514e83be4c97131ce8f0d99abfc365526d8384caa88df";

    public ReplicateOmniParser(
        string apiToken,
        ReplicateOmniParserOptions? options = null,
        ILogger<ReplicateOmniParser>? logger = null)
    {
        _apiToken = apiToken;
        _options = options ?? new ReplicateOmniParserOptions();
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiToken}");
        _httpClient.DefaultRequestHeaders.Add("Prefer", "wait"); // Synchronous mode
    }

    public async Task<OmniParserResult> ParseAsync(
        string screenshotBase64,
        int imageWidth,
        int imageHeight,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("OmniParser: Starting analysis of {Width}x{Height} image", imageWidth, imageHeight);

        // Create the prediction request
        var request = new
        {
            version = OmniParserVersion,
            input = new
            {
                image = $"data:image/png;base64,{screenshotBase64}",
                imgsz = _options.ImageSize,
                box_threshold = _options.BoxThreshold,
                iou_threshold = _options.IouThreshold
            }
        };

        var response = await _httpClient.PostAsJsonAsync(ReplicateApiUrl, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger?.LogError("OmniParser: API error {StatusCode}: {Error}", response.StatusCode, errorBody);
            throw new HttpRequestException($"Replicate API error: {response.StatusCode} - {errorBody}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger?.LogDebug("OmniParser: Raw response: {Response}", json[..Math.Min(500, json.Length)]);

        var result = JsonSerializer.Deserialize<ReplicatePredictionResponse>(json, JsonOptions);

        if (result?.Output == null)
        {
            _logger?.LogError("OmniParser: No output in response");
            throw new InvalidOperationException("OmniParser returned no output");
        }

        // Download the annotated image
        var annotatedImageBase64 = await DownloadImageAsBase64(result.Output.Img, cancellationToken);

        // Parse the elements from the text output
        var elements = ParseElements(result.Output.Elements, imageWidth, imageHeight);

        _logger?.LogInformation("OmniParser: Detected {Count} UI elements", elements.Count);

        return new OmniParserResult
        {
            AnnotatedImageBase64 = annotatedImageBase64,
            Elements = elements,
            ImageWidth = imageWidth,
            ImageHeight = imageHeight
        };
    }

    private async Task<string> DownloadImageAsBase64(string imageUrl, CancellationToken cancellationToken)
    {
        _logger?.LogDebug("OmniParser: Downloading annotated image from {Url}", imageUrl);

        var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        return Convert.ToBase64String(imageBytes);
    }

    private List<OmniParserElement> ParseElements(string elementsText, int imageWidth, int imageHeight)
    {
        var elements = new List<OmniParserElement>();
        var lines = elementsText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                // Parse: "icon 0: {'type': 'text', 'bbox': [0.83, 0.03, 0.89, 0.05], 'interactivity': False, 'content': 'Networks'}"
                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0) continue;

                var prefix = line[..colonIndex].Trim();
                var idPart = prefix.Split(' ').LastOrDefault();
                if (!int.TryParse(idPart, out int id)) continue;

                var jsonPart = line[(colonIndex + 1)..].Trim();

                // Convert Python dict syntax to JSON
                jsonPart = jsonPart
                    .Replace("'", "\"")
                    .Replace("True", "true")
                    .Replace("False", "false");

                using var doc = JsonDocument.Parse(jsonPart);
                var root = doc.RootElement;

                var type = root.GetProperty("type").GetString() ?? "unknown";
                var content = root.GetProperty("content").GetString() ?? "";
                var interactivity = root.GetProperty("interactivity").GetBoolean();

                var bboxArray = root.GetProperty("bbox");
                var bbox = new double[4];
                for (int i = 0; i < 4; i++)
                {
                    bbox[i] = bboxArray[i].GetDouble();
                }

                // Scale normalized coordinates to screen dimensions
                var scaledBbox = new int[]
                {
                    (int)(bbox[0] * imageWidth),
                    (int)(bbox[1] * imageHeight),
                    (int)(bbox[2] * imageWidth),
                    (int)(bbox[3] * imageHeight)
                };

                var center = new int[]
                {
                    (int)((bbox[0] + bbox[2]) / 2 * imageWidth),
                    (int)((bbox[1] + bbox[3]) / 2 * imageHeight)
                };

                elements.Add(new OmniParserElement
                {
                    Id = id,
                    Type = type,
                    Content = content,
                    Interactivity = interactivity,
                    BoundingBox = scaledBbox,
                    Center = center
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OmniParser: Failed to parse element line: {Line}", line);
            }
        }

        return elements;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>
/// Options for Replicate OmniParser.
/// </summary>
public class ReplicateOmniParserOptions
{
    /// <summary>
    /// Image size for detection (default: 640).
    /// </summary>
    public int ImageSize { get; init; } = 640;

    /// <summary>
    /// Box detection threshold (default: 0.05).
    /// </summary>
    public double BoxThreshold { get; init; } = 0.05;

    /// <summary>
    /// IOU threshold for deduplication (default: 0.1).
    /// </summary>
    public double IouThreshold { get; init; } = 0.1;
}

// Response models for Replicate API
internal class ReplicatePredictionResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("output")]
    public ReplicateOutput? Output { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal class ReplicateOutput
{
    [JsonPropertyName("img")]
    public string Img { get; set; } = "";

    [JsonPropertyName("elements")]
    public string Elements { get; set; } = "";
}
