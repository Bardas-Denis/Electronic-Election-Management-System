using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Electronic_Election_Management_System.Constants;
using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Electronic_Election_Management_System.Geo
{
    /// <summary>
    /// Populates the geographic label tree from <c>Geo/world-subdivisions.json</c>: every country
    /// as a root, every first-level subdivision as its child. Localities are not seeded — see
    /// <see cref="LabelCategories.Locality"/>.
    /// <para>
    /// Source: Natural Earth admin-1 (public domain), reduced to code, name and type. Duplicate
    /// rows in the source — one subdivision split across several polygons — were collapsed by
    /// code, and names that clash within a country were disambiguated by type or code, so that
    /// the unique index on (ParentId, Name) holds.
    /// </para>
    /// </summary>
    public static class GeoLabelSeeder
    {
        private const string DataFile = "Geo/world-subdivisions.json";

        /// <summary>Kept small so the change tracker never holds the whole world at once.</summary>
        private const int BatchSize = 500;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task EnsureSeededAsync(
            ElectionDbContext db, ILogger logger, string contentRootPath)
        {
            // One country is enough to know the tree has been planted. Deliberately not a
            // per-code merge: an administrator may have renamed or pruned nodes, and a seeder
            // that ran on every boot would quietly undo that.
            if (await db.Labels.AnyAsync(l => l.Category == LabelCategories.Country))
                return;

            var path = Path.Combine(contentRootPath, DataFile);
            if (!File.Exists(path))
            {
                logger.LogWarning(
                    "Geographic seed file not found at {Path}; the label tree stays empty.", path);
                return;
            }

            List<SeedCountry>? countries;
            try
            {
                await using var stream = File.OpenRead(path);
                countries = await JsonSerializer.DeserializeAsync<List<SeedCountry>>(stream, JsonOptions);
            }
            catch (JsonException ex)
            {
                // A malformed seed file must not take the whole application down with it.
                logger.LogError(ex, "Geographic seed file at {Path} could not be read.", path);
                return;
            }

            if (countries is null || countries.Count == 0)
            {
                logger.LogWarning("Geographic seed file at {Path} held no countries.", path);
                return;
            }

            var pending = new List<Label>();
            var countryCount = 0;
            var subdivisionCount = 0;

            foreach (var country in countries)
            {
                if (string.IsNullOrWhiteSpace(country.Code) || string.IsNullOrWhiteSpace(country.Name))
                    continue;

                // The id is assigned here rather than by the database, so a subdivision can point
                // at its parent without a round trip in between.
                var countryLabel = new Label
                {
                    Name = country.Name.Trim(),
                    Code = country.Code.Trim(),
                    Category = LabelCategories.Country,
                    ParentId = null
                };
                pending.Add(countryLabel);
                countryCount++;

                foreach (var sub in country.Subs ?? new List<SeedSubdivision>())
                {
                    if (string.IsNullOrWhiteSpace(sub.Code) || string.IsNullOrWhiteSpace(sub.Name))
                        continue;

                    pending.Add(new Label
                    {
                        Name = sub.Name.Trim(),
                        Code = sub.Code.Trim(),
                        Category = LabelCategories.Subdivision,
                        ParentId = countryLabel.Id
                    });
                    subdivisionCount++;
                }
            }

            // Change detection is off for the duration: every entity here is new, so there is
            // nothing to detect, and leaving it on costs more than the insert itself.
            var autoDetect = db.ChangeTracker.AutoDetectChangesEnabled;
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                for (var i = 0; i < pending.Count; i += BatchSize)
                {
                    var batch = pending.GetRange(i, Math.Min(BatchSize, pending.Count - i));
                    db.Labels.AddRange(batch);
                    await db.SaveChangesAsync();
                    db.ChangeTracker.Clear();
                }
            }
            finally
            {
                db.ChangeTracker.AutoDetectChangesEnabled = autoDetect;
            }

            logger.LogInformation(
                "Seeded the geographic label tree: {Countries} countries, {Subdivisions} subdivisions.",
                countryCount, subdivisionCount);
        }

        private sealed class SeedCountry
        {
            [JsonPropertyName("code")] public string? Code { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("subs")] public List<SeedSubdivision>? Subs { get; set; }
        }

        private sealed class SeedSubdivision
        {
            [JsonPropertyName("code")] public string? Code { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("type")] public string? Type { get; set; }
        }
    }
}
