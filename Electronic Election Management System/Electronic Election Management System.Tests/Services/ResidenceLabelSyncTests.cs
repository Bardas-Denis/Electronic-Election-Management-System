using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Data.Repositories;
using Electronic_Election_Management_System.Models;
using Electronic_Election_Management_System.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Tests.Services;

/// <summary>
/// Runs against a real database and the real repository. Every rule here is about what ends up
/// stored - whether a second user reuses a label, whether a move leaves anything behind - and a
/// mocked repository would only confirm its own setup.
/// </summary>
public class ResidenceLabelSyncTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ElectionDbContext _db;
    private readonly ResidenceLabelSync _sync;

    private readonly Label _romania, _cluj, _timis, _moldova, _chisinau;

    public ResidenceLabelSyncTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _db = new ElectionDbContext(
            new DbContextOptionsBuilder<ElectionDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _sync = new ResidenceLabelSync(new LabelRepository(_db));

        _romania = Node("Romania", "RO", LabelCategories.Country, null);
        _moldova = Node("Moldova", "MD", LabelCategories.Country, null);
        _cluj = Node("Cluj", "RO-CJ", LabelCategories.Subdivision, _romania);
        _timis = Node("Timis", "RO-TM", LabelCategories.Subdivision, _romania);
        _chisinau = Node("Chisinau", "MD-CU", LabelCategories.Subdivision, _moldova);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private Label Node(string name, string code, string category, Label? parent)
    {
        var label = new Label { Name = name, Code = code, Category = category, ParentId = parent?.Id };
        _db.Labels.Add(label);
        return label;
    }

    private async Task<User> UserAsync()
    {
        var user = new User { Email = $"{Guid.NewGuid()}@test.com" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private static UserDetails Lives(User user, string? country, string? county, string? city) => new()
    {
        UserId = user.Id,
        ResidenceCountry = country,
        ResidenceCountyCode = county,
        ResidenceCity = city
    };

    private Task<List<string>> LabelNamesOf(User user) => _db.UserLabels
        .Where(ul => ul.UserId == user.Id)
        .Select(ul => ul.Label.Name)
        .OrderBy(n => n)
        .ToListAsync();

    private Task<List<Label>> LocalitiesUnder(Label county) => _db.Labels
        .Where(l => l.ParentId == county.Id && l.Category == LabelCategories.Locality)
        .ToListAsync();

    // --- the chain ---

    // Every level, not only the deepest: audience expansion matches label ids exactly and does
    // not walk the tree, so an election aimed at Romania reaches only users who carry Romania.
    [Fact]
    public async Task SyncAsync_LinksTheCountryTheCountyAndTheCity()
    {
        var ana = await UserAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "RO-CJ", "Cluj-Napoca"));

        (await LabelNamesOf(ana)).Should().Equal("Cluj", "Cluj-Napoca", "Romania");
    }

    [Fact]
    public async Task SyncAsync_CreatesAnUnknownCityAsALocalityUnderTheCounty()
    {
        var ana = await UserAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "RO-CJ", "Cluj-Napoca"));

        var city = (await LocalitiesUnder(_cluj)).Should().ContainSingle().Subject;
        city.Name.Should().Be("Cluj-Napoca");
    }

    [Fact]
    public async Task SyncAsync_ForASecondUserFromTheSameCity_ReusesTheLabel()
    {
        var ana = await UserAsync();
        var bob = await UserAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "RO-CJ", "Cluj-Napoca"));
        await _sync.SyncAsync(bob.Id, Lives(bob, "RO", "RO-CJ", "Cluj-Napoca"));

        (await LocalitiesUnder(_cluj)).Should().ContainSingle();
    }

    [Fact]
    public async Task SyncAsync_TreatsSpellingVariantsAsOneCity()
    {
        var ana = await UserAsync();
        var bob = await UserAsync();
        var cora = await UserAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "RO-TM", "Timișoara"));
        await _sync.SyncAsync(bob.Id, Lives(bob, "RO", "RO-TM", "TIMISOARA"));
        await _sync.SyncAsync(cora.Id, Lives(cora, "RO", "RO-TM", "timisoara"));

        var city = (await LocalitiesUnder(_timis)).Should().ContainSingle().Subject;
        city.Name.Should().Be("Timișoara", "the first spelling to arrive is the one kept for display");
    }

    // --- moving ---

    [Fact]
    public async Task SyncAsync_WhenTheUserMoves_ReplacesTheOldLinks()
    {
        var ana = await UserAsync();
        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "RO-CJ", "Cluj-Napoca"));

        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "RO-TM", "Timisoara"));

        (await LabelNamesOf(ana)).Should().Equal("Romania", "Timis", "Timisoara");
    }

    [Fact]
    public async Task SyncAsync_WhenTheCountryIsCleared_RemovesEveryGeographicLabel()
    {
        var ana = await UserAsync();
        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "RO-CJ", "Cluj-Napoca"));

        await _sync.SyncAsync(ana.Id, Lives(ana, null, null, null));

        (await LabelNamesOf(ana)).Should().BeEmpty();
    }

    // --- what the sync must never do ---

    // Geographic labels are owned by the profile; everything else was handed out by an admin, and
    // saving a profile must not be a way to shed it.
    [Fact]
    public async Task SyncAsync_LeavesLabelsOutsideTheTreeAlone()
    {
        var ana = await UserAsync();
        var hr = new Label { Name = "HR", Category = "Department" };
        _db.Labels.Add(hr);
        _db.UserLabels.Add(new UserLabel { UserId = ana.Id, LabelId = hr.Id, AssignedBy = ana.Id });
        await _db.SaveChangesAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, null, null, null));

        (await LabelNamesOf(ana)).Should().Equal("HR");
    }

    // The request can name any pair of codes. A county that does not belong to the country is
    // not linked, so a hand-built request cannot claim a region the tree does not support.
    [Fact]
    public async Task SyncAsync_DoesNotLinkACountyFromAnotherCountry()
    {
        var ana = await UserAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", "MD-CU", "Chisinau"));

        (await LabelNamesOf(ana)).Should().Equal("Romania");
        (await LocalitiesUnder(_chisinau)).Should().BeEmpty();
    }

    [Fact]
    public async Task SyncAsync_ForAnUnknownCountryCode_LinksNothing()
    {
        var ana = await UserAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, "ZZ", null, null));

        (await LabelNamesOf(ana)).Should().BeEmpty();
    }

    [Fact]
    public async Task SyncAsync_RecordsTheUserAsTheAssigner()
    {
        var ana = await UserAsync();

        await _sync.SyncAsync(ana.Id, Lives(ana, "RO", null, null));

        var link = await _db.UserLabels.SingleAsync(ul => ul.UserId == ana.Id);
        link.AssignedBy.Should().Be(ana.Id);
    }
}
