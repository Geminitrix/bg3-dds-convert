using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DDS_Convert;

public sealed class MainForm : Form
{
    // ---- Menu -------------------------------------------------------------------
    readonly ToolStripMenuItem _fileMenu = new();
    readonly ToolStripMenuItem _addFilesMenuItem = new();
    readonly ToolStripMenuItem _openAssetsMenuItem = new();
    readonly ToolStripMenuItem _openLowResMenuItem = new();
    readonly ToolStripMenuItem _exitMenuItem = new();
    readonly ToolStripMenuItem _helpMenu = new();
    readonly ToolStripMenuItem _howToUseMenuItem = new();
    readonly ToolStripMenuItem _donateMenuItem = new();
    readonly ToolStripMenuItem _aboutMenuItem = new();
    readonly ToolStripMenuItem _languageMenu = new();
    readonly List<ToolStripMenuItem> _languageMenuItems = new();

    // ---- Destination folders -------------------------------------------------
    readonly ComboBox _assetsCombo = new() { DropDownStyle = ComboBoxStyle.DropDown };
    readonly ComboBox _lowResCombo = new() { DropDownStyle = ComboBoxStyle.DropDown };
    readonly Button _browseAssets = new();
    readonly Button _browseLowRes = new();
    readonly Button _locateGuiButton = new();
    readonly GroupBox _destinationGroup = new();
    readonly ToolTip _toolTip = new();

    // ---- Toolbar --------------------------------------------------------------
    readonly ComboBox _extensionCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Label _extLabel = new();
    readonly Button _convertButton = new();
    readonly Button _clearListButton = new();
    readonly ProgressBar _progressBar = new() { Style = ProgressBarStyle.Continuous };

    // ---- File list ----------------------------------------------------------------
    readonly ListView _listView = new();
    readonly ContextMenuStrip _lvContextMenu = new();
    readonly ToolStripMenuItem _removeSelectedMenuItem = new();
    readonly ListViewColumnSorter _sorter = new();
    Control? _inlineEditControl;

    const int ColSubfolder = 0;
    const int ColFinalName = 1;
    const int ColAssetType = 2;
    const int ColSourceFile = 3;
    const int ColExt = 4;
    const int ColStatus = 5;

    static string[] ColumnHeaders => new[]
    {
        Loc.T("col.subfolder"), Loc.T("col.finalName"), Loc.T("col.assetType"),
        Loc.T("col.sourceFile"), Loc.T("col.ext"), Loc.T("col.status"),
    };

    /// <summary>Sorts ListViewItems by the text of one column, toggling ascending/descending on repeat clicks.</summary>
    sealed class ListViewColumnSorter : IComparer
    {
        public int SortColumn = -1;
        public SortOrder Order = SortOrder.None;

        public int Compare(object? x, object? y)
        {
            if (SortColumn < 0 || Order == SortOrder.None) return 0;
            var textX = ((ListViewItem)x!).SubItems[SortColumn].Text;
            var textY = ((ListViewItem)y!).SubItems[SortColumn].Text;
            int result = string.Compare(textX, textY, StringComparison.OrdinalIgnoreCase);
            return Order == SortOrder.Descending ? -result : result;
        }
    }

    // ---- Log --------------------------------------------------------------------
    readonly TextBox _log = new();
    readonly Panel _logPanel = new();
    readonly Button _toggleLogButton = new();
    readonly Label _logTitleLabel = new();
    readonly Button _copyLogButton = new();
    readonly Button _clearLogButton = new();
    bool _logVisible = true;

    // ---- Status bar ---------------------------------------------------------------
    readonly StatusStrip _statusStrip = new();
    readonly ToolStripStatusLabel _statusLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripStatusLabel _itemCountLabel = new();

    // ---- Drop zone -----------------------------------------------------------------
    readonly Label _dropZone = new();
    readonly Panel _texconvWarning = new();
    readonly Label _texconvWarningLabel = new();
    readonly Label _bannerSubtitle = new();

    readonly AppConfig _config = AppConfig.Load();
    CancellationTokenSource? _cts;
    bool _busy;

    public MainForm(string[] initialFiles)
    {
        Loc.Current = Loc.FromCode(_config.Language);

        Text = "BG3 DDS Convert — Hub Studio";
        ClientSize = new Size(980, 780);
        MinimumSize = new Size(880, 640);
        AllowDrop = true;
        Font = Theme.Body;
        BackColor = Theme.White;
        if (Theme.AppIcon != null) Icon = Theme.AppIcon;

        var menuStrip = BuildMenuStrip();
        var topPanel = BuildTopPanel();
        var bottomPanel = BuildBottomPanel();

        BuildListView();
        SetupContextMenus();

        // NOTE: docking order matters. Controls are laid out in the reverse order
        // they were added, so the Fill control is added first (it absorbs whatever
        // space remains) and edge-docked panels are added afterwards, outermost last.
        Controls.Add(_listView);
        Controls.Add(bottomPanel);
        Controls.Add(topPanel);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        FormClosing += OnFormClosing;

        LoadConfigIntoUi();
        ApplyLanguageTexts();

        if (!Converter.TexconvPresent)
            Log("WARNING: texconv.exe was not found in the lib\\ folder. Conversions will fail.");
        else
            Log("Ready. Add files and press Convert. Tip: name a file \"%Sub1%Sub2#FinalName.png\" to auto-fill its subfolder and final name.");

        if (initialFiles.Length > 0)
            AddFiles(initialFiles);
    }

    // ======================================================================
    // Localization
    // ======================================================================

    void SwitchLanguage(AppLanguage lang)
    {
        if (Loc.Current == lang) return;

        Loc.Current = lang;
        _config.Language = Loc.Code(lang);
        _config.Save();

        foreach (var item in _languageMenuItems)
            item.Checked = Loc.NativeName(lang) == item.Text;

        ApplyLanguageTexts();
    }

