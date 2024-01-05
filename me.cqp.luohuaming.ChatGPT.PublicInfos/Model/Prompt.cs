using System.IO;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class Prompt
    {
        public string Key { get; set; } = "";

        public string Content { get; set; } = "";

        public static Prompt Load(string path)
        {
            if (File.Exists(path))
            {
                Prompt prompt = new()
                {
                    Content = File.ReadAllText(path),
                    Key = Path.GetFileNameWithoutExtension(path)
                };
                return prompt;
            }
            else
            {
                return null;
            }
        }
    }
}
