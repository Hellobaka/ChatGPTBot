using System.Security.Cryptography;
using ChatGPTv3.Core.Api;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.DB;
using ChatGPTv3.Core.Utilities;
using ChatGPTv3.OpenAIClient;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Pure image description — given a file path, produces a textual description.
///
/// Caching: results are saved to the Picture table keyed by MD5 hash.
/// </summary>
public static class ImageScraper
{
    public const string PicturePrompt = "请用中文描述这张图片的内容。如果有文字，请把文字都描述出来。并尝试猜测这个图片的含义。最多200个字。";
    public const string EmojiPrompt = "这是一个表情包，使用中文简洁的描述一下表情包的内容和表情包所表达的情感。";

    /// <summary>
    /// Describe an image file using a vision model.
    /// Returns null if the image can't be read or the vision model fails.
    /// </summary>
    /// <param name="filePath">Absolute path to the image file.</param>
    /// <param name="extraPrompt">Optional extra instructions appended to the base prompt.</param>
    /// <param name="isEmoji">Whether this is an emoji/sticker (saved to DB for filtering).</param>
    public static async Task<string?> DescribeAsync(string filePath, string? extraPrompt = null, bool isEmoji = false)
    {
        if (!File.Exists(filePath))
            return null;

        // ── Compute hash ──
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

            // Ensure Qdrant has this (may have been lost on restart)
            _ = Task.Run(() =>
            {
                try { MemoryManager.Qdrant?.InsertWithId(cached.Description, QdrantService.ImageCollectionName, hash); }
                catch { }
            });

            return cached.Description;
        }

        // ── Build prompt ──
        var prompt = !string.IsNullOrEmpty(extraPrompt)
            ? $"{PicturePrompt}\n额外要求：{extraPrompt}"
            : PicturePrompt;

        // ── Call vision model ──
        var keys = AppConfig.ImageDescriberApiKeyId;
        if (keys.Count == 0) return null;

        var description = await CallVisionModel(keys, prompt, filePath);
        if (string.IsNullOrEmpty(description))
            return null;

        // ── Save to cache ──
        Picture.Upsert(new Picture
        {
            Md5 = hash,
            FilePath = filePath,
            IsEmoji = isEmoji,
            Description = description,
            Time = DateTime.Now,
            LastUsedAt = DateTime.Now
        });

        // ── Index in Qdrant for semantic search (fire-and-forget) ──
        _ = Task.Run(() =>
        {
            try { MemoryManager.Qdrant?.InsertWithId(description, QdrantService.ImageCollectionName, hash); }
            catch { /* embedding failure shouldn't block */ }
        });

        return description;
    }

    /// <summary>
    /// Resolve an image's local file path. Tries FilePath first,
    /// falls back to the AMN2 framework image cache via hash.
    /// </summary>
    public static async Task<string?> ResolvePathAsync(string filePath, string hash)
    {
        if (File.Exists(filePath))
            return filePath;

        if (!string.IsNullOrEmpty(hash) && Entry.MessageApi != null)
        {
            var (resolved, resolvedPath) = await Entry.MessageApi.TryGetImageByHashAsync(hash);
            if (resolved && File.Exists(resolvedPath))
                return resolvedPath;
        }

        return null;
    }

    // ── Vision model call ─────────────────────────────────

    private static async Task<string?> CallVisionModel(
        List<APIKeyPurpose> keys, string prompt, string filePath)
    {
        try
        {
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
}
