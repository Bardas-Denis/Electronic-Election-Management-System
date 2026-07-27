using Electronic_Election_Management_System.Services;
using FluentAssertions;

namespace Electronic_Election_Management_System.Tests.Services;

public class CnpServiceTests
{
    private readonly CnpService _service = new();

    [Fact]
    public void Parse_WithValidCnp_ReturnsDerivedIdentityInformation()
    {
        var cnp = WithValidChecksum("501010140001");

        var result = _service.Parse(cnp);

        result.Should().NotBeNull();
        result!.BirthDate.Should().Be(new DateOnly(2001, 1, 1));
        result.Gender.Should().Be("M");
        result.CountyCode.Should().Be("40");
        result.CountyName.Should().Be("București");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-cnp")]
    [InlineData("123")]
    [InlineData("0010101400010")]
    public void Parse_WithInvalidFormat_ReturnsNull(string cnp)
    {
        _service.Parse(cnp).Should().BeNull();
    }

    [Fact]
    public void Parse_WithInvalidCalendarDate_ReturnsNull()
    {
        var cnp = WithValidChecksum("501023040001");

        _service.Parse(cnp).Should().BeNull();
    }

    [Fact]
    public void Parse_WithUnknownCounty_ReturnsNull()
    {
        var cnp = WithValidChecksum("501010199001");

        _service.Parse(cnp).Should().BeNull();
    }

    [Fact]
    public void Parse_WithFutureBirthDate_ReturnsNull()
    {
        var cnp = WithValidChecksum("599010140001");

        _service.Parse(cnp).Should().BeNull();
    }

    [Fact]
    public void Parse_WithIncorrectChecksum_ReturnsNull()
    {
        var valid = WithValidChecksum("501010140001");
        var invalidDigit = valid[^1] == '9' ? '0' : (char)(valid[^1] + 1);
        var invalid = valid[..^1] + invalidDigit;

        _service.Parse(invalid).Should().BeNull();
    }

    private static string WithValidChecksum(string firstTwelveDigits)
    {
        int[] controlKey = [2, 7, 9, 1, 4, 6, 3, 5, 8, 2, 7, 9];
        var sum = firstTwelveDigits
            .Select((digit, index) => (digit - '0') * controlKey[index])
            .Sum();
        var remainder = sum % 11;
        var controlDigit = remainder == 10 ? 1 : remainder;
        return firstTwelveDigits + controlDigit;
    }
}
