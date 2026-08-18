# Recipe: writing `metadata.lsf.lsx` to match the converted images

This document is the recipe for doing that. Everything below was measured from 19 real
`metadata.lsf.lsx` files — Larian's own (Shared, SharedDev, Gustav, GustavX, Game, PhotoMode,
ModBrowser), four community mods, and one BG3 Toolkit-generated mod — **20,132 entries in total**.
Where a claim has a number attached, that is the count behind it.

---

## 1. Where the file goes

The metadata sits in the **GUI folder itself — the parent of `Assets`**, not inside it:

```
Data\Mods\<ModName>\GUI\
├── metadata.lsf.lsx        <- this file
├── Assets\                 <- AssetsPath
│   └── ClassIcons\hotbar\MyMod_MyClass.DDS
└── AssetsLowRes\           <- AssetsLowResPath
    └── ClassIcons\hotbar\MyMod_MyClass.DDS
```

**It can be deduced from `AssetsPath` and should be.** `AppConfig.AssetsPath` already points at the
`Assets` root, and `Locate BG3 GUI Folder` already fills both paths from the same GUI folder. So:

```csharp
/// The mod's metadata file, deduced from the full-res destination.
/// Returns null when AssetsPath is not an "...\GUI\Assets" shape, in which case ask the user.
public static string? DeduceMetadataPath(string assetsPath)
{
    if (string.IsNullOrWhiteSpace(assetsPath)) return null;
    var dir = new DirectoryInfo(assetsPath.TrimEnd(Path.DirectorySeparatorChar));
    if (!string.Equals(dir.Name, "Assets", StringComparison.OrdinalIgnoreCase)) return null;
    var gui = dir.Parent;
    if (gui == null) return null;
    return Path.Combine(gui.FullName, "metadata.lsf.lsx");
}
```

Deduce, then **show the resolved path in the UI and let the user override it**. Do not deduce
silently: a user who has pointed `AssetsPath` at a staging folder rather than at a real mod tree
would otherwise get a metadata file written somewhere they never look, and the symptom (icons
missing in game) points nowhere near the cause.

Guard on the leaf being named `Assets` rather than on the parent being named `GUI`. Both hold in
every sample, but the `Assets` leaf is the one the tool itself controls.

> ⚠️ **Deduce from `AssetsPath`, never from `AssetsLowResPath`.** Nothing in the metadata refers to
> the low-res tree at all (see §3), and the two paths are independently editable in this app — a
> user can point them at different mods. `AssetsPath` is the one the keys are relative to.

---

## 2. The file's shape

Five nesting levels, then one `Object` node per icon:

```xml
<?xml version="1.0" encoding="utf-8"?>
<save>
    <version major="4" minor="8" revision="0" build="500"/>
    <region id="config">
        <node id="config">
            <children>
                <node id="entries">
                    <children>
                        <node id="Object">
                            <attribute id="MapKey" type="FixedString" value="Assets/ClassIcons/hotbar/MyMod_MyClass.png"/>
                            <children>
                                <node id="entries">
                                    <attribute id="h" type="int16" value="140"/>
                                    <attribute id="mipcount" type="int8" value="1"/>
                                    <attribute id="w" type="int16" value="140"/>
                                </node>
                            </children>
                        </node>
                        <!-- ... one Object per icon ... -->
                    </children>
                </node>
            </children>
        </node>
    </region>
</save>
```

Note `region id="config"` contains `node id="config"` — the region and the node genuinely share the
name, which looks like a copy-paste error and is not one. And `entries` appears at two different
depths: once as the container of all `Object` nodes, once as the payload inside each one.

**`<version>` for a new file: `major="4" minor="8" revision="0" build="500"`.** That is current
(Patch 8); 11 of the 19 samples still carry the older `4.0.9.319`, which is what those files were
authored against and is not a reason to write it now. **When upserting, leave whatever version the
existing file has alone** — it describes the document, and rewriting it claims something about a
file you only added one node to.

### Attribute order and formatting are free

Two Larian convention is

| | `h`/`w` order | `mipcount` |
|---|---|---|
| BG3 Toolkit, Larian | `h`, `mipcount`, `w` (alphabetical) | present |

