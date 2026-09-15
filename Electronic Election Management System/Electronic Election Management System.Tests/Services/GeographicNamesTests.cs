using Electronic_Election_Management_System.Services;
using FluentAssertions;

namespace Electronic_Election_Management_System.Tests.Services;

public class GeographicNamesTests
{
    [Theory]
    [InlineData("Timișoara")]
    [InlineData("Timişoara")]      // cedilla ş, from older Romanian keyboard layouts
    [InlineData("TIMISOARA")]
    [InlineData("timisoara")]
    [InlineData("  Timisoara  ")]
    public void Key_FoldsEverySpellingOfOneCityOntoTheSameValue(string spelling)
        => GeographicNames.Key(spelling).Should().Be(GeographicNames.Key("Timisoara"));

    [Theory]
    [InlineData("Cluj-Napoca")]
    [InlineData("Cluj Napoca")]
    [InlineData("Cluj  -  Napoca")]
    public void Key_TreatsHyphensAndRunsOfSpaceAsOneSeparator(string spelling)
        => GeographicNames.Key(spelling).Should().Be(GeographicNames.Key("Cluj Napoca"));

    [Fact]
    public void Key_KeepsDifferentPlacesApart()
        => GeographicNames.Key("Sibiu").Should().NotBe(GeographicNames.Key("Sighisoara"));
}
