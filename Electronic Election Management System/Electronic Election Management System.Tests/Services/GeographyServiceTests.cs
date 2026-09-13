using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using NSubstitute;

namespace Electronic_Election_Management_System.Tests.Services;

public class GeographyServiceTests
{
    private readonly ILabelRepository _labels = Substitute.For<ILabelRepository>();
    private readonly GeographyService _service;

    public GeographyServiceTests()
    {
        _service = new GeographyService(_labels);
    }

    [Fact]
    public async Task GetCountriesAsync_CarriesEveryFieldThrough()
    {
        _labels.GetCountriesAsync().Returns(new List<GeographicNode>
        {
            new(Guid.NewGuid(), "Romania", "RO", LabelCategories.Country, null, true)
        });

        var result = await _service.GetCountriesAsync();

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Romania");
        result[0].Code.Should().Be("RO");
        result[0].Category.Should().Be(LabelCategories.Country);
        result[0].HasChildren.Should().BeTrue();
    }

    [Fact]
    public async Task GetCountriesAsync_WhenTreeIsEmpty_ReturnsEmptyList()
    {
        _labels.GetCountriesAsync().Returns(new List<GeographicNode>());

        var result = await _service.GetCountriesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChildrenAsync_WhenNodeIsUnknown_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _labels.GetByIdAsync(id).Returns((Label?)null);

        var result = await _service.GetChildrenAsync(id);

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.LabelNotFound);
        await _labels.DidNotReceive().GetChildrenAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetChildrenAsync_WhenLabelIsNotGeographic_ReturnsNotFound()
    {
        // "HR" is a real label, but it is not part of the tree. Answering with an empty list
        // would confirm the id exists and turn this endpoint into a way to probe label ids.
        var hr = new Label { Id = Guid.NewGuid(), Name = "HR", Category = "Department" };
        _labels.GetByIdAsync(hr.Id).Returns(hr);

        var result = await _service.GetChildrenAsync(hr.Id);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.LabelNotFound);
        await _labels.DidNotReceive().GetChildrenAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task GetChildrenAsync_WhenNodeIsGeographic_ReturnsItsChildren()
    {
        var romania = new Label { Id = Guid.NewGuid(), Name = "Romania", Category = LabelCategories.Country };
        _labels.GetByIdAsync(romania.Id).Returns(romania);
        _labels.GetChildrenAsync(romania.Id).Returns(new List<GeographicNode>
        {
            new(Guid.NewGuid(), "Cluj", "RO-CJ", LabelCategories.Subdivision, "County", false)
        });

        var result = await _service.GetChildrenAsync(romania.Id);

        result.Success.Should().BeTrue();
        result.Data.Should().ContainSingle();
        result.Data![0].Code.Should().Be("RO-CJ");
        result.Data[0].HasChildren.Should().BeFalse();
    }

    [Fact]
    public async Task GetChildrenAsync_WhenGeographicNodeHasNoChildren_ReturnsEmptyListNotNotFound()
    {
        // A leaf and an unknown id must not look the same to the caller.
        var county = new Label { Id = Guid.NewGuid(), Name = "Cluj", Category = LabelCategories.Subdivision };
        _labels.GetByIdAsync(county.Id).Returns(county);
        _labels.GetChildrenAsync(county.Id).Returns(new List<GeographicNode>());

        var result = await _service.GetChildrenAsync(county.Id);

        result.Success.Should().BeTrue();
        result.Data.Should().BeEmpty();
    }

    // --- CreateChildAsync ---

    [Fact]
    public async Task CreateChildAsync_UnderACountry_MakesASubdivision()
    {
        var romania = Geo("Romania", LabelCategories.Country);
        _labels.GetByIdAsync(romania.Id).Returns(romania);

        var result = await _service.CreateChildAsync(romania.Id, "Cluj", null);

        result.Success.Should().BeTrue();
        result.Data!.Category.Should().Be(LabelCategories.Subdivision);
        result.Data.Code.Should().BeNull("nodes added by hand have no ISO code");
        await _labels.Received(1).AddAsync(Arg.Is<Label>(l => l.ParentId == romania.Id));
    }

