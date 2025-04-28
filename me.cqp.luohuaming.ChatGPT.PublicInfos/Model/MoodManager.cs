using me.cqp.luohuaming.ChatGPT.PublicInfos.API;
using me.cqp.luohuaming.ChatGPT.Sdk.Cqp.Model;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace me.cqp.luohuaming.ChatGPT.PublicInfos.Model
{
    public class MoodManager
    {
        public double Valence { get; set; }

        public double Arousal { get; set; }

        private Timer MoodDecreaseTimer { get; set; }

        public static MoodManager Instance { get; private set; }

        public event Action MoodChanged;

        public const string Prompt = @"请根据以下对话内容，完成以下任务：
1. 判断回复者的立场是'supportive'（支持）、'opposed'（反对）还是'neutrality'（中立）。
2. 从'happy,angry,sad,surprised,disgusted,fearful,neutral'中选出最匹配的1个情感标签。
3. 按照'立场-情绪'的格式输出结果，例如：'supportive-happy'。
被回复的内容：
{0}
回复内容：
{1}
请分析回复者的立场和情感倾向，只输出结果，无需任何解释性文本，只能从我给出的几个选项中选择，不可自己使用新的选项。";

        public enum Mood
        {
            None,
            happy,
            angry,
            sad,
            surprised,
            disgusted,
            fearful,
            neutral,
        }

        public enum Stand
        {
            None,
            supportive,
            neutrality,
            opposed,
        }

        public static Dictionary<Mood, double> MoodFavorValue { get; set; }
            = new()
            {
                { Mood.happy, 15 },
                { Mood.angry, -30 },
                { Mood.sad, -15 },
                { Mood.surprised, 6 },
                { Mood.disgusted, -45 },
                { Mood.fearful, -20 },
                { Mood.neutral, 3 }
            };

        public static Dictionary<Mood, (double valence, double arousal)> MoodValues { get; set; } =
            new()
            {
                { Mood.happy, (0.8, 0.6) },
                { Mood.angry, (-0.7, 0.7) },
                { Mood.sad, (-0.6, 0.3) },
                { Mood.surprised, (0.4, 0.8) },
                { Mood.disgusted, (-0.8, 0.5) },
                { Mood.fearful, (-0.7, 0.6) },
                { Mood.neutral, (0.0, 0.5) }
            };

        public static Dictionary<string, (double valence, double arousal)> MoodMap { get; set; } =
            new()
            {
                {"兴奋", (0.5, 0.7) },
                {"快乐", (0.3, 0.8) },
                {"满足", (0.2, 0.65) },
                {"愤怒", (-0.5, 0.7) },
                {"焦虑", (-0.3, 0.8) },
                {"烦躁", (-0.2, 0.65) },
                {"悲伤", (-0.5, -0.3) },
                {"疲倦", (-0.4, -0.15) },
                {"平静", (0.2, -0.45) },
                {"安宁", (0.3, -0.4) },
                {"放松", (0.5, -0.3) },
            };

        public MoodManager()
        {
            Instance = this;
            StartMoodDecreaseTimer();
        }

        public void UpdateMood(Mood mood)
        {
            var (valence, arousal) = MoodValues[mood];
            Valence = Math.Max(-1, Math.Min(1, Valence + valence));
            Arousal = Math.Max(0, Math.Min(1, Arousal + arousal));

            MoodChanged?.Invoke();

            CommonHelper.DebugLog("更新心情", $"心情：{mood}，计算后新的愉悦值为：{Valence}，唤醒值为：{Arousal}");
        }

        public void StartMoodDecreaseTimer()
        {
            MoodDecreaseTimer = new()
            {
                AutoReset = true,
                Interval = 30000,
            };
            MoodDecreaseTimer.Elapsed += MoodDecreaseTimer_Elapsed;
            MoodDecreaseTimer.Start();
        }

        public (Mood mood, Stand stand) GetTextMood(string input, string detailMessage)
        {
            string prompt = string.Format(Prompt, detailMessage, input);
            string reply = Chat.GetChatResult(AppConfig.SpliterUrl, AppConfig.SpliterApiKey,
                [
                    new SystemChatMessage(prompt),
                    new UserChatMessage("请回复")
                ], AppConfig.SpliterModelName, Chat.Purpose.获取心情);
            var split = reply.Split('-');

            if (split.Length == 2)
            {
                Mood mood = Enum.TryParse(split[1].ToLower(), out Mood v) ? v : Mood.None;
                Stand stand = Enum.TryParse(split[0].ToLower(), out Stand v2) ? v2 : Stand.None;
                if (mood == Mood.None)
                {
                    CommonHelper.DebugLog("情绪转换", $"无效的情绪转换：{split[1]}");
                    mood = Mood.neutral;
                }
                if (stand == Stand.None)
                {
                    CommonHelper.DebugLog("情绪转换", $"无效的立场转换：{split[0]}");
                    stand = Stand.neutrality;
                }

                CommonHelper.DebugLog("更新心情", $"输入获取到的心情为：{mood}，立场为：{stand}");

                return (mood, stand);
            }
            else
            {
                MainSave.CQLog.Error("情绪转换", $"无效的情绪转换：{reply}");
                return (Mood.None, Stand.None);
            }
        }

        private void MoodDecreaseTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Valence *= 0.8;
            Arousal *= 0.9;

            Valence = Math.Max(-1, Math.Min(1, Valence));
            Arousal = Math.Max(0, Math.Min(1, Arousal));

            MoodChanged?.Invoke();
        }

        public static string GetCurrentMoodText(double valence, double arousal)
        {
            return MoodMap.AsParallel()
                .Select(x => new { Mood = x.Key, Distance = Math.Sqrt(Math.Pow(x.Value.valence - valence, 2) + Math.Pow(x.Value.arousal - arousal, 2)) })
                .OrderBy(x => x.Distance)
                .FirstOrDefault().Mood;
        }

        public override string ToString()
        {
            var mood = GetCurrentMoodText(Valence, Arousal);

            return $"当前心情：{mood}。你现在心情{(Valence > 0.5 ? "很好" : (Valence < -0.5 ? "不太好" : "一般"))}，" +
                $"情绪比较{(Arousal > 0.7 ? "激动" : (Valence < -0.5 ? "平静" : "普通"))}。";
        }
    }
}
