using Microsoft.JSInterop;
using NameTagGenerator.Text;

namespace NameTagGenerator.Web;

/// <summary>The font list for this browser, with uploaded fonts kept between visits.</summary>
public sealed class UploadedFonts(IJSRuntime js)
{
    public FontLibrary Library { get; } = new();

    public async Task LoadAsync()
    {
        try
        {
            var store = await OpenStore();
            foreach (var name in await store.InvokeAsync<string[]>("names"))
            {
                try
                {
                    Library.Add(await store.InvokeAsync<byte[]>("get", name));
                }
                catch (JSException)
                {
                    // One unreadable file must not cost the others.
                }
            }
        }
        catch (JSException)
        {
            // A browser that blocks storage still has the built-in fonts.
        }
    }

    /// <returns>False when this browser would not keep the file.</returns>
    public async Task<bool> SaveAsync(string name, byte[] file)
    {
        try
        {
            var store = await OpenStore();
            await store.InvokeVoidAsync("put", name, file);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }

    ValueTask<IJSObjectReference> OpenStore() => js.InvokeAsync<IJSObjectReference>("import", "./js/storage.js");
}
