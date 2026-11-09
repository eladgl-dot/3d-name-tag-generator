using System.Text;
using Microsoft.JSInterop;
using NameTagGenerator.Pictures;

namespace NameTagGenerator.Web;

/// <summary>Reads picture files and keeps the chosen one in this browser between visits.</summary>
public sealed class SavedPicture(IJSRuntime js)
{
    // Enough detail for a tag: at 14 mm tall each pixel is under 0.05 mm, well below what a printer shows.
    const int MaxPixelsPerSide = 320;

    /// <returns>Null when the file can't be read as a picture.</returns>
    public async Task<PictureSource?> ReadAsync(string fileName, byte[] file)
    {
        try
        {
            if (fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) return PictureSource.FromSvg(Encoding.UTF8.GetString(file));
            var module = await js.InvokeAsync<IJSObjectReference>("import", "./js/picker.js");
            var data = await module.InvokeAsync<byte[]>("grayImage", file, MaxPixelsPerSide);
            int width = data[0] | data[1] << 8, height = data[2] | data[3] << 8;
            return PictureSource.FromPixels(data[4..], width, height);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A bad file can fail in many ways, and every one of them means the same thing here.
            return null;
        }
    }

    /// <returns>Null when there is no saved file or this browser won't give it back.</returns>
    public async Task<PictureSource?> LoadAsync(string fileName)
    {
        try
        {
            var store = await Store();
            var file = await store.InvokeAsync<byte[]?>("getPicture");
            return file is null ? null : await ReadAsync(fileName, file);
        }
        catch (JSException)
        {
            return null;
        }
    }

    /// <returns>False when this browser would not keep the file.</returns>
    public async Task<bool> SaveAsync(byte[] file)
    {
        try
        {
            var store = await Store();
            await store.InvokeVoidAsync("putPicture", file);
            return true;
        }
        catch (JSException)
        {
            return false;
        }
    }

    public async Task RemoveAsync()
    {
        try
        {
            var store = await Store();
            await store.InvokeVoidAsync("removePicture");
        }
        catch (JSException)
        {
            // The settings no longer point at it, so a leftover file does no harm.
        }
    }

    ValueTask<IJSObjectReference> Store() => js.InvokeAsync<IJSObjectReference>("import", "./js/storage.js");
}