    /// <summary>
    /// Re-applies every translatable string to its control. Safe to call at startup and again on
    /// every language switch - it only ever sets .Text on already-built controls, it never creates
    /// or reparents anything, so it can't disturb the layout or lose the current file list.
    /// </summary>
    void ApplyLanguageTexts()
    {
        _fileMenu.Text = Loc.T("menu.file");
        _addFilesMenuItem.Text = Loc.T("menu.file.addFiles");
        _openAssetsMenuItem.Text = Loc.T("menu.file.openAssets");
        _openLowResMenuItem.Text = Loc.T("menu.file.openLowRes");
        _exitMenuItem.Text = Loc.T("menu.file.exit");
        _helpMenu.Text = Loc.T("menu.help");
        _howToUseMenuItem.Text = Loc.T("menu.help.howToUse");
        _donateMenuItem.Text = Loc.T("menu.help.donate");
        _aboutMenuItem.Text = Loc.T("menu.help.about");
        _languageMenu.Text = Loc.T("menu.language");

        _bannerSubtitle.Text = Loc.T("banner.subtitle");
        _destinationGroup.Text = Loc.T("dest.groupTitle");
        _browseAssets.Text = Loc.T("dest.browse");
        _browseLowRes.Text = Loc.T("dest.browse");
        _locateGuiButton.Text = Loc.T("dest.locateGui");

        _extLabel.Text = Loc.T("toolbar.outputExtension");
        _convertButton.Text = Loc.T(_busy ? "toolbar.cancel" : "toolbar.convertAll");
        _clearListButton.Text = Loc.T("toolbar.clearList");

        _texconvWarningLabel.Text = Loc.T("warning.texconvMissing");
        ResetDropVisual();

        UpdateSortGlyphs();
        _removeSelectedMenuItem.Text = Loc.T("ctx.removeSelected");

        _logTitleLabel.Text = Loc.T("log.title");
        _copyLogButton.Text = Loc.T("log.copy");
        _clearLogButton.Text = Loc.T("log.clear");
        _toggleLogButton.Text = Loc.T(_logVisible ? "log.hide" : "log.show");
    }

    // ======================================================================
    // Layout construction
    // ======================================================================

    MenuStrip BuildMenuStrip()
    {
        var menu = new MenuStrip { BackColor = Theme.White };

        _addFilesMenuItem.Click += (_, _) => SelectFilesViaDialog();
        _openAssetsMenuItem.Click += (_, _) => OpenFolder(_assetsCombo.Text);
        _openLowResMenuItem.Click += (_, _) => OpenFolder(_lowResCombo.Text);
        _exitMenuItem.Click += (_, _) => Close();
        _fileMenu.DropDownItems.Add(_addFilesMenuItem);
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _fileMenu.DropDownItems.Add(_openAssetsMenuItem);
        _fileMenu.DropDownItems.Add(_openLowResMenuItem);
        _fileMenu.DropDownItems.Add(new ToolStripSeparator());
        _fileMenu.DropDownItems.Add(_exitMenuItem);

        _howToUseMenuItem.Click += (_, _) => new HelpDialog().ShowDialog(this);
        _donateMenuItem.Click += (_, _) => new DonateDialog().ShowDialog(this);
        _aboutMenuItem.Click += (_, _) => new AboutDialog().ShowDialog(this);
        _helpMenu.DropDownItems.Add(_howToUseMenuItem);
        _helpMenu.DropDownItems.Add(new ToolStripSeparator());
        _helpMenu.DropDownItems.Add(_donateMenuItem);
        _helpMenu.DropDownItems.Add(new ToolStripSeparator());
        _helpMenu.DropDownItems.Add(_aboutMenuItem);

        foreach (AppLanguage lang in Enum.GetValues<AppLanguage>())
        {
            var item = new ToolStripMenuItem(Loc.NativeName(lang)) { Checked = lang == Loc.Current };
            item.Click += (_, _) => SwitchLanguage(lang);
            _languageMenuItems.Add(item);
            _languageMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(_fileMenu);
        menu.Items.Add(_languageMenu);
        menu.Items.Add(_helpMenu);
        return menu;
    }

    TableLayoutPanel BuildTopPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Padding = new Padding(0),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(BuildHeaderBanner(), 0, 0);
        layout.Controls.Add(BuildDestinationGroup(), 0, 1);
        layout.Controls.Add(BuildToolbarPanel(), 0, 2);
        layout.Controls.Add(BuildTexconvWarning(), 0, 3);
        layout.Controls.Add(BuildDropZone(), 0, 4);
        return layout;
    }

    Panel BuildHeaderBanner()
    {
        var banner = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Theme.Accent };

        var title = new Label
        {
            Text = "BG3 DDS Convert",
            Font = Theme.Title,
            ForeColor = Theme.White,
            AutoSize = true,
            Location = new Point(Theme.PagePadding, 8),
        };
        _bannerSubtitle.Font = Theme.Subtitle;
        _bannerSubtitle.ForeColor = Color.FromArgb(225, 238, 250);
        _bannerSubtitle.AutoSize = true;
        _bannerSubtitle.Location = new Point(Theme.PagePadding, 32);