Write alphabetical with `mipcount` present, matching the toolkit —
but **when upserting, do not reformat entries you did not touch.** A modder diffing their file
should see the one icon they added, not 300 reordered lines.

Indentation in the wild is tabs (LSLib's output). Match the surrounding file when upserting; for a
new file either is fine.

---

## 3. What one entry says

### `MapKey` — the trap

```
Assets/ClassIcons/hotbar/MyMod_MyClass.png
```

Four rules, each of which is a way to get this wrong:

1. **Always prefixed `Assets/`** — 20,132 of 20,132. Even though it is a path fragment, the prefix
   is part of the key.
2. **Always the `.png` extension**, even though the file on disk is `.DDS` — 20,128 of 20,132. The
   key names the *source* image, not the converted one.
3. **Forward slashes**, not `\`. `AssetProfileSpec.Subfolder` stores Windows separators
   (`ClassIcons\hotbar`), so this needs converting.
4. **The extension case is `.png` regardless of the `UpperCaseExtension` setting.** That option
   governs the DDS file on disk and must not reach the key.

```csharp
/// GUI-relative key for one converted icon. Always Assets/, always /, always .png.
static string MapKeyFor(string subfolder, string finalName)
{
    var folder = (subfolder ?? "").Replace('\\', '/').Trim('/');
    var name = Path.GetFileNameWithoutExtension(finalName);
    return folder.Length == 0
        ? $"Assets/{name}.png"
        : $"Assets/{folder}/{name}.png";
}
```

### `w` / `h` — full resolution only

`int16`. **The dimensions of the file written into `Assets`**, after the block-alignment rounding
in `RoundToBlockAlignment` — not the source image's dimensions, and never the low-res ones.

For a file converted with a named profile these are just `ExpectedWidth` / `ExpectedHeight`.
Measured, per folder:

| Folder | Declared | Profile |
|---|---|---|
| `Assets/ClassIcons` | 300×300 (73 of 74) | Class Icon |
| `Assets/ClassIcons/hotbar` | 140×140 (74 of 75) | Class Icon (Hotbar) |
| `Assets/Tooltips/Icons` | 380×380 (1,625) | Tooltip Icon |
| `Assets/ControllerUIIcons/skills_png` | 144×144 (1,624) | Controller Skill Icon |

The two outliers are one `ClassIcons/hotbar` entry at **112×112** — which is the wrong size the
community guides state and that `AssetProfileSpec`'s own comment already calls out as wrong. Real
files disagree with the guides here, and the corpus sides with the real files 74 to 1.

For **Custom / Other**, where the profile has no fixed size (`ExpectedWidth == 0`), take the
dimensions from the converted output: read them back from the written `.DDS` header — the tool
already has `TryGetDdsSize` — rather than from the source image, so the block-alignment rounding is
included. A source at 301px becomes a 300px file, and the metadata must say 300.

### `mipcount` — `1` for everything this tool produces

`int8`. It is the mip level count of the DDS, i.e. exactly the `-m` value in
`AssetProfileSpec.MipArg`, and **all 16 profiles use `"1"`**. So write `1` — but write
`int.Parse(spec.MipArg)` rather than a literal, so a future profile with real mipmaps stays
correct.

Corpus distribution: 14,451 entries at `1`, 5,647 at `8`, 11 at `0`, and a scattering at 5/6/7/9/11.
The 8s are almost entirely `Assets/Portraits` (5,646 of them, all 152×152) — a category this tool
does not convert. Nothing in scope here is anything but `1`.

### There is **no** `AssetsLowRes` entry

**0 of 20,132 keys reference `AssetsLowRes`.** Every key starts `Assets/`. The half-res companion
this tool writes gets **no metadata entry at all** — the game finds it by convention, from the
parallel directory structure, not by lookup.

Confirmed end-to-end on the BG3 Toolkit's own output, which is the closest thing to a reference
implementation available:

```
Assets/ClassIcons/hotbar/MyMod_MyClass.DDS         140x140   <- one metadata entry, 140x140
AssetsLowRes/ClassIcons/hotbar/MyMod_MyClass.DDS    72x72    <- no entry
```

One file on each side, **one** entry in the metadata. (And 72 is `RoundToBlockAlignment(140/2)` =
`((70+2)/4)*4` — this tool's existing rule reproduces the toolkit's number exactly.)

So: **one converted image produces two DDS files and exactly one metadata entry.**

---

## 4. Create or upsert

The file may not exist, may exist with unrelated entries, or may already have an entry for this
exact icon (a re-export after redrawing the art — the common case). All three must work, and the
second must not lose anything.

```
for each converted image:
    key = MapKeyFor(profile.Subfolder, finalName)      // the Assets/... .png key
    w, h = dimensions of the file written to Assets    // NOT AssetsLowRes, NOT the source
    mip  = int.Parse(profile.MipArg)                   // 1

if metadata file does not exist:
    create it with the §2 skeleton, version 4.8.0.500, and one Object per key
else:
    load and preserve the document as-is (version tag, indentation, entry order, unknown nodes)
    for each key:
        if an Object with that MapKey exists  -> replace ONLY its inner <node id="entries">
        else                                  -> append a new Object to the outer entries children
    write back
```

Points that matter:

- **Match on `MapKey` exactly, ordinal, case-sensitive.** `FixedString` keys are matched by the
  engine as written. Case-insensitive matching would merge two keys BG3 treats as distinct, which
  turns a re-export into silent data loss.
- **Replace the inner `entries` node, not the whole `Object`.** Rewriting the `Object` discards
  anything a modder or a future patch put inside it that this tool does not model.
- **Preserve entry order and append at the end.** Sorting on write turns a one-icon change into a
  whole-file diff. Nothing indicates the reader cares about order.
- **Deduplicate the batch on the key before writing**, not on the source path. Two source files in
  one run can land on the same subfolder and final name, and appending twice writes a document with
  a duplicated key — legal XML, undefined lookup.
- **Never delete entries.** An entry whose file is absent is a modder's problem to notice; removing
  entries because the current batch does not mention them would gut the file on a one-icon export.
- **Write the file only if something changed**, so a no-op re-run does not touch the timestamp.

### Round-tripping safely

The safest implementation loads with `XDocument` (which preserves structure) and navigates
`save/region[@id='config']/node[@id='config']/children/node[@id='entries']/children`, rather than
deserialising to a model and re-serialising — that would silently drop everything not modelled and
reformat the whole file.

Write UTF-8 **with BOM** and `encoding="utf-8"` in the declaration: every one of the 19 samples has
both.

### If the container nodes are missing

A metadata file that exists but has no `region id="config"` (or an empty one) is either a different
document or a corrupted one. **Do not repair it silently.** Create the missing container only when
the file is absent entirely; otherwise report it and leave the file alone. A tool that "fixes" a
file it did not understand is how a modder loses work.

---

## 5. `.lsf` vs `.lsx`

The name `metadata.lsf.lsx` means "the LSX serialisation of `metadata.lsf`". All 19 samples carry
an `lslib_meta="v1,bswap_guids"` attribute on `<version>` — the marker LSLib stamps on when it
converts a binary `.lsf` out of a `.pak`. So what actually ships inside a `.pak` is the binary
`metadata.lsf`; the `.lsx` is the working-tree form, and the packer converts it.

**Writing `metadata.lsf.lsx` into the source tree is therefore right** — it is what the modder
edits and what the BG3 Toolkit and LSLib-based packers consume. Do **not** write `lslib_meta`
yourself on a new file; it describes a conversion that did not happen. When upserting, leave an
existing one untouched.

Worth confirming against whichever packer the users of this tool actually run, since that is the
one step of this recipe not measured directly from shipped files.

---

## 6. Summary

| | |
|---|---|
| File | `<GUI>\metadata.lsf.lsx`, sibling of `Assets` — deduce from `AssetsPath`, show it, allow override |
| Key | `Assets/<Subfolder with />/<FinalName>.png` — `.png` always, never `.DDS`, never the low-res tree |
| `w` / `h` | The **full-res** output dimensions, after block alignment |
| `mipcount` | `int.Parse(profile.MipArg)` — `1` for all 16 current profiles |
| Low-res | **No entry.** Found by directory convention |
| Existing file | Upsert by exact `MapKey`; replace the inner `entries` node only; never reorder, reformat, or delete |
| New file | Version `4.8.0.500`, UTF-8 with BOM |
