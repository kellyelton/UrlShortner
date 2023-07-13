using UrlShortener;
using UrlShortener.Providers;
using UrlShortener.Services;

var builder = WebApplication.CreateBuilder(args);

new ShortUrlProvider().InitializeDatabase();

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

builder.Services.AddOptions<CleanupOptions>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        configuration.GetSection("Cleanup").Bind(settings);
    });

builder.Services.AddScoped<ShortUrlProvider>();
builder.Services.AddScoped<AnonPageViewTracker>();

builder.Services.AddHostedService<CleanupUrls>();

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
    var url = req.Url;
    if (string.IsNullOrWhiteSpace(url)) {
        return Results.BadRequest();
    }

    CleanupUrls.TryFix(ref url);

    if (!CleanupUrls.IsValidUrl(url, out var reason)) {
        return Results.BadRequest(reason); 
    }

    var short_url = shortener.Assign(url);

    return Results.Ok(short_url);
});

// Map any get request with a single parameter to the ShortUrlProvider
app.MapGet("/{shortUrl}", (ShortUrlProvider provider, string shortUrl) =>
{
    var url = provider.GetUrl(shortUrl);

    if (string.IsNullOrWhiteSpace(url)) {
        return Results.Redirect("/", permanent: false);
    }

    // redirect to the long url

    return Results.Redirect(url);
});

app.Run();
