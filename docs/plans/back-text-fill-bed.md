# Candle back text, Fill the bed, 3MF objects

Asked 26 Nov 2026. Website and EXE both (shared code in `NameTagGenerator.Core`).

## 1. Candle back text
- New optional text box "Back text" on the candle panel. Empty = no back text, the candle is exactly as today.
- Same back text on every candle.
- It is a third part ("Back text", filament 3) sunk into the bottom of the base: it starts on the print bed (z = 0) and goes up to Back depth. The base has the same shape cut out, so the bottom stays flat and flush.
- Mirrored, so it reads the right way when you turn the candle over.
- Runs along the body like the name, kept clear of the ring hole and the flame, with the same side rim.
- Body length = the longer of the name and the back text, plus the end margins (still at least Shortest body). The Longest body shrink rule then applies to whichever is longer.
- Needs: a text setting (today settings hold only numbers), a third optional mesh in `ItemModel`, and the 3MF/STL/preview/volume code to include it.
- Bambu note: the first layer then has two colours. That is normal for Bambu, but the back text must be at least 2 layers deep to show well.

## 2a. Arrange: "Fill the bed"
- New shape in the Arrange menu: "Fill the bed".
- Packs the tags as tightly as it can, turning each one 90° when that fits better, keeping the Gap.
- When a bed is full it starts bed 2, bed 3 and so on. The preview shows the beds side by side, with "Bed 1", "Bed 2" labels.
- The problem line "Does not fit the plate" goes away for this shape; instead the status says "3 beds".
- In the 3MF each bed becomes a Bambu plate (Bambu reads plates from `Metadata/model_settings.config`). Has to be checked headless in Bambu.
- STL has no plates: beds 2+ are placed to the right of bed 1 with a gap.

## 2b. 3MF: one object or separate objects?
Yes, it is possible. Today the whole plate is one object with two parts (Base, Letters). New choice:
- **One object**: as today (one object per bed when there are several beds, since one object cannot sit on two plates).
- **Separate objects**: each tag is its own object, with its own parts (Base, Letters, Back text). In Bambu each one can be moved, turned or deleted alone, and Bambu's own Arrange works on them.

## Questions
1. Back text size?
   - A. Same letter size as the name (70% of width), shrinking with it. Simple, one look. **(recommended)**
   - B. Its own "Back letter size (%)" setting, so a long dedication can be smaller than the name.
2. Back text depth (how far it goes into the base, base is 2 mm)?
   - A. 0.6 mm (3 layers at 0.2), a setting "Back depth (mm)". **(recommended)**
   - B. 1 mm.
   - C. Full base height (the text goes all the way through to the top colour).
3. Back text: font?
   - A. Same font as the name. **(recommended)**
   - B. Its own font choice.
4. Fill the bed: when may it turn a tag 90°?
   - A. Whenever that packs better (candles and tags mixed angles on the same bed). **(recommended)**
   - B. Only all-or-nothing: the whole bed turned or not, so every tag faces the same way.
5. How do you choose One object / Separate objects?
   - A. A choice next to the download/export button, remembered between uses (website: two-option switch above "Download 3MF"; EXE: in the Export dialog). **(recommended)**
   - B. A question pop-up every time you export.
6. Default for that choice?
   - A. Separate objects (easier to move around in Bambu). **(recommended)**
   - B. One object, as today.
7. When done and checked: push to `main` so the live site updates?
   - A. Yes, after checks and `code-reviewer`, no extra approval.
   - B. No, stop on the branch and show me first. **(recommended, since this changes the 3MF for every item)**

## Owner answers
- 1A Back text uses the same letter size as the name.
- 2A Back depth 0.6 mm, as a setting "Back depth (mm)".
- 3A Same font as the name.
- 4A Turn whenever it packs better; owner does not mind any angle. Built with 0° and 90° only: the tags are close to rectangles, so other angles leave more empty space, not less.
- 5A Switch next to Download 3MF (website) / in the Export dialog (EXE), remembered.
- 6A Default Separate objects. Owner asked: can a single object be split in Bambu? Answer: Bambu's "Split" cuts by loose pieces, not by tag, so letters would come apart from their base. Use Separate objects when you want to move tags.
- 7A Push to `main` after checks and `code-reviewer`, no extra approval.
- 8 (answered "5. A", read as 8A) Mockup approved, plus: a toggle "Flip back text" to turn the back text end over end (reads from the flame down or from the ring up).

