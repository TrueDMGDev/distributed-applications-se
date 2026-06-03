using HouseOfRuns.Frontend.Models;
using HouseOfRuns.Frontend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace HouseOfRuns.Frontend.Controllers;

[Authorize(Roles = "Admin")]
public sealed class RunBoonsController(HouseApiClient api) : Controller
{
    public async Task<IActionResult> Index(Guid? runId, Guid? boonId, string? slotType, bool? isCore, int page = 1, string sortBy = "addedAt", string sortDir = "desc")
    {
        var pageData = await api.GetAsync<PagedResponse<RunBoonResponse>>($"/api/run-boons?{Query(new()
        {
            ["runId"] = runId?.ToString(),
            ["boonId"] = boonId?.ToString(),
            ["slotType"] = slotType,
            ["isCore"] = isCore?.ToString().ToLowerInvariant(),
            ["page"] = page.ToString(),
            ["sortBy"] = sortBy,
            ["sortDir"] = sortDir
        })}");

        return View(new ListPageViewModel<RunBoonResponse>
        {
            Page = pageData,
            SortBy = sortBy,
            SortDir = sortDir,
            Filters = new()
            {
                ["runId"] = runId?.ToString(),
                ["boonId"] = boonId?.ToString(),
                ["slotType"] = slotType,
                ["isCore"] = isCore?.ToString()
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create() => View("Form", await BuildFormAsync(new RunBoonFormViewModel()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RunBoonFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model));
        }

        await api.PostAsync<RunBoonRequest, RunBoonResponse>("/api/run-boons", model.RunBoon);
        TempData["Message"] = "Run boon created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var runBoon = await api.GetAsync<RunBoonResponse>($"/api/run-boons/{id}");
        return View("Form", await BuildFormAsync(new RunBoonFormViewModel
        {
            Id = id,
            RunBoon = new RunBoonRequest
            {
                RunId = runBoon.RunId,
                BoonId = runBoon.BoonId,
                SlotType = runBoon.SlotType,
                LevelUsed = runBoon.LevelUsed,
                IsCoreBoon = runBoon.IsCoreBoon,
                PomLevel = runBoon.PomLevel,
                Notes = runBoon.Notes
            }
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, RunBoonFormViewModel model)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", await BuildFormAsync(model));
        }

        await api.PutAsync<RunBoonRequest, RunBoonResponse>($"/api/run-boons/{id}", model.RunBoon);
        TempData["Message"] = "Run boon updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await api.DeleteAsync($"/api/run-boons/{id}");
        TempData["Message"] = "Run boon deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<RunBoonFormViewModel> BuildFormAsync(RunBoonFormViewModel model)
    {
        var runs = await api.GetAsync<PagedResponse<RunResponse>>("/api/runs?mineOnly=true&pageSize=500&sortBy=playedAt&sortDir=desc");
        var boons = await api.GetAsync<PagedResponse<BoonResponse>>("/api/boons?pageSize=500&sortBy=name");

        if (model.RunBoon.RunId == Guid.Empty && runs.Items.Count > 0)
        {
            model.RunBoon.RunId = runs.Items[0].Id;
        }

        if (model.RunBoon.BoonId == Guid.Empty && boons.Items.Count > 0)
        {
            model.RunBoon.BoonId = boons.Items[0].Id;
        }

        model.Runs = runs.Items.Select(run => new SelectListItem(run.Title, run.Id.ToString())).ToList();
        model.Boons = boons.Items.Select(boon => new SelectListItem($"{boon.Name} ({boon.God})", boon.Id.ToString())).ToList();
        return model;
    }

    private static string Query(Dictionary<string, string?> values) =>
        string.Join("&", values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
}
