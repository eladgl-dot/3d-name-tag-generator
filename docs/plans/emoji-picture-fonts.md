# Emoji, pictures, font preview, handwriting font

## Goal
Owner's list after the website went live: use emoji in names, import a picture (turned into an outline), see each font in the font list, and add a Hebrew handwriting (כתב) font.

Mockup: [2026-11-09-tag-extras.html](../mockups/2026-11-09-tag-extras.html)

## Questions (waiting for answers)

## Owner answers
- 1A Mockup approved: build it like that.
- 2A Use Gveret Levin as the handwriting font, if it passes the print check.
- 3A The same picture on every name, beside the name.
- 4A Website first, then the EXE as part of build 0.2.
- 5 "update the online site when done": push to `main` once checks and `code-reviewer` pass, with no extra approval step for this work.

## Steps
Typed emoji and the new font come from the shared code, so the EXE gets them when it is rebuilt. The picture controls and the font preview are separate work in each app.

### 1. Handwriting font
- [x] Add the font and its licence to assets/fonts and LICENSES.md, in a new Handwriting group. Verify: print check test, no part thinner than 0.8 mm at 12 mm (the rule used for all fonts); Hebrew samples build closed meshes. Result: Gveret Levin passes the print check and the all-font tests. The EXE lists it under Hebrew until step 6.

### 2. Emoji
- [x] Bundle Noto Emoji (one colour, SIL OFL), made heavier so it prints. Test first: common emoji, emoji with a variation mark, joined emoji (family, skin tone), emoji mixed with Hebrew and English. Verify: closed meshes, correct order, a clear message for any emoji that can't be drawn. Result: all pass, including joined emoji and flags. Noto Emoji draws many emoji as outlines (a star is a ring with the base colour inside). Characters no font has show "Can't draw".
- [x] Emoji button and picker by the names box. Verify: `ui-check`, added at the cursor, Bambu headless check of a tag with emoji. Result: 24 emoji, each passes the print rule on all items; butterfly was dropped for thin wings. Bambu headless: 2 parts, no repairs.

### 3. Picture
- [x] SVG reader in the shared code: paths, basic shapes, transforms, filled areas. Test first with sample files. Verify: outline sizes match the file. Result: also reads strokes, class styles and white parts (they cut away what is beneath). Files with entity definitions are refused.
- [x] Outline tracer in the shared code: black and white grid to smooth outlines, with parts too thin to print removed and reported. Test first. Verify: closed shapes, no part under 0.8 mm.
- [x] Place the picture beside the name on every item. Verify: tests per item, stays attached, no loose parts, Bambu headless check. Result: tests pass on all 6 items, Hebrew and English. Bambu headless: 2 parts, no repairs.
- [x] Website: choose, change and remove a picture, read PNG and JPG in the browser, level slider, remembered between visits, damaged file gives a message and changes nothing. Verify: headless Chrome, `ui-check`, `rtl-check`. Result: headless Chrome checks pass for SVG and PNG. JPG uses the same browser decoder and was not tried separately.

### 4. Font list preview
- [x] Replace the plain list with one that shows the current name in each font, drawn from the print outlines, including uploaded fonts. Keyboard, Esc and phone must work. Verify: `ui-check`, `rtl-check`, typing 30 names stays smooth. Result: works by mouse, arrows and Esc at desktop and phone width. With 30 names one typed letter updates in about 0.6 s on software graphics. "Upload a font file" now opens the picker straight from the click, which should also suit Safari; not tested there.

### 5. Publish
- [ ] `code-reviewer`, fix high and medium findings, push to `main` after owner approval. Verify: live site check with `ui-check`. Review result: fixed a confirmed hang on hostile SVG files (limits on points, shapes, nesting, stroke size and non-finite numbers; 11 new tests), an unhandled error on bad pictures, and a kept-file mix-up when the browser refuses to save. A noisy photo with 20 names takes about 2 s. `ui-check` fixes: long file name no longer breaks the phone layout; emoji picker opens under the names box. Bold Gveret Levin letters were spaced wrongly (found by the parity test), fixed. Low items left: emoji grid has no arrow keys; some SVG features are ignored (`use`, `image`, clip paths, `rgba` colours); no warning when a font lacks a name's letters and Rubik stands in.

### 6. EXE (depends on question 4)
- [ ] Picture controls and font preview in the EXE, then build 0.2. Verify: tests, Bambu headless check, `ui-check` in the app's own window.

## Limits to expect
- Emoji print in one colour, as a solid shape. Detailed ones lose fine lines at small sizes.
- Photos become black and white shapes. Logos and simple drawings work best.
- The emoji font adds about 0.6 MB to the first website visit.

## Risks
- Joined emoji depend on how well SixLabors.Fonts 2.1.3 handles them. Fallback: draw the parts side by side, or show a message.
- Noto Emoji comes as a variable font and its default weight is light. It has to be fixed at a heavier weight with fontTools first, which the OFL licence allows.
- Safari and iPhone cannot be tested from Windows.

## Tools I'll use
| Tool | Type | Why | Cost hint |
|---|---|---|---|
| `dev-practices` | skill | Test first for emoji, SVG reader, tracer | low |
| `build-runner` | agent | Builds and tests, short results | low |
| `gui-polish` | skill | Font list, picker, picture panel | low |
| `ui-check` | agent | Screenshots, layout, contrast | med |
| `rtl-check` | agent | Hebrew in the new controls | low |
| `code-reviewer` | agent | Review before merging | med |
| Bambu Studio headless | local tool | Proves tags with emoji and pictures load cleanly | low |
| fontTools | Python tool | Fixes the emoji font's weight | low |
