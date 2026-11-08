# Next

Branch: `website-phase-2`. Phases 2 and part of 3 are committed there, not pushed or merged. Commit only when the owner asks. `code-reviewer` before merging to `main`.
Active plan: [plans/website.md](plans/website.md). Next step: Phase 3, "Remember settings in the browser; Hebrew right-to-left", then font upload, then help panel (add the "How to print" link from the mockup; credit three.js too), then `ui-check` and `rtl-check`. Build 0.2 after.
Website: `src/NameTagGenerator.Web` (Blazor). `preview.js` draws with three.js 0.170, copied into `wwwroot/lib/three`; OrbitControls.js has its import path edited. Before deploy: GitHub Pages needs `<base href>` set to the repo path.
Keep SixLabors.Fonts at 2.1.3 (owner's choice): newer versions stop Release builds without a licence key. Do not work around that.

Waiting for the owner:
- Try the Arrange menu by hand, especially the first click after switching to the app.
- Real prints of desk plate, wine glass marker, revised bookmark. Cup marker prints well but fits too loose: try a smaller Rim thickness.
- `crash.log` in `%LOCALAPPDATA%\3D name tag generator` if the memory error returns.
Hand tests open: EXE preview rotation, Save dialogs, Add font file, a PC without .NET 10, 100% text size. Website: real graphics card, touch. Low priority: EXE toolbar wraps when narrow; open colour button low contrast.

Run and test:
- Tests: `dotnet test NameTagGenerator.slnx` (1352 pass). Site: `dotnet run --project src/NameTagGenerator.Web`, or publish Release (trimming only shows there).
- EXE: `dotnet publish src/NameTagGenerator -c Release -o dist` (close any running copy from `dist` first). Version comes from the csproj. v0.1.2 and v0.1.3 (dist/0.1.x) not released.

Rules:
- Bambu Studio may hold unsaved work. Never send a file into an open window. Test headless: `bambu-studio.exe --datadir <private folder> --export-3mf out.3mf in.3mf`, then read `Metadata/model_settings.config`.
- Never change Windows text size (181%). UI tests click only inside the app's own window; never screenshot the whole screen. Browser tests: private headless Chrome.
- Test copies share `settings.json` with the owner's copy: back up and restore. Never test by sleeping the PC or filling graphics memory.
- Letter size stays constant; longer names make wider tags. Bundle only redistributable fonts. Hebrew in PowerShell 5 scripts: `[char]` codes.
- Repo is PUBLIC (MIT): https://github.com/eladgl-dot/3d-name-tag-generator. Never commit private files; source_zip/ stays local.
