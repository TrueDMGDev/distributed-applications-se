using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Dtos;
using HouseOfRuns.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseOfRuns.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/import")]
public sealed partial class ImportController(HouseOfRunsDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("run-draft")]
    [RequestSizeLimit(2_000_000)]
    public async Task<ActionResult<ImportRunDraftResponse>> ImportRunDraft(IFormFile? file, [FromForm] int? runIndex)
    {
        var validationError = ValidateImportFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        await using var stream = file!.OpenReadStream();
        using var document = await JsonDocument.ParseAsync(stream);
        var runs = ReadRuns(document.RootElement);

        if (runs.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid import file",
                Detail = "The JSON did not contain any runs.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var selectedRun = runIndex.HasValue
            ? runs.FirstOrDefault(run => GetInt(run, "index") == runIndex.Value)
            : runs.OrderByDescending(run => GetInt(run, "index") ?? 0).First();

        if (selectedRun.ValueKind == JsonValueKind.Undefined)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Run not found",
                Detail = $"The uploaded JSON does not contain run index {runIndex}.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var importIndex = GetInt(selectedRun, "index") ?? runs.Count;
        var localizations = await LoadLocalizationsAsync();
        var draft = await BuildDraftAsync(selectedRun, runs.Count, importIndex, localizations);

        return Ok(draft);
    }

    [HttpPost("run-drafts")]
    [RequestSizeLimit(8_000_000)]
    public async Task<ActionResult<ImportRunsDraftResponse>> ImportRunDrafts(IFormFile? file)
    {
        var validationError = ValidateImportFile(file);
        if (validationError is not null)
        {
            return validationError;
        }

        await using var stream = file!.OpenReadStream();
        using var document = await JsonDocument.ParseAsync(stream);
        var runs = ReadRuns(document.RootElement);

        if (runs.Count == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid import file",
                Detail = "The JSON did not contain any runs.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var localizations = await LoadLocalizationsAsync();
        var drafts = new List<ImportRunDraftResponse>();
        var orderedRuns = runs
            .Select((run, position) => new
            {
                Run = run,
                ImportIndex = GetInt(run, "index") ?? position + 1
            })
            .OrderByDescending(item => item.ImportIndex)
            .ToList();

        foreach (var item in orderedRuns)
        {
            drafts.Add(await BuildDraftAsync(item.Run, runs.Count, item.ImportIndex, localizations));
        }

        return Ok(new ImportRunsDraftResponse(drafts));
    }

    private async Task<ImportRunDraftResponse> BuildDraftAsync(
        JsonElement selectedRun,
        int runCount,
        int importIndex,
        IReadOnlyDictionary<string, string> localizations)
    {
        var weaponInfo = BuildWeaponInfo(GetString(selectedRun, "weapon"), GetString(selectedRun, "aspect"), localizations);
        var weapon = await EnsureWeaponAsync(weaponInfo);
        var traits = ReadTraits(selectedRun).ToList();
        var boons = await EnsureBoonsAsync(traits, localizations);
        var resultInfo = BuildResultInfo(GetString(selectedRun, "result"));
        var clearMessage = Localize(GetString(selectedRun, "clearMessage"), localizations);
        var heatLevel = GetInt(selectedRun, "heatPoints") ?? 0;
        var durationSeconds = ParseDurationSeconds(GetString(selectedRun, "time"));
        var sourceNotes = BuildNotes(selectedRun, runCount, clearMessage);

        var draftBoons = traits
            .Select(trait =>
            {
                var boon = boons[trait.Name];
                var traitInfo = BuildTraitInfo(trait.Name, localizations);
                return new ImportBoonDraftResponse(
                    boon.Id,
                    boon.Name,
                    boon.God,
                    traitInfo.EffectType,
                    Math.Max(1, trait.Level),
                    traitInfo.IsCore);
            })
            .ToList();

        return new ImportRunDraftResponse(
            importIndex,
            $"Run #{importIndex} - {resultInfo.Result} with {weapon.AspectName}",
            weapon.Id,
            weapon.Name,
            heatLevel,
            durationSeconds,
            resultInfo.Result,
            resultInfo.FinalBiome,
            resultInfo.DefeatedBoss,
            DateTime.UtcNow,
            "Imported",
            sourceNotes,
            draftBoons);
    }

    private BadRequestObjectResult? ValidateImportFile(IFormFile? file)
    {
        if (file is not null && file.Length > 0 && file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return BadRequest(new ProblemDetails
        {
            Title = "Invalid import file",
            Detail = "Upload a non-empty JSON file exported from the Hades run-history mod.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    private async Task<Weapon> EnsureWeaponAsync(WeaponImportInfo info)
    {
        var weapon = await db.Weapons.FirstOrDefaultAsync(candidate =>
            candidate.Name == info.Name && candidate.AspectName == info.AspectName);

        if (weapon is not null)
        {
            return weapon;
        }

        weapon = new Weapon
        {
            Name = info.Name,
            AspectName = info.AspectName,
            WeaponType = info.WeaponType,
            TitanBloodLevel = info.AspectName == "Aspect of Zagreus" ? 0 : 1,
            UnlockCost = info.AspectName == "Aspect of Zagreus" ? 0 : 1,
            BaseDamage = info.BaseDamage,
            IsUnlocked = true,
            Description = $"Imported from ExportRunHistory key '{info.InternalWeaponKey}'."
        };

        db.Weapons.Add(weapon);
        await db.SaveChangesAsync();
        return weapon;
    }

    private async Task<Dictionary<string, Boon>> EnsureBoonsAsync(IEnumerable<TraitImportInfo> traits, IReadOnlyDictionary<string, string> localizations)
    {
        var result = new Dictionary<string, Boon>(StringComparer.OrdinalIgnoreCase);
        var existing = await db.Boons.ToListAsync();

        foreach (var trait in traits.DistinctBy(trait => trait.Name))
        {
            var traitInfo = BuildTraitInfo(trait.Name, localizations);
            var marker = $"Imported key: {trait.Name}";
            var boon = existing.FirstOrDefault(candidate =>
                    candidate.Description != null && candidate.Description.Contains(marker, StringComparison.OrdinalIgnoreCase))
                ?? existing.FirstOrDefault(candidate =>
                    candidate.Name == traitInfo.DisplayName && candidate.God == traitInfo.God);

            if (boon is null)
            {
                boon = new Boon
                {
                    Name = traitInfo.DisplayName,
                    God = traitInfo.God,
                    EffectType = traitInfo.EffectType,
                    Level = Math.Max(1, trait.Level),
                    PowerScale = 1,
                    IsDuo = traitInfo.IsDuo,
                    IsLegendary = traitInfo.IsLegendary,
                    Description = $"{marker}. Source: Hades ExportRunHistory."
                };

                db.Boons.Add(boon);
                existing.Add(boon);
            }
            else
            {
                boon.Name = traitInfo.DisplayName;
                boon.God = traitInfo.God;
                boon.EffectType = traitInfo.EffectType;
                boon.Level = Math.Max(boon.Level, Math.Max(1, trait.Level));
                boon.IsDuo = traitInfo.IsDuo;
                boon.IsLegendary = traitInfo.IsLegendary;
            }

            result[trait.Name] = boon;
        }

        await db.SaveChangesAsync();
        return result;
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadLocalizationsAsync()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inheritedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in CandidateLocalizationDirectories())
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.sjson"))
            {
                var text = await System.IO.File.ReadAllTextAsync(file);
                foreach (Match match in LocalizationEntryRegex().Matches(text))
                {
                    var id = match.Groups["id"].Value;
                    var body = match.Groups["body"].Value;
                    var displayName = LocalizationDisplayNameRegex().Match(body);
                    if (displayName.Success)
                    {
                        map[id] = CleanLocalizedText(displayName.Groups["name"].Value);
                        continue;
                    }

                    var inheritFrom = LocalizationInheritFromRegex().Match(body);
                    if (inheritFrom.Success)
                    {
                        inheritedNames[id] = inheritFrom.Groups["parent"].Value;
                    }
                }
            }
        }

        foreach (var (id, _) in inheritedNames)
        {
            if (map.ContainsKey(id))
            {
                continue;
            }

            var inheritedName = ResolveInheritedLocalization(id, map, inheritedNames);
            if (!string.IsNullOrWhiteSpace(inheritedName))
            {
                map[id] = inheritedName;
            }
        }

        return map;
    }

    private static string? ResolveInheritedLocalization(
        string id,
        IReadOnlyDictionary<string, string> map,
        IReadOnlyDictionary<string, string> inheritedNames,
        HashSet<string>? seen = null)
    {
        if (map.TryGetValue(id, out var value))
        {
            return value;
        }

        if (!inheritedNames.TryGetValue(id, out var parent))
        {
            return null;
        }

        seen ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!seen.Add(id))
        {
            return null;
        }

        return ResolveInheritedLocalization(parent, map, inheritedNames, seen);
    }

    private IEnumerable<string> CandidateLocalizationDirectories()
    {
        var current = new DirectoryInfo(environment.ContentRootPath);
        for (var i = 0; current is not null && i < 6; i++, current = current.Parent)
        {
            yield return Path.Combine(current.FullName, "example-json");
        }
    }

    private static List<JsonElement> ReadRuns(JsonElement root)
    {
        if (root.TryGetProperty("runs", out var runs) && runs.ValueKind == JsonValueKind.Array)
        {
            return runs.EnumerateArray().ToList();
        }

        return root.ValueKind == JsonValueKind.Object ? [root] : [];
    }

    private static IEnumerable<TraitImportInfo> ReadTraits(JsonElement run)
    {
        if (!run.TryGetProperty("trait", out var traits) || traits.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var trait in traits.EnumerateArray())
        {
            var name = GetString(trait, "traitName");
            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return new TraitImportInfo(name, GetInt(trait, "traitLevel") ?? 1);
            }
        }
    }

    private static WeaponImportInfo BuildWeaponInfo(string? weaponKey, string? aspectKey, IReadOnlyDictionary<string, string> localizations)
    {
        var baseInfo = (weaponKey ?? string.Empty) switch
        {
            "SwordWeapon" => new WeaponImportInfo("Stygius", "Aspect of Zagreus", "Sword", 20, weaponKey ?? "SwordWeapon"),
            "SpearWeapon" => new WeaponImportInfo("Varatha", "Aspect of Zagreus", "Spear", 25, weaponKey ?? "SpearWeapon"),
            "ShieldWeapon" => new WeaponImportInfo("Aegis", "Aspect of Zagreus", "Shield", 15, weaponKey ?? "ShieldWeapon"),
            "BowWeapon" => new WeaponImportInfo("Coronacht", "Aspect of Zagreus", "Bow", 45, weaponKey ?? "BowWeapon"),
            "FistWeapon" => new WeaponImportInfo("Malphon", "Aspect of Zagreus", "Fists", 15, weaponKey ?? "FistWeapon"),
            "GunWeapon" => new WeaponImportInfo("Exagryph", "Aspect of Zagreus", "Rail", 10, weaponKey ?? "GunWeapon"),
            _ => new WeaponImportInfo(PrettyKey(weaponKey ?? "Unknown Weapon"), "Aspect of Zagreus", "Unknown", 10, weaponKey ?? "UnknownWeapon")
        };

        if (string.IsNullOrWhiteSpace(aspectKey))
        {
            return baseInfo;
        }

        var aspectName = aspectKey switch
        {
            "SpearTeleportTrait" => "Aspect of Achilles",
            "FistVacuumTrait" => "Aspect of Talos",
            "SwordCriticalTrait" => "Aspect of Nemesis",
            "BowMarkHomingTrait" => "Aspect of Chiron",
            "ShieldLoadAmmoTrait" => "Aspect of Beowulf",
            "GunManualReloadTrait" => "Aspect of Hestia",
            _ => Localize(aspectKey, localizations) ?? PrettyKey(aspectKey)
        };

        return baseInfo with { AspectName = aspectName };
    }

    private static ResultImportInfo BuildResultInfo(string? resultKey) => resultKey switch
    {
        "RunHistoryScreen_Cleared" => new("Escaped", "Greece", "Hades"),
        "RunHistoryScreenResult_Tartarus" => new("Died", "Tartarus", null),
        "RunHistoryScreenResult_Asphodel" => new("Died", "Asphodel", null),
        "RunHistoryScreenResult_Elysium" => new("Died", "Elysium", null),
        "RunHistoryScreenResult_Styx" => new("Died", "Temple of Styx", null),
        "RunHistoryScreenResult_A_Boss01" => new("Died", "Tartarus", "Fury Sisters"),
        "RunHistoryScreenResult_A_MiniBoss01" => new("Died", "Tartarus", "Tartarus Mini-Boss"),
        "RunHistoryScreenResult_A_MiniBoss02" => new("Died", "Tartarus", "Tartarus Mini-Boss"),
        _ => new(resultKey == "RunHistoryScreen_Cleared" ? "Escaped" : "Died", PrettyKey(resultKey ?? "Unknown"), null)
    };

    private static TraitImportDisplay BuildTraitInfo(string traitKey, IReadOnlyDictionary<string, string> localizations)
    {
        var displayName = Localize(traitKey, localizations) ?? TraitDisplayNames.GetValueOrDefault(traitKey) ?? PrettyKey(traitKey);
        var god = InferGod(traitKey);
        var effectType = InferEffectType(traitKey);
        var isDuo = traitKey.Contains("Duo", StringComparison.OrdinalIgnoreCase);
        var isLegendary = traitKey.Contains("Legendary", StringComparison.OrdinalIgnoreCase);
        var isCore = effectType is "Attack" or "Special" or "Cast" or "Dash" or "Call";
        return new TraitImportDisplay(displayName, god, effectType, isDuo, isLegendary, isCore);
    }

    private static string? Localize(string? key, IReadOnlyDictionary<string, string> localizations)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return localizations.GetValueOrDefault(key) ?? ClearMessageNames.GetValueOrDefault(key);
    }

    private static string BuildNotes(JsonElement run, int runCount, string? clearMessage)
    {
        return "Imported from ExportRunHistory mod.";
    }

    private static int ParseDurationSeconds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var pieces = value.Split(':');
        if (pieces.Length == 2 &&
            int.TryParse(pieces[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) &&
            decimal.TryParse(pieces[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var seconds))
        {
            return (int)Math.Round(minutes * 60 + seconds, MidpointRounding.AwayFromZero);
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var totalSeconds)
            ? (int)Math.Round(totalSeconds, MidpointRounding.AwayFromZero)
            : 0;
    }

    private static string? GetString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed) ? parsed : null;
    }

    private static string InferGod(string traitKey)
    {
        foreach (var god in Gods)
        {
            if (traitKey.StartsWith(god, StringComparison.OrdinalIgnoreCase))
            {
                return god;
            }
        }

        if (traitKey.StartsWith("Chaos", StringComparison.OrdinalIgnoreCase))
        {
            return "Chaos";
        }

        if (traitKey.StartsWith("Spear", StringComparison.OrdinalIgnoreCase) ||
            traitKey.StartsWith("Sword", StringComparison.OrdinalIgnoreCase) ||
            traitKey.StartsWith("Bow", StringComparison.OrdinalIgnoreCase) ||
            traitKey.StartsWith("Shield", StringComparison.OrdinalIgnoreCase) ||
            traitKey.StartsWith("Fist", StringComparison.OrdinalIgnoreCase) ||
            traitKey.StartsWith("Gun", StringComparison.OrdinalIgnoreCase))
        {
            return "Daedalus";
        }

        if (traitKey.Contains("Keepsake", StringComparison.OrdinalIgnoreCase))
        {
            return "Keepsake";
        }

        if (traitKey.StartsWith("RoomReward", StringComparison.OrdinalIgnoreCase))
        {
            return "Reward";
        }

        if (traitKey.StartsWith("Temporary", StringComparison.OrdinalIgnoreCase))
        {
            return "Temporary";
        }

        return "Other";
    }

    private static string InferEffectType(string traitKey)
    {
        if (traitKey.EndsWith("WeaponTrait", StringComparison.OrdinalIgnoreCase))
        {
            return "Attack";
        }

        if (traitKey.EndsWith("SecondaryTrait", StringComparison.OrdinalIgnoreCase))
        {
            return "Special";
        }

        if (traitKey.EndsWith("RangedTrait", StringComparison.OrdinalIgnoreCase))
        {
            return "Cast";
        }

        if (traitKey.EndsWith("RushTrait", StringComparison.OrdinalIgnoreCase))
        {
            return "Dash";
        }

        if (traitKey.EndsWith("ShoutTrait", StringComparison.OrdinalIgnoreCase))
        {
            return "Call";
        }

        if (traitKey.Contains("Keepsake", StringComparison.OrdinalIgnoreCase))
        {
            return "Keepsake";
        }

        if (InferGod(traitKey) == "Daedalus")
        {
            return "Hammer";
        }

        return "Trait";
    }

    private static string PrettyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Unknown";
        }

        var withoutSuffix = key
            .Replace("RunHistoryScreenResult_", string.Empty, StringComparison.Ordinal)
            .Replace("RunHistoryScreen_", string.Empty, StringComparison.Ordinal)
            .Replace("Trait", string.Empty, StringComparison.Ordinal)
            .Replace("Weapon", string.Empty, StringComparison.Ordinal);

        return PascalCaseBoundaryRegex().Replace(withoutSuffix, " $1").Replace("_", " ").Trim();
    }

    private static string CleanLocalizedText(string value) =>
        StyleTokenRegex().Replace(value, string.Empty)
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Trim()
            .Trim('\'');

    private static readonly string[] Gods =
    [
        "Zeus", "Poseidon", "Athena", "Aphrodite", "Ares", "Artemis", "Dionysus", "Hermes", "Demeter"
    ];

    private static readonly Dictionary<string, string> ClearMessageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ClearNumOne"] = "First Clear",
        ["ClearTimeVeryFast"] = "Hermes Would Be Jealous",
        ["ClearTimeFast"] = "Fast Clear",
        ["ClearTimeSlow"] = "Slow and Steady"
    };

    private static readonly Dictionary<string, string> TraitDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AthenaWeaponTrait"] = "Divine Strike",
        ["AthenaSecondaryTrait"] = "Divine Flourish",
        ["AthenaRushTrait"] = "Divine Dash",
        ["AthenaRangedTrait"] = "Phalanx Shot",
        ["AthenaShoutTrait"] = "Athena's Aid",
        ["AthenaBackstabDebuffTrait"] = "Blinding Flash",
        ["AphroditeWeaponTrait"] = "Heartbreak Strike",
        ["AphroditeSecondaryTrait"] = "Heartbreak Flourish",
        ["AphroditeShoutTrait"] = "Aphrodite's Aid",
        ["AresWeaponTrait"] = "Curse of Agony",
        ["AresSecondaryTrait"] = "Curse of Pain",
        ["AresRangedTrait"] = "Slicing Shot",
        ["AresLongCurseTrait"] = "Dire Misfortune",
        ["ArtemisWeaponTrait"] = "Deadly Strike",
        ["ArtemisSecondaryTrait"] = "Deadly Flourish",
        ["ArtemisRushTrait"] = "Hunter Dash",
        ["ArtemisShoutTrait"] = "Artemis' Aid",
        ["DionysusWeaponTrait"] = "Drunken Strike",
        ["DionysusSecondaryTrait"] = "Drunken Flourish",
        ["DionysusRushTrait"] = "Drunken Dash",
        ["DionysusShoutTrait"] = "Dionysus' Aid",
        ["PoseidonWeaponTrait"] = "Tempest Strike",
        ["PoseidonSecondaryTrait"] = "Tempest Flourish",
        ["PoseidonRushTrait"] = "Tidal Dash",
        ["PoseidonShoutTrait"] = "Poseidon's Aid",
        ["PoseidonPickedUpMinorLootTrait"] = "Ocean's Bounty",
        ["ZeusWeaponTrait"] = "Lightning Strike",
        ["ZeusSecondaryTrait"] = "Thunder Flourish",
        ["ZeusRushTrait"] = "Thunder Dash",
        ["ZeusRangedTrait"] = "Electric Shot",
        ["ZeusShoutTrait"] = "Zeus' Aid",
        ["ZeusBonusBoltTrait"] = "Double Strike",
        ["ZeusBonusBounceTrait"] = "Storm Lightning",
        ["PerfectDashBoltTrait"] = "Lightning Reflexes",
        ["AmmoBoltTrait"] = "Lightning Rod",
        ["SpeedDamageTrait"] = "Rush Delivery",
        ["RushSpeedBoostTrait"] = "Hyper Sprint",
        ["RoomRewardMaxHealthTrait"] = "Centaur Heart",
        ["RoomRewardEmptyMaxHealthTrait"] = "Centaur Soul",
        ["RoomRewardBonusTrait"] = "Room Reward Bonus",
        ["MaxHealthKeepsakeTrait"] = "Old Spiked Collar",
        ["BonusMoneyTrait"] = "Chthonic Coin Purse",
        ["LastStandHealTrait"] = "Deathless Stand",
        ["HealthRewardBonusTrait"] = "Life Affirmation",
        ["GiftHealthTrait"] = "Premium Vintage",
        ["PreloadSuperGenerationTrait"] = "Proud Bearing",
        ["CriticalSuperGenerationTrait"] = "Hunter Instinct",
        ["LowHealthDefenseTrait"] = "Positive Outlook",
        ["SpearSpinChargeLevelTime"] = "Quick Spin",
        ["SpearSpinDamageRadius"] = "Massive Spin",
        ["SpearSpinChargeAreaDamageTrait"] = "Flaring Spin",
        ["SpearReachAttack"] = "Extended Jab",
        ["SpearAutoAttack"] = "Flurry Jab",
        ["SpearThrowBounce"] = "Chain Skewer",
        ["SpearThrowExplode"] = "Exploding Launcher",
        ["SpearThrowElectiveCharge"] = "Charged Skewer",
        ["SpearTeleportTrait"] = "Aspect of Achilles",
        ["SwordCriticalTrait"] = "Aspect of Nemesis",
        ["SwordBackstabTrait"] = "Shadow Slash",
        ["SwordHealthBufferDamageTrait"] = "Cursed Slash",
        ["BowSlowChargeDamageTrait"] = "Sniper Shot",
        ["FistVacuumTrait"] = "Aspect of Talos",
        ["FistChargeSpecialTrait"] = "Flying Cutter",
        ["FistKillTrait"] = "Draining Cutter",
        ["FistDashAttackHealthBufferTrait"] = "Breaching Cross",
        ["FistSpecialLandTrait"] = "Kinetic Launcher",
        ["FistDoubleDashSpecialTrait"] = "Flying Cutter",
        ["ChaosBlessingBoonRarityTrait"] = "Favor",
        ["ChaosBlessingDashAttackTrait"] = "Lunge",
        ["ChaosBlessingAmmoTrait"] = "Shot",
        ["ChaosBlessingMoneyTrait"] = "Affluence",
        ["ChaosBlessingMeleeTrait"] = "Strike",
        ["TemporaryMoveSpeedTrait"] = "Temporary Move Speed",
        ["TemporaryImprovedTrapDamageTrait"] = "Temporary Trap Damage",
        ["TemporaryWeaponLifeOnKillTrait"] = "Temporary Life On Kill",
        ["TemporaryBoonRarityTrait"] = "Temporary Boon Rarity",
        ["TemporaryMoreAmmoTrait"] = "Temporary Ammo",
        ["TemporaryPreloadSuperGenerationTrait"] = "Temporary God Gauge",
        ["TemporaryImprovedRangedTrait"] = "Temporary Cast Damage",
        ["TemporaryImprovedSecondaryTrait"] = "Temporary Special Damage",
        ["TemporaryImprovedWeaponTrait"] = "Temporary Attack Damage",
        ["TemporaryDoorHealTrait"] = "Temporary Door Heal",
        ["TemporaryAlphaStrikeTrait"] = "Temporary First Strike",
        ["OnEnemyDeathDamageInstanceBuffTrait"] = "Battle Rage",
        ["EncounterStartOffenseBuffTrait"] = "Hydraulic Might",
        ["IncreasedDamageTrait"] = "Urge to Kill",
        ["EnemyDamageTrait"] = "Different League",
        ["RetaliateWeaponTrait"] = "Holy Shield",
        ["RapidCastTrait"] = "Flurry Cast",
        ["GodModeTrait"] = "God Mode"
    };

    [GeneratedRegex("^\\s*Id\\s*=\\s*\"(?<id>[^\"]+)\"(?<body>[\\s\\S]*?)(?=^\\s*\\}\\s*$)", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex LocalizationEntryRegex();

    [GeneratedRegex("DisplayName\\s*=\\s*\"(?<name>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled)]
    private static partial Regex LocalizationDisplayNameRegex();

    [GeneratedRegex("InheritFrom\\s*=\\s*\"(?<parent>[^\"]+)\"", RegexOptions.Compiled)]
    private static partial Regex LocalizationInheritFromRegex();

    [GeneratedRegex("\\{#[^}]+\\}", RegexOptions.Compiled)]
    private static partial Regex StyleTokenRegex();

    [GeneratedRegex("(?<!^)([A-Z])", RegexOptions.Compiled)]
    private static partial Regex PascalCaseBoundaryRegex();

    private sealed record WeaponImportInfo(string Name, string AspectName, string WeaponType, decimal BaseDamage, string InternalWeaponKey);

    private sealed record TraitImportInfo(string Name, int Level);

    private sealed record TraitImportDisplay(string DisplayName, string God, string EffectType, bool IsDuo, bool IsLegendary, bool IsCore);

    private sealed record ResultImportInfo(string Result, string FinalBiome, string? DefeatedBoss);
}
