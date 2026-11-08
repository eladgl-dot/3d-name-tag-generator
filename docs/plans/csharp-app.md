# 3D name tag generator: C# app

## Goal
A single, very small Windows EXE called "3D name tag generator" that turns Hebrew names into 3D-printable items. It has a rotatable 3D preview before anything is saved, built-in STL export (no OpenSCAD), a "Send to Bambu Studio" button, a Help menu and a logo. New item types must be easy to add and try.

Background: [app-overview.md](../app-overview.md). Mockup: [main window](../mockups/2026-11-07-main-window.html). Logo: `assets/logo.svg`, `assets/logo.ico`.

## Status
| Item | Value |
|---|---|
| Status | v0.1.3 (cup marker inner and outer grip depth set separately; version in the window title), Arrange menu and the 3 new items built, cup marker redesigned. Cup marker printed by the owner and looks great. Waiting for the owner: other real prints |
| Output | `dist/3D name tag generator.exe`, about 700 KB, one file |

## Questions (waiting for answers)
Website questions are in [website.md](website.md).

## Owner answers
Answered 2026-11-07.
- First-round Q1: A, .NET 10 single file. Must also support other PCs (asked what happens without .NET 10).
- First-round Q2: A, 3MF with base and letters as separate parts.
- First-round Q3: A, but exporting shows a pop-up to make sure the user wants this.
- First-round Q4: Yes to one class per item. "Give me 3 ideas" for new items (see Questions above).
- First-round Q5: Mockup approved.
- Fonts populate from the PC. Also include some unique Hebrew fonts from this PC, like Gan CLM, as part of the app.
- Names must be arrangeable in an array in 3D, not only a list.

Feature list: all approved ones may go in version 1.
- 1 Two-line tags: yes
- 2 Drag the ring: yes
- 3 Maximum width: **no**. Letter size stays constant, and the tag grows with the number of characters.
- 4 Plate layout: yes
- 5 Printability check: yes
- 6 Saved presets: yes
- 7 Filament and cost estimate: yes, with PETG as the default
- 8 Icons: yes, emojis
- 9 Engraved and cut-out text: yes, but must be 2 colours
- 10 Proof sheet: no need

Request: build the prototype app, and make a plan with phases for the next builds.

Second round, 2026-11-07, after trying v0.1:
- Which new items first: A (desk name plate) and C (cup marker), plus a wine glass marker.
- Who gets the app: A, own PCs and people the owner knows.
- Send to Bambu Studio works.
- Bug: "Insufficient memory" pop-up after the program was open for a few hours.
- Request: an option to change the two colours, so the user sees how it will look.

Third round, 2026-11-07:
- Colour mockup: A, build it.
- After the colours: A, the three new items next.
- Cup marker: A, adjustable rim thickness, default 3 mm.
- Feedback with a screenshot of 3 bookmarks: "i do not like the gaps between the letters nor the gap in אבי".
- Request: array on the plate should have a button on the top panel that pops up a small menu to rearrange the models.

Fourth round, 2026-11-07:
- Arrange menu mockup: A, build it in the bar above the preview, and remove "Array on the plate" from the side panel.
- Gaps between the letters: A, that was the blue pockets, now filled. No letter spacing setting.
- Cup marker: "i think it will fall - not a good design". Needs a redesign.
- Cup marker redesign: A, saddle along the rim. Pictures: [cup marker options](../mockups/2026-11-07-cup-marker.html).
- After the first cup marker print: fit is too loose (1C). Fix by entering a smaller Rim thickness and printing again. Commit and push to GitHub with the EXE (2A). Stop for now (3C).
- Owner (2026-11-08): let the inner and outer wall heights be set separately. Done in v0.1.3.
- Sharing (2026-11-08): turn it into a website instead of EXE + PDF. 1A web version from the same C# code (Blazor WebAssembly), keep the EXE. 2A make this repo public after font licences and README. 3A same 4 fonts. Plan: [website.md](website.md). PDF on hold.

## Decisions
From the owner:
- No OpenSCAD. Real 3D preview before saving. Send to Bambu Studio button. As small as possible, single EXE.
- English interface, with Hebrew support. App name "3D name tag generator". Logo and Help menu.
- Constant letter size.