        banner.Controls.Add(title);
        banner.Controls.Add(_bannerSubtitle);
        return banner;
    }

    GroupBox BuildDestinationGroup()
    {
        _destinationGroup.Dock = DockStyle.Top;
        _destinationGroup.Font = Theme.SectionHeader;
        _destinationGroup.Padding = new Padding(Theme.PagePadding, 6, Theme.PagePadding, Theme.PagePadding);
        _destinationGroup.Height = 162;

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Font = Theme.Body,
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        _locateGuiButton.Dock = DockStyle.Fill;
        _locateGuiButton.Margin = new Padding(4, 2, 4, 6);
        Theme.StyleButton(_locateGuiButton, Theme.Accent, Theme.White);
        _locateGuiButton.Click += (_, _) => LocateGuiFolder();
        grid.Controls.Add(_locateGuiButton, 0, 0);
        grid.SetColumnSpan(_locateGuiButton, 3);

        AddDestinationRow(grid, 1, "Assets", _assetsCombo, _browseAssets, () => _config.RecentAssetsPaths, () => _config.RememberAssetsPath(_assetsCombo.Text));
        AddDestinationRow(grid, 2, "AssetsLowRes", _lowResCombo, _browseLowRes, () => _config.RecentLowResPaths, () => _config.RememberLowResPath(_lowResCombo.Text));

        _destinationGroup.Controls.Add(grid);
        return _destinationGroup;
    }

    /// <summary>
    /// Tries to auto-detect the installed BG3 mod's GUI folder (Data\Mods\&lt;ModName&gt;\GUI) via the
    /// Steam registry key and libraryfolders.vdf, falling back to a manually-browsed folder if nothing
    /// or more than one candidate is found. Either way, the user gets a chance to correct the result -
    /// this never silently commits to a wrong path.
    /// </summary>
    void LocateGuiFolder()
    {
        List<string> candidates;
        try { candidates = FindCandidateGuiFolders(); }
        catch (Exception ex) { candidates = new List<string>(); Log($"GUI folder auto-detect failed: {ex.Message}"); }

        string chosen;
        if (candidates.Count == 1)
        {
            chosen = candidates[0];
            Log($"Found BG3 mod GUI folder automatically: {chosen}");
        }
        else
        {
            using var fbd = new FolderBrowserDialog { Description = Loc.T("dest.locateGuiDialogDesc") };
            string seed = candidates.Count > 1
                ? Path.GetDirectoryName(Path.GetDirectoryName(candidates[0])) ?? ""
                : FindBg3ModsRootGuess();
            if (!string.IsNullOrEmpty(seed) && Directory.Exists(seed))
                fbd.SelectedPath = seed;

            Log(candidates.Count > 1
                ? $"Found {candidates.Count} mod GUI folders — pick the right one in the dialog that just opened."
                : "Could not auto-detect a BG3 mod GUI folder — please browse to it manually (it opened where a guess seemed reasonable).");

            if (fbd.ShowDialog(this) != DialogResult.OK) return;
            chosen = fbd.SelectedPath;
        }

        SetComboTextSafely(_assetsCombo, Path.Combine(chosen, "Assets"));
        SetComboTextSafely(_lowResCombo, Path.Combine(chosen, "AssetsLowRes"));
        _config.RememberAssetsPath(_assetsCombo.Text);
        _config.RememberLowResPath(_lowResCombo.Text);
        RefreshComboItems(_assetsCombo, _config.RecentAssetsPaths);
        RefreshComboItems(_lowResCombo, _config.RecentLowResPaths);
        SaveConfigFromUi();
        Log($"Destination folders set to {_assetsCombo.Text} and {_lowResCombo.Text}");
    }

    /// <summary>Every mod folder under any detected Steam library's BG3 install that already has a GUI subfolder.</summary>
    static List<string> FindCandidateGuiFolders()
    {
        var results = new List<string>();
        foreach (var modsDir in FindBg3ModsRoots())
        {
            foreach (var modDir in Directory.EnumerateDirectories(modsDir))
            {
                var gui = Path.Combine(modDir, "GUI");
                if (Directory.Exists(gui)) results.Add(gui);
            }
        }
        return results;
    }

    static string FindBg3ModsRootGuess() => FindBg3ModsRoots().FirstOrDefault() ?? "";

    static IEnumerable<string> FindBg3ModsRoots()
    {
        foreach (var steamLibrary in FindSteamLibraryRoots())
        {
            var modsDir = Path.Combine(steamLibrary, "steamapps", "common", "Baldurs Gate 3", "Data", "Mods");
            if (Directory.Exists(modsDir)) yield return modsDir;
        }
    }

    /// <summary>
    /// Every Steam library folder that might contain BG3 - the main install path plus any additional
    /// libraries listed in Steam's own libraryfolders.vdf (e.g. a game installed on a second drive).
    /// </summary>
    static IEnumerable<string> FindSteamLibraryRoots()
    {
        var roots = new List<string>();

        string? installPath = null;
        try
        {
            installPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
        }
        catch { /* registry can be locked down in some environments - fall through to the default guess */ }

        installPath ??= @"C:\Program Files (x86)\Steam";
        if (Directory.Exists(installPath)) roots.Add(installPath);

        var libraryVdf = Path.Combine(installPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(libraryVdf))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(libraryVdf), "\"path\"\\s*\"([^\"]+)\""))
            {
                var path = m.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(path) && !roots.Contains(path, StringComparer.OrdinalIgnoreCase))
                    roots.Add(path);
            }
        }

        return roots;
    }

    void AddDestinationRow(TableLayoutPanel grid, int row, string labelText, ComboBox combo, Button browseButton,
        Func<List<string>> recentPaths, Action rememberPath)
    {
        var label = new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.BodyBold, AutoEllipsis = true };
        combo.Dock = DockStyle.Fill;
        combo.Margin = new Padding(4, 4, 4, 4);
        combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        combo.AutoCompleteSource = AutoCompleteSource.ListItems;
        combo.Items.AddRange(recentPaths().ToArray());

        browseButton.Dock = DockStyle.Fill;
        browseButton.Margin = new Padding(4);
        Theme.StyleButton(browseButton);
        browseButton.Click += (_, _) =>
        {
            using var fbd = new FolderBrowserDialog { Description = Loc.F("dest.folderDialogDesc", labelText) };
            if (!string.IsNullOrWhiteSpace(combo.Text) && Directory.Exists(combo.Text))
                fbd.SelectedPath = combo.Text;

            if (fbd.ShowDialog() == DialogResult.OK)
            {
                combo.Text = fbd.SelectedPath;
                rememberPath();
                RefreshComboItems(combo, recentPaths());
            }
        };

        combo.TextChanged += (_, _) => _toolTip.SetToolTip(combo, combo.Text);
        combo.Leave += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(combo.Text))
            {
                rememberPath();
                RefreshComboItems(combo, recentPaths());
            }
            SaveConfigFromUi();
        };

        grid.Controls.Add(label, 0, row);
        grid.Controls.Add(combo, 1, row);
        grid.Controls.Add(browseButton, 2, row);
    }

    static void RefreshComboItems(ComboBox combo, List<string> recent)
    {
        var current = combo.Text;
        combo.Items.Clear();
        combo.Items.AddRange(recent.ToArray());
        SetComboTextSafely(combo, current);
    }

    Panel BuildToolbarPanel()
    {
        // A FlowLayoutPanel sizes each control to its actual rendered content instead of a
        // hand-picked pixel offset, so a slightly-wider label (different font metrics, DPI, etc.)
        // simply pushes its neighbor over rather than overlapping it.
        var panel = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(Theme.PagePadding, 6, Theme.PagePadding, 6) };

        var leftFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        _extLabel.AutoSize = true;
        _extLabel.Font = Theme.Body;
        _extLabel.Margin = new Padding(0, 9, 6, 0);

        _extensionCombo.Width = 80;
        _extensionCombo.Margin = new Padding(0, 5, 24, 0);
        _extensionCombo.Items.AddRange(new object[] { ".DDS", ".dds" });
        _extensionCombo.SelectedIndex = _config.UpperCaseExtension ? 0 : 1;
        _extensionCombo.SelectedIndexChanged += (_, _) => { _config.UpperCaseExtension = _extensionCombo.SelectedIndex == 0; _config.Save(); };

        _convertButton.Size = new Size(170, 32);
        _convertButton.Margin = new Padding(0, 1, 16, 0);
        Theme.StyleButton(_convertButton, Theme.Success, Theme.White);
        _convertButton.Click += async (_, _) => await OnConvertButtonClicked();

        _progressBar.Size = new Size(170, 20);
        _progressBar.Margin = new Padding(0, 7, 0, 0);
        _progressBar.Visible = false;

        leftFlow.Controls.Add(_extLabel);
        leftFlow.Controls.Add(_extensionCombo);
        leftFlow.Controls.Add(_convertButton);
        leftFlow.Controls.Add(_progressBar);

        var rightFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };

        _clearListButton.Size = new Size(110, 32);
        _clearListButton.Margin = new Padding(0, 1, 0, 0);
        Theme.StyleButton(_clearListButton);
        _clearListButton.Click += (_, _) => ClearList();

        rightFlow.Controls.Add(_clearListButton);

        panel.Controls.Add(leftFlow);
        panel.Controls.Add(rightFlow);
        return panel;
    }

    Panel BuildTexconvWarning()
    {
        _texconvWarning.Dock = DockStyle.Top;
        _texconvWarning.Height = 32;
        _texconvWarning.BackColor = Theme.Warning;
        _texconvWarning.Visible = !Converter.TexconvPresent;
        _texconvWarning.Padding = new Padding(Theme.PagePadding, 0, Theme.PagePadding, 0);

        _texconvWarningLabel.Dock = DockStyle.Fill;
        _texconvWarningLabel.ForeColor = Theme.WarningText;
        _texconvWarningLabel.TextAlign = ContentAlignment.MiddleLeft;
        _texconvWarningLabel.Font = Theme.BodyBold;
        _texconvWarning.Controls.Add(_texconvWarningLabel);
        return _texconvWarning;
    }

    Panel BuildDropZone()
    {
        _dropZone.TextAlign = ContentAlignment.MiddleCenter;
        _dropZone.Font = new Font(Font.FontFamily, 10.5f, FontStyle.Regular);
        _dropZone.ForeColor = Theme.TextSecondary;
        _dropZone.BackColor = Theme.PanelBackground;
        _dropZone.Margin = new Padding(Theme.PagePadding);
        _dropZone.BorderStyle = BorderStyle.None;
        _dropZone.AllowDrop = true;
        _dropZone.Cursor = Cursors.Hand;
        _dropZone.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, _dropZone.ClientRectangle,
            Theme.Border, 2, ButtonBorderStyle.Dashed, Theme.Border, 2, ButtonBorderStyle.Dashed,
            Theme.Border, 2, ButtonBorderStyle.Dashed, Theme.Border, 2, ButtonBorderStyle.Dashed);

        _dropZone.Click += (_, _) => SelectFilesViaDialog();
        _dropZone.DragEnter += OnDragEnter;
        _dropZone.DragDrop += OnDragDrop;
        _dropZone.DragLeave += (_, _) => ResetDropVisual();

        // Wrap in a padded container so the dashed border doesn't touch the panel above/below.
        var wrapper = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(Theme.PagePadding, 6, Theme.PagePadding, 6) };
        _dropZone.Dock = DockStyle.Fill;
        wrapper.Controls.Add(_dropZone);
        return wrapper;
    }

    void BuildListView()
    {
        _listView.Dock = DockStyle.Fill;
        _listView.View = View.Details;
        _listView.FullRowSelect = true;
        _listView.GridLines = true;
        _listView.Font = Theme.Body;
        _listView.AllowDrop = true;
        _listView.HideSelection = false;

        var headers = ColumnHeaders;
        _listView.Columns.Add(headers[ColSubfolder], 180);
        _listView.Columns.Add(headers[ColFinalName], 190);
        _listView.Columns.Add(headers[ColAssetType], 160);
        _listView.Columns.Add(headers[ColSourceFile], 220);
        _listView.Columns.Add(headers[ColExt], 55);
        _listView.Columns.Add(headers[ColStatus], 115);

        _listView.ListViewItemSorter = _sorter;
        _listView.ColumnClick += (_, e) =>
        {
            if (_busy) return;
            CommitInlineEdit();

            _sorter.Order = _sorter.SortColumn == e.Column && _sorter.Order == SortOrder.Ascending
                ? SortOrder.Descending
                : SortOrder.Ascending;
            _sorter.SortColumn = e.Column;
            _listView.Sort();
            UpdateSortGlyphs();
        };

        _listView.SizeChanged += (_, _) =>
        {
            if (_listView.Columns.Count < 6) return;
            int w = _listView.ClientSize.Width;
            _listView.Columns[ColSubfolder].Width = (int)(w * 0.16);
            _listView.Columns[ColFinalName].Width = (int)(w * 0.19);
            _listView.Columns[ColAssetType].Width = (int)(w * 0.17);
            _listView.Columns[ColSourceFile].Width = (int)(w * 0.24);
            _listView.Columns[ColExt].Width = (int)(w * 0.06);
            _listView.Columns[ColStatus].Width = (int)(w * 0.18);
        };

        _listView.MouseDoubleClick += (_, e) =>
        {
            var hit = _listView.HitTest(e.Location);
            if (hit.Item == null || hit.SubItem == null) return;

            int col = hit.Item.SubItems.IndexOf(hit.SubItem);
            if (col == ColSubfolder || col == ColFinalName || col == ColAssetType)
                BeginInlineEdit(hit.Item, col);
        };

        _listView.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) RemoveSelectedItems();
            else if (e.KeyCode == Keys.F2 && _listView.SelectedItems.Count > 0) BeginInlineEdit(_listView.SelectedItems[0], ColFinalName);
        };

        _listView.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Right && _listView.SelectedItems.Count > 0)
                _lvContextMenu.Show(_listView, e.Location);
        };

        _listView.DragEnter += OnDragEnter;
        _listView.DragDrop += OnDragDrop;
    }

    // ======================================================================
    // Inline cell editing (Subfolder / Final Name / Asset Type columns)
    // ======================================================================

    void BeginInlineEdit(ListViewItem item, int columnIndex)
    {
        if (_busy) return;

        CommitInlineEdit();

        var cellBounds = item.SubItems[columnIndex].Bounds;
        Control editor = columnIndex == ColAssetType ? BuildAssetTypeEditor(item) : BuildTextEditor(item, columnIndex);

        editor.Location = cellBounds.Location;
        editor.Size = new Size(Math.Max(cellBounds.Width, 60), cellBounds.Height);
        editor.Tag = (item, columnIndex);
        editor.LostFocus += (_, _) => CommitInlineEdit();

        _listView.Controls.Add(editor);
        _inlineEditControl = editor;
        editor.BringToFront();
        editor.Focus();

        if (editor is TextBox box) box.SelectAll();
        else if (editor is ComboBox combo) combo.DroppedDown = true;
    }

    TextBox BuildTextEditor(ListViewItem item, int columnIndex)
    {
        var box = new TextBox
        {
            Text = item.SubItems[columnIndex].Text,
            BorderStyle = BorderStyle.FixedSingle,
        };
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.Handled = true; e.SuppressKeyPress = true; CommitInlineEdit(); }
            else if (e.KeyCode == Keys.Escape) { e.Handled = true; CancelInlineEdit(); }
        };
        return box;
    }

    ComboBox BuildAssetTypeEditor(ListViewItem item)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = _listView.Font };
        foreach (var spec in AssetProfileSpec.Profiles) combo.Items.Add(spec.DisplayName);
        combo.SelectedIndex = Math.Max(0, combo.Items.IndexOf(item.SubItems[ColAssetType].Text));
        combo.SelectedIndexChanged += (_, _) =>
        {
            // Capture before CommitInlineEdit() disposes this combo (it's the editor being committed).
            int selectedIndex = combo.SelectedIndex;
            CommitInlineEdit();

            if (selectedIndex < 0 || selectedIndex >= AssetProfileSpec.Profiles.Count) return;
            var spec = AssetProfileSpec.Profiles[selectedIndex];
            if (!string.IsNullOrEmpty(spec.Subfolder))
                item.SubItems[ColSubfolder].Text = spec.Subfolder;
        };
        combo.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) { e.Handled = true; CancelInlineEdit(); }
        };
        return combo;
    }

    void CommitInlineEdit()
    {
        var editor = _inlineEditControl;
        if (editor == null) return;
        _inlineEditControl = null; // cleared first so a re-entrant LostFocus during Dispose is a no-op

        var (item, columnIndex) = ((ListViewItem, int))editor.Tag!;
        var text = editor.Text;

        bool valid = columnIndex switch
        {
            ColSubfolder => text.IndexOfAny(Path.GetInvalidPathChars()) < 0,
            ColFinalName => !string.IsNullOrWhiteSpace(text) && text.IndexOfAny(Path.GetInvalidFileNameChars()) < 0,
            _ => true, // Asset Type always comes from the fixed dropdown list
        };

        if (valid)
        {
            item.SubItems[columnIndex].Text = columnIndex == ColSubfolder
                ? text.Trim().Trim('\\', '/')
                : text;
        }

        _listView.Controls.Remove(editor);
        editor.Dispose();
    }

    void CancelInlineEdit()
    {
        var editor = _inlineEditControl;
        if (editor == null) return;
        _inlineEditControl = null;

        _listView.Controls.Remove(editor);
        editor.Dispose();
    }

    void UpdateSortGlyphs()
    {
        var headers = ColumnHeaders;
        for (int i = 0; i < _listView.Columns.Count; i++)
        {
            string arrow = i == _sorter.SortColumn
                ? (_sorter.Order == SortOrder.Ascending ? " ▲" : " ▼")
                : "";
            _listView.Columns[i].Text = headers[i] + arrow;
        }
    }

    Panel BuildBottomPanel()
    {
        var container = new Panel { Dock = DockStyle.Bottom, Height = 190 };

        BuildStatusStrip();

        _logPanel.Dock = DockStyle.Fill;
        BuildLogPanel();

        container.Controls.Add(_logPanel);
        container.Controls.Add(_statusStrip);
        return container;
    }

    void BuildStatusStrip()
    {
        _statusStrip.Dock = DockStyle.Bottom;
        _statusLabel.Text = "Created By Lumox and Bert";
        _itemCountLabel.Text = "";
        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.Items.Add(_itemCountLabel);
    }

    void BuildLogPanel()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.PanelBackground, Padding = new Padding(Theme.PagePadding, 0, 8, 0) };
        _logTitleLabel.Font = Theme.SectionHeader;
        _logTitleLabel.Dock = DockStyle.Left;
        _logTitleLabel.AutoSize = true;
        _logTitleLabel.TextAlign = ContentAlignment.MiddleLeft;
        _logTitleLabel.Margin = new Padding(0, 8, 0, 0);

        // AutoSize buttons in a FlowLayoutPanel instead of a fixed pixel Size - "▾ Hide" and
        // "▸ Show" are different lengths, and a fixed width cramps whichever one is longer.
        var headerButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };

        _copyLogButton.AutoSize = true;
        _copyLogButton.Padding = new Padding(10, 2, 10, 2);
        _copyLogButton.Margin = new Padding(6, 4, 0, 4);
        _clearLogButton.AutoSize = true;
        _clearLogButton.Padding = new Padding(10, 2, 10, 2);
        _clearLogButton.Margin = new Padding(6, 4, 0, 4);
        Theme.StyleButton(_copyLogButton);
        Theme.StyleButton(_clearLogButton);
        _copyLogButton.Click += (_, _) => { if (_log.Text.Length > 0) Clipboard.SetText(_log.Text); };
        _clearLogButton.Click += (_, _) => _log.Clear();

        _toggleLogButton.AutoSize = true;
        _toggleLogButton.Padding = new Padding(10, 2, 10, 2);
        _toggleLogButton.Margin = new Padding(6, 4, 0, 4);
        Theme.StyleButton(_toggleLogButton);
        _toggleLogButton.Click += (_, _) => ToggleLogVisibility();

        // RightToLeft flow starts from the panel's right edge, so the first control added
        // ends up rightmost - matches the original left-to-right order of Copy, Clear, Hide/Show.
        headerButtons.Controls.Add(_toggleLogButton);
        headerButtons.Controls.Add(_clearLogButton);
        headerButtons.Controls.Add(_copyLogButton);

        header.Controls.Add(_logTitleLabel);
        header.Controls.Add(headerButtons);

        _log.Multiline = true;
        _log.ReadOnly = true;
        _log.ScrollBars = ScrollBars.Vertical;
        _log.Dock = DockStyle.Fill;
        _log.Font = Theme.Mono;
        _log.BackColor = Theme.White;
        _log.BorderStyle = BorderStyle.FixedSingle;

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(_log);

        _logPanel.Controls.Add(body);
        _logPanel.Controls.Add(header);
    }

    void ToggleLogVisibility()
    {
        var body = _logPanel.Controls.OfType<Panel>().First(p => p.Dock == DockStyle.Fill);
        body.Visible = !body.Visible;
        _logVisible = body.Visible;
        _toggleLogButton.Text = Loc.T(_logVisible ? "log.hide" : "log.show");
        _logPanel.Parent!.Height = body.Visible ? 190 : 62;
    }

    // ======================================================================
    // Context menu
    // ======================================================================

    void SetupContextMenus()
    {
        _removeSelectedMenuItem.Text = Loc.T("ctx.removeSelected");
        _removeSelectedMenuItem.Click += (_, _) => RemoveSelectedItems();
        _lvContextMenu.Items.Add(_removeSelectedMenuItem);
    }

    void RemoveSelectedItems()
    {
        if (_listView.SelectedItems.Count == 0) return;

        foreach (ListViewItem item in _listView.SelectedItems)
            _listView.Items.Remove(item);
    }

    void ClearList()
    {
        if (_busy) return;
        _listView.Items.Clear();
    }

    // ======================================================================
    // File selection
    // ======================================================================

    void SelectFilesViaDialog()
    {
        if (_busy) return;

        using var ofd = new OpenFileDialog
        {
            Multiselect = true,
            Filter = $"{Loc.T("filedlg.filterImages")}|*.png;*.jpg;*.jpeg;*.bmp;*.tga;*.tif;*.tiff;*.hdr;*.dds|{Loc.T("filedlg.filterAll")}|*.*",
            Title = Loc.T("filedlg.title"),
        };

        if (ofd.ShowDialog(this) == DialogResult.OK)
            AddFiles(ofd.FileNames);
    }

    void AddFiles(string[] paths)
    {
        var existing = _listView.Items.Cast<ListViewItem>()
            .Select(i => (string)i.Tag!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var files = ExpandToSupportedFiles(paths);
        int added = 0, duplicates = 0, patterned = 0, autoDetected = 0;

        foreach (var file in files)
        {
            if (!existing.Add(file)) { duplicates++; continue; }

            var (subfolder, finalName, matched) = ParseNamingPattern(Path.GetFileNameWithoutExtension(file));
            if (matched) patterned++;

            string assetTypeLabel = AssetProfileSpec.Profiles[0].DisplayName;

            // Only guess a File Type from pixel dimensions when the naming pattern above didn't
            // already give an explicit subfolder - an explicit "%Sub#Name" pattern is a stronger
            // signal of user intent than a size match, so it always wins. Only auto-fills when
            // exactly one known category matches that exact size - several real categories share
            // identical dimensions (e.g. CC backgrounds/deities/races are all 196x196), so an
            // ambiguous match is deliberately left for the user to pick by hand instead of guessing.
            if (!matched && Converter.TryGetImageSize(file, out int srcWidth, out int srcHeight))
            {
                var candidates = AssetProfileSpec.Profiles
                    .Where(p => p.ExpectedWidth == srcWidth && p.ExpectedHeight == srcHeight)
                    .ToList();
                if (candidates.Count == 1)
                {
                    assetTypeLabel = candidates[0].DisplayName;
                    subfolder = candidates[0].Subfolder;
                    autoDetected++;
                }
            }

            var item = new ListViewItem(subfolder);
            item.SubItems.Add(finalName);
            item.SubItems.Add(assetTypeLabel);
            item.SubItems.Add(Path.GetFileName(file));
            item.SubItems.Add(Path.GetExtension(file).ToUpperInvariant());
            item.SubItems.Add("Pending");
            item.Tag = file;
            _listView.Items.Add(item);
            added++;
        }

        var details = new List<string>();
        if (duplicates > 0) details.Add($"{duplicates} duplicate(s) skipped");
        if (patterned > 0) details.Add($"{patterned} via naming pattern");
        if (autoDetected > 0) details.Add($"{autoDetected} File Type/subfolder auto-detected by size");
        var suffix = details.Count > 0 ? $" ({string.Join(", ", details)})" : "";
        Log($"Added {added} file(s){suffix}.");
    }

    /// <summary>
    /// Parses the "%Subfolder1%Subfolder2#FinalName" naming convention out of a file name
    /// (without extension), so dropping a pre-named export straight into the app auto-fills its
    /// destination subfolder and output name instead of requiring a manual edit afterwards.
    /// Only triggers when the name starts with '%', so ordinary file names that happen to
    /// contain a '#' are left untouched.
    /// </summary>
    static (string Subfolder, string FinalName, bool Matched) ParseNamingPattern(string rawName)
    {
        if (!rawName.StartsWith('%'))
            return ("", rawName, false);

        int hashIndex = rawName.IndexOf('#');
        if (hashIndex < 0)
            return ("", rawName, false);

        string subfolderPart = rawName[..hashIndex];
        string finalName = rawName[(hashIndex + 1)..];
        if (string.IsNullOrWhiteSpace(finalName))
            return ("", rawName, false);

        var segments = subfolderPart.Split('%', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string subfolder = string.Join('\\', segments);

        return (subfolder, finalName, true);
    }

    static List<string> ExpandToSupportedFiles(IEnumerable<string> paths)
    {
        var supportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".tif", ".tiff", ".hdr", ".dds" };
        var result = new List<string>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                result.AddRange(Directory
                    .EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())));
            }
            else if (File.Exists(path) && supportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()))
            {
                result.Add(path);
            }
        }

        return result;
    }

    // ======================================================================
    // Drag & drop
    // ======================================================================

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (_busy || e.Data?.GetDataPresent(DataFormats.FileDrop) != true)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        e.Effect = DragDropEffects.Copy;
        _dropZone.BackColor = Color.FromArgb(224, 240, 224);
        _dropZone.Text = Loc.T("dropzone.hover");
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        ResetDropVisual();
        if (_busy) return;

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            AddFiles(files);
    }

    void ResetDropVisual()
    {
        _dropZone.BackColor = Theme.PanelBackground;
        _dropZone.Text = Loc.T("dropzone.idle");
    }

    // ======================================================================
    // Conversion
    // ======================================================================

    async Task OnConvertButtonClicked()
    {
        if (_busy)
        {
            _cts?.Cancel();
            _convertButton.Enabled = false; // avoid double-clicks while cancellation is in flight
            return;
        }

        await RunBatchConversion();
    }

    async Task RunBatchConversion()
    {
        string assetsDir = _assetsCombo.Text.Trim();
        string lowResDir = _lowResCombo.Text.Trim();

        if (string.IsNullOrEmpty(assetsDir) && string.IsNullOrEmpty(lowResDir))
        {
            Log("ERROR: fill in at least one destination folder before converting.");
            MessageBox.Show(this, Loc.T("msg.nothingToDo.body"),
                Loc.T("msg.nothingToDo.title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var pendingItems = CollectPendingItems();
        if (pendingItems.Count == 0)
        {
            Log("Nothing to convert — the list is empty or everything is already done.");
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        BeginBusyState(pendingItems.Count * ((string.IsNullOrEmpty(assetsDir) ? 0 : 1) + (string.IsNullOrEmpty(lowResDir) ? 0 : 1)));

        var stopwatch = Stopwatch.StartNew();
        Log("--- Starting batch conversion ---");

        var overwriteState = new OverwriteState();
        var counters = new BatchCounters();
        bool cancelled = false;
        bool upperCaseExt = _extensionCombo.SelectedIndex == 0;

        foreach (var item in pendingItems)
        {
            if (token.IsCancellationRequested) { cancelled = true; break; }

            string sourceFile = (string)item.Tag!;
            string subfolder = item.SubItems[ColSubfolder].Text;
            string fileName = item.SubItems[ColFinalName].Text + (upperCaseExt ? ".DDS" : ".dds");
            AssetProfile profile = ParseAssetProfile(item.SubItems[ColAssetType].Text);
            item.SubItems[ColStatus].Text = "Processing…";
            _listView.EnsureVisible(item.Index);

            int convertedCount = 0, skippedCount = 0, failedCount = 0;

            if (!string.IsNullOrEmpty(assetsDir))
            {
                var outcome = await ConvertOne(sourceFile, assetsDir, subfolder, fileName, profile, isHalfRes: false,
                    overwriteState, counters, token);
                Tally(outcome, ref convertedCount, ref skippedCount, ref failedCount);
                BumpProgress();
            }

            if (!token.IsCancellationRequested && !string.IsNullOrEmpty(lowResDir))
            {
                var outcome = await ConvertOne(sourceFile, lowResDir, subfolder, fileName, profile, isHalfRes: true,
                    overwriteState, counters, token, " [50%]");
                Tally(outcome, ref convertedCount, ref skippedCount, ref failedCount);
                BumpProgress();
            }

            item.SubItems[ColStatus].Text = token.IsCancellationRequested
                ? "Cancelled"
                : failedCount > 0
                    ? "Failed"
                    : convertedCount == 0 && skippedCount > 0
                        ? "Skipped"
                        : "Done";

            UpdateStatus(counters.Done, counters.Skipped, counters.Failed, pendingItems.Count, stopwatch.Elapsed);
        }

        stopwatch.Stop();

        if (cancelled || token.IsCancellationRequested)
            Log($"--- Conversion cancelled — {counters.Done} created, {counters.Skipped} skipped, {counters.Failed} failed ---");
        else
            Log($"--- Conversion finished in {stopwatch.Elapsed:mm\\:ss} — {counters.Done} created, {counters.Skipped} skipped, {counters.Failed} failed ---");
        Log("");

        EndBusyState();
    }

    /// <summary>Tracks "Yes to All" / "No to All" overwrite choices across an entire batch run.</summary>
    sealed class OverwriteState
    {
        public bool OverwriteAll;
        public bool SkipAll;
    }

    /// <summary>Running totals across an entire batch run (mutable reference so async helpers can update it without ref parameters).</summary>
    sealed class BatchCounters
    {
        public int Done;
        public int Skipped;
        public int Failed;
    }

    /// <summary>Gathers every list item that isn't already marked "Done".</summary>
    List<ListViewItem> CollectPendingItems()
    {
        var pending = new List<ListViewItem>();
        foreach (ListViewItem item in _listView.Items)
        {
            if (item.SubItems[ColStatus].Text != "Done")
                pending.Add(item);
        }
        return pending;
    }

    enum ConvertOutcome { Converted, Skipped, Failed }

    static AssetProfile ParseAssetProfile(string label)
    {
        var profiles = AssetProfileSpec.Profiles;
        for (int i = 0; i < profiles.Count; i++)
            if (profiles[i].DisplayName == label) return (AssetProfile)i;
        return AssetProfile.Custom;
    }

    async Task<ConvertOutcome> ConvertOne(string sourceFile, string baseDir, string subfolder, string fileName, AssetProfile profile, bool isHalfRes,
        OverwriteState overwriteState, BatchCounters counters, CancellationToken cancellationToken, string logSuffix = "")
    {
        try
        {
            string destDir = Path.Combine(baseDir, subfolder);
            Directory.CreateDirectory(destDir);
            string destPath = Path.Combine(destDir, fileName);

            if (!ShouldOverwrite(destPath, ref overwriteState.OverwriteAll, ref overwriteState.SkipAll))
            {
                counters.Skipped++;
                Log($"  skipped    {Path.GetFileName(destPath)}");
                return ConvertOutcome.Skipped;
            }

            ConvertResult result = await Task.Run(() => SafeConvert(sourceFile, destPath, profile, isHalfRes, cancellationToken), cancellationToken);
            if (result.Success)
            {
                counters.Done++;
                Log($"  OK{logSuffix}        {Path.GetFileName(sourceFile)}  ->  {destPath}");
                if (!string.IsNullOrEmpty(result.Warning))
                    Log($"    WARNING: {result.Warning}");
                return ConvertOutcome.Converted;
            }

            counters.Failed++;
            Log($"  FAILED{logSuffix}    {Path.GetFileName(sourceFile)}  :  {result.Note}");
            return ConvertOutcome.Failed;
        }
        catch (OperationCanceledException)
        {
            return ConvertOutcome.Failed;
        }
        catch (Exception ex)
        {
            counters.Failed++;
            Log($"  FAILED{logSuffix}    {Path.GetFileName(sourceFile)}  :  {ex.Message}");
            return ConvertOutcome.Failed;
        }
    }

    static void Tally(ConvertOutcome outcome, ref int converted, ref int skippedCount, ref int failedCount)
    {
        switch (outcome)
        {
            case ConvertOutcome.Converted: converted++; break;
            case ConvertOutcome.Skipped: skippedCount++; break;
            case ConvertOutcome.Failed: failedCount++; break;
        }
    }

    /// <summary>Wraps the external Converter call, catching any exception it throws so a single bad file can't abort the batch.</summary>
    static ConvertResult SafeConvert(string sourceFile, string destPath, AssetProfile profile, bool isHalfRes, CancellationToken cancellationToken)
    {
        try
        {
            return Converter.Convert(sourceFile, destPath, isHalfRes, profile, cancellationToken);
        }
        catch (Exception ex)
        {
            return new ConvertResult { Source = sourceFile, Dest = destPath, Note = ex.Message };
        }
    }

    /// <summary>
    /// Advances the progress bar by one step. Native ProgressBar controls animate value
    /// increases smoothly, which reads as visibly lagging behind the log during fast batches;
    /// briefly overshooting resets that animation so the bar jumps straight to the new value.
    /// </summary>
    void BumpProgress()
    {
        int target = Math.Min(_progressBar.Value + 1, _progressBar.Maximum);
        if (target < _progressBar.Maximum) _progressBar.Value = target + 1;
        _progressBar.Value = target;
    }

    bool ShouldOverwrite(string destPath, ref bool overwriteAll, ref bool skipAll)
    {
        if (!File.Exists(destPath) || overwriteAll) return true;
        if (skipAll) return false;

        using var dialog = new OverwriteDialog(destPath);
        dialog.ShowDialog(this);
        switch (dialog.Answer)
        {
            case OverwriteAnswer.Yes: return true;
            case OverwriteAnswer.YesToAll: overwriteAll = true; return true;
            case OverwriteAnswer.NoToAll: skipAll = true; return false;
            default: return false;
        }
    }

    void BeginBusyState(int totalSteps)
    {
        _busy = true;
        SetControlsEnabled(false);

        _convertButton.Enabled = true;
        _convertButton.Text = Loc.T("toolbar.cancel");
        Theme.StyleButton(_convertButton, Theme.Danger, Theme.White);

        _progressBar.Visible = true;
        _progressBar.Minimum = 0;
        _progressBar.Maximum = Math.Max(totalSteps, 1);
        _progressBar.Value = 0;
    }

    void EndBusyState()
    {
        _busy = false;
        SetControlsEnabled(true);

        _convertButton.Enabled = true;
        _convertButton.Text = Loc.T("toolbar.convertAll");
        Theme.StyleButton(_convertButton, Theme.Success, Theme.White);
        _progressBar.Visible = false;

        _cts?.Dispose();
        _cts = null;
    }

    void SetControlsEnabled(bool enabled)
    {
        _extensionCombo.Enabled = enabled;
        _assetsCombo.Enabled = enabled;
        _browseAssets.Enabled = enabled;
        _lowResCombo.Enabled = enabled;
        _browseLowRes.Enabled = enabled;
        _locateGuiButton.Enabled = enabled;
        _clearListButton.Enabled = enabled;
        _listView.Enabled = enabled;
    }

    void UpdateStatus(int done, int skipped, int failed, int total, TimeSpan elapsed)
    {
        _itemCountLabel.Text = $"{done + skipped + failed}/{total} processed  •  {elapsed:mm\\:ss}";
    }

    // ======================================================================
    // Config persistence
    // ======================================================================

    void LoadConfigIntoUi()
    {
        // Items were already populated in AddDestinationRow - only the text needs restoring here.
        SetComboTextSafely(_assetsCombo, _config.AssetsPath);
        SetComboTextSafely(_lowResCombo, _config.AssetsLowResPath);
    }

    /// <summary>
    /// Assigns ComboBox.Text with AutoComplete (SuggestAppend) briefly disabled. Setting Text while
    /// SuggestAppend is active can make WinForms silently append an unrelated matching item from the
    /// list onto the assigned text (e.g. loading "C:\Foo" ends up showing "C:\FooC:\bar"), since the
    /// native autocomplete engine reacts to the change just like it would to a keystroke.
    /// </summary>
    static void SetComboTextSafely(ComboBox combo, string text)
    {
        var mode = combo.AutoCompleteMode;
        combo.AutoCompleteMode = AutoCompleteMode.None;
        combo.Text = text;
        combo.AutoCompleteMode = mode;
    }

    void SaveConfigFromUi()
    {
        _config.AssetsPath = _assetsCombo.Text;
        _config.AssetsLowResPath = _lowResCombo.Text;
        _config.Save();
    }

    // ======================================================================
    // Misc
    // ======================================================================

    static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            MessageBox.Show(Loc.T("msg.openFolder.missing"), Loc.T("msg.openFolder.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_busy)
        {
            var confirm = MessageBox.Show(this, Loc.T("msg.conversionRunning.body"),
                Loc.T("msg.conversionRunning.title"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) { e.Cancel = true; return; }
            _cts?.Cancel();
        }

        SaveConfigFromUi();
    }

    void Log(string line)
    {
        if (InvokeRequired) { BeginInvoke(new Action<string>(Log), line); return; }
        _log.AppendText(line + Environment.NewLine);
    }
}
