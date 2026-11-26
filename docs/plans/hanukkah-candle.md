# Hanukkah candle keychain

## Goal
New item "Hanukkah candle": a keychain shaped like a candle, printed flat. The name runs along the body, a flame sits on top with a big raised inlay, and there is a keyring hole. Two parts (base and raised) for one colour change. Hebrew and English.

Mockup: [2026-11-22-hanukkah-candle.html](../mockups/2026-11-22-hanukkah-candle.html)

## How it fits the app
- Items are found automatically: one new class `CandleItem : IItemType` in `src/NameTagGenerator.Core/Items/CandleItem.cs` appears in the Item list with its own settings panel, preview, STL and 3MF. No new export code.
- Base part = body + flame + ring, extruded 0 to Base height (2 mm). Raised part = name + flame inlay, extruded Base height to Base height + Letter height (1 mm). The existing `TagAreas.Solid` already does this, so the 3MF stays two parts.
- Exception to the project rule "letter size stays constant": for this item only, names longer than the maximum body shrink, as the spec asks.
- The EXE gets the item too (shared code), on its next build.

## Sizing rules
- Letter size = Letter size % × Candle width (70% × 16 = 11.2 mm).
- Name turned 90°. Body length = name length + End margin × 2, at least Shortest body (45), short names centred.
- If that is over Longest body (85): shrink the letters and the width by the same factor so the body is exactly 85, but never below Smallest letters (7 mm). Below the floor the body grows past 85.
- Flame: 1.2 × width wide, 1.3 × width tall above the body, rounded tip (circle 0.18 × width), joint 0.7 × width. Built as the hull of two circles, so the inlay is the same hull with both circles shrunk by Flame rim (1.5 mm).
- Ring: 4 mm hole with a 3 mm wall (position: question 1).

## Settings (defaults)
Common fields kept: Text weight 0, Base height 2, Letter height 1. Hidden for this item: Font size, Outline, Fill gaps (the candle decides them).
New: Candle width 16, Letter size 70 %, Smallest letters 7, Shortest body 45, Longest body 85, End margin 6, Flame rim 1.5, Ring hole 4, Read upward (off).
Hiding common fields needs one small optional addition to `IItemType` (a list of hidden common keys, empty by default), read by the website and the EXE panels.

## Questions
1. Keyring position?
   - A. Round tab below the body, as written. The candle hangs flame down.
   - B. Hole in the body just under the flame, as in your second picture. The candle hangs upright. **(recommended)**
2. How is "letter size" measured, since ל and ק stick out above and below the other letters?
   - A. Same size for every name in a font: 70% covers the font's normal letters, and ל/ק tails get the leftover rim. Each name is checked so no tail ever reaches the edge (letters shrink a little only if one would). **(recommended)**
   - B. Each name's own ink (tails included) fills 70%. Names with ל/ק get smaller letters than names without.
3. Reading direction?
   - A. First letter always by the flame: Hebrew turned one way, English the other. "Read upward" flips both. **(recommended)**
   - B. Same turn for everything: Hebrew reads from the flame down, English reads from the bottom up.
4. Mockup OK to build as shown (apart from the answers above)?
   - A. Yes, build it. **(recommended)**
   - B. Change something first (say what).
5. When done and checked: push to `main` so the live site updates?
   - A. Yes, after the checks and `code-reviewer` pass, no extra approval. **(recommended)**
   - B. No, stop on the branch and show me first.

## Owner answers
- 1 Both: a "Ring position" choice with "Bottom tab" (default) and "Under the flame".
- 2A Same letter size for every name in a font; letters shrink only if a tail would reach the edge.
- 3A First letter always by the flame; "Read upward" flips both.
- 4A Mockup approved.
- 5A Push to `main` once checks and `code-reviewer` pass, no extra approval.

## Steps
- [x] 0. New branch `hanukkah-candle` from `main`.
- [x] 1. Tests first (`tests/`): "נוי", "נויה", "אלכסנדרה", "Ben", a very long name. Check: body length 45 / ≈74 / 85 rule, letters 11.2 mm and shrink floor 7 mm, letters and inlay inside the base with at least 1 mm rim, base is one piece, both parts closed, raised part exactly 2 to 3 mm, flame size ratios, reading direction (first letter nearest the flame), ring hole clear of letters. Result: 35 tests in `tests/NameTagGenerator.Core.Tests/CandleTests.cs`.
- [x] 2. Build `CandleItem` until the tests pass. Result: all 35 pass.
  - Letter size = height of a plain letter (ה, or H for English) in the chosen font, recorded when the name is drawn, so 11.2 mm means the same in every font.
  - Found: tails are big. In Gan CLM, ל rises 34% and ק drops 47% of the letter height. With the 70% rule and a 1.2 mm side rim, names with ל or ק shrink: שלמה 10.1 mm, יעקב 9.2 mm (Heebo, Rubik, Alef: about 10–10.8 mm). Names without tails stay at 11.2 mm.
  - Gan CLM is wide: אלכסנדרה already reaches 85 mm (letters 9.1 mm). In Heebo it is 10.1 mm.
  - Full suite: 1866 pass. The Windows-parity volume test needed the reference shape to get the same direction and letter height (it records neither).
