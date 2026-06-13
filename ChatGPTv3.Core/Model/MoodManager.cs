using ChatGPTv3.Core.Utilities;

namespace ChatGPTv3.Core.Model;

/// <summary>
/// Tracks the bot's emotional state using valence (pleasure) and arousal (energy) dimensions.
/// Decays over time toward neutral, can be updated by MCP tools.
/// </summary>
public class MoodManager
{
    public double Valence { get; private set; } = 0.5;  // 0 (negative) to 1 (positive)
    public double Arousal { get; private set; } = 0.5;  // 0 (calm) to 1 (excited)

    private Timer? _decayTimer;

    public MoodManager()
    {
        StartDecay();
    }

    public void UpdateMood(double valence, double arousal)
    {
        Valence = Math.Clamp(valence, 0, 1);
        Arousal = Math.Clamp(arousal, 0, 1);
    }

    private void StartDecay()
    {
        _decayTimer = new Timer(_ =>
        {
            // Decay toward neutral (0.5, 0.5) by 1% each tick
            Valence = Valence + (0.5 - Valence) * 0.01;
            Arousal = Arousal + (0.5 - Arousal) * 0.01;
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public string GetMoodDescription()
    {
        var mood = (Valence, Arousal) switch
        {
            ( >= 0.7, >= 0.7) => "兴奋、开心",
            ( >= 0.7, < 0.4) => "满足、平静",
            ( < 0.4, >= 0.7) => "烦躁、焦虑",
            ( < 0.4, < 0.4) => "低落、忧郁",
            _ => "情绪比较普通"
        };

        return $"当前心情：{mood}。你现在心情{(Valence >= 0.6 ? "很好" : Valence <= 0.4 ? "不太好" : "一般")}，情绪{(Arousal >= 0.6 ? "比较激动" : Arousal <= 0.4 ? "比较安静" : "比较普通")}。";
    }

    public override string ToString() => GetMoodDescription();

    public void Dispose()
    {
        _decayTimer?.Dispose();
    }
}
