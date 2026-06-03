using HouseOfRuns.Frontend.Models;
using HouseOfRuns.Frontend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseOfRuns.Frontend.Controllers;

[Authorize(Roles = "Admin")]
public sealed class UsersController(HouseApiClient api) : Controller
{
    public async Task<IActionResult> Index(string? q, string? email, bool? isActive, int page = 1, string sortBy = "createdAt", string sortDir = "desc")
    {
        var pageData = await api.GetAsync<PagedResponse<UserResponse>>($"/api/users?{Query(new()
        {
            ["q"] = q,
            ["email"] = email,
            ["isActive"] = isActive?.ToString().ToLowerInvariant(),
            ["page"] = page.ToString(),
            ["sortBy"] = sortBy,
            ["sortDir"] = sortDir
        })}");

        return View(new ListPageViewModel<UserResponse>
        {
            Page = pageData,
            Q = q,
            SortBy = sortBy,
            SortDir = sortDir,
            Filters = new() { ["email"] = email, ["isActive"] = isActive?.ToString() }
        });
    }

    [HttpGet]
    public IActionResult Create() => View("Create", new CreateUserRequest());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        await api.PostAsync<CreateUserRequest, UserResponse>("/api/users", request);
        TempData["Message"] = "User created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var user = await api.GetAsync<UserResponse>($"/api/users/{id}");
        return View(new UpdateUserRequest
        {
            UserName = user.UserName,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role,
            Reputation = user.Reputation,
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, UpdateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        await api.PutAsync<UpdateUserRequest, UserResponse>($"/api/users/{id}", request);
        TempData["Message"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        await api.DeleteAsync($"/api/users/{id}");
        TempData["Message"] = "User deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static string Query(Dictionary<string, string?> values) =>
        string.Join("&", values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));
}
