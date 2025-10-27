using me.cqp.luohuaming.ChatGPT.PublicInfos.DB;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.API
{
    public class ToolCallService
    {
        private static readonly ConcurrentDictionary<string, int> ToolCallCount = new ConcurrentDictionary<string, int>();
        private static ConcurrentBag<string> AbortIdentityList = new ConcurrentBag<string>();

        private readonly JsonSerializerOptions _disableEscapingSerializerOptions;

        public ToolCallService(JsonSerializerOptions disableEscapingSerializerOptions)
        {
            _disableEscapingSerializerOptions = disableEscapingSerializerOptions;
        }

        public void ResetToolCallState(string identity)
        {
            if (identity != null)
            {
                ToolCallCount.AddOrUpdate(identity, 0, (key, oldValue) => 0);
            }
        }

        public void CleanupToolCallState(string identity)
        {
            if (identity != null)
            {
                ToolCallCount.TryRemove(identity, out _);
                var newAbortList = new ConcurrentBag<string>(AbortIdentityList.Except(new[] { identity }));
                AbortIdentityList = [];
                foreach (var item in newAbortList)
                {
                    AbortIdentityList.Add(item);
                }
            }
        }

        public bool ShouldAbortConversation(string identity)
        {
            return identity != null && AbortIdentityList.Any(x => x == identity);
        }

        public async Task<object> LogToolCall(FunctionInvocationContext context, System.Threading.CancellationToken token, string identity)
        {
            CommonHelper.DebugLog("ToolCall追踪", $"调用函数 {context.Function.Name}，参数 {JsonSerializer.Serialize(context.Arguments, _disableEscapingSerializerOptions)}");
            try
            {
                if (identity != null && ToolCallCount.TryGetValue(identity, out int count))
                {
                    var newCount = ToolCallCount.AddOrUpdate(identity, 1, (key, oldValue) => oldValue + 1);

                    if (newCount > AppConfig.AbortToolCallCountEachTurn)
                    {
                        MainSave.CQLog?.Warning("ToolCall追踪", $"本轮对话已调用 {newCount} 次Tool，本轮会话强制终止");
                        if (!AbortIdentityList.Any(x => x == identity))
                        {
                            AbortIdentityList.Add(identity);
                        }
                        return "The maximum tool call limit for this turn has been reached, this conversation will be aborted.";
                    }
                    if (newCount > AppConfig.MaxToolCallCountEachTurn)
                    {
                        MainSave.CQLog?.Warning("ToolCall追踪", $"本轮对话已调用 {newCount} 次Tool，无法再调用");
                        return "The maximum tool call limit for this turn has been reached, you cannot do tool calls any more.";
                    }
                }
                var result = await context.Function.InvokeAsync(context.Arguments, token);
                CommonHelper.DebugLog("ToolCall追踪", $"函数 {context.Function.Name} 调用完成，结果 {JsonSerializer.Serialize(result, _disableEscapingSerializerOptions)}");
                return result;
            }
            catch (Exception e)
            {
                CommonHelper.DebugLog("ToolCall追踪", $"函数 {context.Function.Name} 调用失败，错误信息 {e.Message}");
                return $"Exception when call tool {context.Function.Name}, {e}";
            }
        }
    }
}