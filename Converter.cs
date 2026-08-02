using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DDS_Convert;

public sealed class ConvertResult
{
    public string Source { get; init; } = "";
    public string Dest { get; init; } = "";
    public bool Success { get; init; }
    public bool Skipped { get; init; }
    public string Note { get; init; } = "";
    public string Warning { get; init; } = "";
}

/// <summary>
/// The different DDS configurations BG3's own shipped assets use, verified by extracting and
/// inspecting real files from Game.pak / Icons.pak (patch 8) rather than relying on wiki guidance -
/// see AssetProfileSpec.Profiles for the source of each value. Order matters: this enum is used as
/// a direct index into AssetProfileSpec.Profiles.
/// </summary>
public enum AssetProfile
{
    Custom,
    ClassIcon,
    ClassIconHotbar,
    AbilityScoreIcon,
    CcAbilityIcon,
    CcBackgroundIcon,
    CcDeityIcon,
    CcRaceIcon,
    CcResourceIcon,
    ProficiencyIcon,
    SkillIcon,
    EquipmentSlotIcon,
    TooltipIcon,
    ItemTooltipIcon,
    ControllerSkillIcon,
    ControllerItemIcon,
    ControllerIconBackground,
}

/// <summary>
/// DXGI_FORMAT reference for the values this app cares about, since they're easy to transpose:
/// BC7_TYPELESS = 97, BC7_UNORM = 98, BC7_UNORM_SRGB = 99, BC3_UNORM (DXT5) = 77, BC1_UNORM (DXT1) = 71.
/// (Confirmed against Microsoft's dxgiformat.h and by reading the raw dxgiFormat byte out of a real
/// shipped BG3 icon directly - no patching required: texconv's default "-f BC7_UNORM" output already
/// writes 98, identical to the game's own files.)
/// </summary>
/// <param name="Subfolder">
/// The real subfolder path (relative to both GUI\Assets and GUI\AssetsLowRes - the game mirrors the
/// same structure in both) this category ships in. Empty for the generic "Custom" entry, which never
/// auto-fills a destination.
/// </param>
/// <param name="ExpectedWidth">
/// Full-resolution pixel width/height real shipped files of this category use, 0 for categories with
/// no single fixed size (used both to auto-detect a File Type from a dropped image's dimensions, and
/// to warn - not block - when a chosen File Type doesn't match the actual source image).
/// </param>
public sealed record AssetProfileSpec(string DisplayName, string TexconvFormat, string MipArg, string Subfolder, int ExpectedWidth, int ExpectedHeight)
{
    // Index in this array must match the AssetProfile enum's declaration order (used as a direct index below).
    // Dimensions/paths below were measured directly from real DDS headers extracted from the game's own
    // GUI\Assets tree (patch 8), cross-checked against mod.io/bg3.wiki community guides - several of those
    // guides turned out to state wrong sizes (e.g. ClassIcons\hotbar as 112x112, CC\icons_backgrounds as
    // 300x300), so the real files were trusted over the guides wherever they disagreed.
    public static readonly IReadOnlyList<AssetProfileSpec> Profiles = new[]
    {
        new AssetProfileSpec("Custom / Other (BC7)", "BC7_UNORM", "1", "", 0, 0),

        new AssetProfileSpec("Class Icon", "BC7_UNORM", "1", "ClassIcons", 300, 300),
        new AssetProfileSpec("Class Icon (Hotbar)", "BC7_UNORM", "1", "ClassIcons\\hotbar", 140, 140),
        new AssetProfileSpec("Ability Score Icon", "BC7_UNORM", "1", "AbilityIcons", 184, 184),

        new AssetProfileSpec("CC Ability Icon", "BC7_UNORM", "1", "CC\\icons_abilities", 104, 104),
        new AssetProfileSpec("CC Background Icon", "BC7_UNORM", "1", "CC\\icons_backgrounds", 196, 196),
        new AssetProfileSpec("CC Deity Icon", "BC7_UNORM", "1", "CC\\icons_deities", 196, 196),
        new AssetProfileSpec("CC Race Icon", "BC7_UNORM", "1", "CC\\icons_races", 196, 196),

        // The Character Creation screen's alternate resource icon set (GUI\Assets\CC\icons_resources):
        // verified DXT1, 1 mip, legacy (non-DX10) header - a distinct, simpler format from the BC7 set
        // used everywhere else, despite depicting the same resources (e.g. Bardic Inspiration).
        new AssetProfileSpec("CC Resource Icon (Legacy DXT1)", "BC1_UNORM", "1", "CC\\icons_resources", 128, 128),

        new AssetProfileSpec("Proficiency Icon", "BC7_UNORM", "1", "Shared\\ProficiencyIcons", 60, 60),
        new AssetProfileSpec("Skill Icon", "BC7_UNORM", "1", "Shared_c\\c_SkillsIcons", 60, 60),
        new AssetProfileSpec("Equipment Slot Icon", "BC7_UNORM", "1", "CharacterPanel\\EquipSlots", 100, 100),

        new AssetProfileSpec("Tooltip Icon (Spell/Passive/Skill)", "BC7_UNORM", "1", "Tooltips\\Icons", 380, 380),
        new AssetProfileSpec("Item Tooltip Icon", "BC7_UNORM", "1", "Tooltips\\ItemIcons", 380, 380),

        new AssetProfileSpec("Controller Skill Icon", "BC7_UNORM", "1", "ControllerUIIcons\\skills_png", 144, 144),
        new AssetProfileSpec("Controller Item Icon", "BC7_UNORM", "1", "ControllerUIIcons\\items_png", 144, 144),
        // The background plate behind a controller icon (not the icon artwork itself) - legacy DXT1,
        // same as CC Resource Icon above, despite sharing its 144x144 size with the two entries just above.
        new AssetProfileSpec("Controller Icon Background (Legacy DXT1)", "BC1_UNORM", "1", "ControllerUIIcons\\icon_bg_png", 144, 144),
    };
}

