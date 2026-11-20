using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers hold the API endpoints.
builder.Services.AddControllers();

// Entity Framework Core, Code-First, against SQL Server.
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

// Hashes and verifies passwords, so none are ever stored as plain text.
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// Event banner images are stored in Azure Blob Storage. The connection
// string comes from configuration (user secrets locally), never from code.
builder.Services.AddSingleton<IImageStorage, BlobImageStorage>();

// Live weather for event venues, from the free Open-Meteo service.
builder.Services.AddHttpClient(EventWeatherController.HttpClientName, client =>
{
    client.BaseAddress = new Uri("https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Server-side session state. The session holds the user's id and role; the
// browser only receives an opaque, HttpOnly cookie that points at it.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "RaceDay.Session";
    options.Cookie.HttpOnly = true;          // not readable by JavaScript
    options.Cookie.IsEssential = true;       // required for the app to work
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});

// Swagger, so every endpoint can be viewed and tested in a browser.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Pull the XML comments above each action into the Swagger UI, so each
    // endpoint shows its purpose, expected input and response.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Apply any pending migrations and seed demo data on startup.
DbInitializer.InitialiseAsync(app.Services).GetAwaiter().GetResult();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
        options.DocumentTitle = "RaceDay API";
    });
}

app.UseHttpsRedirection();

// Must come before the endpoints, so HttpContext.Session is available.
app.UseSession();

app.MapControllers();

app.Run();