using Electronic_Election_Management_System.Data;
using Electronic_Election_Management_System.Models;
using Microsoft.EntityFrameworkCore;

namespace Electronic_Election_Management_System.Plugins;

/// <summary>
/// Gives every loaded plugin a row in ScoringSchemes, so it appears in the picker the election
/// creator already uses. Nothing in the frontend has to know that plugins exist.
/// </summary>
public static class ScoringSchemeSynchronizer
{
    public static async Task SyncAsync(
        ElectionDbContext db, IScoringPluginRegistry registry, ILogger logger)
    {
        var pluginSchemes = await db.ScoringSchemes
            .Where(s => s.PluginKey != null)
            .ToListAsync();

        foreach (var plugin in registry.Plugins)
        {
            var row = pluginSchemes.FirstOrDefault(s => s.PluginKey == plugin.Key);

            if (row is null)
            {
                db.ScoringSchemes.Add(new ScoringScheme
                {
                    Name = plugin.DisplayName,
                    PluginKey = plugin.Key,
                    Points = new List<int>(),
                    IsLinear = false,
                    // Predefined keeps it visible to every creator and blocks user edits: the
                    // points come from code, so an edited points list would be a lie.
                    IsPredefined = true
                });
            }
            else if (row.Name != plugin.DisplayName)
            {
                row.Name = plugin.DisplayName;
            }
        }

        // Rows outlive their plugin: questions pointing at them keep a valid foreign key, so
        // nothing fails loudly on its own. This line is the only warning anyone gets.
        foreach (var orphan in pluginSchemes.Where(s => !registry.TryGet(s.PluginKey!, out _)))
        {
            logger.LogError(
                "Scoring scheme {Name} ({Id}) needs plugin {Key}, which is not loaded. Ranked "
                + "questions using it fall back to linear scoring.",
                orphan.Name, orphan.Id, orphan.PluginKey);
        }

        await db.SaveChangesAsync();
    }
}
