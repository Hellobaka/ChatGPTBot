using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using Microsoft.Extensions.AI;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class UsageTracker
    {
        public void TrackUsage(string baseUrl, string modelName, string purpose, UsageDetails usage, LLMModel? model, string apiKey)
        {
            long inputTokenCount = usage.InputTokenCount ?? 0;
            long outputTokenCount = usage.OutputTokenCount ?? 0;
            long totalTokenCount = usage.TotalTokenCount ?? 0;
            long cachedTokenCount = usage.AdditionalCounts.TryGetValue("InputTokenDetails.CachedTokenCount", out long t) ? t : 0;

            Usage.Insert(baseUrl, modelName, purpose
                , inputTokenCount
                , outputTokenCount
                , totalTokenCount
                , cachedTokenCount
                , model == null ? 0 : model.CalcConsume(inputTokenCount, outputTokenCount, totalTokenCount, cachedTokenCount));

            APIKeys.UpdateTokenConsume(apiKey, usage.TotalTokenCount.Value);
        }
    }
}