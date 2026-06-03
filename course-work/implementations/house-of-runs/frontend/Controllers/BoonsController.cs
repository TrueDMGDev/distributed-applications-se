using HouseOfRuns.Frontend.Models;
using HouseOfRuns.Frontend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseOfRuns.Frontend.Controllers;

[Authorize(Roles = "Admin")]
public sealed class BoonsController(HouseApiClient api) : Controller
{
    public async Task<IActionResult> Index(string? q, string? god, string? effectType, bool? isDuo, int page = 1, string sortBy = "name", string sortDir = "asc")
    {
        var pageData = await api.GetAsync<PagedResponse<BoonResponse>>($"/api/boons?{Query(new()
        {
            ["q"] = q,
            ["god"] = god,
            ["effectType"] = effectType,
            ["isDuo"] = isDuo?.ToString().ToLowerInvariant(),
            ["page"] = page.ToString(),
            ["sortBy"] = sortBy,
            ["sortDir"] = sortDir
        })}");

        return View(new ListPageViewModel<BoonResponse>
        {
            Page = pageData,
            Q = q,
            SortBy = sortBy,
            SortDir = sortDir,
            Filters = new() { ["god"] = god, ["effectType"] = effectType, ["isDuo"] = isDuo?.ToString() }
        });
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new BoonRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BoonRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", request);
        }

        await api.PostAsync<BoonRequest, BoonResponse>("/api/boons", request);
        TempData["Message"] = "Boon created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var boon = await api.GetAsync<BoonResponse>($"/api/boons/{id}");
        return View("Form", new BoonRequest
        {
            Name = boon.Name,
            God = boon.God,
            EffectType = boon.EffectType,
            Level = boon.Level,
            PowerScale = boon.PowerScale,
            IsDuo = boon.IsDuo,
            IsLegendary = boon.IsLegendary,
            Description = boon.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, BoonRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", request);
        }

        await api.PutAsync<BoonRequest, BoonResponse>($"/api/boons/{id}", request);
        TempData["Message"] = "Boon updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await api.DeleteAsync($"/api/boons/{id}");
        TempData["Message"] = "Boon deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static string Query(Dictionary<string, string?> values) =>
        string.Join("&", values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
}
