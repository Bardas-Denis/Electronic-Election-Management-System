using System.ComponentModel.DataAnnotations;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.DTOs;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;

namespace Electronic_Election_Management_System.Tests.Services;

public class PersonalDetailsMapperTests
{
    private static PersonalDetailsDto DtoWithCountry(string? country) => new()
    {
        FullName = "Ion Popescu",
        ResidenceCountry = country,
        ResidenceCounty = "Cluj",
        ResidenceCountyCode = "RO-CJ"
    };

    [Fact]
    public void ApplyTrimmed_ToUserDetails_CarriesResidenceCountry()
    {
        var target = new UserDetails();

        PersonalDetailsMapper.ApplyTrimmed(DtoWithCountry("RO"), target);

        target.ResidenceCountry.Should().Be("RO");
    }

    [Fact]
    public void ApplyTrimmed_ToVoterDeclaration_CarriesResidenceCountry()
    {
        var target = new VoterDeclaration();

        PersonalDetailsMapper.ApplyTrimmed(DtoWithCountry("RO"), target);

        target.ResidenceCountry.Should().Be("RO");
    }

    [Theory]
    [InlineData(" RO ", "RO")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void ApplyTrimmed_TreatsResidenceCountryLikeEveryOtherField(string? input, string? expected)
    {
        var target = new UserDetails();

        PersonalDetailsMapper.ApplyTrimmed(DtoWithCountry(input), target);

        target.ResidenceCountry.Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(UserDetails))]
    [InlineData(typeof(VoterDeclaration))]
    public void ApplyTrimmed_CarriesResidenceCountyCode(Type targetType)
    {
        var dto = DtoWithCountry("RO");

        string? mapped;
        if (targetType == typeof(UserDetails))
        {
            var target = new UserDetails();
            PersonalDetailsMapper.ApplyTrimmed(dto, target);
            mapped = target.ResidenceCountyCode;
        }
        else
        {
            var target = new VoterDeclaration();
            PersonalDetailsMapper.ApplyTrimmed(dto, target);
            mapped = target.ResidenceCountyCode;
        }

        mapped.Should().Be("RO-CJ");
    }

    // Natural Earth ships 170 placeholder subdivision codes shaped like "AE-X01~", so the only
    // contract this field can carry is a length cap - the real check is whether it resolves to a
    // node in the tree, which happens when the label is linked.
    [Fact]
    public void PersonalDetailsDto_RejectsAnOverlongCountyCode()
    {
        var dto = DtoWithCountry("RO");
        dto.ResidenceCountyCode = new string('X', ValidationRules.SubdivisionCodeMaxLength + 1);

        Validate(dto).Should()
            .Contain(r => r.MemberNames.Contains(nameof(PersonalDetailsDto.ResidenceCountyCode)));
    }

    [Theory]
    [InlineData("RO")]
    [InlineData("GB")]
    public void PersonalDetailsDto_AcceptsAnAlpha2CountryCode(string code)
        => Validate(DtoWithCountry(code)).Should().BeEmpty();

    // The value is matched against Label.Code, which is ISO 3166-1 alpha-2 throughout the seeded
    // tree, so anything else can only ever fail to resolve to a country.
    [Theory]
    [InlineData("ro")]          // lower case would not match the seeded code
    [InlineData("ROU")]         // alpha-3
    [InlineData("Romania")]     // a display name
    [InlineData("R1")]
    public void PersonalDetailsDto_RejectsAnythingThatIsNotAnAlpha2Code(string code)
        => Validate(DtoWithCountry(code))
            .Should().Contain(r => r.MemberNames.Contains(nameof(PersonalDetailsDto.ResidenceCountry)));

    private static List<ValidationResult> Validate(PersonalDetailsDto dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, true);
        return results;
    }
}
