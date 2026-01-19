using ArtistInsightTool.Apps;
using ArtistInsightTool.Apps.Views;
using ArtistInsightTool.Apps.Tables;
using ArtistInsightTool.Apps.Services;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
var server = new Server();
#if DEBUG
server.UseHotReload();
#endif
server.Services.AddSingleton<ArtistInsightService>();
server.AddAppsFromAssembly();
server.AddConnectionsFromAssembly();
var chromeSettings = new ChromeSettings().DefaultApp<RevenueTableApp>().UseTabs(preventDuplicates: true);
server.UseChrome(chromeSettings);
await server.RunAsync();
