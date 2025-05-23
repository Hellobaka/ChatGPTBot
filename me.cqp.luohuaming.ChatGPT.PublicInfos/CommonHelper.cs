﻿using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos
{
    public static class CommonHelper
    {
        /// <summary>
        /// 获取时间戳
        /// </summary>
        /// <returns></returns>
        public static long GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds);
        }

        public static long GetTimeStamp(this DateTime time)
        {
            TimeSpan ts = time.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds);
        }

        public static string GetAppImageDirectory()
        {
            var ImageDirectory = Path.Combine(Environment.CurrentDirectory, "data", "image\\");
            return ImageDirectory;
        }

        public static string GetAppRecordDirectory()
        {
            return Path.Combine(Environment.CurrentDirectory, "data", "record\\");
        }

        public static string ParsePic2Base64(string path)
        {
            return File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : null;
        }

        /// <summary>
        /// 获取CQ码中的图片网址
        /// </summary>
        /// <param name="imageCQCode">需要解析的图片CQ码</param>
        /// <returns></returns>
        public static string GetImageURL(CQCode imageCQCode)
        {
            string path = MainSave.ImageDirectory + imageCQCode.Items["file"] + ".cqimg";
            if (File.Exists(path))
            {
                Regex regex = new("url=(.*)");
                var a = regex.Match(File.ReadAllText(path));
                if (a.Groups.Count > 1)
                {
                    string capture = a.Groups[1].Value;
                    capture = capture.Split('\r').First();
                    return capture;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <param name="url">网址</param>
        /// <param name="path">目标文件夹</param>
        /// <param name="overwrite">重复时是否覆写</param>
        /// <returns></returns>
        public static async Task<bool> DownloadFile(string url, string fileName, string path, bool overwrite = false)
        {
            using var http = new HttpClient();
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return false;
                }

                if (!overwrite && File.Exists(Path.Combine(path, fileName)))
                {
                    return true;
                }

                var r = await http.GetAsync(url);
                byte[] buffer = await r.Content.ReadAsByteArrayAsync();
                Directory.CreateDirectory(path);
                File.WriteAllBytes(Path.Combine(path, fileName), buffer);
                return true;
            }
            catch (Exception e)
            {
                MainSave.CQLog.Error("下载文件", e.Message + e.StackTrace);
                return false;
            }
        }

        public static string TextTemplateParse(string input, long id)
        {
            string modelName = AppConfig.ChatModelName;
            string currentTime = DateTime.Now.ToString("G");
            string botName = AppConfig.BotName;

            input = input.Replace("$ModelName$", modelName)
                .Replace("$Time$", currentTime)
                .Replace("$BotName$", botName)
                .Replace("$Id$", id.ToString());

            string groupNamePattern = "$GroupName$";
            if (input.Contains(groupNamePattern))
            {
                input = input.Replace(groupNamePattern, MainSave.CQApi.GetGroupInfo(id)?.Name ?? "未获取到昵称");
            }

            Regex regex = new("\\[CQ:at,qq=(\\d+)\\]");
            input = regex.Replace(input, "<@$1>");

            regex = new("<@(\\d+?)>");
            input = regex.Replace(input, "[CQ:at,qq=$1]");
            return input;
        }

        public static string? Post(string method, string url, string payload, string token, int timeout = 60000)
        {
            string result = "";
            try
            {
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromMilliseconds(timeout);
                var request = new HttpRequestMessage(new HttpMethod(method), url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {token}");

                HttpResponseMessage response = client.SendAsync(request).Result;
                result = response.Content.ReadAsStringAsync().Result;
                if (!response.IsSuccessStatusCode)
                {
                    MainSave.CQLog?.Info("发送请求返回失败", result);
                }
                response.EnsureSuccessStatusCode();
                return result;
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Error("发送请求", url + "\n" + $"Payload: {payload}\n{result}\n" + ex.Message + ex.StackTrace);
                return null;
            }
        }

        public static string ToJson(this object obj, bool indented = false)
        {
            return JsonConvert.SerializeObject(obj, indented ? Formatting.Indented : Formatting.None);
        }

        public static string[] SplitV2(this string message, string pattern)
        {
            string regexPattern = $"({pattern})";
            var parts = Regex.Split(message, regexPattern);

            var ls = parts.ToList();
            ls.RemoveAll(string.IsNullOrEmpty);
            return ls.ToArray();
        }

        private static RNGCryptoServiceProvider Rng { get; set; } = new();

        public static int Next(int minValue, int maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            long diff = (long)maxValue - minValue;
            byte[] uint32Buffer = new byte[4];
            uint rand;
            do
            {
                Rng.GetBytes(uint32Buffer);
                rand = BitConverter.ToUInt32(uint32Buffer, 0);
            }
            while (rand >= (uint.MaxValue - (((uint.MaxValue % diff) + 1) % diff)));

            return (int)(minValue + (rand % diff));
        }

        public static double NextDouble()
        {
            byte[] bytes = new byte[8];
            Rng.GetBytes(bytes);
            ulong ul = BitConverter.ToUInt64(bytes, 0) >> 11; // 53位精度
            return ul / (double)(1UL << 53);
        }

        public static double NextDouble(double minValue, double maxValue)
        {
            if (minValue >= maxValue)
            {
                throw new ArgumentOutOfRangeException();
            }

            return minValue + (maxValue - minValue) * NextDouble();
        }

        public static string GetRelativePath(string value, string currentDirectory)
        {
            if (File.Exists(value))
            {
                string fullPath = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string currentDir = currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

                return fullPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase) ? fullPath.Substring(currentDir.Length) : fullPath;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string Post_TecentSignV3(string payload, string action, int timeout = 10000)
        {
            string result = "";
            try
            {
                string host = "lkeap.tencentcloudapi.com";
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromMilliseconds(timeout);
                var request = new HttpRequestMessage(new HttpMethod("POST"), $"https://{host}")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                foreach (var item in TencentSign.BuildHeaders("lkeap", host, "ap-guangzhou", action, "2024-05-22", payload))
                {
                    if (request.Headers.Contains(item.Key))
                    {
                        request.Headers.Remove(item.Key);
                    }
                    request.Headers.TryAddWithoutValidation(item.Key, item.Value);
                }

                HttpResponseMessage response = client.SendAsync(request).Result;
                result = response.Content.ReadAsStringAsync().Result;
                if (!response.IsSuccessStatusCode)
                {
                    MainSave.CQLog?.Info("发送请求返回失败", result);
                }
                response.EnsureSuccessStatusCode();
                return result;
            }
            catch (Exception ex)
            {
                MainSave.CQLog?.Error("发送请求", $"腾讯云接口：Action={action}，Payload={payload}" + ex.Message + ex.StackTrace);
                return null;
            }
        }

        public static void DebugLog(string type, string message)
        {
            if (AppConfig.DebugMode)
            {
                MainSave.CQLog?.Debug(type, message);
            }
            else
            {
                // no impletment
            }
        }
    }
}