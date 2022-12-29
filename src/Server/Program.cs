using UrlShortener;
using UrlShortener.Providers;

var builder = WebApplication.CreateBuilder(args);

new ShortUrlProvider().InitializeDatabase();

builder.Services.AddScoped<ShortUrlProvider>();
builder.Services.AddScoped<AnonPageViewTracker>();

var app = builder.Build();

// capture every request and log the path and ip address
app.Use(async (context, next) =>
{
    var tracker = context.RequestServices.GetRequiredService<AnonPageViewTracker>();

    // Domain name, without the port or scheme
    var host = context.Request.Host.Host;
    var url = context.Request.Path.Value ?? "/";
    var ip = context.Connection.RemoteIpAddress.ToString();
    var user_agent = context.Request.Headers["User-Agent"].ToString();

    tracker.Track(host, url, ip, user_agent);

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

// create /api/genereate endpoint to take a url and return a short url
app.MapPost("/api/generate", (ShortUrlRequest req, ShortUrlProvider shortener) =>
{
    var short_url = shortener.Assign(req.Url);

    return Results.Ok(short_url);
});

// Map any get request with a single parameter to the ShortUrlProvider
app.MapGet("/{shortUrl}", (ShortUrlProvider provider, string shortUrl) =>
{
    var url = provider.GetUrl(shortUrl);

    if (string.IsNullOrWhiteSpace(url))
    {
        return Results.NotFound();
    }

    // redirect to the long url

    return Results.Redirect(url);
});

app.Run();
