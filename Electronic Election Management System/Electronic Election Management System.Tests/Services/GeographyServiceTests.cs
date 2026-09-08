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
            new(Guid.NewGuid(), "Romania", "RO", LabelCategories.Country, true)
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
            new(Guid.NewGuid(), "Cluj", "RO-CJ", LabelCategories.Subdivision, false)
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
}
