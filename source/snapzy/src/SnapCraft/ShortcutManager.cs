namespace SnapCraft;

internal sealed class ShortcutManager : IDisposable
{
    private readonly Dictionary<string, GlobalHotkey> bindings = new();
    public Dictionary<string, string> Errors { get; } = new();

    public ShortcutManager(IntPtr window, IReadOnlyDictionary<string, ShortcutBinding> settings)
    {
        var id = 1;
        foreach (var action in new[] { "launcher", "area", "window", "scroll", "video" })
        {
            bindings[action] = new GlobalHotkey(window, id);
            id += 2;
            if (!settings.TryGetValue(action, out var binding)) continue;
            try { Update(action, binding); }
            catch (Exception error) { Errors[action] = error.Message; }
        }
    }

    public void Update(string action, ShortcutBinding binding)
    {
        if (!bindings.TryGetValue(action, out var hotkey)) throw new ArgumentException("ไม่รู้จักโหมดคีย์ลัด");
        if (binding.Enabled) hotkey.Set(binding.Modifiers, binding.Key);
        else hotkey.Dispose();
        Errors.Remove(action);
    }

    public string? ActionFor(Message message) => bindings.FirstOrDefault(entry => entry.Value.Matches(message)).Key;
    public void UpdateAndSave(AppSettings settings, string action, ShortcutBinding binding)
    {
        var values = settings.GetShortcuts();
        if (!values.TryGetValue(action, out var previous)) throw new ArgumentException("Unknown shortcut mode.");
        Update(action, binding);
        values[action] = binding;
        try { settings.Save(); }
        catch (Exception saveError)
        {
            values[action] = previous;
            try { Update(action, previous); }
            catch (Exception restoreError)
            {
                Errors[action] = restoreError.Message;
                throw new AggregateException("Settings could not be saved and the previous shortcut could not be restored.", saveError, restoreError);
            }
            throw;
        }
    }
    public void Dispose() { foreach (var hotkey in bindings.Values) hotkey.Dispose(); }
}
