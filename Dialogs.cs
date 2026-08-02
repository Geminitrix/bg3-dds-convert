using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace DDS_Convert;

public enum OverwriteAnswer
{
    Yes,
    No,
    YesToAll,
    NoToAll,
}

/// <summary>Asks the user how to proceed when a destination file already exists.</summary>
public sealed class OverwriteDialog : Form
{
    public OverwriteAnswer Answer { get; private set; } = OverwriteAnswer.No;

    public OverwriteDialog(string destinationPath)
    {
        Text = Loc.T("overwrite.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        Font = Theme.Body;
        Icon = Theme.AppIcon;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(24, 22, 24, 22),
        };

        var header = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 18),
        };

        var icon = new Label
        {
            Text = "⚠",
            Font = new Font("Segoe UI", 26f),
            ForeColor = Color.FromArgb(210, 150, 40),
            AutoSize = true,
            Margin = new Padding(0, 0, 16, 0),
        };

        var textStack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };

        var fileLine = new Label
        {
            Text = Loc.F("overwrite.fileExists", Path.GetFileName(destinationPath)),
            Font = Theme.BodyBold,
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            Margin = new Padding(0, 2, 0, 2),
        };

        var folderLine = new Label
        {
            Text = Path.GetDirectoryName(destinationPath) ?? "",
            Font = Theme.Small,
            ForeColor = Theme.TextSecondary,
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            Margin = new Padding(0, 0, 0, 10),
        };

        var question = new Label
        {
            Text = Loc.T("overwrite.question"),
            AutoSize = true,
            MaximumSize = new Size(340, 0),
            Margin = new Padding(0),
        };

        textStack.Controls.Add(fileLine);
        textStack.Controls.Add(folderLine);
        textStack.Controls.Add(question);

        header.Controls.Add(icon, 0, 0);
        header.Controls.Add(textStack, 1, 0);

        var buttonGrid = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(356, 0),
        };
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        void AddButton(string text, OverwriteAnswer result, Color? back, int col, int row)
        {
            var button = new Button
            {
                Text = text, Dock = DockStyle.Fill, Height = 34, Margin = new Padding(3), AutoEllipsis = true,
            };
            Theme.StyleButton(button, back, back != null ? Theme.White : null);
            button.Click += (_, _) => { Answer = result; DialogResult = DialogResult.OK; Close(); };
            buttonGrid.Controls.Add(button, col, row);
        }

        AddButton(Loc.T("overwrite.yes"), OverwriteAnswer.Yes, Theme.Accent, 0, 0);
        AddButton(Loc.T("overwrite.yesToAll"), OverwriteAnswer.YesToAll, Theme.Accent, 1, 0);
        AddButton(Loc.T("overwrite.no"), OverwriteAnswer.No, null, 0, 1);
        AddButton(Loc.T("overwrite.noToAll"), OverwriteAnswer.NoToAll, null, 1, 1);

        root.Controls.Add(header);
        root.Controls.Add(buttonGrid);
        Controls.Add(root);

        CancelButton = null; // force an explicit choice
    }
}

/// <summary>Minimal "About" dialog with app version and credits.</summary>
public sealed class AboutDialog : Form
{
    public AboutDialog()
    {
        Text = Loc.T("about.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(360, 190);
        Font = Theme.Body;
        Icon = Theme.AppIcon;

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(2) ?? "1.0";

        var title = new Label { Text = "BG3 DDS Convert", Font = Theme.Title, Location = new Point(20, 20), AutoSize = true };
        var subtitle = new Label { Text = Loc.T("banner.subtitle"), Font = Theme.Subtitle, ForeColor = Theme.TextSecondary, Location = new Point(20, 48), AutoSize = true };
        var versionLabel = new Label { Text = Loc.F("about.version", version), Location = new Point(20, 78), AutoSize = true };
        var credits = new Label { Text = "Created By Lumox and Bert", Location = new Point(20, 100), AutoSize = true };
        var texconv = new Label
        {
            Text = Converter.TexconvPresent ? Loc.T("about.texconvFound") : Loc.T("about.texconvMissing"),
            ForeColor = Converter.TexconvPresent ? Theme.SuccessDark : Theme.Danger,
            Location = new Point(20, 124),
            AutoSize = true,
        };

        var closeButton = new Button { Text = Loc.T("about.close"), Size = new Size(90, 30), Location = new Point(250, 148), DialogResult = DialogResult.OK };
        Theme.StyleButton(closeButton, Theme.Accent, Theme.White);

        Controls.Add(title);
        Controls.Add(subtitle);
        Controls.Add(versionLabel);
        Controls.Add(credits);
        Controls.Add(texconv);
        Controls.Add(closeButton);
        AcceptButton = closeButton;
    }
}

/// <summary>Scrollable in-app usage guide, replacing the old README.txt as the primary source of instructions.</summary>
public sealed class HelpDialog : Form
{
    public HelpDialog()
    {
        Text = Loc.T("help.title");
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(640, 620);
        MinimumSize = new Size(520, 400);
        Font = Theme.Body;
        Icon = Theme.AppIcon;

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(24, 20, 24, 20),
            BackColor = Theme.White,
        };

