using System;

namespace TimeTracker.Domain.Utilities;

public static class QuarterHourRounder
{
    private const int MinutesPerQuarter = 15;
    private const long TicksPerQuarter = TimeSpan.TicksPerMinute * MinutesPerQuarter;
    private const double TicksPerQuarterd = TicksPerQuarter;

    public static TimeSpan Round(TimeSpan value)
    {
        if (value <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var quarters = (long)Math.Round(value.Ticks / TicksPerQuarterd, MidpointRounding.AwayFromZero);

        return TimeSpan.FromTicks(quarters * TicksPerQuarter);
    }
}
