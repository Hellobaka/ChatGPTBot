using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public static class TencentSign
    {
        private static string SHA256Hex(string s)
        {
            using SHA256 algo = SHA256.Create();
            byte[] hashbytes = algo.ComputeHash(Encoding.UTF8.GetBytes(s));
            StringBuilder builder = new();
            for (int i = 0; i < hashbytes.Length; ++i)
            {
                builder.Append(hashbytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        private static byte[] HmacSHA256(byte[] key, byte[] msg)
        {
            using HMACSHA256 mac = new(key);
            return mac.ComputeHash(msg);
        }

        public static Dictionary<string, string> BuildHeaders(string service, string endpoint, string region,
            string action, string version, string requestPayload)
        {
            DateTime date = DateTime.UtcNow;
            string secretKey = AppConfig.TencentSecretKey, secretId = AppConfig.TencentSecretId;
            if (string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(secretId))
            {
                throw new ArgumentException($"腾讯云 secretKey 或 secretId 为空");
            }
            string datestr = date.ToString("yyyy-MM-dd");
            DateTime startTime = new(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            long requestTimestamp = (long)Math.Round((date - startTime).TotalMilliseconds, MidpointRounding.AwayFromZero) / 1000;
            // ************* 步骤 1：拼接规范请求串 *************
            string algorithm = "TC3-HMAC-SHA256";
            string httpRequestMethod = "POST";
            string canonicalUri = "/";
            string canonicalQueryString = "";
            string contentType = "application/json";
            string canonicalHeaders = "content-type:" + contentType + "; charset=utf-8\n"
                + "host:" + endpoint + "\n"
                + "x-tc-action:" + action.ToLower() + "\n";
            string signedHeaders = "content-type;host;x-tc-action";
            string hashedRequestPayload = SHA256Hex(requestPayload);
            string canonicalRequest = httpRequestMethod + "\n"
                + canonicalUri + "\n"
                + canonicalQueryString + "\n"
                + canonicalHeaders + "\n"
                + signedHeaders + "\n"
                + hashedRequestPayload;

            // ************* 步骤 2：拼接待签名字符串 *************
            string credentialScope = datestr + "/" + service + "/" + "tc3_request";
            string hashedCanonicalRequest = SHA256Hex(canonicalRequest);
            string stringToSign = algorithm + "\n"
                + requestTimestamp.ToString() + "\n"
                + credentialScope + "\n"
                + hashedCanonicalRequest;

            // ************* 步骤 3：计算签名 *************
            byte[] tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + secretKey);
            byte[] secretDate = HmacSHA256(tc3SecretKey, Encoding.UTF8.GetBytes(datestr));
            byte[] secretService = HmacSHA256(secretDate, Encoding.UTF8.GetBytes(service));
            byte[] secretSigning = HmacSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
            byte[] signatureBytes = HmacSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
            string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

            // ************* 步骤 4：拼接 Authorization *************
            string authorization = algorithm + " "
                + "Credential=" + secretId + "/" + credentialScope + ", "
                + "SignedHeaders=" + signedHeaders + ", "
                + "Signature=" + signature;

            Dictionary<string, string> headers = new()
            {
                { "Authorization", authorization },
                { "Host", endpoint },
                { "X-TC-Timestamp", requestTimestamp.ToString() },
                { "X-TC-Version", version },
                { "X-TC-Action", action },
                { "X-TC-Region", region }
            };
            return headers;
        }
    }
}