        void AddTitle(string text) => content.Controls.Add(new Label
        {
            Text = text, Font = Theme.Title, ForeColor = Theme.TextPrimary, AutoSize = true, Margin = new Padding(0, 0, 0, 2),
        });

        void AddHeader(string key) => content.Controls.Add(new Label
        {
            Text = Loc.T(key), Font = Theme.SectionHeader, ForeColor = Theme.Accent, AutoSize = true, Margin = new Padding(0, 16, 0, 4),
        });

        void AddBody(string key) => content.Controls.Add(new Label
        {
            Text = Loc.T(key), Font = Theme.Body, ForeColor = Theme.TextPrimary, AutoSize = true, MaximumSize = new Size(580, 0), Margin = new Padding(0, 0, 0, 2),
        });

        AddTitle("BG3 DDS Convert");
        content.Controls.Add(new Label
        {
            Text = Loc.T("banner.subtitle"),
            Font = Theme.Subtitle, ForeColor = Theme.TextSecondary, AutoSize = true, Margin = new Padding(0, 0, 0, 12),
        });

        AddBody("help.overview");

        AddHeader("help.step1.header");
        AddBody("help.step1.body");

        AddHeader("help.step2.header");
        AddBody("help.step2.body");

        AddHeader("help.step3.header");
        AddBody("help.step3.body");

        AddHeader("help.step4.header");
        AddBody("help.step4.body");

        AddHeader("help.step5.header");
        AddBody("help.step5.body");

        AddHeader("help.assetTypeIntro.header");
        AddBody("help.assetTypeIntro.body");

        AddHeader("help.uiIcon.header");
        AddBody("help.uiIcon.body");

        AddHeader("help.ccIcon.header");
        AddBody("help.ccIcon.body");

        AddHeader("help.atlas.header");
        AddBody("help.atlas.body");

        AddHeader("help.tips.header");
        AddBody("help.tips.body");

        var closeButton = new Button { Text = Loc.T("about.close"), Size = new Size(90, 30), DialogResult = DialogResult.OK };
        Theme.StyleButton(closeButton, Theme.Accent, Theme.White);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom, Height = 50, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(20, 10, 20, 10),
        };
        footer.Controls.Add(closeButton);

        Controls.Add(content);
        Controls.Add(footer);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }
}

/// <summary>Friendly "support the developer" dialog with a PayPal donation link.</summary>
public sealed class DonateDialog : Form
{
    const string PayPalEmail = "lumox.gemini@gmail.com";

    public DonateDialog()
    {
        Text = Loc.T("donate.title");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;
        ShowInTaskbar = false;
        Font = Theme.Body;
        Icon = Theme.AppIcon;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // A single non-docked root panel, like OverwriteDialog - mixing Dock (Top/Bottom) with
        // AutoSize on a child of an AutoSize Form is unreliable in WinForms and previously made
        // this dialog compute a much-too-narrow width. Every panel below relies purely on AutoSize.
        var root = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(28, 24, 28, 22),
        };

        var heading = new Label
        {
            Text = Loc.T("donate.heading"),
            Font = Theme.Title, ForeColor = Theme.TextPrimary, AutoSize = true,
            MaximumSize = new Size(340, 0), Margin = new Padding(0, 0, 0, 12),
        };

        var message = new Label
        {
            Text = Loc.T("donate.message"),
            Font = Theme.Body, ForeColor = Theme.TextSecondary, AutoSize = true, MaximumSize = new Size(340, 0), Margin = new Padding(0, 0, 0, 20),
        };

        var paypalButton = new Button
        {
            Text = Loc.T("donate.button"),
            Size = new Size(340, 46),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 0, 14),
            Font = new Font(Theme.BodyBold.FontFamily, 10.5f, FontStyle.Bold),
        };
        Theme.StyleButton(paypalButton, Color.FromArgb(0, 112, 186), Theme.White); // PayPal brand blue
        paypalButton.Click += (_, _) => OpenDonateLink();

        var closeButton = new Button { Text = Loc.T("donate.later"), Size = new Size(340, 30), Margin = new Padding(0), DialogResult = DialogResult.OK };
        Theme.StyleButton(closeButton);

        root.Controls.Add(heading);
        root.Controls.Add(message);
        root.Controls.Add(paypalButton);
        root.Controls.Add(closeButton);

        Controls.Add(root);
        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    static void OpenDonateLink()
    {
        try
        {
            var url = "https://www.paypal.com/cgi-bin/webscr?cmd=_donations"
                + "&business=" + Uri.EscapeDataString(PayPalEmail)
                + "&currency_code=USD"
                + "&item_name=" + Uri.EscapeDataString("Support BG3 DDS Convert");
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.F("donate.error", ex.Message), Loc.T("donate.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
