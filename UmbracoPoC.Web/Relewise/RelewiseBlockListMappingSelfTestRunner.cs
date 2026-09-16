using System.Reflection;
using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;

namespace UmbracoPoC.Web.Relewise;

internal sealed class RelewiseBlockListMappingSelfTestRunner
{
    private readonly RelewiseBlockListJsonSerializer _serializer;
    private readonly RelewiseContentMapper _mapper;
    private readonly IServiceProvider _services;
    private readonly IUmbracoContextFactory _umbracoContextFactory;

    public RelewiseBlockListMappingSelfTestRunner(
        RelewiseBlockListJsonSerializer serializer,
        RelewiseContentMapper mapper,
        IServiceProvider services,
        IUmbracoContextFactory umbracoContextFactory)
    {
        _serializer = serializer;
        _mapper = mapper;
        _services = services;
        _umbracoContextFactory = umbracoContextFactory;
    }

    public async Task<IReadOnlyList<string>> RunAsync()
    {
        var failures = new List<string>();
        using var contextReference = _umbracoContextFactory.EnsureUmbracoContext();
        var contentCache = contextReference.UmbracoContext.Content;
        if (contentCache is null)
        {
            failures.Add("Self-test: published content cache was not available.");
            return failures;
        }

        var home = contentCache.GetById(1120);
        var features = contentCache.GetById(1121);
        var blog = contentCache.GetById(1123);

        if (home is null)
        {
            failures.Add("Self-test: home content was not found.");
        }

        if (features is null)
        {
            failures.Add("Self-test: features content was not found.");
        }

        if (blog is null)
        {
            failures.Add("Self-test: blog article list content was not found.");
        }

        if (home is not null)
        {
            await RunMapperIntegrationTest(home, failures);
        }

        if (features is not null)
        {
            RunSerializerTest(features, failures);
        }

        if (blog is not null)
        {
            RunConfigurationRowTest(blog, failures);
        }

        return failures;
    }

    private void RunSerializerTest(IPublishedContent features, List<string> failures)
    {
        var contentRows = features.Properties.FirstOrDefault(property => property.Alias == "contentRows");
        if (contentRows is null)
        {
            failures.Add("Serializer test: Features contentRows property was not found.");
            return;
        }

        var extractedContent = _serializer.Serialize(contentRows, null);

        Assert(!string.IsNullOrWhiteSpace(extractedContent),
            "Serializer test: extracted contentRows text was empty.",
            failures);

        Assert(extractedContent.Contains("Rich Text Row", StringComparison.Ordinal),
            "Serializer test: expected rich text content was missing from extracted text.",
            failures);

        Assert(extractedContent.Contains("Bulleted List", StringComparison.Ordinal),
            "Serializer test: expected list content was missing from extracted text.",
            failures);

        Assert(!extractedContent.Contains("propertyAlias", StringComparison.OrdinalIgnoreCase),
            "Serializer test: structural JSON keys leaked into extracted text.",
            failures);

        Assert(!extractedContent.Contains("\"contentTypeAlias\"", StringComparison.OrdinalIgnoreCase),
            "Serializer test: content type metadata leaked into extracted text.",
            failures);
    }

    private void RunConfigurationRowTest(IPublishedContent blog, List<string> failures)
    {
        var contentRows = blog.Properties.FirstOrDefault(property => property.Alias == "contentRows");
        if (contentRows is null)
        {
            failures.Add("Configuration row test: Blog contentRows property was not found.");
            return;
        }

        var extractedContent = _serializer.Serialize(contentRows, null);

        Assert(!extractedContent.Contains("showPagination", StringComparison.OrdinalIgnoreCase),
            "Configuration row test: configuration aliases leaked into extracted text.",
            failures);

        Assert(!extractedContent.Contains("pageSize", StringComparison.OrdinalIgnoreCase),
            "Configuration row test: numeric configuration leaked into extracted text.",
            failures);

        Assert(!extractedContent.Contains("umb://", StringComparison.OrdinalIgnoreCase),
            "Configuration row test: picker UDIs leaked into extracted text.",
            failures);
    }

    private async Task RunMapperIntegrationTest(IPublishedContent home, List<string> failures)
    {
        var update = new ContentUpdate(new Content(home.Id.ToString()));
        var context = (ContentMappingContext)Activator.CreateInstance(
            typeof(ContentMappingContext),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: new object?[] { home, update, new List<string> { "en-US" }, _services },
            culture: null)!;

        var result = await _mapper.Map(context, CancellationToken.None);
        Assert(ReferenceEquals(result, update), "Mapper test: original content update was replaced.", failures);
        var values = result.Content.Data;
        if (values is null)
        {
            failures.Add("Mapper test: content data was not populated.");
            return;
        }
        var standardProperties = home.Properties.Where(property => property.PropertyType.EditorAlias != "Umbraco.BlockList");
        var expected = _services.GetRequiredService<IRelewisePropertyConverter>().Convert(standardProperties, new[] { "en-US" });
        foreach (var pair in expected)
        {
            Assert(values.TryGetValue(pair.Key, out var actual)
                   && System.Text.Json.JsonSerializer.Serialize(actual) == System.Text.Json.JsonSerializer.Serialize(pair.Value),
                $"Mapper test: standard property {pair.Key} was not preserved.", failures);
        }

        if (!values.TryGetValue("socialIconLinks", out var dataValue) || dataValue is null)
        {
            failures.Add("Mapper test: socialIconLinks was not mapped.");
            return;
        }

        var extractedContent = dataValue.ValueAs<string>();
        Assert(!string.IsNullOrWhiteSpace(extractedContent),
            "Converter test: socialIconLinks extracted text was empty.",
            failures);

        Assert(extractedContent.Contains("GitHub", StringComparison.OrdinalIgnoreCase),
            "Converter test: visible link text was not preserved.",
            failures);

        Assert(!extractedContent.Contains("https://", StringComparison.OrdinalIgnoreCase),
            "Converter test: link URLs leaked into extracted text.",
            failures);

        Assert(!extractedContent.Contains("target", StringComparison.OrdinalIgnoreCase),
            "Converter test: link metadata leaked into extracted text.",
            failures);
    }

    private static void Assert(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }
}
