using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using OpenAI.Chat;
using System;
using System.IO;
using System.Linq;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class PictureDescriber
    {
        public const string PicturePrompt = "请用中文描述这张图片的内容。如果有文字，请把文字都描述出来。并尝试猜测这个图片的含义。最多200个字。";
        public const string EmojiPrompt = "这是一个表情包，使用中文简洁的描述一下表情包的内容和表情包所表达的情感。";

        public static string? Describe(string prompt, string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            return Chat.GetChatResult(AppConfig.ImageDescriberUrl, AppConfig.ImageDescriberApiKey,
                [
                    new SystemChatMessage(prompt),
                    new UserChatMessage(ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(File.ReadAllBytes(path)), "image/jpg"))
                ], AppConfig.ImageDescriberModelName, Chat.Purpose.图片描述);
        }

        public static string? DescribePicture(string path)
        {
            return Describe(PicturePrompt, path);
        }

        public static string? DescribeEmoji(string path)
        {
            return Describe(EmojiPrompt, path);
        }

        public static string? ReceiveImage(CQCode img)
        {
            try
            {
                string fileName = img.Items["file"];
                var existImage = Directory.GetFiles(GetPictureCachePath()).FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).Equals(Path.GetFileNameWithoutExtension(fileName)));
                if (!string.IsNullOrEmpty(existImage))
                {
                    CommonHelper.DebugLog("缓存图片", $"图片已经缓存，路径为 {existImage}");
                    return existImage;
                }
                CommonHelper.DebugLog("缓存图片", $"开始缓存图片 {img.Items["file"]}");
                string path = MainSave.CQApi.ReceiveImage(img);
                if (!File.Exists(path))
                {
                    MainSave.CQLog.Error("缓存图片", "缓存失败，文件不存在");
                    return null;
                }
                string newPath = Path.Combine(GetPictureCachePath(), Path.GetFileName(path));
                File.Move(path, newPath);
                CommonHelper.DebugLog("缓存图片", $"图片缓存成功");

                return newPath;
            }
            catch (Exception e)
            {
                MainSave.CQLog.Error("缓存图片", $"缓存图片失败，错误信息：{e.Message}");
                return null;
            }
        }

        public static string GetPictureCachePath()
        {
            string path = Path.Combine(MainSave.ImageDirectory, "ChatGPT", "cached");
            Directory.CreateDirectory(path);

            return path;
        }

        public static string? ComputeImageHash(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }
                using var stream = File.OpenRead(filePath);
                using var md5 = System.Security.Cryptography.MD5.Create();

                var hash = md5.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToUpperInvariant();
            }
            catch (Exception e)
            {
                MainSave.CQLog.Error("计算图片Hash", $"计算Hash失败，错误信息：{e.Message}");
                return null;
            }
        }

        public static void DeleteImage(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception e)
            {
                MainSave.CQLog.Error("删除图片", $"删除图片失败，错误信息：{e.Message}");
            }
        }
    }
}
