using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatGPTv3.HttpSse;

/// <summary>
/// A multi-modal content part. Can be text or image_url.
/// </summary>
public class ContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImageUrl? ImageUrl { get; set; }

    /// <summary>
    /// Creates a text content part.
    /// </summary>
    public static ContentPart FromText(string text) => new() { Type = "text", Text = text };

    /// <summary>
    /// Creates an image content part from a URL or base64 data URI.
    /// </summary>
    public static ContentPart FromImageUrl(string url, string? detail = null) =>
        new() { Type = "image_url", ImageUrl = new ImageUrl { Url = url, Detail = detail } };

    /// <summary>
    /// Creates an image content part from raw bytes (auto-converts to base64 data URI).
    /// </summary>
    public static ContentPart FromImageBytes(byte[] imageBytes, string mimeType = "image/jpeg", string? detail = null)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        return new()
        {
            Type = "image_url",
            ImageUrl = new ImageUrl { Url = $"data:{mimeType};base64,{base64}", Detail = detail }
        };
    }

    /// <summary>
    /// Creates an image content part from an image file path.
    /// </summary>
    public static ContentPart FromImageFile(string filePath, string? detail = null)
    {
        var bytes = File.ReadAllBytes(filePath);
        var mimeType = GetMimeType(filePath);
        return FromImageBytes(bytes, mimeType, detail);
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "image/jpeg"
        };
    }

    /// <summary>
    /// Serializes to the format expected by the OpenAI API.
    /// </summary>
    public object ToSerializable()
    {
        if (Type == "text")
        {
            return new { type = "text", text = Text };
        }
        if (Type == "image_url" && ImageUrl != null)
        {
            return new { type = "image_url", image_url = ImageUrl };
        }
        throw new InvalidOperationException($"Unknown content part type: {Type}");
    }
}

/// <summary>
/// Image URL descriptor for multi-modal content.
/// </summary>
public class ImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Detail { get; set; }
}
