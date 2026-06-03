using HouseOfRuns.Api.Data;
using HouseOfRuns.Api.Middleware;
using HouseOfRuns.Api.Security;
using HouseOfRuns.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

builder.Services.AddDbContext<HouseOfRunsDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<PasswordHasher>();
builder.Services.AddSingleton<TokenService>();

builder.Services
    .AddAuthentication(TokenAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, TokenAuthenticationHandler>(
        TokenAuthenticationDefaults.Scheme,
        options => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:EnsureCreatedOnStartup", true))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HouseOfRunsDbContext>();
        await db.Database.EnsureCreatedAsync();
        await DatabaseCompatibility.EnsureAsync(db);
        await SeedData.SeedAsync(db, scope.ServiceProvider.GetRequiredService<PasswordHasher>(), app.Environment.ContentRootPath);
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Database initialization skipped. Check the PostgreSQL connection string. {exception.Message}");
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
