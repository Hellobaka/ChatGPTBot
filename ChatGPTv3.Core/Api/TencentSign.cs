using System.Security.Cryptography;
using System.Text;
using ChatGPTv3.Core.Config;
using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core.Api;

/// <summary>
/// Tencent Cloud API v3 signature generation.
/// </summary>
public static class TencentSign
{
    private const string Algorithm = "TC3-HMAC-SHA256";

    public static Dictionary<string, string> BuildHeaders(
        string service, string host, string region,
        string action, string version, string payload)
    {
        var timestamp = CommonHelper.GetTimeStamp();
        var date = DateTimeOffset.FromUnixTimeSeconds(timestamp)
            .UtcDateTime.ToString("yyyy-MM-dd");

        // Step 1: Canonical Request
        var canonicalUri = "/";
        var canonicalQueryString = "";
        var canonicalHeaders = $"content-type:application/json\nhost:{host}\n";
        var signedHeaders = "content-type;host";
        var hashedPayload = SHA256Hash(payload);
        var canonicalRequest = $"POST\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{hashedPayload}";

        // Step 2: String to Sign
        var credentialScope = $"{date}/{service}/tc3_request";
        var hashedCanonicalRequest = SHA256Hash(canonicalRequest);
        var stringToSign = $"{Algorithm}\n{timestamp}\n{credentialScope}\n{hashedCanonicalRequest}";

        // Step 3: Signature
        var secretDate = HMACSHA256(Encoding.UTF8.GetBytes("TC3" + AppConfig.TencentSecretKey), Encoding.UTF8.GetBytes(date));
        var secretService = HMACSHA256(secretDate, Encoding.UTF8.GetBytes(service));
        var secretSigning = HMACSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        var signature = BitConverter.ToString(HMACSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign)))
            .Replace("-", "").ToLowerInvariant();

        // Step 4: Authorization header
        var authorization = $"{Algorithm} Credential={AppConfig.TencentSecretId}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        return new()
        {
            ["Authorization"] = authorization,
            ["Content-Type"] = "application/json",
            ["Host"] = host,
            ["X-TC-Action"] = action,
            ["X-TC-Version"] = version,
            ["X-TC-Timestamp"] = timestamp.ToString(),
            ["X-TC-Region"] = region
        };
    }

    private static string SHA256Hash(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private static byte[] HMACSHA256(byte[] key, byte[] data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(data);
    }
}
