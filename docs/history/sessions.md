# Sessions

- 2026-11-07: Read the old React/OpenSCAD app, wrote plan, mockup and logo, owner approved. Built prototype v0.1: 657 KB WPF EXE with 3D preview, array, 3 items, 4 built-in fonts, Help, Save STL/3MF, Bambu button. 216 tests pass; Bambu's loader reports 2 parts and zero mesh errors. In progress: owner trying it; Send to Bambu not clicked because Bambu had an unsaved project.
- 2026-11-07: Owner tried v0.1: Send to Bambu works, memory pop-up after hours open. Not reproduced by stress or idle runs; added crash.log and a software-drawing fallback (221 tests). Owner chose desk plate, cup marker, wine glass marker. Colour mockup waiting for approval.
- 2026-11-07: Filled base gaps and rebuilt the bookmark join (owner's screenshot). Added preview colours, desk plate, cup marker, wine glass marker; 429 tests, Bambu loader clean. `ui-check` found and I fixed view refit and wheel-changes-font. Arrange menu mockup waiting for approval.
- 2026-11-07: Built the Arrange menu (444 tests, `ui-check` passed); menu moved to the right edge so names stay visible. Owner expects the cup marker to fall off; options drawn, waiting for the choice. Owner's running copy disappeared during testing, cause unknown, no crash log.
- 2026-11-07: Owner chose saddle (A) for the cup marker. Rebuilt it: curved grip, flush letters, prints name side down, preview flips it. 457 tests, Bambu loader clean, not printed.
- 2026-11-07: Owner printed the cup marker: looks great, fit too loose. First commit, pushed to a private GitHub repo with the EXE as a release.
- 2026-11-07: Added the version to the window title, v0.1.2 (462 tests). Published to dist/0.1.2, not committed.
- 2026-11-08: Cup marker inner and outer grip depth set separately, v0.1.3 (467 tests, Bambu check clean). Published to dist/0.1.3, not committed.
- 2026-11-08: 17 built-in fonts (Frank Ruhl Libre dropped, too thin), font licences, README, MIT; repo made public. 1410 tests. Code at v0.1.4, no v0.1.4 EXE built yet.

## 2026-11-08 (website Phase 3 start)
Done: Blazor site in src/NameTagGenerator.Web with the mockup layout, all 6 items, Arrange, three.js preview, Download 3MF and STL. Bambu headless clean for all 6; 1352 tests pass.
In progress: Phase 3 remaining steps (remember settings, font upload, help). Nothing committed.

## 2026-11-08 (website: settings and font upload)
Done: first website commit; settings remembered in the browser; font upload kept in IndexedDB; direction marks removed from names. 1384 tests pass, headless Chrome checks pass on the Release build.
In progress: help panel, waiting on plan question 1. Settings and font upload not committed yet.
