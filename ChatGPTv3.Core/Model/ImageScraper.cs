using System.Security.Cryptography;
using Another_Mirai_Native.Abstractions.Models.MessageItem;
using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Image scraping — describe chat images using a vision model.
///
/// Flow:
///   1. Image arrives in message chain → check hash
///   2. Hash in Picture table? → reuse cached description
///   3. No cache → download + vision model describe + save
///
/// Different prompts for emojis vs normal images (from v2):
///   - Emoji: describe the expression + emotion
///   - Picture: describe content + text + guess meaning
/// </summary>
public static class ImageScraper
{
    private const string PicturePrompt = "请用中文描述这张图片的内容。如果有文字，请把文字都描述出来。并尝试猜测这个图片的含义。最多200个字。";
    private const string EmojiPrompt = "这是一个表情包，使用中文简洁的描述一下表情包的内容和表情包所表达的情感。";

    /// <summary>
    /// Set by Entry during startup. Resolves an image hash to a local file path
    /// via the AMN2 framework (TryGetImageByHashAsync).
    /// </summary>
    public static Func<string, Task<string?>>? TryResolveImageHash { get; set; }

    /// <summary>
    /// Process an image from the message chain.
    /// Returns the description text for context injection, or null if processing should be skipped.
    /// </summary>
    public static async Task<string?> DescribeAsync(Image image, bool isMentioned)
    {
        if (!ShouldProcess(image, isMentioned))
            return null;

        // ── Resolve file path: use FilePath first, fall back to hash lookup ──
        var filePath = await ResolveFilePathAsync(image);
        if (string.IsNullOrEmpty(filePath))
            return null;

        // ── Compute hash from file content ──
        var hash = ComputeMD5(filePath);
        if (string.IsNullOrEmpty(hash))
            return null;

        // ── Check cache ──
        var cached = Picture.FindByHash(hash);
        if (cached != null && !string.IsNullOrEmpty(cached.Description))
        {
            cached.UseCount++;
            cached.LastUsedAt = DateTime.Now;
            Picture.Upsert(cached);
            return FormatResult(hash, image.IsEmoji, cached.Description);
        }

        // ── Describe with vision model ──
        var keys = AppConfig.ImageDescriberApiKeyId;
        if (keys.Count == 0) return null;

        var prompt = image.IsEmoji ? EmojiPrompt : PicturePrompt;
        var description = await CallVisionModel(keys, prompt, filePath);
        if (string.IsNullOrEmpty(description))
            return null;

        // ── Save to cache ──
        Picture.Upsert(new Picture
        {
            Md5 = hash,
            FilePath = filePath,
            IsEmoji = image.IsEmoji,
            Description = description,
            Time = DateTime.Now,
            LastUsedAt = DateTime.Now
        });

        return FormatResult(hash, image.IsEmoji, description);
    }

    // ── File path resolution ──────────────────────────────

    private static async Task<string?> ResolveFilePathAsync(Image image)
    {
        // FilePath exists — use it directly
        if (File.Exists(image.FilePath))
            return image.FilePath;

        if (!string.IsNullOrEmpty(image.Hash) && Entry.MessageApi != null)
        {
            (bool resolved, string filePath) = await Entry.MessageApi.TryGetImageByHashAsync(image.Hash);
            if (resolved && File.Exists(filePath))
                return filePath;
        }

        CommonHelper.LogWarning?.Invoke("Scraper", $"无法解析图片路径: hash={image.Hash} file={image.FilePath}");
        return null;
    }

    // ── Decision logic ────────────────────────────────────

    private static bool ShouldProcess(Image image, bool isMentioned)
    {
        if (!AppConfig.EnableVision)
            return false;

        if (AppConfig.EnableVisionWhenMentioned && !isMentioned)
            return false;

        if (AppConfig.IgnoreNotEmoji && !image.IsEmoji)
            return false;

        return true;
    }

    // ── Vision model call ─────────────────────────────────

    private static async Task<string?> CallVisionModel(
        List<APIKeyPurpose> keys, string prompt, string filePath)
    {
        try
        {
            var key = keys.OrderBy(_ => Guid.NewGuid()).FirstOrDefault();
            if (key?.Key == null || key.Model == null) return null;

            var messages = new List<ChatMessage>
            {
                ChatMessage.System(prompt),
                ChatMessage.UserWithImageFile(filePath)
            };

            var chatService = new ChatService();
            var result = await chatService.GetChatResultAsync(
                keys, messages,
                ChatService.Purpose.图片描述,
                timeout: AppConfig.ImageDescriberTimeout);

            if (result == ChatService.ErrorMessage || string.IsNullOrWhiteSpace(result))
                return null;

            return result.Trim();
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("Scraper", $"视觉模型调用失败: {ex.Message}");
            return null;
        }
    }

    // ── Hash ──────────────────────────────────────────────

    private static string? ComputeMD5(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return null;
            using var stream = File.OpenRead(filePath);
            var hash = MD5.HashData(stream);
            return Convert.ToHexString(hash).ToUpperInvariant();
        }
        catch (Exception ex)
        {
            CommonHelper.LogWarning?.Invoke("Scraper", $"计算MD5失败: {ex.Message}");
            return null;
        }
    }

    // ── Formatting ────────────────────────────────────────

    private static string FormatResult(string hash, bool isEmoji, string description)
    {
        var type = isEmoji ? "表情包" : "图片";
        return $"[{type} hash:{hash};描述:{description}]";
    }
}
