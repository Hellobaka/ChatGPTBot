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
        // Quick check: the minimum possible interval is bounded by the minute field
        var parts = cronExpr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5) return null;

        try
        {
            int minInterval = int.MaxValue;

            // Check step values — the smallest step dominates
            foreach (var field in parts)
            {
                if (field == "*") { minInterval = Math.Min(minInterval, 1); continue; }
                foreach (var part in field.Split(','))
                {
                    if (part.StartsWith("*/"))
                    {
                        int step = int.Parse(part[2..]);
                        minInterval = Math.Min(minInterval, part == parts[0] ? step : step * 60);
                    }
                    else if (part.Contains('-'))
                    {
                        minInterval = Math.Min(minInterval, 1); // range could fire every minute
                    }
                    // Single value: doesn't reduce the interval below 1 minute
                }
            }

            // If the minute field is * or */N, min interval is at most 1 minute
            if (parts[0] == "*") minInterval = Math.Min(minInterval, 1);

            // Check for single-value minute (e.g., "30 * * * *" = every hour)
            if (parts[0].Contains('-') || parts[0].Contains(',') || parts[0] == "*" || parts[0].StartsWith("*/"))
            {
                // already handled above
            }
            else
            {
                // Single minute value: fires at most once per hour
                minInterval = Math.Min(minInterval, 60);
            }

            return minInterval == int.MaxValue ? null : minInterval;
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
