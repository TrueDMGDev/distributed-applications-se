namespace HouseOfRuns.Frontend;

public static class BoonIconLookup
{
    private const string BoonIconFolder = "/images/boons/";
    private const string UpgradeIconFolder = "/images/upgrades/";
    private const string KeepsakeIconFolder = "/images/keepsakes/";
    private const string ItemIconFolder = "/images/items/";
    private const string GodSymbolIconFolder = "/images/god-symbols/";
    private const string WeaponIconFolder = "/images/weapons/";

    public static string? GetIconPath(string? boonName, string? god = null, string? slotType = null)
    {
        var slug = Slugify(boonName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var mappedSlug = IconAliases.GetValueOrDefault(slug, slug);

        if (IsUpgrade(god, slotType))
        {
            return $"{UpgradeIconFolder}{mappedSlug}.png";
        }

        if (IsKeepsake(god, slotType))
        {
            return $"{KeepsakeIconFolder}{mappedSlug}.png";
        }

        if (IsItem(mappedSlug, god, slotType))
        {
            return $"{ItemIconFolder}{mappedSlug}.png";
        }

        return $"{BoonIconFolder}{mappedSlug}-i.png";
    }

    public static string? GetGodSymbolPath(string? god)
    {
        var slug = Slugify(god);
        if (string.Equals(slug, "daedalus", StringComparison.OrdinalIgnoreCase))
        {
            return $"{ItemIconFolder}daedalus-hammer.png";
        }

        return KnownGodSymbols.Contains(slug) ? $"{GodSymbolIconFolder}{slug}-symbol.png" : null;
    }

    public static string? GetWeaponIconPath(string? weaponName, string? aspectName = null)
    {
        var slug = Slugify(weaponName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var mappedSlug = WeaponIconAliases.GetValueOrDefault(slug, slug);
        return $"{WeaponIconFolder}{mappedSlug}.png";
    }

    public static string? GetAspectIconPath(string? aspectName)
    {
        var slug = Slugify(aspectName);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        return AspectIconAliases.TryGetValue(slug, out var iconSlug)
            ? $"{UpgradeIconFolder}{iconSlug}.png"
            : null;
    }

    public static string Initial(string? god, string? boonName)
    {
        var source = !string.IsNullOrWhiteSpace(god) && god != "Other" ? god : boonName;
        return string.IsNullOrWhiteSpace(source) ? "?" : source.Trim()[..1].ToUpperInvariant();
    }

    public static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = new List<char>(value.Length);
        var pendingHyphen = false;

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingHyphen && result.Count > 0)
                {
                    result.Add('-');
                }

                result.Add(char.ToLowerInvariant(character));
                pendingHyphen = false;
                continue;
            }

            pendingHyphen = result.Count > 0;
        }

        return new string(result.ToArray());
    }

    private static bool IsUpgrade(string? god, string? slotType) =>
        string.Equals(god, "Daedalus", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(slotType, "Hammer", StringComparison.OrdinalIgnoreCase);

    private static bool IsKeepsake(string? god, string? slotType) =>
        string.Equals(god, "Keepsake", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(slotType, "Keepsake", StringComparison.OrdinalIgnoreCase);

    private static bool IsItem(string slug, string? god, string? slotType) =>
        KnownItemIcons.Contains(slug) &&
        (string.Equals(god, "Reward", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(god, "Item", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(god, "Temporary", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(god, "Other", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(slotType, "Item", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(slotType, "Reward", StringComparison.OrdinalIgnoreCase));

    private static readonly Dictionary<string, string> IconAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chthonic-coin-purse"] = "chthonic-coin-purse",
        ["coin-purse"] = "chthonic-coin-purse",
        ["flaring-spin"] = "spear-flaring-spin",
        ["charged-skewer"] = "spear-charged-skewer",
        ["aspect-of-achilles"] = "achilles-aspect",
        ["extending-jab"] = "extended-jab",
        ["chain-skewer"] = "multi-skewer",
        ["room-reward-bonus"] = "wrapped-boon",
        ["temporary-move-speed"] = "ignited-ichor",
        ["temporary-trap-damage"] = "stygian-shard",
        ["temporary-life-on-kill"] = "eye-of-lamia",
        ["temporary-boon-rarity"] = "yarn-of-ariadne",
        ["temporary-ammo"] = "prometheus-stone",
        ["temporary-god-gauge"] = "aether-net",
        ["temporary-cast-damage"] = "braid-of-atlas",
        ["temporary-special-damage"] = "chimaera-jerky",
        ["temporary-attack-damage"] = "cyclops-jerky",
        ["temporary-door-heal"] = "hydralite",
        ["temporary-first-strike"] = "eris-bangle"
    };

    private static readonly Dictionary<string, string> WeaponIconAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["stygius"] = "stygian-blade",
        ["sword"] = "stygian-blade",
        ["swordweapon"] = "stygian-blade",
        ["stygian-blade"] = "stygian-blade",
        ["varatha"] = "eternal-spear",
        ["spear"] = "eternal-spear",
        ["spearweapon"] = "eternal-spear",
        ["eternal-spear"] = "eternal-spear",
        ["aegis"] = "shield-of-chaos",
        ["shield"] = "shield-of-chaos",
        ["shieldweapon"] = "shield-of-chaos",
        ["shield-of-chaos"] = "shield-of-chaos",
        ["coronacht"] = "heart-seeker-bow",
        ["bow"] = "heart-seeker-bow",
        ["bowweapon"] = "heart-seeker-bow",
        ["heart-seeker-bow"] = "heart-seeker-bow",
        ["malphon"] = "twin-fists",
        ["fists"] = "twin-fists",
        ["fistweapon"] = "twin-fists",
        ["twin-fists"] = "twin-fists",
        ["exagryph"] = "adamant-rail",
        ["rail"] = "adamant-rail",
        ["gunweapon"] = "adamant-rail",
        ["adamant-rail"] = "adamant-rail"
    };

    private static readonly Dictionary<string, string> AspectIconAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["aspect-of-achilles"] = "achilles-aspect"
    };

    private static readonly HashSet<string> KnownGodSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        "aphrodite", "ares", "artemis", "athena", "chaos", "demeter", "dionysus", "hermes", "poseidon", "zeus"
    };

    private static readonly HashSet<string> KnownItemIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        "aether-net", "ambrosia-delight", "ambrosia-small", "ambrosia", "ammo", "anvil-of-fates",
        "bedroom-decor", "braid-of-atlas", "centaur-heart", "centaur-soul", "charon-s-obol",
        "chimaera-jerky", "chthonic-coin-purse", "chthonic-key-small", "chthonic-key", "codex-locked", "cyclops-jerky",
        "daedalus-hammer", "darkness-small", "darkness", "diamond-small", "diamond", "eris-bangle",
        "eye-of-lamia", "fateful-twist", "flame-wheels-release", "gaea-s-treasure", "gemstone-small",
        "gemstone", "healthitem02", "healthrestore", "healthup", "heart", "heat", "hydralite",
        "ignited-ichor", "kiss-of-styx", "life-essence", "light-of-ixion", "loyalty-card",
        "nail-of-talos", "nectar", "nemesis-crest", "night-spindle", "obol", "pom-of-power",
        "pom-porridge", "pom-slice", "price-of-midas", "prometheus-stone", "red-onion",
        "refreshing-nectar", "shieldhealth", "skeletal-lure", "skeleton-key-new", "skeleton-key",
        "status-curse", "stygian-shard", "tinge-of-erebus", "titan-blood-small", "titan-blood",
        "trove-tracker", "wrapped-boon", "yarn-of-ariadne"
    };
}
