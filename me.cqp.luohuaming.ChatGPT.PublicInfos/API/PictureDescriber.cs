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

        public static string? DescribePicture(CQCode img)
        {
            return Describe(PicturePrompt, ReceiveImage(img));
        }

        public static string? DescribeEmoji(CQCode img)
        {
            return Describe(EmojiPrompt, ReceiveImage(img));
        }

        public static string? ReceiveImage(CQCode img)
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

        public static string GetPictureCachePath()
        {
            string path = Path.Combine(MainSave.ImageDirectory, "ChatGPT", "cached");
            Directory.CreateDirectory(path);

            return path;
        }
    }
}
