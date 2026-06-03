using System.Text.Json;
using System.Security.Claims;
using HouseOfRuns.Frontend.Models;
using HouseOfRuns.Frontend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HouseOfRuns.Frontend.Controllers;

[Authorize]
public sealed class RunsController(HouseApiClient api, IWebHostEnvironment environment) : Controller
{
    private const string ImportDraftsSessionKey = "ImportDrafts";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IActionResult> Index(string? q, string? result, string? finalBiome, int? minHeat, int? maxHeat, int page = 1, string sortBy = "playedAt", string sortDir = "desc")
    {
        var model = await BuildPublicRunListAsync(q, result, finalBiome, minHeat, maxHeat, page, sortBy, sortDir);
        ViewBag.Title = "Public Runs";
        ViewBag.MineOnly = false;
        ViewBag.CanManageRuns = false;
        ViewBag.IsPublicHome = true;
        ViewBag.CurrentUserId = CurrentUserId();
        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View(model);
    }

    public async Task<IActionResult> Mine(string? q, string? result, string? finalBiome, int? minHeat, int? maxHeat, int page = 1, string sortBy = "playedAt", string sortDir = "desc")
    {
        var model = await BuildRunListAsync(true, q, result, finalBiome, minHeat, maxHeat, page, sortBy, sortDir);
        ViewBag.Title = "My Runs";
        ViewBag.MineOnly = true;
        ViewBag.CanManageRuns = true;
        ViewBag.IsPublicHome = false;
        ViewBag.CurrentUserId = CurrentUserId();
        ViewBag.IsAdmin = User.IsInRole("Admin");
        return View("Index", model);
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var run = await api.GetAsync<RunResponse>($"/api/runs/{id}");
        return View(run);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = await BuildRunFormAsync(new RunFormViewModel
        {
            Run = new RunRequest { PlayedAt = DateTime.Now, Source = "Manual", IsPublic = false },
            BoonRows = EmptyBoonRows()
        });

        return View("Form", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RunFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildRunFormAsync(model));
        }

        model.Run.Source = model.ImportIndex.HasValue ? "Imported" : "Manual";
        model.Run.Boons = CleanBoonRows(model.BoonRows);
        await api.PostAsync<RunRequest, RunResponse>("/api/runs", model.Run);
        if (model.ImportIndex.HasValue)
        {
            RemoveImportDraft(model.ImportIndex.Value);
        }

        TempData["Message"] = "Run saved.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var run = await api.GetAsync<RunResponse>($"/api/runs/{id}");
        var model = new RunFormViewModel
        {
            Id = id,
            Run = new RunRequest
            {
                WeaponId = run.WeaponId,
                Title = run.Title,
                HeatLevel = run.HeatLevel,
                DurationSeconds = run.DurationSeconds,
                Result = run.Result,
                FinalBiome = run.FinalBiome,
                DefeatedBoss = run.DefeatedBoss,
                PlayedAt = run.PlayedAt.ToLocalTime(),
                IsPublic = run.IsPublic,
                Source = run.Source,
                Notes = run.Notes,
                ScreenshotUrl = run.ScreenshotUrl
            },
            BoonRows = NormalizeBoonRows(run.Boons.Select(boon => new RunBoonRequest
            {
                RunId = run.Id,
                BoonId = boon.BoonId,
                SlotType = boon.SlotType,
                LevelUsed = boon.LevelUsed,
                IsCoreBoon = boon.IsCoreBoon,
                PomLevel = boon.PomLevel,
                Notes = boon.Notes
            }).ToList())
        };

