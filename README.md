# 3D name tag generator

Type names, pick an item, and get a two-colour 3D print file for Bambu Studio. Hebrew and English names both work.

Items: keychain, pencil top, bookmark, desk name plate, cup marker (sits on the rim of a cup) and wine glass marker.

## Use it

**Website** (no install, any computer): coming soon.

**Windows app:** download `3D-name-tag-generator.exe` from [Releases](https://github.com/eladgl-dot/3d-name-tag-generator/releases/latest) and double-click it.

- It needs Microsoft's free **.NET 10 Desktop Runtime**. If it is missing, Windows offers a download link the first time; install it and start the app again.
- Windows may show **"Windows protected your PC"**, because the app is not digitally signed. Click **More info**, then **Run anyway**.

## Printing

1. Type one name per line and choose an item and a font.
2. Click **Send to Bambu** in the app, or save the 3MF file and double-click it.
3. In Bambu Studio the item has two parts, base and letters. Give each part its own colour and print.

The cup marker prints name side down, so in Bambu Studio it looks upside down and mirrored. That is correct: do not flip it.

## Fonts

From v0.1.4, 17 fonts are built in: everyday and decorative Hebrew fonts, and English fonts. You can also add your own font file. Licences for the built-in fonts are listed in [assets/fonts/LICENSES.md](assets/fonts/LICENSES.md).

## Build it yourself

.NET 10 SDK on Windows:

```
dotnet test tests/NameTagGenerator.Tests
dotnet publish src/NameTagGenerator -c Release -o dist
```

## Licence

The app's code is under the [MIT licence](LICENSE). The built-in fonts keep their own licences, listed in [assets/fonts/LICENSES.md](assets/fonts/LICENSES.md).
