using System.ComponentModel.DataAnnotations;

namespace UrlShortener;

public sealed class ShortUrlRequest
{
    [Required]
    [Url]
    public string Url { get; set; }
}