- [x] 3. Hide unused common fields (optional `IItemType` list), website and EXE panels.
- [x] 4. Website: run it, preview the 4 names, download STL and 3MF. Verify: private headless Chrome. Result: candle chip works, Font size / Outline / Fill gaps hidden, נוי/נויה/אלכסנדרה/Ben preview with the first letter by the flame, both ring positions, 3MF and STL download. Help text added on the website and in the EXE.
- [x] 5. Bambu headless check of the 3MF: 2 parts, no repairs. Result: Base (filament 1) and Letters (filament 2), 0 edges fixed.
- [x] 6. `code-reviewer`, help text, docs, commit and push. Review fixes: the hole under the flame stays 1.5 mm from the sides and a rim below the inlay at any width; the name keeps 1 mm from the hole and at least 1.2 mm from the ends; Text weight counted in the length; the candle is never narrower than its side rims; Bold measured on the thickened letter; letter % of 0 is safe. 11 new tests for unusual settings. Full suite 1877 pass, 0 warnings. Re-checked on the final code: website preview and downloads, Bambu 2 parts, 0 repairs.
  - Left as is: a picture added beside the name counts as part of the name, so a tall picture makes the letters smaller (it still fits).

Not verified until a real print: how the flame inlay and the ring hole feel on the printed piece.

## Change 2 (23 Nov 2026): more ring positions
Owner asked for: no keychain hole, and a hole at the top of the flame.
Mockup: [2026-11-23-candle-ring-options.html](../mockups/2026-11-23-candle-ring-options.html)

Plan: "Ring position" gets two new entries after the existing ones, so saved settings keep working: "Top of the flame" and "No hole". Bottom tab stays the default. Tests first (hole inside the base with a wall, inlay and name clear of the hole, one piece, no hole at all for "No hole"), then code, website check, Bambu headless, `code-reviewer`, push.

### Questions
6. Hole at the top of the flame: which shape?
   - A. Hole inside the flame tip: outline unchanged, the yellow inlay wraps round the hole. **(recommended)**
   - B. A small blue loop sticking out above the tip: full yellow flame, candle about 4 mm taller.
   - C. Both, as two separate entries.
7. Push to `main` when done, same rules as before (checks + `code-reviewer`, no extra approval)?
   - A. Yes. **(recommended)**
   - B. No, show me first.

### Owner answers
- 6B Loop above the flame tip, drawn by the owner as a thin ring (picture, 23 Nov): outer about 7–8 mm, hole about 4.5 mm. Built as a 4 mm hole with a 2 mm wall (thinner than 2 mm would break on a keyring), overlapping the tip so it is one piece. List entries: "Top of the flame" and "No hole".
- 7 not answered: build and check everything, stop before pushing and ask.

### Steps
- [x] 7. Tests for the new positions: loop above the tip (2 names), no hole (2 names), list order, 3 unusual-settings cases with the loop.
- [x] 8. Code until they pass. Full suite 1885 pass. Website: loop 19 × 72 mm, no hole 19 × 66 mm for a 45 mm body, both previews correct, 3MF and STL download. Bambu headless: Base (filament 1) and Letters (filament 2), no repairs.
- [x] 9. Website check, Bambu headless, `code-reviewer`, help text, push. Pushed (c3e314c), live site checked: loop 19 × 72 mm, no hole 19 × 66 mm. Review fix: an unknown saved ring value (4, -1, 2.7) now means Bottom tab, as before. Added tests for that, the loop with Text weight and Read upward, the smallest hole, and No hole on a narrow candle. Full suite 1892 pass.

**Tools I'll use:** `dev-practices` skill for test-first (low), `build-runner` agent for build and tests (low), `ui-check` agent for the website screenshots (med), `rtl-check` agent for Hebrew (low), `code-reviewer` agent before merging (med), `doc-sync` agent for docs (low). Bambu Studio headless from the command line (low).
