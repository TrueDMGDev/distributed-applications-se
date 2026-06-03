using System.Security.Claims;
using HouseOfRuns.Frontend.Models;
using HouseOfRuns.Frontend.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseOfRuns.Frontend.Controllers;

public sealed class AccountController(HouseApiClient api) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Runs");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginRequest { EmailOrUserName = "demo" });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequest request, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        try
        {
            var auth = await api.PostAsync<LoginRequest, AuthResponse>("/api/auth/login", request);
            await SignInAsync(auth);
            return LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? Url.Action("Index", "Runs")! : returnUrl);
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(request);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Runs")
            : View(new RegisterRequest());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return View(request);
        }

        try
        {
            var auth = await api.PostAsync<RegisterRequest, AuthResponse>("/api/auth/register", request);
            await SignInAsync(auth);
            return RedirectToAction("Index", "Runs");
        }
        catch (ApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(request);
        }
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        HttpContext.Session.Clear();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public IActionResult AccessDenied()
    {
        TempData["Error"] = "You are logged in, but your account does not have access to that page.";
        return RedirectToAction("Index", "Runs");
    }

    private async Task SignInAsync(AuthResponse auth)
    {
        HttpContext.Session.SetString("ApiToken", auth.Token);
        HttpContext.Session.SetString("DisplayName", auth.User.DisplayName);
        HttpContext.Session.SetString("Role", auth.User.Role);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.Id.ToString()),
            new(ClaimTypes.Name, auth.User.UserName),
            new(ClaimTypes.Email, auth.User.Email),
            new(ClaimTypes.Role, auth.User.Role),
            new("display_name", auth.User.DisplayName)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false });
    }
}
