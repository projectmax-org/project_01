using Boatdrill.Core.Text;

namespace Boatdrill.Core.Tests;

public sealed class HumanizeTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(999, "999 bytes")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(212_400_000_000, "197.8 GB")]
    public void Bytes(long bytes, string expected) => Assert.Equal(expected, Humanize.Bytes(bytes));

    [Fact]
    public void Duration()
    {
        Assert.Equal("45 s", Humanize.Duration(TimeSpan.FromSeconds(45)));
        Assert.Equal("12 min", Humanize.Duration(TimeSpan.FromMinutes(12)));
        Assert.Equal("2 h 10 min", Humanize.Duration(TimeSpan.FromMinutes(130)));
        Assert.Equal("3 h", Humanize.Duration(TimeSpan.FromHours(3)));
        Assert.Equal("3 days", Humanize.Duration(TimeSpan.FromDays(3)));
    }

    [Fact]
    public void Age()
    {
        Assert.Equal("less than a minute", Humanize.Age(TimeSpan.FromSeconds(20)));
        Assert.Equal("45 minutes", Humanize.Age(TimeSpan.FromMinutes(45)));
        Assert.Equal("1 hour", Humanize.Age(TimeSpan.FromMinutes(61)));
        Assert.Equal("9 days", Humanize.Age(TimeSpan.FromDays(9)));
        Assert.Equal("3 months", Humanize.Age(TimeSpan.FromDays(92)));
        Assert.Equal("2 years", Humanize.Age(TimeSpan.FromDays(740)));
    }
}