        return View("Form", await BuildRunFormAsync(model));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RunFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildRunFormAsync(model));
        }

        model.Run.Source = NormalizeSource(model.Run.Source);
        model.Run.Boons = CleanBoonRows(model.BoonRows);
        await api.PutAsync<RunRequest, RunResponse>($"/api/runs/{id}", model.Run);
        TempData["Message"] = "Run updated.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await api.DeleteAsync($"/api/runs/{id}");
        TempData["Message"] = "Run deleted.";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Like(Guid id)
    {
        await api.PostAsync<object, RunSocialSummaryResponse>($"/api/runs/{id}/likes", new { });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlike(Guid id)
    {
        await api.DeleteAsync($"/api/runs/{id}/likes");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Comment(Guid id, RunCommentRequest request)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(request.Body))
        {
            TempData["Error"] = "Write a comment before posting.";
            return RedirectToAction(nameof(Index));
        }

        await api.PostAsync<RunCommentRequest, RunCommentResponse>($"/api/runs/{id}/comments", request);
        TempData["Message"] = "Comment posted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId)
    {
        await api.DeleteAsync($"/api/runs/{id}/comments/{commentId}");
        TempData["Message"] = "Comment deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Import() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file, int? runIndex)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Choose a JSON file first.");
            return View();
        }

        var allDrafts = (await api.ImportRunDraftsAsync(file)).Runs.ToList();
        var drafts = runIndex.HasValue
            ? allDrafts.Take(Math.Max(1, runIndex.Value)).ToList()
            : allDrafts;

        SaveImportDrafts(drafts);
        TempData["Message"] = $"{drafts.Count} import draft(s) generated from JSON.";
        return View("ImportDrafts", drafts);
    }

    [HttpGet]
    public IActionResult ImportDrafts() => View(LoadImportDrafts());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveImported(int importIndex, string? notes)
    {
        var draft = FindImportDraft(importIndex);
        if (draft is null)
        {
            TempData["Error"] = "That import draft is no longer available.";
            return RedirectToAction(nameof(ImportDrafts));
        }

        draft = draft with { Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim() };
        await api.PostAsync<RunRequest, RunResponse>("/api/runs", ToRunRequest(draft));
        RemoveImportDraft(importIndex);
        TempData["Message"] = $"Saved imported run #{importIndex}.";
        return RedirectToAction(nameof(ImportDrafts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAllImported()
    {
        var drafts = LoadImportDrafts();
        foreach (var draft in drafts)
        {
            var notesKey = $"notes_{draft.ImportIndex}";
            var notes = Request.Form.TryGetValue(notesKey, out var values) ? values.ToString() : draft.Notes;
            var draftToSave = draft with { Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim() };
            await api.PostAsync<RunRequest, RunResponse>("/api/runs", ToRunRequest(draftToSave));
        }

        SaveImportDrafts([]);
        TempData["Message"] = $"Saved {drafts.Count} imported run(s).";
        return RedirectToAction(nameof(Mine));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteImported(int importIndex)
    {
        RemoveImportDraft(importIndex);
        TempData["Message"] = $"Removed import draft #{importIndex}.";
        return RedirectToAction(nameof(ImportDrafts));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditImported(int importIndex)
    {
        var draft = FindImportDraft(importIndex);
        if (draft is null)
        {
            TempData["Error"] = "That import draft is no longer available.";
            return RedirectToAction(nameof(ImportDrafts));
        }

        TempData["Message"] = "Editing imported draft. Saving this form will create a new run.";
        return View("Form", await BuildRunFormAsync(BuildRunFormFromDraft(draft)));
    }

    private async Task<ListPageViewModel<RunResponse>> BuildRunListAsync(bool mineOnly, string? q, string? result, string? finalBiome, int? minHeat, int? maxHeat, int page, string sortBy, string sortDir)
    {
        var pageData = await api.GetAsync<PagedResponse<RunResponse>>($"/api/runs?{Query(new()
        {
            ["q"] = q,
            ["result"] = result,
            ["finalBiome"] = finalBiome,
            ["minHeat"] = minHeat?.ToString(),
            ["maxHeat"] = maxHeat?.ToString(),
            ["mineOnly"] = mineOnly.ToString().ToLowerInvariant(),
            ["page"] = page.ToString(),
            ["sortBy"] = sortBy,
            ["sortDir"] = sortDir
        })}");

        return new ListPageViewModel<RunResponse>
        {
            Page = pageData,
            Q = q,
            SortBy = sortBy,
            SortDir = sortDir,
            Filters = new()
            {
                ["result"] = result,
                ["finalBiome"] = finalBiome,
                ["minHeat"] = minHeat?.ToString(),
                ["maxHeat"] = maxHeat?.ToString()
            }
        };
    }

    private async Task<ListPageViewModel<RunResponse>> BuildPublicRunListAsync(string? q, string? result, string? finalBiome, int? minHeat, int? maxHeat, int page, string sortBy, string sortDir)
    {
        var pageData = await api.GetAsync<PagedResponse<RunResponse>>($"/api/runs/public?{Query(new()
        {
            ["q"] = q,
            ["result"] = result,
            ["finalBiome"] = finalBiome,
            ["minHeat"] = minHeat?.ToString(),
            ["maxHeat"] = maxHeat?.ToString(),
            ["page"] = page.ToString(),
            ["pageSize"] = "9",
            ["sortBy"] = sortBy,
            ["sortDir"] = sortDir
        })}");

        return new ListPageViewModel<RunResponse>
        {
            Page = pageData,
            Q = q,
            SortBy = sortBy,
            SortDir = sortDir,
            Filters = new()
            {
                ["result"] = result,
                ["finalBiome"] = finalBiome,
                ["minHeat"] = minHeat?.ToString(),
                ["maxHeat"] = maxHeat?.ToString()
            }
        };
    }

    private async Task<RunFormViewModel> BuildRunFormAsync(RunFormViewModel model)
    {
        model.Run.Source = NormalizeSource(model.Run.Source);
        var weapons = await api.GetAsync<PagedResponse<WeaponResponse>>("/api/weapons?pageSize=500&sortBy=name");
        var boons = await api.GetAsync<PagedResponse<BoonResponse>>("/api/boons?pageSize=500&sortBy=name");

        if (model.Run.WeaponId == Guid.Empty && weapons.Items.Count > 0)
        {
            model.Run.WeaponId = weapons.Items[0].Id;
        }

        model.Weapons = weapons.Items.Select(weapon => new SelectListItem($"{weapon.Name} / {weapon.AspectName}", weapon.Id.ToString())).ToList();
        model.WeaponOptions = weapons.Items.Select(ToWeaponOption).ToList();
        model.BoonOptions = boons.Items
            .Select(ToBoonOption)
            .OrderBy(option => option.God == "Daedalus" ? 1 : option.God == "Reward" || option.God == "Item" ? 2 : 0)
            .ThenBy(option => option.God)
            .ThenBy(option => option.Name)
            .ToList();
        model.BoonRows = NormalizeBoonRows(model.BoonRows);
        return model;
    }

    private static List<RunBoonRequest> EmptyBoonRows() => [new RunBoonRequest()];

    private static List<RunBoonRequest> NormalizeBoonRows(List<RunBoonRequest> rows)
    {
        var normalized = rows.Take(30).Select(row =>
        {
            row.RunId = Guid.Empty;
            return row;
        }).ToList();

        if (normalized.Count == 0)
        {
            normalized.Add(new RunBoonRequest());
        }

        return normalized;
    }

    private static List<RunBoonRequest> CleanBoonRows(IEnumerable<RunBoonRequest> rows) =>
        rows.Where(row => row.BoonId != Guid.Empty).Select(row =>
        {
            row.RunId = Guid.Empty;
            return row;
        }).ToList();

    private Guid? CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private RunFormViewModel BuildRunFormFromDraft(ImportRunDraftResponse draft) => new()
    {
        ImportIndex = draft.ImportIndex,
        Run = ToRunRequest(draft),
        BoonRows = NormalizeBoonRows(ToRunRequest(draft).Boons)
    };

    private static RunRequest ToRunRequest(ImportRunDraftResponse draft) => new()
    {
        WeaponId = draft.WeaponId ?? Guid.Empty,
        Title = draft.Title,
        HeatLevel = draft.HeatLevel,
        DurationSeconds = draft.DurationSeconds,
        Result = draft.Result,
        FinalBiome = draft.FinalBiome,
        DefeatedBoss = draft.DefeatedBoss,
        PlayedAt = draft.PlayedAt.ToLocalTime(),
        IsPublic = false,
        Source = draft.Source,
        Notes = draft.Notes,
        Boons = draft.Boons
            .Where(boon => boon.BoonId.HasValue)
            .Select(boon => new RunBoonRequest
            {
                BoonId = boon.BoonId!.Value,
                SlotType = boon.SlotType,
                LevelUsed = boon.LevelUsed,
                IsCoreBoon = boon.IsCoreBoon
            })
            .ToList()
    };

    private RunWeaponOption ToWeaponOption(WeaponResponse weapon)
    {
        var iconPath = BoonIconLookup.GetWeaponIconPath(weapon.Name, weapon.AspectName);
        return new RunWeaponOption(
            weapon.Id,
            weapon.Name,
            weapon.AspectName,
            weapon.WeaponType,
            $"{weapon.Name} / {weapon.AspectName}",
            WebPathExists(iconPath) ? iconPath : null);
    }

    private RunBoonOption ToBoonOption(BoonResponse boon)
    {
        var iconPath = BoonIconLookup.GetIconPath(boon.Name, boon.God, boon.EffectType);
        return new RunBoonOption(
            boon.Id,
            boon.Name,
            boon.God,
            boon.EffectType,
            $"{boon.Name} ({boon.God})",
            WebPathExists(iconPath) ? iconPath : null,
            WeaponFamilyForBoon(boon),
            WebPathExists(iconPath));
    }

    private bool WebPathExists(string? webPath)
    {
        if (string.IsNullOrWhiteSpace(webPath) || string.IsNullOrWhiteSpace(environment.WebRootPath))
        {
            return false;
        }

        var relativePath = webPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return System.IO.File.Exists(Path.Combine(environment.WebRootPath, relativePath));
    }

    private static string WeaponFamilyForBoon(BoonResponse boon)
    {
        if (!string.Equals(boon.God, "Daedalus", StringComparison.OrdinalIgnoreCase))
        {
            return "All";
        }

        var slug = BoonIconLookup.Slugify(boon.Name);
        if (slug.StartsWith("sword-", StringComparison.OrdinalIgnoreCase))
        {
            return "Sword";
        }

        if (slug.StartsWith("spear-", StringComparison.OrdinalIgnoreCase))
        {
            return "Spear";
        }

        if (slug.StartsWith("bow-", StringComparison.OrdinalIgnoreCase))
        {
            return "Bow";
        }

        return HammerWeaponFamilies.GetValueOrDefault(slug, "All");
    }

    private static string NormalizeSource(string? source) =>
        source != null &&
        (source.Contains("import", StringComparison.OrdinalIgnoreCase) ||
         source.Contains("export", StringComparison.OrdinalIgnoreCase))
            ? "Imported"
            : "Manual";

    private ImportRunDraftResponse? FindImportDraft(int importIndex) =>
        LoadImportDrafts().FirstOrDefault(draft => draft.ImportIndex == importIndex);

    private void RemoveImportDraft(int importIndex)
    {
        var drafts = LoadImportDrafts();
        SaveImportDrafts(drafts.Where(draft => draft.ImportIndex != importIndex).ToList());
    }

    private List<ImportRunDraftResponse> LoadImportDrafts()
    {
        var json = HttpContext.Session.GetString(ImportDraftsSessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ImportRunDraftResponse>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void SaveImportDrafts(IReadOnlyList<ImportRunDraftResponse> drafts) =>
        HttpContext.Session.SetString(ImportDraftsSessionKey, JsonSerializer.Serialize(drafts, JsonOptions));

    private static readonly Dictionary<string, string> HammerWeaponFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["armor-slayer"] = "Sword",
        ["cruel-thrust"] = "Sword",
        ["cursed-slash"] = "Sword",
        ["double-edge"] = "Sword",
        ["empowering-nova"] = "Sword",
        ["flurry-blade"] = "Sword",
        ["piercing-wave"] = "Sword",
        ["shadow-slash"] = "Sword",
        ["super-nova"] = "Sword",
        ["world-splitter"] = "Sword",
        ["charged-skewer"] = "Spear",
        ["armor-skewer"] = "Spear",
        ["chain-skewer"] = "Spear",
        ["exploding-launcher"] = "Spear",
        ["extended-jab"] = "Spear",
        ["extending-jab"] = "Spear",
        ["flaring-spin"] = "Spear",
        ["flurry-jab"] = "Spear",
        ["javelin-throw"] = "Spear",
        ["massive-spin"] = "Spear",
        ["multi-skewer"] = "Spear",
        ["quick-spin"] = "Spear",
        ["vicious-skewer"] = "Spear",
        ["perfect-shot"] = "Bow",
        ["flurry-shot"] = "Bow",
        ["point-blank-shot"] = "Bow",
        ["relentless-barrage"] = "Bow",
        ["sniper-shot"] = "Bow",
        ["triple-shot"] = "Bow",
        ["twin-shot"] = "Bow",
        ["charged-flight"] = "Shield",
        ["charged-shot"] = "Shield",
        ["dashing-flight"] = "Shield",
        ["dashing-wallop"] = "Shield",
        ["dread-flight"] = "Shield",
        ["empowering-flight"] = "Shield",
        ["explosive-return"] = "Shield",
        ["ferocious-guard"] = "Shield",
        ["minotaur-rush"] = "Shield",
        ["pulverizing-blow"] = "Shield",
        ["sudden-rush"] = "Shield",
        ["unyielding-defense"] = "Shield",
        ["breaching-cross"] = "Fists",
        ["colossus-knuckle"] = "Fists",
        ["concentrated-knuckle"] = "Fists",
        ["draining-cutter"] = "Fists",
        ["explosive-upper"] = "Fists",
        ["flying-cutter"] = "Fists",
        ["heavy-knuckle"] = "Fists",
        ["kinetic-launcher"] = "Fists",
        ["long-knuckle"] = "Fists",
        ["quake-cutter"] = "Fists",
        ["rending-claws"] = "Fists",
        ["rolling-knuckle"] = "Fists",
        ["rush-kick"] = "Fists",
        ["armor-shredder"] = "Rail",
        ["cluster-bomb"] = "Rail",
        ["concentrated-beam"] = "Rail",
        ["concentrated-fire"] = "Rail",
        ["cooling-chamber"] = "Rail",
        ["eternal-chamber"] = "Rail",
        ["explosive-fire"] = "Rail",
        ["flash-fire"] = "Rail",
        ["flurry-fire"] = "Rail",
        ["greater-inferno"] = "Rail",
        ["hazard-bomb"] = "Rail",
        ["heavy-slug"] = "Rail",
        ["inescapable-blast"] = "Rail",
        ["infinity-chamber"] = "Rail",
        ["invigorating-blast"] = "Rail",
        ["piercing-fire"] = "Rail",
        ["ricochet-fire"] = "Rail",
        ["rocket-bomb"] = "Rail",
        ["seeking-fire"] = "Rail",
        ["spread-fire"] = "Rail",
        ["targeting-system"] = "Rail",
        ["triple-beam"] = "Rail",
        ["triple-bomb"] = "Rail"
    };

    private static string Query(Dictionary<string, string?> values) =>
        string.Join("&", values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
}
