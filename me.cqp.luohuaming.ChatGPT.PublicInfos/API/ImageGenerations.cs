using OpenAI;
using OpenAI.Images;
using System.IO;
using System;
using System.Threading.Tasks;
using System.ClientModel;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class ImageGeneration
    {
        public static string GetImageGenerationAsync(string prompt)
        {
            ImageClient client = new("dall-e-3", new ApiKeyCredential(AppConfig.ImageGenerateAPIKey), new OpenAIClientOptions() { Endpoint = new(AppConfig.ImageGenerateBaseURL), NetworkTimeout = TimeSpan.FromMilliseconds(AppConfig.ImageGenerationTimeout) });
            GeneratedImage image = client.GenerateImage(prompt, new ImageGenerationOptions()
            {
                Size = GetImageSize(),
                Quality = GetImageQuality(),
                Style = GeneratedImageStyle.Vivid,
                ResponseFormat = GeneratedImageFormat.Bytes
            });

            Directory.CreateDirectory(Path.Combine(MainSave.ImageDirectory, "ChatGPT"));
            string filePath = Path.Combine(MainSave.ImageDirectory, "ChatGPT", $"{Guid.NewGuid()}.jpg");
            File.WriteAllBytes(filePath, image.ImageBytes.ToArray());

            return $"[CQ:image,file=ChatGPT\\{Path.GetFileName(filePath)}]";
        }

        private static GeneratedImageSize GetImageSize()
        {
            return AppConfig.ImageGenerateSize switch
            {
                0 => GeneratedImageSize.W256xH256,
                2 => GeneratedImageSize.W1024xH1024,
                3 => GeneratedImageSize.W1024xH1792,
                4 => GeneratedImageSize.W1792xH1024,
                _ => GeneratedImageSize.W512xH512,
            };
        }

        private static GeneratedImageQuality GetImageQuality()
        {
            return AppConfig.ImageGenerateQuality switch
            {
                1 => GeneratedImageQuality.High,
                _ => GeneratedImageQuality.Standard,
            };
        }
    }
}
