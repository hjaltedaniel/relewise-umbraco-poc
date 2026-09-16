using System.ComponentModel.DataAnnotations;

namespace UmbracoPoC.Web.Configuration;

public sealed class RelewiseOptions
{
    public const string SectionName = "Relewise";

    [Required]
    public Guid DatasetId { get; init; }

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    [Url]
    public string ServerUrl { get; init; } = string.Empty;
}
