using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Providers;

public sealed class AvailableShortCode
{
    [Key]
    [MaxLength(7)]
    public string ShortCode { get; set; }
    
    public bool IsAvailable { get; set; } = true;
}