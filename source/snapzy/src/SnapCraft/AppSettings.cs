using System.Text.Json;

namespace SnapCraft;

internal sealed class AppSettings
{
    private int delayMs;
    private string language = ProductProfile.Current.DefaultLanguage;
    public int DelayMs { get => delayMs; set => delayMs = value is 3000 or 5000 or 10000 ? value : 0; }
    public string OpenMode { get; set; } = "window";
    public string Language { get => language; set => language = value is "en" or "th" ? value : ProductProfile.Current.DefaultLanguage; }
    public uint HotkeyModifiers { get; set; } = 6;
    public uint HotkeyKey { get; set; } = (uint)Keys.S;
    public Dictionary<string, ShortcutBinding>? Shortcuts { get; set; }
    public bool HistoryEnabled { get; set; }
    private int historyDays = 7;
    public int HistoryDays { get => historyDays; set => historyDays = value is 1 or 7 or 30 ? value : 7; }

    public Dictionary<string, ShortcutBinding> GetShortcuts()
    {
        Shortcuts ??= new();
        var defaults = new Dictionary<string, ShortcutBinding>
        {
            ["launcher"] = new(HotkeyModifiers, HotkeyKey),
            ["area"] = new(2, (uint)Keys.PrintScreen),
            ["window"] = new(3, (uint)Keys.W),
            ["scroll"] = new(3, (uint)Keys.L),
            ["video"] = new(3, (uint)Keys.V)
        };
        foreach (var entry in defaults) if (!Shortcuts.ContainsKey(entry.Key) || Shortcuts[entry.Key] is null) Shortcuts[entry.Key] = entry.Value;
        return Shortcuts;
    }

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(WebAssets.SettingsPath)) ?? new(); }
        catch (Exception error) when (error is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(WebAssets.DataRoot);
        var temporary = WebAssets.SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this));
            File.Move(temporary, WebAssets.SettingsPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

internal sealed record ShortcutBinding(uint Modifiers, uint Key, bool Enabled = true);
