using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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

        public static string GetAppImageDirectory()
        {
            var ImageDirectory = Path.Combine(Environment.CurrentDirectory, "data", "image\\");
            return ImageDirectory;
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
    }
}