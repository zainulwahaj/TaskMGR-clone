using FluentAssertions;
using TaskMGR.UI.Converters;

namespace TaskMGR.Tests.Unit;

public sealed class BytesToStringConverterTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1_048_576L, "1 MB")]
    [InlineData(1_073_741_824L, "1 GB")]
    public void FormatBytes_MapsExpectedThresholds(long bytes, string expected)
    {
        var result = BytesToStringConverter.FormatBytes(bytes);

        result.Should().Be(expected);
    }
}
