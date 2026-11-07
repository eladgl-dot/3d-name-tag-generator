# Next

Branch: `master` (git initialised, nothing committed; the whole project is untracked). Commit only when the owner asks.
Active plan: [plans/csharp-app.md](plans/csharp-app.md). Done: crash log, colours, gap filling, desk plate, wine glass marker, Arrange menu, cup marker saddle. Next: Build 0.2.

Waiting for the owner:
- Trying the Arrange menu by hand, especially the first click after switching to the app.
- Real prints of the desk plate, wine glass marker and revised bookmark. Cup marker saddle: owner printed it, "looks great" (2026-11-07), but the fit is too loose: try a smaller Rim thickness. Fit on other cup sizes is unknown.
- `crash.log` in `%LOCALAPPDATA%\3D name tag generator` if the memory error comes back. The cause is not proven yet.

Hand tests still open: rotating the preview by mouse, Save dialogs, Add font file, a clean PC without .NET 10, 100% text size.
UI check leftovers, low priority: toolbar wraps in a narrow window; open colour button is low contrast.

Run and test:
- Tests: `dotnet test tests/NameTagGenerator.Tests`
- Publish: `dotnet publish src/NameTagGenerator -c Release -o dist` (close any running copy from `dist` first)
- Output: `dist/3D name tag generator.exe`

Rules to remember:
- Bambu Studio may be open with unsaved work. Never send a file into an open window without asking. Test with a headless copy: `bambu-studio.exe --datadir <private folder> --export-3mf out.3mf in.3mf`, then read `Metadata/model_settings.config`.
- This PC's Windows text size is 181%. The UI scales with it; never change that setting.
- UI tests must only click inside the app's own window: a `ui-check` run once sent a stray click into VS Code.
- Never screenshot by copying the screen: it once captured a private VS Code session. Capture only the app's own window.
- Test copies share `settings.json` with the owner's copy. Back it up first and restore it afterwards.
- Never test by sleeping the PC or filling graphics memory: Fusion 360 and Bambu Studio may hold unsaved work.
- Letter size stays constant; longer names make wider tags.
- Bundle only fonts with redistributable licences.
- Hebrew in PowerShell 5 scripts: use `[char]` codes.

GitHub: private repo https://github.com/eladgl-dot/3d-name-tag-generator, EXE under Releases. The old app copy (source_zip/ and its ZIP) stays local only. Add the full font licence texts before making anything public.