public static class Converter
{
    public static string AppDir =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    public static string TexconvPath
    {
        get
        {
            var inLib = Path.Combine(AppDir, "lib", "texconv.exe");
            return File.Exists(inLib) ? inLib : Path.Combine(AppDir, "texconv.exe");
        }
    }

    public static bool TexconvPresent => File.Exists(TexconvPath);

    // Safety net for a texconv.exe process that never exits (corrupt input, I/O stall, etc.).
    const int ProcessTimeoutMs = 120_000;

    public static ConvertResult Convert(string source, string dest, bool isHalfRes, AssetProfile profile, CancellationToken cancellationToken = default)
    {
        var spec = AssetProfileSpec.Profiles[(int)profile];
        var destDir = Path.GetDirectoryName(dest) ?? AppDir;

        if (!TexconvPresent)
            return new ConvertResult { Source = source, Dest = dest, Note = "texconv.exe not found in lib\\" };

        if (!File.Exists(source))
            return new ConvertResult { Source = source, Dest = dest, Note = "file not found" };

        Directory.CreateDirectory(destDir);

        // Every profile this app produces is a 4x4 block-compressed format (BC7 or BC1), which
        // Direct3D requires to be block-aligned - a source image whose width or height isn't a
        // multiple of 4 (e.g. 301x300) still compresses "successfully" with no complaint from
        // texconv, but the game silently fails to load the resulting texture at runtime (verified:
        // texconv itself warns "Direct3D requires BC image to be multiple of 4 in width & height"
        // when decoding such a file back). Always resolve an explicit, rounded target size instead
        // of letting texconv pass the source's native (possibly unaligned) dimensions through.
        if (!TryGetImageSize(source, out int sourceWidth, out int sourceHeight))
            return new ConvertResult { Source = source, Dest = dest, Note = "failed to read image dimensions" };

        int targetWidth = RoundToBlockAlignment(isHalfRes ? Math.Max(1, sourceWidth / 2) : sourceWidth);
        int targetHeight = RoundToBlockAlignment(isHalfRes ? Math.Max(1, sourceHeight / 2) : sourceHeight);

        // Only checked against the full-res pass - the half-res pass is derived from the same
        // source, so flagging it too would just repeat the same warning a second time per file.
        string sizeWarning = !isHalfRes && spec.ExpectedWidth > 0
            && (sourceWidth != spec.ExpectedWidth || sourceHeight != spec.ExpectedHeight)
            ? $"source is {sourceWidth}x{sourceHeight}, but real \"{spec.DisplayName}\" files are {spec.ExpectedWidth}x{spec.ExpectedHeight} - double-check the File Type"
            : "";

        if (cancellationToken.IsCancellationRequested)
            return new ConvertResult { Source = source, Dest = dest, Note = "cancelled" };

        // texconv's WIC loader tags a PNG as sRGB whenever it has an sRGB chunk, or a gAMA chunk
        // whose value is exactly 45455 - and DirectXTex then silently applies an sRGB->linear
        // transform when compressing that tagged source into a non-sRGB target like BC7_UNORM,
        // even though no -srgb flag was ever passed here. Most art tools (and .NET's own GDI+ PNG
        // encoder) embed one or both of these by default, so this is common, not an edge case.
        // Strip just those two chunks from a temp copy before conversion - verified by round-trip
        // testing that this restores exact pixel values with zero effect on anything else.
        string texconvInput = source;
        string? tempDirToClean = null;
        if (string.Equals(Path.GetExtension(source), ".png", StringComparison.OrdinalIgnoreCase)
            && TryStripSrgbTriggerChunks(source, out var sanitizedPath))
        {
            texconvInput = sanitizedPath;
            tempDirToClean = Path.GetDirectoryName(sanitizedPath);
        }

        try
        {
            var psi = new ProcessStartInfo(TexconvPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppDir,
            };
            psi.ArgumentList.Add("-f"); psi.ArgumentList.Add(spec.TexconvFormat);
            psi.ArgumentList.Add("-m"); psi.ArgumentList.Add(spec.MipArg);
            psi.ArgumentList.Add("-y");
            psi.ArgumentList.Add("-nologo");
            // Deliberately no -srgb/-srgbi/-srgbo: verified by round-tripping a real shipped BG3 icon
            // (decode -> re-encode -> compare decoded pixels) that omitting any colour-space flag
            // reproduces the game's own bytes exactly, while -srgbo visibly brightens the result.
            // Without this, texconv's WIC pipeline processes RGB as premultiplied by alpha during
            // any resize/format conversion, so partially-transparent pixels come out darker than the
            // source - this is unrelated to the colour-space finding above and still needed either way.
            psi.ArgumentList.Add("-sepalpha");
            psi.ArgumentList.Add("-w"); psi.ArgumentList.Add(targetWidth.ToString());
            psi.ArgumentList.Add("-h"); psi.ArgumentList.Add(targetHeight.ToString());
            psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(destDir);
            psi.ArgumentList.Add(Path.GetFullPath(texconvInput));

            Process? proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                return new ConvertResult { Source = source, Dest = dest, Note = "failed to launch texconv.exe: " + ex.Message };
            }

            if (proc == null)
                return new ConvertResult { Source = source, Dest = dest, Note = "failed to launch texconv.exe" };

            string output;
            using (proc)
            using (cancellationToken.Register(() => TryKillProcess(proc)))
            {
                // Read both streams asynchronously *before* waiting for exit - reading them
                // sequentially after WaitForExit can deadlock if a pipe buffer fills up
                // while the process is blocked writing to the other one.
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();

                if (!proc.WaitForExit(ProcessTimeoutMs))
                {
                    TryKillProcess(proc);
                    proc.WaitForExit();
                }

                try
                {
                    output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();
                }
                catch
                {
                    output = "";
                }
            }

            if (cancellationToken.IsCancellationRequested)
                return new ConvertResult { Source = source, Dest = dest, Note = "cancelled" };

            // texconv always writes an upper-case .DDS extension; rename if .dds was asked for.
            // texconvInput always shares source's exact file name (sanitizing only ever changes
            // which directory the file lives in), so this matches regardless of which was used.
            var produced = Path.Combine(destDir, Path.GetFileNameWithoutExtension(texconvInput) + ".DDS");
            if (!File.Exists(produced))
            {
                var firstLine = output.Split('\n').FirstOrDefault(l => l.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
                                ?? output.Trim();
                return new ConvertResult
                {
                    Source = source, Dest = dest,
                    Note = string.IsNullOrWhiteSpace(firstLine) ? "texconv produced no output" : firstLine.Trim(),
                };
            }

            if (!string.Equals(produced, dest, StringComparison.Ordinal))
                RenameExact(produced, dest);

            return new ConvertResult { Source = source, Dest = dest, Success = true, Warning = sizeWarning };
        }
        finally
        {
            if (tempDirToClean != null)
            {
                try { Directory.Delete(tempDirToClean, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    static void TryKillProcess(Process p)
    {
        try
        {
            if (!p.HasExited) p.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited between the check and the kill - not our problem.
        }
    }

    /// <summary>
    /// Rounds to the nearest multiple of 4 (minimum 4, ties round up) so BC7/BC1 block-compressed
    /// output is valid for Direct3D. The game's own half-res icon pairs (e.g. a 300px full-res class
    /// icon's AssetsLowRes companion is 152px, not 148) look like "always round up", but 150 is
    /// exactly equidistant from 148 and 152 - it's a tie, not evidence of ceiling rounding. Nearest
    /// keeps an already-close source (e.g. 301) at 300 instead of pushing it out to 304.
    /// </summary>
    static int RoundToBlockAlignment(int value) => Math.Max(4, ((value + 2) / 4) * 4);

    /// <summary>
    /// Reads just the pixel dimensions of an image. System.Drawing handles the common raster
    /// formats fine, but it cannot open .tga, .dds or .hdr, so those get a small dedicated
    /// header parser instead of failing outright.
    /// </summary>
    public static bool TryGetImageSize(string path, out int width, out int height)
    {
        width = height = 0;
        try
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".tga":
                    return TryGetTgaSize(path, out width, out height);
                case ".dds":
                    return TryGetDdsSize(path, out width, out height);
                case ".hdr":
                    return TryGetHdrSize(path, out width, out height);
                default:
                    using (var img = Image.FromFile(path))
                    {
                        width = img.Width;
                        height = img.Height;
                    }
                    return true;
            }
        }
        catch
        {
            return false;
        }
    }

    static bool TryGetTgaSize(string path, out int width, out int height)
    {
        width = height = 0;
        using var fs = File.OpenRead(path);
        var header = new byte[18];
        if (fs.Read(header, 0, 18) != 18) return false;

        width = header[12] | (header[13] << 8);
        height = header[14] | (header[15] << 8);
        return width > 0 && height > 0;
    }

    static bool TryGetDdsSize(string path, out int width, out int height)
    {
        width = height = 0;
        using var fs = File.OpenRead(path);
        var header = new byte[128];
        if (fs.Read(header, 0, 128) != 128) return false;
        if (header[0] != (byte)'D' || header[1] != (byte)'D' || header[2] != (byte)'S' || header[3] != (byte)' ')
            return false;

        height = BitConverter.ToInt32(header, 12);
        width = BitConverter.ToInt32(header, 16);
        return width > 0 && height > 0;
    }

    static bool TryGetHdrSize(string path, out int width, out int height)
    {
        width = height = 0;
        using var reader = new StreamReader(path);

        string? line;
        bool pastHeader = false;
        while ((line = reader.ReadLine()) != null)
        {
            if (!pastHeader)
            {
                if (line.Length == 0) pastHeader = true;
                continue;
            }

            // Resolution line, e.g. "-Y 512 +X 1024"
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 && int.TryParse(parts[1], out height) && int.TryParse(parts[3], out width))
                return width > 0 && height > 0;

            break;
        }
        return false;
    }

    /// <summary>
    /// Writes a copy of a PNG with its sRGB chunk and any gAMA==45455 chunk removed, into a fresh
    /// temp directory (so the copy can keep the exact original file name - texconv derives its
    /// output name from the input name). Pure metadata surgery: every other byte, including all
    /// IDAT image data, is copied through unchanged, so this can never alter pixel values itself.
    /// Returns false (leaving sanitizedPath equal to source) if neither chunk was found, so the
    /// common case skips creating a temp file entirely.
    /// </summary>
    static bool TryStripSrgbTriggerChunks(string source, out string sanitizedPath)
    {
        sanitizedPath = source;
        try
        {
            var bytes = File.ReadAllBytes(source);
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47)
                return false;

            using var output = new MemoryStream(bytes.Length);
            output.Write(bytes, 0, 8); // PNG signature

            int pos = 8;
            bool strippedAny = false;
            while (pos + 8 <= bytes.Length)
            {
                uint len = ((uint)bytes[pos] << 24) | ((uint)bytes[pos + 1] << 16) | ((uint)bytes[pos + 2] << 8) | bytes[pos + 3];
                string type = Encoding.ASCII.GetString(bytes, pos + 4, 4);
                long chunkTotal = 8L + len + 4L; // length + type + data + CRC
                if (chunkTotal <= 0 || pos + chunkTotal > bytes.Length)
                    break; // malformed/truncated - stop touching the file, copy the remainder verbatim below

                bool drop = type == "sRGB";
                if (type == "gAMA" && len == 4)
                {
                    uint gamaVal = ((uint)bytes[pos + 8] << 24) | ((uint)bytes[pos + 9] << 16) | ((uint)bytes[pos + 10] << 8) | bytes[pos + 11];
                    if (gamaVal == 45455) drop = true; // 1/2.2 to five decimal digits - the sRGB gamma
                }

                if (drop) strippedAny = true;
                else output.Write(bytes, pos, (int)chunkTotal);

                pos += (int)chunkTotal;
                if (type == "IDAT" || type == "IEND") break; // colour-metadata chunks never appear after IDAT
            }

            if (!strippedAny) return false;

            if (pos < bytes.Length) output.Write(bytes, pos, bytes.Length - pos);

            var tempDir = Path.Combine(Path.GetTempPath(), "bg3ddsconv_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            sanitizedPath = Path.Combine(tempDir, Path.GetFileName(source));
            File.WriteAllBytes(sanitizedPath, output.ToArray());
            return true;
        }
        catch
        {
            sanitizedPath = source;
            return false;
        }
    }

    static void RenameExact(string from, string to)
    {
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            var tmp = to + ".casetmp";
            File.Move(from, tmp, overwrite: true);
            File.Move(tmp, to, overwrite: true);
        }
        else
        {
            File.Move(from, to, overwrite: true);
        }
    }
}
