# App overview

The first section describes the old web app from the ZIP, which was read but not run. The second describes the C# app, whose prototype (v0.1) is built. Exactly what works and what is still unverified is in [the plan](plans/csharp-app.md).

## The existing app (in `source_zip/`)
A React page built in Google AI Studio. You enter Hebrew names and choose settings, and it writes an OpenSCAD script. You then open that script in OpenSCAD to get an STL. It has no 3D preview: the right side only shows the script text.

### Tag types
| Type | What it is | How it is built |
|---|---|---|
| Keychain | Name on a base, with a ring hole | Base is the text outline grown by the outline radius. Letters sit on top. A round ring body with a hole through it is added |
| Pencil top | Name that slides onto a pencil | Tall base with a sideways round hole through the whole width, plus an optional bulge around the hole |
| Bookmark | Name on top of a page clip | Thin base with a rounded tongue below, and a U-shaped cut that forms the clip |

### Settings and defaults
| Setting | Keychain | Pencil top | Bookmark |
|---|---|---|---|
| Font | Gan CLM Bold | same | same |
| Font size | 18 | 12.5 | 14 |
| Base height (mm) | 3 | 11 | 1.3 |
| Letter height (mm) | 4 | 1.8 | 0.3 |
| Outline radius (mm) | 3 | 1.6 | 4 |
| Text weight (mm) | 0 | 0 | 0 |
| Vertical spacing (mm) | 40 | 40 | 120 |
| Connector | Hyphen, solid | same | same |
| Ring hole (mm) | 4 | n/a | n/a |
| Ring position | Right | n/a | n/a |
| Ring distance adjust (mm) | -2 | n/a | n/a |
| Pencil hole / outline (mm) | n/a | 7.8 / 10 | n/a |
| Clip width / length (mm) | n/a | n/a | 35 / 90 |
| Clip border / U-cut gap (mm) | n/a | n/a | 3 / 1 |

Each type keeps its own settings when you switch between types.

### Behaviour to keep
- Paste a column from Excel: each row becomes one name. Ctrl+Enter adds.
- Clear the whole list with one click, without a confirmation prompt.
- Two-word names stay on one line, joined by a connector on the base.
- Hollow connector: the connector appears only in the base colour, with no raised letter layer.
- Font dropdown, including any font installed on the PC.

### Problems found in the old code
| Problem | Effect | In the new app |
|---|---|---|
| Hollow mode swaps the connector for spaces in the letter layer | Letters can drift out of line with the base | Same layout used for both layers |
| Your request for an adjustable outline around the hollow hyphen is not in the final code | Setting is missing | Decide during the build, likely a connector outline setting |
| `Reverse text` is needed because OpenSCAD can mishandle Hebrew | Mirrored names are possible (seen in the history screenshot) | Removed, Windows lays out Hebrew correctly |
| Clip border has a default but no control on screen | Cannot be changed | Control added (in the mockup) |
| Sleeve length is set but never used | Dead setting | Removed |
| Setting a clip value to 0 silently restores its default | Surprising result | Real value used |
| Gemini service is present but never called | Unused code and dependency | Removed |
| Prompt history file name contains colons | Windows can't extract it normally | Extracted as `prompt_history.json` |

## The C# app: 3D name tag generator
Status and steps are in [the plan](plans/csharp-app.md). The interface is in English.

### Hebrew and fonts
Names read right-to-left with correct letter order. The font list comes from the PC, with Hebrew fonts first; this PC has 45. Four Hebrew fonts are built into the app so they work everywhere: Gan CLM, MigdalHaemeq, Noot and Suez One. "Add font file" loads any other font and remembers it. Commercial fonts such as Guttman are not bundled, for licence reasons, but appear wherever they are installed.

### Items
Keychain, pencil top and bookmark. Each item is one small class in `src/NameTagGenerator/Items`, with its own settings. A new class appears in the Item list automatically, and its settings panel is generated from it.

### Array
"All names" shows every name on the plate. Set columns (0 = auto), gaps and plate size. The first name goes top right, and a warning appears if the array doesn't fit. Letter size is always constant, so longer names make wider tags.

### Help and logo
A Help menu covers quick start, settings, arrays, Hebrew and fonts, two-colour printing, mouse and keyboard, troubleshooting and about. F1 opens it. The logo is in `assets/`: a blue tag with a keyring hole and raised white "3D".

### How you use it
1. Pick an item and paste the names.
2. Adjust settings. The 3D preview updates, and you rotate it with the mouse.
3. Choose **Save STL** or **Send to Bambu Studio**. Nothing is written until you do.

### How it works
```
Names + settings
  -> Windows text layout (correct Hebrew order, any installed font)
  -> 2D outlines (grow, add ring, cut holes)
  -> 3D mesh (base + letters, closed bodies)
  -> Preview on screen, and the same mesh goes to the file
```
The preview and the file use the same mesh, so what you rotate is what gets printed.

### Bambu Studio
The button writes a temporary 3MF and opens it in Bambu Studio, as one object with two parts: Base on filament 1 and Letters on filament 2. If Bambu is already open, the app warns you first, because the file may open in that window. The app never closes Bambu Studio.

### Size and other PCs
One 657 KB EXE, with no installer, OpenSCAD or Node. It uses the .NET Desktop Runtime. On a PC without it, Windows' .NET launcher should show a message offering the download page; after installing, the app runs. This hasn't been tried on a clean PC yet. It also runs on newer .NET versions.

### Files it keeps
`%LOCALAPPDATA%\3D name tag generator` holds `settings.json` and a `fonts` folder with the built-in and added fonts.
