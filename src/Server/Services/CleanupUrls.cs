using Microsoft.Extensions.Options;
using UrlShortener.Providers;

namespace UrlShortener.Services
{
    public class CleanupUrls : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly ILogger<CleanupUrls> _logger;

        private readonly IOptionsMonitor<CleanupOptions> _options;

        public CleanupUrls(IOptionsMonitor<CleanupOptions> options, IServiceProvider serviceProvider, ILogger<CleanupUrls> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Started Service {0}", nameof(CleanupUrls));

                while (!stoppingToken.IsCancellationRequested)
                {
                    var opt = _options.CurrentValue;

                    var interval = opt.Interval ?? CleanupOptions.DefaultInterval;

                    // Run every 30 minutes
                    await Task.Delay(interval, stoppingToken);

                    opt = _options.CurrentValue;

                    _logger.LogTrace("Cleaning up service is running");

                    // Get all urls from the database
                    using var scope = _serviceProvider.CreateScope();
                    var provider = scope.ServiceProvider.GetRequiredService<ShortUrlProvider>();

                    foreach (var short_url in provider.All())
                    {
                        stoppingToken.ThrowIfCancellationRequested();

                        var fixed_url = short_url.LongUrl;
                        if (TryFix(ref fixed_url)) {
                            var original_url = short_url.LongUrl;
                            short_url.LongUrl = fixed_url;
                            _logger.LogTrace("Fixed {0}: {1} -> {2}", short_url.ShortCode, original_url, short_url.LongUrl);

                            if (!opt.LogOnly)
                            {
                                provider.UpdateUrl(short_url.ShortCode, short_url.LongUrl);
                                _logger.LogTrace("Fix saved in database {0}", short_url.ShortCode);
                            } else {
                                _logger.LogTrace("FIX NOT SAVED (due to app settings) {0}", short_url.ShortCode);
                            }
                        }

                        if (IsGarbage(short_url, out var reason))
                        {
                            _logger.LogTrace("Deleting {0} because {1}: {2}", short_url.ShortCode, reason, short_url.LongUrl);

                            if (!opt.LogOnly)
                            {
                                provider.Delete(short_url.ShortCode);

                                _logger.LogTrace("Deleted {0}", short_url.ShortCode);
                            }
                            else
                            {
                                _logger.LogTrace("URL NOT DELETED (due to app settings) {0}", short_url.ShortCode);
                            }
                        }
                    }

                    _logger.LogTrace("Cleaning up service is done");
                }
            }
            finally
            {
                _logger.LogInformation("Stopped Service {0}", nameof(CleanupUrls));
            }
        }

        public static bool TryFix(ref string url)
        {
            // If null or whitespace, just return
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var applied_fix = false;

            // Has any whitespace in front or back?
            if (url.Trim() != url)
            {
                url = url.Trim();

                applied_fix = true;
            }

            // If it's a url with no scheme, add http://
            if (Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri) && !uri.IsAbsoluteUri)
            {
                url = $"http://{url}";

                applied_fix = true;
            }

            return applied_fix;
        }

        public static bool IsValidUrl(string url, out string reason)
        {
            // Clean urls that are null or whitespace
            if (string.IsNullOrWhiteSpace(url))
            {
                reason = "null or whitespace";
                return false;
            }

            // Clean any urls that aren't urls
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                reason = "not a valid url";
                return false;
            }

            reason = string.Empty;

            return true;
        } 

        private bool IsGarbage(ShortUrl url, out string reason) {
            if (!IsValidUrl(url.LongUrl, out reason))
            {
                reason = "invalid url";

                return true;
            }

            var uri = new Uri(url.LongUrl);

            // Clean urls that point to this site
            // tny.wtf or dic.lol
            if (uri.Host.ToLower() == "tny.wtf" || uri.Host.ToLower() == "dic.lol")
            {
                reason = $"points to this site ({uri.Host})";
                return true;
            }


            // Clean any urls with 1 or less access count and are older than 14 days
            if (url.AccessCount <= 1 && url.Created < DateTime.UtcNow.AddDays(-14))
            {
                reason = $"viewed {url.AccessCount} times and created more than 14 days ago";
                return true;
            }

            reason = "not garbage";

            return false;
        }
    }
}