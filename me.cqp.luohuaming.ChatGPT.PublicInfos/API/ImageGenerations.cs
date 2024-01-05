using Azure;
using Azure.AI.OpenAI;
using HarmonyLib;
using System;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class ImageGeneration
    {
        public static async Task<string> GetImageGenerationAsync(string prompt)
        {
            var client = new OpenAIClient(AppConfig.APIKey, new OpenAIClientOptions());
            var a = Traverse.Create(client).Field("_endpoint");
            a.SetValue(new Uri(a.GetValue().ToString().Replace("https://api.openai.com", AppConfig.BaseURL)));
            client.Pipeline.CreateRequest();
            Response<ImageGenerations> imageGenerations = await client.GetImageGenerationsAsync(
            new ImageGenerationOptions()
            {
                Prompt = prompt,
                Size = GetImageSize(),
                Quality = GetImageQuality(),
                DeploymentName = AppConfig.ImageGenerationModelName
            });
            Uri imageUri = imageGenerations.Value.Data[0].Url;
            return imageUri.ToString();
        }

        private static ImageSize GetImageSize()
        {
            switch (AppConfig.ImageGenerateSize)
            {
                case 0:
                    return ImageSize.Size256x256;
                default:
                case 1:
                    return ImageSize.Size512x512;
                case 2:
                    return ImageSize.Size1024x1024;
                case 3:
                    return ImageSize.Size1024x1792;
                case 4:
                    return ImageSize.Size1792x1024;
            }
        }

        private static ImageGenerationQuality GetImageQuality()
        {
            switch (AppConfig.ImageGenerateQuality)
            {
                default:
                case 0:
                    return ImageGenerationQuality.Standard;
                case 1:
                    return ImageGenerationQuality.Hd;
            }
        }
    }
}
