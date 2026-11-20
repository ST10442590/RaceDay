using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// The MVC app's own session. It holds who is logged in, their role, and the
// API session cookie that is forwarded on every API call.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "RaceDay.Web.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

builder.Services.AddHttpContextAccessor();

// Typed HttpClient for the Part 2 API. The address comes from configuration
// ("Api:BaseUrl"), so it can be changed for Docker without touching code.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5120/";
if (!apiBaseUrl.EndsWith('/'))
{
    apiBaseUrl += "/";
}

builder.Services.AddHttpClient<ApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // The API cookie is managed per user in ApiClient, never shared
        // between users through a handler-wide cookie jar.
        UseCookies = false
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Always read and write numbers with a "." decimal point, whatever the
// machine's regional settings are, so "21.1" km binds the same everywhere.
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture),
    SupportedCultures = new[] { CultureInfo.InvariantCulture },
    SupportedUICultures = new[] { CultureInfo.InvariantCulture }
});

app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage", "?code={0}");

app.UseRouting();

app.UseSession();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
