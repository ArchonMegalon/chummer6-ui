using Chummer.Blazor;
using Chummer.Blazor.Components;
using Chummer.Desktop.Runtime;
using Chummer.Presentation;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddChummerLocalRuntimeClient(AppContext.BaseDirectory, Directory.GetCurrentDirectory());
builder.Services.AddHttpClient<IWorkbenchCoachApiClient, WorkbenchCoachApiClient>();

WebApplication app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
