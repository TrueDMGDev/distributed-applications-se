using System.Diagnostics;
using HouseOfRuns.Frontend.Models;
using Microsoft.AspNetCore.Mvc;

namespace HouseOfRuns.Frontend.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Runs");

    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
