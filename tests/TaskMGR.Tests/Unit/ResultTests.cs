using FluentAssertions;
using TaskMGR.Core.Results;

namespace TaskMGR.Tests.Unit;

public sealed class ResultTests
{
    [Fact]
    public void Ok_IsSuccessTrue_AndValueAccessible()
    {
        var result = Result<int, string>.Ok(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Fail_IsSuccessFalse_AndErrorAccessible()
    {
        var result = Result<int, string>.Fail("boom");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("boom");
    }

    [Fact]
    public void AccessingValueOnFail_ThrowsInvalidOperationException()
    {
        var result = Result<int, string>.Fail("boom");

        Action act = () => _ = result.Value;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AccessingErrorOnOk_ThrowsInvalidOperationException()
    {
        var result = Result<int, string>.Ok(1);

        Action act = () => _ = result.Error;

        act.Should().Throw<InvalidOperationException>();
    }
}
