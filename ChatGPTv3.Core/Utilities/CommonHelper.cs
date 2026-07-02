using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ChatGPTv3.Core.Utilities;

public static class CommonHelper
{
    // ── Timestamp Utilities ──────────────────────────────

    public static long GetTimeStamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static long GetTimeStamp(this DateTime time)
    {
        return new DateTimeOffset(time.ToUniversalTime()).ToUnixTimeSeconds();
    }

    public static DateTime TimestampToDateTime(long timestamp)
    {
        return DateTimeOffset.FromUnixTimeSeconds(timestamp).LocalDateTime;
    }

    // ── Directory Utilities ──────────────────────────────

    public static string GetAppImageDirectory()
    {
        return Path.Combine(Environment.CurrentDirectory, "data", "image");
    }

    public static string GetAppRecordDirectory()
    {
        return Path.Combine(Environment.CurrentDirectory, "data", "record");
    }

    // ── HTTP Helpers ─────────────────────────────────────

    public static string? ParsePic2Base64(string path)
    {
        return File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null;
    }

    // ── Random Utilities (crypto-grade) ──────────────────

    public static int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue));
        }

        long diff = (long)maxValue - minValue;
        byte[] uint32Buffer = new byte[4];
        uint rand;
        do
        {
            RandomNumberGenerator.Fill(uint32Buffer);
            rand = BitConverter.ToUInt32(uint32Buffer, 0);
        }
        while (rand >= (uint.MaxValue - (((uint.MaxValue % diff) + 1) % diff)));

        return (int)(minValue + (rand % diff));
    }

    public static double NextDouble()
    {
        byte[] bytes = new byte[8];
        RandomNumberGenerator.Fill(bytes);
        ulong ul = BitConverter.ToUInt64(bytes, 0) >> 11;
        return ul / (double)(1UL << 53);
    }

    public static double NextDouble(double minValue, double maxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(minValue));
        }

        return minValue + ((maxValue - minValue) * NextDouble());
    }

    // ── Logging ──────────────────────────────────────────

    /// <summary>
    /// Set by Entry during initialization. Used for debug-level logging.
    /// </summary>
    public static Action<string, string>? LogDebug { get; set; }

    public static Action<string, string>? LogInfo { get; set; }

    public static Action<string, string>? LogWarning { get; set; }

    public static Action<string, string>? LogError { get; set; }

    public static bool DebugMode { get; set; }

    public static void DebugLog(string type, string message)
    {
        if (DebugMode)
        {
            LogDebug?.Invoke(type, message);
        }
    }

    // ── String Extensions ────────────────────────────────

    public static string ToJson(this object obj, bool indented = false)
    {
        return System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = indented
        });
    }

    public static string[] SplitV2(this string message, string pattern)
    {
        string regexPattern = $"({Regex.Escape(pattern)})";
        var parts = Regex.Split(message, regexPattern);
        return parts.Where(x => !string.IsNullOrEmpty(x)).ToArray();
    }

    public static string RemoveFirstLineFast(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        int firstNewLineIndex = input.IndexOfAny(['\r', '\n']);
        if (firstNewLineIndex == -1)
        {
            return string.Empty;
        }

        if (input[firstNewLineIndex] == '\r' &&
            firstNewLineIndex + 1 < input.Length &&
            input[firstNewLineIndex + 1] == '\n')
        {
            return input.Substring(firstNewLineIndex + 2);
        }
        else
        {
            return input.Substring(firstNewLineIndex + 1);
        }
    }

    // ── Path Utilities ───────────────────────────────────

    public static string GetRelativePath(string fullPath, string baseDirectory)
    {
        if (!File.Exists(fullPath))
        {
            return string.Empty;
        }

        string normalizedFull = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalizedBase = baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        return normalizedFull.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase)
            ? normalizedFull.Substring(normalizedBase.Length)
            : normalizedFull;
    }

    // ── Hash Utilities ───────────────────────────────────

    public static string ComputeMD5(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    public static string ComputeSHA256(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}