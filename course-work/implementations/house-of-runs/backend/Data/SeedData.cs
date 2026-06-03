using System.Text.Json;
using HouseOfRuns.Api.Models;
using HouseOfRuns.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(HouseOfRunsDbContext db, PasswordHasher passwordHasher, string contentRootPath)
    {
        if (!await db.Users.AnyAsync(user => user.UserName == "admin"))
        {
            db.Users.Add(new AppUser
            {
                UserName = "admin",
                Email = "admin@houseofruns.local",
                DisplayName = "House Admin",
                PasswordHash = passwordHasher.Hash("admin1234"),
                Bio = "Seeded administrator account.",
                Role = "Admin",
                Reputation = 999
            });
        }

        if (!await db.Users.AnyAsync(user => user.UserName == "demo"))
        {
            db.Users.Add(new AppUser
            {
                UserName = "demo",
                Email = "demo@houseofruns.local",
                DisplayName = "Demo Shade",
                PasswordHash = passwordHasher.Hash("demo1234"),
                Bio = "Seeded account for quick coursework demos.",
                Role = "User",
                Reputation = 25
            });
        }

        await SeedWeaponsAsync(db);
        await SeedBoonsAsync(db, contentRootPath);
        await db.SaveChangesAsync();
    }

    private static async Task SeedWeaponsAsync(HouseOfRunsDbContext db)
    {
        var existingKeys = (await db.Weapons
                .Select(weapon => new { weapon.Name, weapon.AspectName })
                .ToListAsync())
            .Select(weapon => Key(weapon.Name, weapon.AspectName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var weapon in WeaponSeeds())
        {
            if (existingKeys.Add(Key(weapon.Name, weapon.AspectName)))
            {
                db.Weapons.Add(weapon);
            }
        }
    }

    private static async Task SeedBoonsAsync(HouseOfRunsDbContext db, string contentRootPath)
    {
        var existingKeys = (await db.Boons
                .Select(boon => new { boon.Name, boon.God })
                .ToListAsync())
            .Select(boon => Key(boon.Name, boon.God))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seeds = DefaultBoonSeeds().Concat(await AssetBoonSeedsAsync(contentRootPath));
        foreach (var boon in seeds)
        {
            if (existingKeys.Add(Key(boon.Name, boon.God)))
            {
                db.Boons.Add(boon);
            }
        }
    }

    private static IEnumerable<Weapon> WeaponSeeds()
    {
        yield return Weapon("Stygius", "Aspect of Zagreus", "Sword", 20, 0);
        yield return Weapon("Stygius", "Aspect of Nemesis", "Sword", 20, 1);
        yield return Weapon("Stygius", "Aspect of Poseidon", "Sword", 20, 2);
        yield return Weapon("Stygius", "Aspect of Arthur", "Sword", 38, 3);
        yield return Weapon("Varatha", "Aspect of Zagreus", "Spear", 25, 0);
        yield return Weapon("Varatha", "Aspect of Achilles", "Spear", 25, 1);
        yield return Weapon("Varatha", "Aspect of Hades", "Spear", 25, 2);
        yield return Weapon("Varatha", "Aspect of Guan Yu", "Spear", 45, 3);
        yield return Weapon("Aegis", "Aspect of Zagreus", "Shield", 15, 0);
        yield return Weapon("Aegis", "Aspect of Chaos", "Shield", 15, 1);
        yield return Weapon("Aegis", "Aspect of Zeus", "Shield", 15, 2);
        yield return Weapon("Aegis", "Aspect of Beowulf", "Shield", 20, 3);
        yield return Weapon("Coronacht", "Aspect of Zagreus", "Bow", 45, 0);
        yield return Weapon("Coronacht", "Aspect of Chiron", "Bow", 45, 1);
        yield return Weapon("Coronacht", "Aspect of Hera", "Bow", 45, 2);
        yield return Weapon("Coronacht", "Aspect of Rama", "Bow", 60, 3);
        yield return Weapon("Malphon", "Aspect of Zagreus", "Fists", 15, 0);
        yield return Weapon("Malphon", "Aspect of Talos", "Fists", 15, 1);
        yield return Weapon("Malphon", "Aspect of Demeter", "Fists", 15, 2);
        yield return Weapon("Malphon", "Aspect of Gilgamesh", "Fists", 25, 3);
        yield return Weapon("Exagryph", "Aspect of Zagreus", "Rail", 10, 0);
        yield return Weapon("Exagryph", "Aspect of Eris", "Rail", 10, 1);
        yield return Weapon("Exagryph", "Aspect of Hestia", "Rail", 10, 2);
        yield return Weapon("Exagryph", "Aspect of Lucifer", "Rail", 20, 3);
    }

    private static Weapon Weapon(string name, string aspect, string type, decimal damage, int unlockCost) => new()
    {
        Name = name,
        AspectName = aspect,
        WeaponType = type,
        TitanBloodLevel = aspect == "Aspect of Zagreus" ? 0 : 5,
        UnlockCost = unlockCost,
        BaseDamage = damage,
        IsUnlocked = true,
        Description = $"{type} weapon option seeded for run editing."
    };

    private static IEnumerable<Boon> DefaultBoonSeeds()
    {
        yield return Boon("Deadly Strike", "Artemis", "Attack", "Attack deals more damage and has a chance to crit.");
        yield return Boon("Divine Dash", "Athena", "Dash", "Dash can deflect incoming attacks.");
        yield return Boon("Curse of Agony", "Ares", "Attack", "Attack inflicts Doom.");
        yield return Boon("Tidal Dash", "Poseidon", "Dash", "Dash damages foes and knocks them away.");
        yield return Boon("Heartbreak Flourish", "Aphrodite", "Special", "Special deals more damage and inflicts Weak.");
        yield return Boon("Merciful End", "Ares/Athena", "Duo", "Deflecting attacks immediately trigger Doom effects.", isDuo: true);
    }

    private static async Task<IEnumerable<Boon>> AssetBoonSeedsAsync(string contentRootPath)
    {
        var imageRoot = FindImageRoot(contentRootPath);
        if (imageRoot is null)
        {
            return [];
        }

        var seeds = new List<Boon>();
        foreach (var entry in await ReadAssetEntriesAsync(Path.Combine(imageRoot, "boons", "manifest.json")))
        {
            var name = AssetName(entry.Title, stripBoonRank: true);
            seeds.Add(Boon(name, InferGod(name), InferEffectType(name), "Seeded from downloaded boon icon assets."));
        }

        foreach (var entry in await ReadAssetEntriesAsync(Path.Combine(imageRoot, "upgrades", "manifest.json")))
        {
            var name = AssetName(entry.Title);
            if (!name.EndsWith("Aspect", StringComparison.OrdinalIgnoreCase))
            {
                seeds.Add(Boon(name, "Daedalus", "Hammer", "Seeded from downloaded weapon upgrade icon assets."));
            }
        }

        foreach (var entry in await ReadAssetEntriesAsync(Path.Combine(imageRoot, "items", "manifest.json")))
        {
            var name = AssetName(entry.Title);
            seeds.Add(Boon(name, RewardNames.Contains(name) ? "Reward" : "Item", RewardNames.Contains(name) ? "Reward" : "Item", "Seeded from downloaded item icon assets."));
        }

        foreach (var entry in await ReadAssetEntriesAsync(Path.Combine(imageRoot, "keepsakes", "manifest.json")))
        {
            var name = AssetName(entry.Title);
            seeds.Add(Boon(name, "Keepsake", "Keepsake", "Seeded from downloaded keepsake icon assets."));
        }

        return seeds;
    }

    private static async Task<IReadOnlyList<AssetEntry>> ReadAssetEntriesAsync(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return [];
        }

        await using var stream = File.OpenRead(manifestPath);
        using var document = await JsonDocument.ParseAsync(stream);
        var entries = new List<AssetEntry>();
        CollectAssetEntries(document.RootElement, entries);
        return entries
            .Where(entry => entry.File.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .DistinctBy(entry => entry.File, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void CollectAssetEntries(JsonElement element, List<AssetEntry> entries)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectAssetEntries(item, entries);
            }

            return;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("title", out var title) &&
            element.TryGetProperty("file", out var file) &&
            title.ValueKind == JsonValueKind.String &&
            file.ValueKind == JsonValueKind.String)
        {
            entries.Add(new AssetEntry(title.GetString() ?? string.Empty, file.GetString() ?? string.Empty));
        }

        foreach (var property in element.EnumerateObject())
        {
            CollectAssetEntries(property.Value, entries);
        }
    }

    private static string? FindImageRoot(string contentRootPath)
    {
        var current = new DirectoryInfo(contentRootPath);
        for (var i = 0; current is not null && i < 6; i++, current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "frontend", "wwwroot", "images");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string AssetName(string title, bool stripBoonRank = false)
    {
        var name = Path.GetFileNameWithoutExtension(title)
            .Replace('_', ' ')
            .Trim();

        if (stripBoonRank && name.EndsWith(" I", StringComparison.Ordinal))
        {
            name = name[..^2];
        }

        return TrimTo(name, 80);
    }

    private static Boon Boon(string name, string god, string effectType, string description, bool isDuo = false, bool isLegendary = false) => new()
    {
        Name = TrimTo(name, 80),
        God = TrimTo(god, 40),
        EffectType = TrimTo(effectType, 50),
        Level = 1,
        PowerScale = 1,
        IsDuo = isDuo,
        IsLegendary = isLegendary,
        Description = TrimTo(description, 600)
    };

    private static string InferGod(string name)
    {
        foreach (var god in Gods)
        {
            if (name.Contains(god, StringComparison.OrdinalIgnoreCase))
            {
                return god;
            }
        }

        return "Other";
    }

    private static string InferEffectType(string name)
    {
        if (name.Contains("Dash", StringComparison.OrdinalIgnoreCase))
        {
            return "Dash";
        }

        if (name.Contains("Strike", StringComparison.OrdinalIgnoreCase))
        {
            return "Attack";
        }

        if (name.Contains("Flourish", StringComparison.OrdinalIgnoreCase))
        {
            return "Special";
        }

        if (name.Contains("Shot", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Cast", StringComparison.OrdinalIgnoreCase))
        {
            return "Cast";
        }

        if (name.Contains("Aid", StringComparison.OrdinalIgnoreCase))
        {
            return "Call";
        }

        return "Boon";
    }

    private static string Key(string first, string second) => $"{first.Trim()}|{second.Trim()}";

    private static string TrimTo(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static readonly string[] Gods =
    [
        "Zeus", "Poseidon", "Athena", "Aphrodite", "Ares", "Artemis", "Dionysus", "Hermes", "Demeter", "Chaos"
    ];

    private static readonly HashSet<string> RewardNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ambrosia", "Ambrosia Small", "Centaur Heart", "Centaur Soul", "Charon's Obol", "Chthonic Key",
        "Chthonic Key Small", "Daedalus Hammer", "Darkness", "Darkness Small", "Diamond", "Diamond Small",
        "Gemstone", "Gemstone Small", "Heart", "Heat", "Nectar", "Obol", "Pom of Power",
        "Titan Blood", "Titan Blood Small", "Wrapped Boon"
    };

    private sealed record AssetEntry(string Title, string File);
}
