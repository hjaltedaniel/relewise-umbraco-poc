using Microsoft.Data.Sqlite;
using Relewise.Client.Extensions.DependencyInjection;
using Relewise.Integrations.Umbraco;
using UmbracoPoC.Web.Configuration;
using UmbracoPoC.Web.Relewise;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "umbraco", "Data");

Directory.CreateDirectory(dataDirectory);
AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);

var umbracoConnectionString = builder.Configuration.GetConnectionString("umbracoDbDSN");
if (!string.IsNullOrWhiteSpace(umbracoConnectionString))
{
    var sqliteConnectionString = new SqliteConnectionStringBuilder(umbracoConnectionString);
    if (!string.IsNullOrWhiteSpace(sqliteConnectionString.DataSource))
    {
        var sqliteDirectory = Path.GetDirectoryName(sqliteConnectionString.DataSource);
        if (!string.IsNullOrWhiteSpace(sqliteDirectory))
        {
            Directory.CreateDirectory(sqliteDirectory);
        }

        await using var sqliteConnection = new SqliteConnection(sqliteConnectionString.ConnectionString);
        await sqliteConnection.OpenAsync();
    }
}

builder.Services
    .AddOptions<RelewiseOptions>()
    .BindConfiguration(RelewiseOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddRelewise(options => options.ReadFromConfiguration(builder.Configuration, RelewiseOptions.SectionName));

builder.Services.AddSingleton<RelewiseBlockListJsonSerializer>();
builder.Services.AddSingleton<RelewiseContentMapper>();
builder.Services.AddSingleton<RelewiseBlockListMappingSelfTestRunner>();

var umbracoBuilder = builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddRelewise((options, services) =>
    {
        foreach (var contentTypeAlias in new[]
                 {
                     "article",
                     "articleList",
                     "author",
                     "authorList",
                     "contact",
                     "content",
                     "error",
                     "home",
                     "search",
                     "xMLSitemap"
                 })
        {
            options.AddContentType(contentTypeAlias, contentType => contentType.UseMapper(services.GetRequiredService<RelewiseContentMapper>()));
        }
    });

umbracoBuilder.Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

if (args.Contains("--relewise-blocklist-self-test", StringComparer.Ordinal))
{
    var selfTestRunner = app.Services.GetRequiredService<RelewiseBlockListMappingSelfTestRunner>();
    var failures = await selfTestRunner.RunAsync();

    if (failures.Count > 0)
    {
        Environment.ExitCode = 1;
        foreach (var failure in failures)
        {
            Console.Error.WriteLine(failure);
        }

        return;
    }

    Console.WriteLine("All Relewise blocklist mapping checks passed.");
    return;
}


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.TrackContentViews();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
