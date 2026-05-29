# Effect Image Assembler

Unity UGUI editor tool for reconstructing a static UI prefab from:

- one final effect/reference PNG
- one folder of independent PNG cut assets

The Unity editor window imports PNGs as UI sprites, calls the Python/OpenCV solver, then creates a `Canvas + AutoAssembledUI + Image` prefab using top-left pixel coordinates.

## Install

Use either option:

1. Copy this folder into a Unity project's `Packages` folder.
2. Add it through Unity Package Manager with `Add package from disk...` and select this `package.json`.

## Python dependency

The solver needs Python 3 with:

```bash
pip install opencv-python numpy
```

## Use

1. Open Unity.
2. Select `Tools > Effect Image Assembler`.
3. Assign:
   - `Reference Image`: the final PNG effect image.
   - `Search Scope`: where the tool should search for matching sprites.
   - `Sprites Folder`: only required when `Search Scope` is `SelectedFolder`.
   - `Output Prefab Path`: for example `Assets/AutoAssembledUI.prefab`.
4. Click `Generate UGUI Prefab`.

Generated side files are placed next to the prefab:

- `*.matches.json`
- `Library/EffectImageAssembler/<prefab-name>/asset-index.json`
- `*.report.txt`
- `*.composite.png`
- `*.diff.png`

## Search scopes

- `SelectedFolder`: fastest and most deterministic. The tool may convert PNGs in this folder to UI sprites when `Force Sprite Import Settings` is enabled.
- `UiAndArtFolders`: searches existing sprites under `Assets/UI` and `Assets/Art` when those folders exist.
- `EntireAssets`: searches existing sprites under all of `Assets`.

For project-wide modes, the Unity side first exports a sprite index. This lets the solver match sprites from login-specific folders, common UI folders, background folders, and multiple-sprite PNGs without requiring all source images to live in one directory.

The index stores each sprite's Unity `assetPath`, `spriteName`, source texture size, sprite rect, pivot, border, packing tag, and import mode. The Python solver crops multiple sprites from their source PNG using the Unity rect and returns `asset + spriteName`, so the generated prefab can reference the correct sub-sprite.

Matching is image-only. File names, sprite names, folder names, and atlas names are identifiers for loading the final Unity asset; they do not affect candidate scoring. Names like `c_1.png` and `c_2.png` are fine.

## Coordinate rules

- Reference PNG top-left is `(0, 0)`.
- Generated UI elements use top-left anchors and pivots.
- Unity `anchoredPosition` is `(x, -y)`.
- `CanvasScaler.referenceResolution` is the reference PNG size.

## Limits

This first version reconstructs static image composition. It marks unmatched areas, but it does not generate text objects, animation, particles, custom shaders, or interactions.
