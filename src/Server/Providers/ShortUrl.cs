using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Providers;

public sealed class ShortUrl {
    [Key]
    [MaxLength(7)]
    public string ShortCode { get; set; }

    [Required]  
    [MaxLength(2048)]
    [Url]
    public string LongUrl { get; set; }

    public int AccessCount { get; set; }

    public DateTime Created { get; set; }

    public DateTime LastAccessed { get; internal set; }
}