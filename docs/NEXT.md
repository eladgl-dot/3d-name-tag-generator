# Next

Branch: `website-phase-2`, same as `main` at 766693c, both pushed. Only plan and notes files changed since. Commit only when the owner asks. `code-reviewer` before merging to `main`.
Website is LIVE: https://eladgl-dot.github.io/3d-name-tag-generator/ Every push to `main` republishes it.
Active plan: [plans/website.md](plans/website.md). All phases done. Low items left are listed in the plan's Phase 4. Active now: [plans/emoji-picture-fonts.md](plans/emoji-picture-fonts.md), waiting for its 4 questions; nothing built yet. Candidate fonts are not in the repo yet: get Gveret Levin and Noto Emoji from github.com/google/fonts (ofl/). The EXE has the mixed-name fix but is not rebuilt. `gh` is at `C:\Program Files\GitHub CLI\gh.exe`, not on the PATH.
Website: `src/NameTagGenerator.Web` (Blazor). `preview.js` draws with three.js 0.170, copied into `wwwroot/lib/three`; OrbitControls.js has its import path edited. The workflow sets `<base href>` to the repo path at publish time.
Keep SixLabors.Fonts at 2.1.3 (owner's choice): newer versions stop Release builds without a licence key. Do not work around that.

Waiting for the owner:
- Try the Arrange menu by hand, especially the first click after switching to the app.
- Real prints of desk plate, wine glass marker, revised bookmark. Cup marker prints well but fits too loose: try a smaller Rim thickness.
- `crash.log` in `%LOCALAPPDATA%\3D name tag generator` if the memory error returns.
Hand tests open: EXE preview rotation, Save dialogs, Add font file, a PC without .NET 10, 100% text size. Website: real phone, touch, real graphics card, Safari or iPhone (font upload picker may not open there). Low priority: EXE toolbar wraps when narrow; open colour button low contrast.

Run and test:
- Tests: `dotnet test NameTagGenerator.slnx` (1396 pass). Site: `dotnet run --project src/NameTagGenerator.Web`, or publish Release (trimming only shows there).
- EXE: `dotnet publish src/NameTagGenerator -c Release -o dist` (close any running copy from `dist` first). Version comes from the csproj. v0.1.2 and v0.1.3 (dist/0.1.x) not released.

Rules:
- Bambu Studio may hold unsaved work. Never send a file into an open window. Test headless: `bambu-studio.exe --datadir <private folder> --export-3mf out.3mf in.3mf`, then read `Metadata/model_settings.config`.
- Never change Windows text size (181%). UI tests click only inside the app's own window; never screenshot the whole screen. Browser tests: private headless Chrome.
- Test copies share `settings.json` with the owner's copy: back up and restore. Never test by sleeping the PC or filling graphics memory.
- Letter size stays constant; longer names make wider tags. Bundle only redistributable fonts. Hebrew in PowerShell 5 scripts: `[char]` codes.
- Repo is PUBLIC (MIT): https://github.com/eladgl-dot/3d-name-tag-generator. Never commit private files; source_zip/ stays local.
