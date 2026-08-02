using System;
using System.Drawing;
using System.Windows.Forms;

namespace DDS_Convert;

/// <summary>
/// Central place for colors, fonts and spacing so every dialog and panel in the app
/// looks consistent. Change a value here and it updates everywhere it's used.
/// </summary>
internal static class Theme
{
    // ---- Palette --------------------------------------------------------
    public static readonly Color Accent = Color.FromArgb(45, 125, 210);
    public static readonly Color AccentDark = Color.FromArgb(30, 95, 165);
    public static readonly Color Success = Color.FromArgb(60, 160, 90);
    public static readonly Color SuccessDark = Color.FromArgb(45, 130, 70);
    public static readonly Color Warning = Color.FromArgb(255, 244, 214);
    public static readonly Color WarningBorder = Color.FromArgb(230, 190, 100);
    public static readonly Color WarningText = Color.FromArgb(140, 100, 10);
    public static readonly Color Danger = Color.FromArgb(200, 70, 70);
    public static readonly Color PanelBackground = Color.FromArgb(246, 247, 249);
    public static readonly Color Border = Color.FromArgb(216, 219, 224);
    public static readonly Color TextPrimary = Color.FromArgb(35, 38, 45);
    public static readonly Color TextSecondary = Color.FromArgb(110, 116, 128);
    public static readonly Color White = Color.White;

    // ---- Fonts ------------------------------------------------------------
    // Cached as static fields (not created on every access) to avoid piling up
    // GDI font handles - each control that uses these shares the same instance.
    public static readonly Font Title = new("Segoe UI Semibold", 12f, FontStyle.Regular);
    public static readonly Font Subtitle = new("Segoe UI", 9f, FontStyle.Regular);
    public static readonly Font SectionHeader = new("Segoe UI Semibold", 9.5f, FontStyle.Regular);
    public static readonly Font Body = new("Segoe UI", 9f, FontStyle.Regular);
    public static readonly Font BodyBold = new("Segoe UI Semibold", 9f, FontStyle.Regular);
    public static readonly Font Mono = new(FontFamily.GenericMonospace, 9f);
    public static readonly Font Small = new("Segoe UI", 8f, FontStyle.Regular);

    // ---- Spacing ------------------------------------------------------------
    public const int PagePadding = 12;
    public const int RowGap = 8;
    public const int ControlHeight = 26;

    // ---- App icon -----------------------------------------------------------
    // Shared across the main window and every dialog so they all show the same
    // taskbar/title-bar icon instead of the generic WinForms default.
    public static readonly Icon? AppIcon = LoadAppIcon();

    static Icon? LoadAppIcon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { return null; }
    }

    /// <summary>Applies a flat, modern look to a button, with subtle hover/press feedback.</summary>
    public static void StyleButton(Button b, Color? back = null, Color? fore = null)
    {
        var baseColor = back ?? Color.FromArgb(233, 235, 239);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Lighten(baseColor, 0.12);
        b.FlatAppearance.MouseDownBackColor = Darken(baseColor, 0.12);
        b.BackColor = baseColor;
        b.ForeColor = fore ?? TextPrimary;
        b.Font = BodyBold;
        b.Cursor = Cursors.Hand;
        b.UseVisualStyleBackColor = false;
    }

    static Color Lighten(Color c, double amount) => Blend(c, Color.White, amount);
    static Color Darken(Color c, double amount) => Blend(c, Color.Black, amount);

    static Color Blend(Color c, Color target, double amount)
    {
        int r = c.R + (int)((target.R - c.R) * amount);
        int g = c.G + (int)((target.G - c.G) * amount);
        int b = c.B + (int)((target.B - c.B) * amount);
        return Color.FromArgb(c.A, Clamp(r), Clamp(g), Clamp(b));
    }

    static int Clamp(int v) => Math.Max(0, Math.Min(255, v));
}
