using Relewise.Client.DataTypes;
using Relewise.Integrations.Umbraco;
using Relewise.Integrations.Umbraco.Services;
using Umbraco.Extensions;

namespace UmbracoPoC.Web.Relewise;

internal sealed class RelewiseContentMapper(RelewiseBlockListJsonSerializer serializer) : IContentTypeMapping
{
    public Task<ContentUpdate> Map(ContentMappingContext context, CancellationToken token)
    {
        var properties = context.PublishedContent.Properties.ToLookup(property =>
            string.Equals(property.PropertyType.EditorAlias, "Umbraco.BlockList", StringComparison.OrdinalIgnoreCase));
        var cultures = context.CulturesToPublish.ToArray();
        var converter = context.GetRequiredService<IRelewisePropertyConverter>();
        var data = converter.Convert(properties[false], cultures).ToDictionary(pair => pair.Key, pair => pair.Value);

        foreach (var property in properties[true])
        {
            if (property.PropertyType.VariesByCulture() && cultures.Length > 1)
            {
                data.Add(property.Alias, new Multilingual(cultures.Select(culture =>
                    new Multilingual.Value(culture, serializer.Serialize(property, culture))).ToArray()));
            }
            else
            {
                data.Add(property.Alias, new DataValue(serializer.Serialize(property, cultures[0])));
            }
        }

        context.ContentUpdate.Content.Data = data;
        return Task.FromResult(context.ContentUpdate);
    }
}
