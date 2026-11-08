# Website version

## Goal
A free website on GitHub Pages that makes the same name tags as the app, so people can use it with no install and no Windows warning.
Built from the same C# code (Blazor WebAssembly). The EXE stays and shares that code.

## Questions (waiting for answers)
None right now.

## Owner answers
- 1A Web version from the same C# code (Blazor WebAssembly), on GitHub Pages; keep the EXE.
- 2A Make this repo public, after adding font licence texts and a README.
- 3A The same 4 bundled fonts (no font upload). **Changed** by 4B below.
- 4B "can we do english as well? i want to support more then these 4 fonts (also add regular hebrew fonts)". Read as: add English fonts and everyday Hebrew fonts. Mockup otherwise approved.
- 5A English buttons and labels.
- 6A Use the proposed font list; drop any that print too thin.
- 7A Upload your own font, on the website and in the EXE.
- 8A Same font list in the EXE.
- 9A Sample names in tests and docs are fine to show publicly.
- 10A MIT licence for the app's own code.

## What the website does (first version)
- Same items: keychain, pencil top, bookmark, desk plate, cup marker, wine glass marker.
- Names list, all item settings, colours, Arrange (rows, columns, order), 3D preview.
- **Download 3MF** (two parts, two colours, opens in Bambu Studio by double-click) and **Download STL**.
- Settings remembered in the browser.
- Not on the website: Send to Bambu (a browser cannot open Bambu), adding your own font, crash log.

## Steps

### Phase 1: Get the repo ready to be public
- [x] Add the full licence texts for the 4 fonts (GPL v2, SIL OFL 1.1). Verify: files present, listed in LICENSES.md.
- [x] README: what it is, website link, EXE download, .NET 10 and "Run anyway" notes. Verify: reads well on GitHub.
- [x] Read every file and the commit history for anything private before switching to public; show the owner the list. Verify: owner OK.
- [x] Switch the repo to public. Verify: page opens signed out.

### Phase 1b: More fonts
- [x] Download the chosen fonts and their licences into assets/fonts; list each in LICENSES.md. Verify: every file has a licence entry.
- [x] Print check for each font: Hebrew and English sample names build closed meshes with no part thinner than 0.8 mm at 12 mm size; drop the ones that fail. Verify: test per font. Result: Frank Ruhl Libre dropped (16-20% thinner than 0.8 mm); the other 13 new fonts lose 2% or less. 17 fonts built in, grouped Hebrew and English / Hebrew / English in the EXE font list. 1410 tests pass.

### Phase 2: Split out the shared code
- [ ] New project NameTagGenerator.Core (no WPF): Geometry, Items, file writers. Verify: builds; the EXE still builds.
- [ ] Letters to outlines with a font reader that works in a browser (SixLabors.Fonts), replacing the Windows text drawing. Test first: Hebrew and English names, right-to-left order, bold, connector. Verify: outline sizes match the old ones within 0.1 mm.
- [ ] Move the tests to the shared code. Verify: all tests pass.
- [ ] EXE uses the shared code. Verify: same 3MF volumes as v0.1.3 for each item; Bambu headless check clean.

### Phase 3: The website
- [ ] Blazor WebAssembly project, page laid out like the mockup, from the same design tokens. Verify: runs locally.
- [ ] 3D preview with three.js, name side up, same colours. Verify: screenshot check.
- [ ] Download 3MF and STL from the browser. Verify: downloaded 3MF passes the Bambu headless check.
- [ ] Remember settings in the browser; Hebrew right-to-left everywhere. Verify: `rtl-check`, reload keeps settings.
- [ ] Short help panel: how to open the file in Bambu Studio. Verify: read through.

### Phase 4: Publish
- [ ] GitHub Actions: build and publish to GitHub Pages on every push to main. Verify: site live at https://eladgl-dot.github.io/3d-name-tag-generator/
- [ ] Check in Chrome, Edge and on a phone width. Verify: `ui-check`.
- [ ] `code-reviewer` before calling it done. Verify: high/med findings fixed.

## Tools I'll use
| Tool | Type | Why | Cost hint |
|---|---|---|---|
| `build-runner` | agent | Builds and tests, short results | low |
| `dev-practices` | skill | Test first for the new text outliner | low |
| `gui-polish` + `frontend-design` | skill | The web page look | low |
| `ui-check` | agent | Screenshots of the site, layout and contrast | med |
| `rtl-check` | agent | Hebrew right-to-left problems | low |
| `code-reviewer` | agent | Review before done | med |
| Bambu Studio headless | local tool | Proves downloaded 3MF files load cleanly | low |
| `gh` | CLI | Repo public, Pages, Actions | low |

## Test plan
- All existing geometry tests run against the shared code.
- New tests for the font reader: Hebrew order, sizes, bold, connector, matches old outlines.
- Bambu headless load of a downloaded 3MF for each item.
- Site screenshots at desktop and phone width, light and dark.

## Risks / open questions
- Font reader: the new one must give the same letter shapes as Windows. Bold may differ for fonts with no bold file (Windows fakes it). Fallback: thicken the outline with the Weight setting.
- First visit downloads a few MB; later visits are cached.
- Making the repo public shows the whole history, including docs and mockups.
- The PDF for sharing is on hold; a short "how to use" may live on the website instead.