## Steps
- [x] 0. New branch `back-text-fill-bed` from `main`.
- [x] 1. Mockup of the new controls: [2026-11-26-back-text-fill-bed.html](../mockups/2026-11-26-back-text-fill-bed.html). Approved 26 Nov, plus a Flip back text toggle.
- [x] 2. Tests first: back text is its own closed part, flush with the bottom, cut out of the base, mirrored, inside the body, clear of the ring; body length follows the longer text; empty back text = today's candle unchanged.
- [x] 3. Build back text (text setting, third mesh, STL/3MF/preview/volume). Website + EXE panels. Result: 15 tests in `CandleBackTests.cs` pass. "Flip back text" toggle added. In the preview the back text shows from below, in the letter colour.
- [x] 4. Tests first: Fill the bed packing (no overlaps, gap kept, all inside the bed, turned tags, spill to bed 2, one tag bigger than the bed reported).
- [x] 5. Build Fill the bed + preview of several beds (website `preview.js`, EXE `PreviewBuilder`). Result: 10 tests in `FillBedTests.cs` pass. Turns 0° or 90° only. Website labels "Bed 1, Bed 2"; EXE draws the beds without labels (status says "N beds").
- [x] 6. 3MF: separate objects, one plate per bed. Bambu headless check: number of objects, parts, filaments, plates, no repairs. Result: 11 tests in `ThreeMfExportTests.cs`. Bambu headless (`--arrange 0`, 200 mm bed to match its default printer): 40 candles with back text kept as 4 plates 11/11/11/7 at our positions, parts Base 1 / Letters 2 / Back text 3, 0 repairs; One object = one object per plate. Found: a fake "Bambu project" marker crashes Bambu, so the file stays a plain 3MF; the plate list works without it. Not verified: Bambu's window (GUI) may offer to arrange on open.
- [x] 7. Export choice on the website and in the EXE Export dialog. Remembered (website and EXE settings). Send to Bambu uses the same choice. Help text updated in both.
- [x] 8. Website check in private headless Chrome, `rtl-check` (Hebrew back text), `code-reviewer`, help text, docs, commit.
  - `ui-check` website and EXE: all four features work. Fixed: preview now refits when the model grows or the bed count changes; darker Bed labels; switch as tall as the buttons; hint text follows the switch; help wording "One object per bed"; Quick start lists the candle; spacing in the Export dialog.
  - `rtl-check`: fixed Back text box flow direction in the EXE, isolation marks for Hebrew names in the EXE warning line, control characters stripped from 3MF names. Added tests for mixed Hebrew+English back text and XML characters in names.
  - `code-reviewer` (no high findings). Fixed: a tag alone on a bed is never turned; a base too thin for the pocket gets no back part (at least 0.2 mm stays above it); back text has no connector between words; EXE single-name preview centred.
  - Final: full suite 1903 + 37 pass. Bambu headless on the final code: 30 candles with flipped mixed back text on 2 plates (16 + 14), 0 repairs. Website Release publish clean.
  - Left as is: Bambu's window may offer to arrange on open (not checked); positions assume Plate width/depth match the printer in Bambu; EXE beds have no "Bed N" labels; Fill the bed can leave empty space on bed 1 when later tags are smaller.

**Tools I'll use:** `dev-practices` skill for test-first (low), `build-runner` agent for build and tests (low), Bambu Studio headless for the 3MF plates (low), `ui-check` agent for website and EXE screenshots (med), `rtl-check` agent for Hebrew back text (low), `code-reviewer` agent before merging (med), `doc-sync` agent for docs (low).
