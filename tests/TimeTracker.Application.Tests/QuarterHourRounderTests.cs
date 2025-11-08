using System;
using TimeTracker.Domain.Utilities;

namespace TimeTracker.Application.Tests;

public class QuarterHourRounderTests
{
    [Theory]
    [InlineData("00:07:29", "00:00:00")]
    [InlineData("00:07:30", "00:15:00")]
    [InlineData("00:22:29", "00:15:00")]
    [InlineData("00:22:30", "00:30:00")]
    [InlineData("00:37:29", "00:30:00")]
    [InlineData("00:37:30", "00:45:00")]
    [InlineData("00:52:29", "00:45:00")]
    [InlineData("00:52:30", "01:00:00")]
    public void Round_MidpointAwayFromZero(string input, string expected)
    {
        var value = TimeSpan.Parse(input);
        var rounded = QuarterHourRounder.Round(value);

        Assert.Equal(TimeSpan.Parse(expected), rounded);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("00:03:00")]
    public void Round_AllowsZeroWhenUnderThreshold(string input)
    {
        var value = TimeSpan.Parse(input);
        var rounded = QuarterHourRounder.Round(value);

        Assert.Equal(TimeSpan.Zero, rounded);
    }

    [Fact]
    public void Round_NegativeDurationsClampToZero()
    {
        var rounded = QuarterHourRounder.Round(TimeSpan.FromMinutes(-5));

        Assert.Equal(TimeSpan.Zero, rounded);
    }
}