Made during the build:
- Bundled fonts: only the 4 with redistributable licences (Gan CLM, MigdalHaemeq, Noot, Suez One). Commercial fonts (Guttman, Microsoft) stay usable only where already installed.
- UI scales with the Windows "Text size" setting, which is 181% on this PC.
- Save pop-up appears only when there are several names, because one name has no choice to make. The Bambu pop-up also appears whenever Bambu Studio is already open.
- The STL holds base and letters as two touching closed bodies. The 3MF keeps them as two parts.
- First name in an array goes top right, following Hebrew reading order.
- Removed from the old app: `.scad` export, Gemini, "Reverse text".
- Arrange menu: one Gap setting replaces Gap across and Gap down, following the mockup. The menu sits at the preview's right edge, not under its button as in the mockup, so it does not cover the names.
- Shape buttons keep working as names are added. Order changes the plate only, not the names list.
- Cup marker prints name side down and mirrored, with flush letters and no supports. The preview turns it name side up; files stay in print position. Default grip 30 mm of rim on an 80 mm cup. Inner and Outer grip depth (8 mm each) set each wall separately; an old saved Grip depth carries over to both. The groove is the rim plus 0.2 mm.

## Build 0.1: Prototype (done)
- [x] .NET 10 WPF single-file publish. Verified: one EXE, 657 KB
- [x] Logo in the EXE and window. Verified in the title bar
- [x] Hebrew text to outlines, correct order. Verified by test: first letter on the right
- [x] Closed meshes. Verified: 216 tests, including every item with every bundled font, 6 names, solid and hollow
- [x] Keychain, pencil top, bookmark as one class each, listed automatically. Verified by tests and screenshots
- [x] Real 3D preview: rotate, zoom, pan, two colours. Verified by screenshots; mouse dragging not exercised by a person yet
- [x] Array: columns, auto-fit, gaps, plate size, plate shown, warning when too big. Verified by tests and screenshots
- [x] Fonts from the PC, Hebrew first, 4 built in, Add font file. Listing and built-in fonts verified; Add font file not exercised yet
- [x] Help menu with 8 topics and F1. Verified one topic opens from the menu
- [x] Save STL or 3MF, with the pop-up for several names. Pop-up verified; file writing verified by tests; the file dialogs not exercised by a person yet
- [x] 3MF with two parts on two filaments. Verified: Bambu Studio's own loader reports 2 parts and zero mesh errors for all items and an array
- [x] Send to Bambu Studio button. Verified by the owner: it works
- [x] Partial features: loose-part warning, solid PETG weight, remembers settings

## Build 0.1.1: Memory error and preview colours
- [x] Try to reproduce the memory error. Not reproduced: 1,500 rebuilds and 3,000 rotations stayed flat at about 120 MB, with no handle growth
- [x] Crash log with the full error and memory figures. Verified: 5 tests, and a real error wrote `crash.log`
- [x] On a memory error, the preview switches to software drawing and the app keeps going. Verified with a thrown error: one pop-up, new model drawn. Whether it cures the real failure is unknown
- [x] Check the real app sitting idle. Verified over 9 minutes: 171 MB throughout, handles did not grow, no CPU use. A multi-hour idle run was not done
- [x] Colour choice for base and letters, preview only, remembered. Mockup approved: [preview colours](../mockups/2026-11-07-preview-colours.html). Verified: palette, swap and Any colour dialog
- [x] Fill gaps between letters, on every item, with a setting. Verified: tests on the owner's 3 names, picture compared
- [x] Bookmark clip joined solidly under every name, with slits starting at the same distance below it. Verified: tests and picture. Clip length now counts from under the name
- [x] Refit the view when the item or the This name / All names view changes. Found by `ui-check`. Item switching ran, but the zoom fix was not seen after a manual zoom
- [x] Mouse wheel over a closed list no longer changes it. Found by `ui-check`. Verified by a direct test; panel scrolling past the list not tried by a person
- [x] Arrange button and pop-up menu in the bar above the preview. Mockup approved: [arrange menu](../mockups/2026-11-07-arrange-menu.html). Verified: 444 tests, and `ui-check` on the real window for shapes, Columns box, order, "This name" while sorted, and all ways of closing. After `ui-check`, the menu moved to the preview's right edge with the model beside it. That was seen with one name only; the owner has not tried it yet

## Build 0.2: Tag design
- [ ] Two-line tags on one base, like the photo. Verify: tests plus compare with the photo
- [ ] Drag the ring in the preview, including the small corner loop. Verify: position saved, tag stays one piece
- [ ] Emojis beside the name. Verify: several emojis give closed meshes
- [ ] Custom connector text. Verify: test

