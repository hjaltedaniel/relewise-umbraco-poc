using System.Collections;
using System.Net;
using System.Text.RegularExpressions;
using Clean.Core.Extensions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace UmbracoPoC.Web.Relewise;

internal sealed class RelewiseBlockListJsonSerializer
{
    private static readonly Regex HtmlTagRegex = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new("\\s+", RegexOptions.Compiled);

    public string Serialize(IPublishedProperty property, string? culture)
    {
        var rows = property.GetValue(culture, null) as BlockListModel;
        var fragments = ExtractRows(rows, culture);
        return string.Join(Environment.NewLine, fragments);
    }

    private List<string> ExtractRows(BlockListModel? rows, string? culture)
    {
        var fragments = new List<string>();
        if (rows is null)
        {
            return fragments;
        }

        foreach (var row in rows)
        {
            if (IsHidden(row.Settings, culture))
            {
                continue;
            }

            fragments.AddRange(ExtractElement(row.Content, culture));
        }

        return fragments;
    }

    private List<string> ExtractElement(IPublishedElement element, string? culture)
    {
        var fragments = new List<string>();
        foreach (var property in element.Properties)
        {
            fragments.AddRange(ExtractPropertyValue(property, culture));
        }

        return fragments;
    }

    private List<string> ExtractPropertyValue(IPublishedProperty property, string? culture)
    {
        var value = property.GetValue(culture, null);
        return ExtractValue(value, property, culture);
    }

    private List<string> ExtractValue(object? value, IPublishedProperty? property, string? culture)
    {
        if (value is null)
        {
            return [];
        }

        if (value is BlockListModel nestedRows)
        {
            return ExtractRows(nestedRows, culture);
        }

        if (value is IHtmlEncodedString html)
        {
            return AddIfPresent([], StripHtml(html.ToHtmlString() ?? string.Empty));
        }

        if (value is MediaWithCrops media)
        {
            return ExtractMedia(media);
        }

        if (value is IEnumerable<MediaWithCrops> mediaItems)
        {
            return mediaItems.SelectMany(ExtractMedia).ToList();
        }

        if (value is Link link)
        {
            return AddIfPresent([], link.Name);
        }

        if (value is IEnumerable<Link> links)
        {
            return links
                .Select(linkItem => NormalizeText(linkItem.Name))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList()!;
        }

        if (value is IPublishedContent content)
        {
            return AddIfPresent([], content.Name);
        }

        if (value is IEnumerable<IPublishedContent> contents)
        {
            return contents
                .Select(contentItem => NormalizeText(contentItem.Name))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList()!;
        }

        if (value is IPublishedElement element)
        {
            return ExtractElement(element, culture);
        }

        if (value is string text)
        {
            return ExtractString(text, property);
        }

        if (value is IEnumerable enumerable && value is not string && value is not IDictionary)
        {
            return ExtractEnumerable(enumerable, property, culture);
        }

        return ExtractFallbackValue(property, value, culture);
    }

    private List<string> ExtractEnumerable(IEnumerable values, IPublishedProperty? property, string? culture)
    {
        var fragments = new List<string>();
        foreach (var item in values)
        {
            fragments.AddRange(ExtractValue(item, property, culture));
        }

        return fragments;
    }

    private List<string> ExtractFallbackValue(IPublishedProperty? property, object value, string? culture)
    {
        if (property is not null)
        {
            var sourceValue = property.GetSourceValue(culture, null);
            if (sourceValue is string sourceText)
            {
                return ExtractString(sourceText, property);
            }

            if (sourceValue is IEnumerable sourceEnumerable && sourceValue is not string && sourceValue is not IDictionary)
            {
                return ExtractEnumerable(sourceEnumerable, property, culture);
            }

            if (sourceValue is not null)
            {
                return ExtractString(sourceValue.ToString(), property);
            }
        }

        return ExtractString(value.ToString(), property);
    }

    private static List<string> ExtractMedia(MediaWithCrops media)
    {
        var fragments = new List<string>();
        AddIfPresent(fragments, media.Content?.GetAltText());
        if (fragments.Count == 0)
        {
            AddIfPresent(fragments, media.Name);
        }

        return fragments;
    }

    private static List<string> ExtractString(string? text, IPublishedProperty? property)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (property is not null && ShouldSkipString(property, text))
        {
            return [];
        }

        var normalized = NormalizeText(text);
        return string.IsNullOrWhiteSpace(normalized) ? [] : [normalized];
    }

    private static bool ShouldSkipString(IPublishedProperty property, string text)
    {
        var alias = property.Alias;
        if (alias.Contains("url", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (property.PropertyType.EditorAlias.Contains("url", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (text.StartsWith("umb://", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out _))
        {
            return true;
        }

        return false;
    }

    private static bool IsHidden(IPublishedElement? settings, string? culture)
    {
        if (settings is null)
        {
            return false;
        }

        var hideProperty = settings.Properties.FirstOrDefault(property => property.Alias == "hide");
        if (hideProperty is null)
        {
            return false;
        }

        return hideProperty.GetValue(culture, null) switch
        {
            bool hidden => hidden,
            string hiddenString when bool.TryParse(hiddenString, out var hidden) => hidden,
            _ => false
        };
    }

    private static List<string> AddIfPresent(List<string> fragments, string? value)
    {
        var normalized = NormalizeText(value);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            fragments.Add(normalized);
        }

        return fragments;
    }

    private static string NormalizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return WhitespaceRegex.Replace(text.Trim(), " ");
    }

    private static string StripHtml(string html)
    {
        var withoutTags = HtmlTagRegex.Replace(html, " ");
        return NormalizeText(WebUtility.HtmlDecode(withoutTags));
    }
}
