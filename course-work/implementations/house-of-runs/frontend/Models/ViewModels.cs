using Microsoft.AspNetCore.Mvc.Rendering;

namespace HouseOfRuns.Frontend.Models;

public sealed class ListPageViewModel<T>
{
    public PagedResponse<T> Page { get; set; } = new([], 1, 10, 0, 0);

    public string? Q { get; set; }

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }

    public Dictionary<string, string?> Filters { get; set; } = [];
}

public sealed class RunFormViewModel
{
    public Guid? Id { get; set; }

    public int? ImportIndex { get; set; }

    public RunRequest Run { get; set; } = new();

    public List<RunBoonRequest> BoonRows { get; set; } = [];

    public IReadOnlyList<SelectListItem> Weapons { get; set; } = [];

    public IReadOnlyList<RunWeaponOption> WeaponOptions { get; set; } = [];

    public IReadOnlyList<RunBoonOption> BoonOptions { get; set; } = [];

    public string FormTitle => Id.HasValue ? "Edit Run" : "Create Run";
}

public sealed record RunWeaponOption(
    Guid Id,
    string Name,
    string AspectName,
    string WeaponType,
    string DisplayName,
    string? IconPath);

public sealed record RunBoonOption(
    Guid Id,
    string Name,
    string God,
    string EffectType,
    string DisplayName,
    string? IconPath,
    string WeaponType,
    bool HasIcon);

public sealed class RunBoonFormViewModel
{
    public Guid? Id { get; set; }

    public RunBoonRequest RunBoon { get; set; } = new();

    public IReadOnlyList<SelectListItem> Runs { get; set; } = [];

    public IReadOnlyList<SelectListItem> Boons { get; set; } = [];

    public string FormTitle => Id.HasValue ? "Edit Run Boon" : "Create Run Boon";
}