## Build 0.3: Two-colour styles
- [ ] Engraved inlay: letters set flush into the base, in the second colour. Verify: parts fit with no overlap, closed meshes
- [ ] Cut-out: letters cut through the base with a second-colour backing. Verify: closed meshes, Bambu check
- [ ] Style choice per item. Verify: each item with each style

## Build 0.4: Production
- [ ] Plate layout: printer presets, spill onto more plates, each plate as a Bambu plate. Verify: long list splits correctly
- [ ] Full printability check: thin walls and weak ring, shown on the model. Verify: test shapes trigger each warning
- [ ] Saved presets. Verify: save, load, survive restart
- [ ] Filament settings and cost, PETG default, price per kg. Verify: compare with a Bambu slice

## Build 0.5: New items
- [~] Desk name plate with a built-in stand. Tests and Bambu check pass. Still to do: a real print, to see it stands up
- [x] Cup marker: rebuilt as a saddle along the rim (owner chose A). Tests and Bambu check pass (2 parts, no mesh errors). Owner printed it on 2026-11-07: "printed and looks great!"
- [~] Wine glass marker that clips around the stem. Tests and Bambu check pass. Still to do: a real print on a real stem

## Build 1.0: Release
- [ ] Try it on a PC without .NET 10, and confirm the install prompt
- [ ] Licence files for fonts and libraries
- [ ] `code-reviewer` pass, `ui-check` at 100% text size and 150% DPI
- [ ] Version number and short user guide

## Tools I'll use
| Tool | Type | Why | Cost hint |
|---|---|---|---|
| `dev-practices` | skill | Tests first for geometry | low |
| `build-runner` | agent | Builds and tests with short output | low |
| `gui-polish` | skill | UI rules | low |
| `app-runner` | skill | Launches the app and leaves it open | low |
| `ui-check` | agent | Screenshot check of layout and Hebrew | med |
| `rtl-check` | agent | Scan for Hebrew/RTL problems | low |
| `code-reviewer` | agent | Fresh review before release | med |
| `checkpoint` | skill | Saves progress at stopping points | low |
| `project-helper` | skill | Overview page, rebuilt automatically | low |

Libraries: Clipper2 (outlines and cut-outs) and LibTessDotNet (triangles). The 3D view is WPF's built-in `Viewport3D`.

## Test plan
- Automatic: `dotnet test` covers watertight meshes, volume, sizes, holes, Hebrew order, fonts, array layout and file formats.
- Bambu check: a headless Bambu Studio with its own data folder reloads each sample and reports parts and mesh errors. It never touches an open Bambu window.
- By hand: rotate the preview, Save dialogs, Add font file, Send to Bambu, one real two-colour print.

## Risks / open questions
- **Bambu already open:** whether a sent file opens in a new window or the open project depends on Bambu's own setting. The app warns first. Not tested, to protect an unsaved project.
- **Memory error:** the cause is not proven. The error message is a true out-of-memory error, the PC had 12.5 GB of RAM free when checked afterwards, and normal use does not leak. Leading suspect: graphics memory, because the app was open across sleep and wake with Fusion 360 and Bambu Studio on the same card. `crash.log` will show the real source if it happens again.
- **Other PCs:** the missing-.NET prompt comes from Windows' .NET launcher and has not been tried on a clean PC.
- **100% text size:** this PC runs at 181%, so the default size has not been seen. Changing a Windows setting was not an option.
- **Real prints:** the cup marker has been printed and looks great (owner, 2026-11-07). The other new items have not been reported printed. Fit of the pencil hole needs a real pencil. The cup marker's groove fits one cup diameter, so how much it tolerates on other cups is still unknown.
- **Pencil top hole:** seen indirectly (volume tests and mesh checks), not yet looked at from the end in the preview.

## Future features
Not yet started. Numbers match the owner's list. Item 3 and item 10 were declined.
1. **Two-line tags:** Build 0.2.
2. **Drag the ring:** Build 0.2.
4. **Plate layout:** array is done, the rest is Build 0.4.
5. **Printability check:** loose parts done, the rest is Build 0.4.
6. **Saved presets:** Build 0.4.
7. **Filament and cost:** weight done, the rest is Build 0.4.
8. **Emojis:** Build 0.2.
9. **Engraved and cut-out, two colours:** Build 0.3.

## Future items to try
- Fridge magnet with a magnet pocket
- Luggage tag with a strap slot
- Door sign with screw holes
- Cable label that clips onto a cable
