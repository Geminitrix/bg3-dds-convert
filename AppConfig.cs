using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DDS_Convert;

/// <summary>
/// Persisted user settings: destination folders, recent paths (MRU) and last used options.
/// Stored as JSON next to the executable. Automatically migrates the old plain-text
/// "config.txt" format used by earlier versions of the tool.
/// </summary>
public sealed class AppConfig
{
    public string AssetsPath { get; set; } = "";
    public string AssetsLowResPath { get; set; } = "";
    public bool UpperCaseExtension { get; set; } = true;
    public string Language { get; set; } = "en";
    public List<string> RecentAssetsPaths { get; set; } = new();
    public List<string> RecentLowResPaths { get; set; } = new();

    private const int MaxRecent = 8;

    private static string ConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    private static string LegacyConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg != null) return cfg;
            }
            else if (File.Exists(LegacyConfigPath))
            {
                // Migrate from the old two-line plain text config.
                var lines = File.ReadAllLines(LegacyConfigPath);
                var migrated = new AppConfig
                {
                    AssetsPath = lines.Length > 0 ? lines[0] : "",
                    AssetsLowResPath = lines.Length > 1 ? lines[1] : "",
                };
                migrated.Save();
                return migrated;
            }
        }
        catch
        {
            // A corrupted or unreadable config should never prevent the app from starting.
        }

        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Best effort only - a failed save should not crash the app.
        }
    }

    public void RememberAssetsPath(string path) => Remember(RecentAssetsPaths, path);
    public void RememberLowResPath(string path) => Remember(RecentLowResPaths, path);

    private static void Remember(List<string> list, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);

        if (list.Count > MaxRecent)
            list.RemoveRange(MaxRecent, list.Count - MaxRecent);
    }
}
