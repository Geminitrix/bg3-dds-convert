# BG3 DDS Convert

A lightweight, standalone batch conversion tool for Baldur's Gate 3 modders. It converts source images (`.png`, `.jpg`, `.bmp`, `.tga`, `.tif`, `.hdr`, `.dds`) into game-ready `.DDS` files, using the same settings Baldur's Gate 3 itself uses for each type of icon — verified by extracting and inspecting the game's own shipped assets, not guesswork.

No more washed-out, overly bright, or invisible icons caused by mismatched compression settings.

## Features

- **16 game-accurate DDS profiles** — Class Icons, Hotbar Icons, Ability Score Icons, Character Creation icons (abilities, backgrounds, deities, races, legacy resources), Proficiency and Skill Icons, Equipment Slot Icons, Tooltip Icons, and Controller UI Icons — each matched to the real compression format, mip count, and pixel dimensions found in the game's own files.
- **Automatic File Type & folder detection** — drop in an image and the app reads its pixel dimensions; when the size uniquely matches a known category, it picks the right Asset Type and destination subfolder for you.
- **Locate BG3 GUI Folder** — one click finds your mod's `Data\Mods\<ModName>\GUI` folder from your Steam installation (including secondary Steam libraries) and fills in both destination paths.
- **Dual-resolution export** — converts every source image into `Assets` (full resolution) and `AssetsLowRes` (50% downscale) in one pass.
- **One table, fully editable** — every queued file gets its own Subfolder, Final Name, and Asset Type; double-click any cell to edit it, or press `F2` to rename.
- **Naming pattern automation** — name a source file `%Subfolder1%Subfolder2#FinalName.png` and the app fills in its destination subfolder and output name automatically on import.
- **Drag, drop & browse** — add images by dragging them into the window or clicking the drop zone.
- **Sortable columns**, **multi-language interface** (English, Português (Brasil), Deutsch, Français, 中文), and a **built-in Help screen** explaining exactly which Asset Type to pick and why.

## Requirements

None — the published build is fully self-contained, and the required `texconv.exe` utility is bundled in the package.

## Building from source

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "..\Build"
```

See [BUILD.txt](BUILD.txt) for details. `texconv.exe` and its license are bundled via `lib\` and copied to the output automatically.

## Third-party components

This app bundles Microsoft's [DirectXTex](https://github.com/microsoft/DirectXTex) `texconv.exe` (MIT licensed) for the actual DDS compression — see [lib/LICENSE-texconv.txt](lib/LICENSE-texconv.txt).

## Credits

Created by **Lumox** and **Bert**.
