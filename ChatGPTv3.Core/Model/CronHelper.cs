namespace ChatGPTv3.Core.Model;

/// <summary>
/// Minimal cron expression evaluator.
/// Supports: *, */N, N, N-M (ranges), N,M (lists) for 5-field cron.
/// </summary>
public static class CronHelper
{
    /// <summary>
    /// Estimates the minimum interval (minutes) this cron expression could fire.
    /// Returns null if the expression is invalid.
    /// </summary>
    public static int? GetMinIntervalMinutes(string cronExpr)
    {
        var parts = cronExpr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return null;

        try
        {
            int? minInterval = null;

            // Check step values first — they give the tightest bound
            for (int i = 0; i < parts.Length; i++)
            {
                foreach (var sub in parts[i].Split(','))
                {
                    if (sub.StartsWith("*/"))
                    {
                        int step = int.Parse(sub[2..]);
                        // Scale step based on field position
                        int stepMinutes = i switch
                        {
                            0 => step,           // minute field
                            1 => step * 60,      // hour field
                            2 => step * 1440,    // day of month
                            3 => step * 44640,   // month (~31 days)
                            4 => step * 1440,    // day of week
                            _ => step
                        };
                        minInterval = minInterval.HasValue
                            ? Math.Min(minInterval.Value, stepMinutes)
                            : stepMinutes;
                    }
                }
            }

            // If no step values found, estimate from the minute field
            if (!minInterval.HasValue)
            {
                var minuteField = parts[0];
                if (minuteField == "*")
                    minInterval = 1;
                else if (minuteField.Contains('-'))
                    minInterval = 1;  // range could fire every minute
                else if (minuteField.Contains(','))
                    minInterval = 1;  // conservative: assume sub-values could be dense
                else
                    minInterval = 60; // single minute value: once per hour max
            }

            return minInterval;
        }
        catch
        {
            return null;
        }
    }

    public static DateTime? GetNextFireTime(string cronExpr, DateTime from)
    {
        var parts = cronExpr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return null;

        try
        {
            var mins = ParseField(parts[0], 0, 59);
            var hours = ParseField(parts[1], 0, 23);
            var days = ParseField(parts[2], 1, 31);
            var months = ParseField(parts[3], 1, 12);
            var dow = ParseField(parts[4], 0, 6);

            var candidate = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0).AddMinutes(1);

            for (int i = 0; i < 525600; i++) // search up to 1 year ahead
            {
                if (months.Contains(candidate.Month)
                    && days.Contains(candidate.Day)
                    && dow.Contains((int)candidate.DayOfWeek)
                    && hours.Contains(candidate.Hour)
                    && mins.Contains(candidate.Minute))
                    return candidate;

                candidate = candidate.AddMinutes(1);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<int> ParseField(string field, int min, int max)
    {
        var result = new HashSet<int>();
        if (field == "*")
        {
            for (int i = min; i <= max; i++) result.Add(i);
            return result;
        }
        foreach (var part in field.Split(','))
        {
            if (part.StartsWith("*/"))
            {
                int step = int.Parse(part[2..]);
                for (int i = min; i <= max; i += step) result.Add(i);
            }
            else if (part.Contains('-'))
            {
                var range = part.Split('-');
                for (int i = int.Parse(range[0]); i <= int.Parse(range[1]); i++) result.Add(i);
            }
            else
            {
                result.Add(int.Parse(part));
            }
        }
        return result;
    }
}
