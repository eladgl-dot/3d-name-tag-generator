# Next

Branch: `website-phase-2` (Phase 2 committed there, not pushed or merged; run `code-reviewer` before merging to `main`). Commit only when the owner asks.
Active plan: [plans/website.md](plans/website.md). Phase 2 done: shared code in `src/NameTagGenerator.Core`, browser-safe font reader. Next: Phase 3 (Blazor site), including font upload. Build 0.2 after.
Keep SixLabors.Fonts at 2.1.3 (owner's choice): 2.2.0 and 3.x stop Release builds without a licence key. Do not work around that check.

Waiting for the owner:
- Trying the Arrange menu by hand, especially the first click after switching to the app.
- Real prints of the desk plate, wine glass marker and revised bookmark. Cup marker saddle: owner printed it, "looks great" (2026-11-07), but the fit is too loose: try a smaller Rim thickness. Fit on other cup sizes is unknown.
- `crash.log` in `%LOCALAPPDATA%\3D name tag generator` if the memory error comes back. The cause is not proven yet.

Hand tests still open: rotating the preview by mouse, Save dialogs, Add font file, a clean PC without .NET 10, 100% text size.
UI check leftovers, low priority: toolbar wraps in a narrow window; open colour button is low contrast.

Run and test:
- Tests: `dotnet test NameTagGenerator.slnx` (shared code in `tests/NameTagGenerator.Core.Tests`, Windows-only in `tests/NameTagGenerator.Tests`)
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

GitHub: PUBLIC repo (since 2026-11-08, MIT) https://github.com/eladgl-dot/3d-name-tag-generator, EXE under Releases. Anything committed is public: never commit private files. The old app copy (source_zip/ and its ZIP) stays local only. Add the full font licence texts before making anything public.

v0.1.2 shows its version in the window title; the number comes from Version in the csproj, so raise it there for each build. Built to dist/0.1.2 because a copy was running from dist. Not committed or released yet.

v0.1.3 (dist/0.1.3): cup marker has Inner grip depth and Outer grip depth instead of one Grip depth. 467 tests, Bambu headless check of a 5/12 mm sample: 2 parts, no mesh errors. Not seen in the app window, not printed, not committed.
