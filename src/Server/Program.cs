using UrlShortener;
using UrlShortener.Providers;

var builder = WebApplication.CreateBuilder(args);

new ShortUrlProvider().InitializeDatabase();

builder.Services.AddScoped<ShortUrlProvider>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api", () => "Hello World!");

// create /api/genereate endpoint to take a url and return a short url
app.MapPost("/api/generate", (ShortUrlRequest req, ShortUrlProvider shortener) => {
    var short_url = shortener.Assign(req.Url);

    return Results.Ok(short_url);
});

app.Run();
