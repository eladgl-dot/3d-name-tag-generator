# Website version

## Goal
A free website on GitHub Pages that makes the same name tags as the app, so people can use it with no install and no Windows warning.
Built from the same C# code (Blazor WebAssembly). The EXE stays and shares that code.

## Questions (waiting for answers)

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
- 11A Include font upload on the website, as 7A says.
- 12A Stay on SixLabors.Fonts 2.1.3: no licence key needed, but no new fixes from upstream.
- 13A "How to print" opens a panel that slides in from the side, with the preview still visible.
- 14A Help panel mockup approved (docs/mockups/2026-11-08-help-panel.html): it covers the settings on the left.
- 15A Vowel points (niqqud): leave them. They appear only if someone types them.
- 16A Start Phase 4, and stop for approval just before the push.
- 17A Commit the help panel and the mixed-name fix now.
- 18A Go live: commit the workflow and review fixes, turn on GitHub Pages, push the branch, merge to `main`.

## What the website does (first version)
- Same items: keychain, pencil top, bookmark, desk plate, cup marker, wine glass marker.
- Names list, all item settings, colours, Arrange (rows, columns, order), 3D preview.
- **Download 3MF** (two parts, two colours, opens in Bambu Studio by double-click) and **Download STL**.
- Settings remembered in the browser.
- Upload your own font file (kept in the browser).
- Not on the website: Send to Bambu (a browser cannot open Bambu), crash log.

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
- [x] New project NameTagGenerator.Core (no WPF): Geometry, Items, file writers. Verify: builds; the EXE still builds. Result: Debug and Release both build with 0 warnings.
- [x] Letters to outlines with a font reader that works in a browser (SixLabors.Fonts), replacing the Windows text drawing. Test first: Hebrew and English names, right-to-left order, bold, connector. Verify: outline sizes match the old ones within 0.1 mm. Result: all 17 fonts and Arial match within 0.1 mm, regular and bold, except Righteous, about 0.2 mm tighter (it applies a kerning pair Windows skipped). Fonts with no bold file copy Windows' fake bold. The old outliner is kept in the tests as the yardstick.
- [x] Move the tests to the shared code. Verify: all tests pass. Result: 1309 in NameTagGenerator.Core.Tests (plain .NET, no WPF) and 37 Windows-only in NameTagGenerator.Tests. The sweep's Arial cases were dropped because the shared tests have only bundled fonts; Arial is still in the parity tests.
- [x] EXE uses the shared code. Verify: same 3MF volumes as v0.1.3 for each item; Bambu headless check clean. Result: volumes within 1% of the old Windows outlines, for 4 sample font and name pairs per item; Bambu headless: all 6 items and an array load with 2 parts and no mesh repairs. Not yet opened by hand in the app window.

Changes the owner will notice: a letter the chosen font does not have now comes from Rubik, not a Windows font, in both the EXE and the website.

### Phase 3: The website
- [x] Blazor WebAssembly project, page laid out like the mockup, from the same design tokens. Verify: runs locally. Result: Release (trimmed) build served locally and driven in headless Chrome: all 6 items build, Alphabetical runs on an en-US browser with no error, no console errors, no sideways scroll at phone width, light and dark checked. About 3.5 MB compressed.
- [x] 3D preview with three.js, name side up, same colours. Verify: screenshot check. Result: checked in headless Chrome with software 3D: Hebrew reads correctly, clicking a name line switches the preview, colours redraw, drag turns the view, All names shows the plate, Alphabetical runs from the top right. three.js 0.170 is copied into the site (no CDN). Not tried on a real graphics card or by touch.
- [x] Download 3MF and STL from the browser. Verify: downloaded 3MF passes the Bambu headless check. Result: all 6 items downloaded from the browser; Bambu headless: 2 parts each, no mesh repairs. Downloads hold every name, placed as arranged.
- [x] Remember settings in the browser; Hebrew right-to-left everywhere. Verify: `rtl-check`, reload keeps settings. Result: Release build in headless Chrome: names, item, font, size, Arrange and colours all survive a reload; damaged saved data gives the defaults with no console error. `rtl-check` found nothing high. Fixed from it: Hebrew names isolated in the warning line; invisible direction marks removed from names. Still to look at in `ui-check`: mixed Hebrew and English lines in the names box, Hebrew points in the 3D letters.
- [x] Upload your own font file; a damaged file shows a message and changes nothing. Verify: uploaded font draws a name, is still there after reload. Result: in headless Chrome the list entry opens the file picker; a bad file shows the message and changes nothing; Arial uploaded, drew the names, and was still chosen and listed after a reload. Stored in IndexedDB, 20 MB limit. A font with a built-in font's name is listed as "Name (yours)". There is no way to remove an uploaded font yet.
- [x] Short help panel: how to open the file in Bambu Studio, and library credits (Clipper2, LibTessDotNet, SixLabors.Fonts, three.js). Verify: read through. Result: built as the approved mockup. In headless Chrome: opens and closes by link, × and Esc; focus moves in and back; settings behind it can't be reached; preview doesn't move; fills the screen at phone width. `ui-check` passed the page and panel in light and dark, desktop and phone, and found one high bug, now fixed: mixed Hebrew and English names drew one language backwards (also in the EXE, shared code). Names box alignment is right for mixed lines.

### Phase 4: Publish
- [x] GitHub Actions: build and publish to GitHub Pages on every push to main. Verify: site live at https://eladgl-dot.github.io/3d-name-tag-generator/ Result: first run (766693c) built and deployed; the site is live. Workflow actions print a Node 20 deprecation notice, still working.
- [x] Check in Chrome, Edge and on a phone width. Verify: `ui-check`. Result: live site in headless Chrome and Edge, desktop and phone width, light and dark: no failed requests, no site console errors, no sideways scroll, Hebrew and mixed names correct, help panel opens and closes, names survive reload. Low items for later: a line of page text peeks out below the desktop help panel; phone title wraps to three lines; links, chips and tabs are under 44 px for touch. Not checked: real phone, touch, real graphics card, Safari or iPhone.
- [x] `code-reviewer` before calling it done. Verify: high/med findings fixed. Result: no high findings. Fixed, each reproduced first in headless Chrome: a download right after typing used the old names; one unreadable stored font lost all the others; storage that never answers left the page on Loading (now gives up after 5 s). Site tested under /3d-name-tag-generator/ as GitHub will serve it: 9 checks pass. Not fixed, cannot be tested on Windows: "Upload a font file" may not open the picker in Safari or on iPhone. Low items left for later: preview call in `Rebuild` catches only JS errors; `Repair` does not limit saved numbers; EXE fonts inside `.ttc` collections may pick the wrong face; odd characters in download names; `main` still reachable by Tab behind the phone help panel; .NET runtime missing from credits; `src/NameTagGenerator/result.json` is tracked; workflow has no test step.

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
