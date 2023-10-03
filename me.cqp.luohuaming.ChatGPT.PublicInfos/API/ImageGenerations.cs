using Azure;
using Azure.AI.OpenAI;
using HarmonyLib;
using System;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class ImageGeneration
    {
        public async Task<string> GetImageGenerationAsync(string prompt, ImageSize imageSize)
        {
            var client = new OpenAIClient(AppConfig.APIKey, new OpenAIClientOptions());
            var a = Traverse.Create(client).Field("_endpoint");
            a.SetValue(new Uri(a.GetValue().ToString().Replace("https://api.openai.com", AppConfig.BaseURL)));
            client.Pipeline.CreateRequest();
            Response<ImageGenerations> imageGenerations = await client.GetImageGenerationsAsync(
                new ImageGenerationOptions()
                {
                    Prompt = prompt,
                    Size = imageSize,
                });
            Uri imageUri = imageGenerations.Value.Data[0].Url;
            return imageUri.ToString();
        }
    }
}
