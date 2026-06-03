using HouseOfRuns.Frontend.Models;
using HouseOfRuns.Frontend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseOfRuns.Frontend.Controllers;

[Authorize(Roles = "Admin")]
public sealed class WeaponsController(HouseApiClient api) : Controller
{
    public async Task<IActionResult> Index(string? q, string? weaponType, bool? isUnlocked, int page = 1, string sortBy = "name", string sortDir = "asc")
    {
        var path = $"/api/weapons?{Query(new()
        {
            ["q"] = q,
            ["weaponType"] = weaponType,
            ["isUnlocked"] = isUnlocked?.ToString().ToLowerInvariant(),
            ["page"] = page.ToString(),
            ["sortBy"] = sortBy,
            ["sortDir"] = sortDir
        })}";

        var model = new ListPageViewModel<WeaponResponse>
        {
            Page = await api.GetAsync<PagedResponse<WeaponResponse>>(path),
            Q = q,
            SortBy = sortBy,
            SortDir = sortDir,
            Filters = new() { ["weaponType"] = weaponType, ["isUnlocked"] = isUnlocked?.ToString() }
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new WeaponRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WeaponRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", request);
        }

        await api.PostAsync<WeaponRequest, WeaponResponse>("/api/weapons", request);
        TempData["Message"] = "Weapon created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var weapon = await api.GetAsync<WeaponResponse>($"/api/weapons/{id}");
        return View("Form", new WeaponRequest
        {
            Name = weapon.Name,
            AspectName = weapon.AspectName,
            WeaponType = weapon.WeaponType,
            TitanBloodLevel = weapon.TitanBloodLevel,
            UnlockCost = weapon.UnlockCost,
            BaseDamage = weapon.BaseDamage,
            IsUnlocked = weapon.IsUnlocked,
            Description = weapon.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, WeaponRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", request);
        }

        await api.PutAsync<WeaponRequest, WeaponResponse>($"/api/weapons/{id}", request);
        TempData["Message"] = "Weapon updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await api.DeleteAsync($"/api/weapons/{id}");
        TempData["Message"] = "Weapon deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static string Query(Dictionary<string, string?> values) =>
        string.Join("&", values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
}
