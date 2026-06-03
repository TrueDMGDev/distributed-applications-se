using HouseOfRuns.Frontend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<ApiExceptionFilter>();
});
builder.Services.AddHttpContextAccessor();
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".HouseOfRuns.Frontend.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient("HouseApi", client =>
{
    var baseUrl = builder.Configuration["Backend:BaseUrl"] ?? "http://localhost:5119";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddScoped<HouseApiClient>();
builder.Services.AddScoped<ApiExceptionFilter>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Runs}/{action=Index}/{id?}");

app.Run();