    [Fact]
    public async Task CreateChildAsync_UnderASubdivision_MakesALocality()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);

        var result = await _service.CreateChildAsync(cluj.Id, "Floresti", null);

        result.Data!.Category.Should().Be(LabelCategories.Locality);
    }

    [Fact]
    public async Task CreateChildAsync_TrimsTheName()
    {
        var romania = Geo("Romania", LabelCategories.Country);
        _labels.GetByIdAsync(romania.Id).Returns(romania);

        var result = await _service.CreateChildAsync(romania.Id, "   Cluj   ", null);

        result.Data!.Name.Should().Be("Cluj");
        await _labels.Received(1).NameTakenUnderParentAsync(romania.Id, "Cluj", null);
    }

    [Fact]
    public async Task CreateChildAsync_WhenParentIsUnknown_ReturnsNotFound()
    {
        var id = Guid.NewGuid();
        _labels.GetByIdAsync(id).Returns((Label?)null);

        var result = await _service.CreateChildAsync(id, "Cluj", null);

        result.IsNotFound.Should().BeTrue();
        await _labels.DidNotReceive().AddAsync(Arg.Any<Label>());
    }

    [Fact]
    public async Task CreateChildAsync_WhenParentIsNotGeographic_ReturnsNotFound()
    {
        var hr = new Label { Id = Guid.NewGuid(), Name = "HR", Category = "Department" };
        _labels.GetByIdAsync(hr.Id).Returns(hr);

        var result = await _service.CreateChildAsync(hr.Id, "Cluj", null);

        result.IsNotFound.Should().BeTrue();
        await _labels.DidNotReceive().AddAsync(Arg.Any<Label>());
    }

    [Fact]
    public async Task CreateChildAsync_WhenASiblingAlreadyHasTheName_Fails()
    {
        // Mirrors the unique index on (ParentId, Name): without this the insert would surface
        // as a database failure and a 500.
        var romania = Geo("Romania", LabelCategories.Country);
        _labels.GetByIdAsync(romania.Id).Returns(romania);
        _labels.NameTakenUnderParentAsync(romania.Id, "Cluj", null).Returns(true);

        var result = await _service.CreateChildAsync(romania.Id, "Cluj", null);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCode.LabelNameTakenUnderParent);
        await _labels.DidNotReceive().AddAsync(Arg.Any<Label>());
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_ChangesTheNameAndKeepsTheCode()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision, "RO-CJ");
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);

        var result = await _service.UpdateAsync(cluj.Id, "Cluj-Napoca", null);

        result.Success.Should().BeTrue();
        result.Data!.Name.Should().Be("Cluj-Napoca");
        result.Data.Code.Should().Be("RO-CJ", "a rename must not break what points at the node");
        await _labels.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_ToItsOwnCurrentName_IsAllowed()
    {
        // The clash check excludes the node itself, otherwise saving an untouched form fails.
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);
        _labels.NameTakenUnderParentAsync(cluj.ParentId, "Cluj", cluj.Id).Returns(false);

        var result = await _service.UpdateAsync(cluj.Id, "Cluj", null);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WhenASiblingAlreadyHasTheName_Fails()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);
        _labels.NameTakenUnderParentAsync(cluj.ParentId, "Timis", cluj.Id).Returns(true);

        var result = await _service.UpdateAsync(cluj.Id, "Timis", null);

        result.ErrorCode.Should().Be(ErrorCode.LabelNameTakenUnderParent);
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateAsync_WhenLabelIsNotGeographic_ReturnsNotFound()
    {
        var hr = new Label { Id = Guid.NewGuid(), Name = "HR", Category = "Department" };
        _labels.GetByIdAsync(hr.Id).Returns(hr);

        var result = await _service.UpdateAsync(hr.Id, "HR2", null);

        result.IsNotFound.Should().BeTrue();
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    // --- Kind: descriptive only, never structural ---

    [Fact]
    public async Task CreateChildAsync_StoresTheKindWithoutTouchingTheCategory()
    {
        var romania = Geo("Romania", LabelCategories.Country);
        _labels.GetByIdAsync(romania.Id).Returns(romania);

        var result = await _service.CreateChildAsync(romania.Id, "Cluj", "Județ");

        result.Data!.Kind.Should().Be("Județ");
        result.Data.Category.Should().Be(LabelCategories.Subdivision,
            "the kind describes the place; the category is what the tree is built on");
    }

    [Fact]
    public async Task CreateChildAsync_TrimsTheKindAndTreatsBlankAsUnspecified()
    {
        var romania = Geo("Romania", LabelCategories.Country);
        _labels.GetByIdAsync(romania.Id).Returns(romania);

        var spaced = await _service.CreateChildAsync(romania.Id, "Cluj", "  Oraș  ");
        var blank = await _service.CreateChildAsync(romania.Id, "Alba", "   ");

        spaced.Data!.Kind.Should().Be("Oraș");
        blank.Data!.Kind.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ChangesTheKind()
    {
        // The seed writes English words from the source data, so translating them is the
        // first thing an administrator will want to do.
        var cluj = Geo("Cluj", LabelCategories.Subdivision, "RO-CJ");
        cluj.Kind = "County";
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);

        var result = await _service.UpdateAsync(cluj.Id, "Cluj", "Județ");

        result.Success.Should().BeTrue();
        result.Data!.Kind.Should().Be("Județ");
        result.Data.Category.Should().Be(LabelCategories.Subdivision);
        result.Data.Code.Should().Be("RO-CJ");
    }

    [Fact]
    public async Task UpdateAsync_WithABlankKind_ClearsItBackToUnspecified()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        cluj.Kind = "County";
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);

        var result = await _service.UpdateAsync(cluj.Id, "Cluj", "");

        result.Data!.Kind.Should().BeNull();
    }

    // --- MoveAsync ---

    [Fact]
    public async Task MoveAsync_PutsTheNodeUnderTheNewParentAndSaves()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision, "RO-CJ");
        var moldova = Geo("Moldova", LabelCategories.Country, "MD");
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);
        _labels.GetByIdAsync(moldova.Id).Returns(moldova);

        var result = await _service.MoveAsync(cluj.Id, moldova.Id);

        result.Success.Should().BeTrue();
        cluj.ParentId.Should().Be(moldova.Id);
        result.Data!.Code.Should().Be("RO-CJ", "a move must not disturb what points at the node");
        await _labels.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task MoveAsync_WhenTheNodeIsACountry_Fails()
    {
        var romania = Geo("Romania", LabelCategories.Country, "RO");
        var moldova = Geo("Moldova", LabelCategories.Country, "MD");
        _labels.GetByIdAsync(romania.Id).Returns(romania);
        _labels.GetByIdAsync(moldova.Id).Returns(moldova);

        var result = await _service.MoveAsync(romania.Id, moldova.Id);

        result.ErrorCode.Should().Be(ErrorCode.CountryCannotBeMoved);
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    // Categories describe the level, and a move that changed them would have to rewrite the whole
    // subtree. Requiring the level above keeps the node and everything under it consistent.
    [Fact]
    public async Task MoveAsync_UnderAParentAtTheWrongLevel_Fails()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        var timisoara = Geo("Timisoara", LabelCategories.Locality);
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);
        _labels.GetByIdAsync(timisoara.Id).Returns(timisoara);

        var result = await _service.MoveAsync(cluj.Id, timisoara.Id);

        result.ErrorCode.Should().Be(ErrorCode.InvalidLabelParentLevel);
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task MoveAsync_UnderItsOwnDescendant_Fails()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        var romania = Geo("Romania", LabelCategories.Country, "RO");
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);
        _labels.GetByIdAsync(romania.Id).Returns(romania);
        _labels.IsDescendantOfAsync(cluj.Id, romania.Id).Returns(true);

        var result = await _service.MoveAsync(cluj.Id, romania.Id);

        result.ErrorCode.Should().Be(ErrorCode.CircularLabelParent);
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task MoveAsync_WhenTheDestinationAlreadyHasThatName_Fails()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        var moldova = Geo("Moldova", LabelCategories.Country, "MD");
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);
        _labels.GetByIdAsync(moldova.Id).Returns(moldova);
        _labels.NameTakenUnderParentAsync(moldova.Id, "Cluj", cluj.Id).Returns(true);

        var result = await _service.MoveAsync(cluj.Id, moldova.Id);

        result.ErrorCode.Should().Be(ErrorCode.LabelNameTakenUnderParent);
        await _labels.DidNotReceive().SaveChangesAsync();
    }

    [Fact]
    public async Task MoveAsync_WhenTheNewParentIsUnknown_ReturnsNotFound()
    {
        var cluj = Geo("Cluj", LabelCategories.Subdivision);
        _labels.GetByIdAsync(cluj.Id).Returns(cluj);

        var result = await _service.MoveAsync(cluj.Id, Guid.NewGuid());

        result.IsNotFound.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCode.LabelNotFound);
    }

    private static Label Geo(string name, string category, string? code = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Category = category,
        Code = code,
        ParentId = category == LabelCategories.Country ? null : Guid.NewGuid()
    };
}
