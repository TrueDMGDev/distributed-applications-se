using System.Net;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace HouseOfRuns.Frontend.Services;

public sealed class ApiExceptionFilter(ITempDataDictionaryFactory tempDataFactory) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not ApiException apiException)
        {
            return;
        }

        if (apiException.StatusCode == HttpStatusCode.Unauthorized)
        {
            context.HttpContext.Session.Clear();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
            tempDataFactory.GetTempData(context.HttpContext)["Error"] = "Your session expired. Log in again.";

            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            context.ExceptionHandled = true;
            return;
        }

        tempDataFactory.GetTempData(context.HttpContext)["Error"] = apiException.Message;
        context.Result = new RedirectToActionResult("Index", "Runs", null);
        context.ExceptionHandled = true;
    }
}
