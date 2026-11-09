using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NameTagGenerator.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddSingleton<UploadedFonts>();
builder.Services.AddSingleton<SavedPicture>();

var host = builder.Build();
// Before the page starts, so saved settings can pick an uploaded font.
await host.Services.GetRequiredService<UploadedFonts>().LoadAsync();
await host.RunAsync();
