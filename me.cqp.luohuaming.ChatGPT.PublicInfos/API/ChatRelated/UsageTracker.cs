using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using Microsoft.Extensions.AI;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class UsageTracker
    {
        public void TrackUsage(string baseUrl, LLMModel model, string purpose, UsageDetails usage, string apiKey)
        {
            long inputTokenCount = usage.InputTokenCount ?? 0;
            long outputTokenCount = usage.OutputTokenCount ?? 0;
            long totalTokenCount = usage.TotalTokenCount ?? 0;
            long cachedTokenCount = usage.AdditionalCounts.TryGetValue("InputTokenDetails.CachedTokenCount", out long t) ? t : 0;
            decimal consume = model.CalcConsume(inputTokenCount, outputTokenCount, totalTokenCount, cachedTokenCount);
            Usage.Insert(baseUrl, model.Name, purpose
                , inputTokenCount
                , outputTokenCount
                , totalTokenCount
                , cachedTokenCount
                , consume);

            APIKeys.UpdateTokenConsume(apiKey, model, inputTokenCount, outputTokenCount, cachedTokenCount, totalTokenCount);
        }
    }
}